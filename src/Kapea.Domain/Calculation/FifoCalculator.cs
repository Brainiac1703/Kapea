using Kapea.Domain.Lots;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Motor de cálculo FIFO. Es una función pura: mismos movimientos, mismo resultado,
/// hoy y dentro de tres ejercicios. No conoce la base de datos, ni la fecha del
/// sistema, ni ningún proveedor de tipos de cambio; todo eso llega ya resuelto en
/// cada <see cref="ValuedTransaction"/>.
/// </summary>
/// <remarks>
/// El criterio FIFO se aplica por activo y no por cuenta, como exige la normativa
/// española: los lotes del mismo activo en distintas cuentas forman una única cola
/// ordenada por fecha de adquisición.
/// </remarks>
public static class FifoCalculator
{
    /// <summary>
    /// Cuánto puede faltar en una venta antes de considerarla un descuadre.
    /// </summary>
    /// <remarks>
    /// Relativo a lo vendido y no absoluto, que es lo que separa un redondeo de un
    /// descuadre de verdad: la misma cantidad que es ruido frente a una posición grande
    /// puede ser la mitad de una pequeña.
    ///
    /// Una millonésima porque hay dos órdenes de magnitud que separar y entre ellos
    /// sobran seis. Las cantidades se guardan con ocho decimales, así que el redondeo de
    /// una plataforma al vender una posición entera vive en 1e-8: en el caso que motivó
    /// esto faltaba 1e-8 sobre 0,95674537, una proporción de 1,05e-8. Un descuadre real
    /// —una compra que no se importó— es una fracción apreciable de la posición, por
    /// ciento y no por millón.
    /// </remarks>
    private const decimal DisposalTolerance = 0.000001m;

    public static AssetCalculationResult Calculate(
        UserId userId,
        Guid assetId,
        IEnumerable<ValuedTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var ordered = transactions.ToList();
        ordered.Sort(TransactionOrdering.Instance);

        var lots = new List<Lot>();
        var openLots = new List<Lot>();
        var realized = new List<RealizedResult>();
        var incomes = new List<CapitalIncome>();
        var inconsistencies = new List<CalculationInconsistency>();
        var unresolved = 0;
        var sequence = 0L;

        foreach (var valued in ordered)
        {
            valued.EnsureAmountsAreInEuros();

            if (valued.IsUnresolved)
            {
                unresolved++;
                continue;
            }

            switch (valued.Type)
            {
                case TransactionType.Buy:
                    if (!valued.IsInternalTransferIn)
                    {
                        var lot = CreateLot(userId, assetId, valued, ++sequence);
                        lots.Add(lot);
                        openLots.Add(lot);
                    }

                    break;

                case TransactionType.Deposit when valued.IsInternalTransferIn:
                case TransactionType.Transfer when valued.IsInternalTransferIn:
                    // La entrada de un traspaso interno no crea lote: el lote ya existe
                    // y llega desde la cuenta de origen con su coste y su fecha.
                    break;

                case TransactionType.Sell:
                    Dispose(userId, assetId, valued, openLots, realized, inconsistencies);
                    break;

                case TransactionType.Withdrawal when valued.IsInternalTransferOut:
                case TransactionType.Transfer when valued.IsInternalTransferOut:
                    Transfer(userId, assetId, valued, lots, openLots, inconsistencies);
                    break;

                case TransactionType.Split:
                    ApplySplit(userId, assetId, valued, openLots, inconsistencies);
                    break;

                case TransactionType.Dividend:
                case TransactionType.Interest:
                case TransactionType.Reward:
                    incomes.Add(new CapitalIncome(
                        userId,
                        valued.AssetId,
                        valued.Transaction.Id,
                        valued.Transaction.AccountId,
                        valued.Type,
                        valued.OccurredAt,
                        valued.GrossAmountInEuros,
                        valued.WithholdingInEuros ?? Money.Euros(0m)));

                    // Un rendimiento cobrado en especie —una recompensa de staking, un
                    // dividendo en acciones— además de tributar entrega unidades que
                    // pasan a ser tuyas. Su coste de adquisición es el valor por el que
                    // ya se ha tributado, de modo que venderlas después no vuelve a
                    // gravar lo mismo. Sin esto, esas unidades no existían en la cartera
                    // y al venderlas el resultado salía inflado por su importe entero.
                    if (valued.Transaction.Quantity.Value > 0m)
                    {
                        var earned = CreateLot(userId, assetId, valued, ++sequence);

                        lots.Add(earned);
                        openLots.Add(earned);
                    }

                    break;

                default:
                    // Ingresos y retiradas de efectivo, comisiones de cuenta y traspasos
                    // no confirmados no alteran los lotes de un activo.
                    break;
            }

            openLots.RemoveAll(lot => lot.IsExhausted);
        }

        return new AssetCalculationResult(assetId, lots, realized, incomes, inconsistencies, unresolved);
    }

    private static Lot CreateLot(UserId userId, Guid assetId, ValuedTransaction valued, long sequence) =>
        Lot.Create(
            userId,
            assetId,
            valued.Transaction.AccountId,
            valued.Transaction.Id,
            valued.Quantity,
            // La comisión de adquisición forma parte del coste del lote, no es un gasto aparte.
            valued.GrossAmountInEuros + valued.FeeInEuros,
            valued.OccurredAt,
            sequence);

    private static void Dispose(
        UserId userId,
        Guid assetId,
        ValuedTransaction valued,
        List<Lot> openLots,
        List<RealizedResult> realized,
        List<CalculationInconsistency> inconsistencies)
    {
        var available = TotalRemaining(openLots);

        // Las plataformas redondean al vender una posición entera, y su cifra no siempre
        // coincide al último decimal con la suma de lo que vendieron. Rechazar la venta
        // por eso deja el activo en cartera para siempre y el ejercicio sin un resultado
        // que sí ocurrió; aceptarla sin mirar taparía un descuadre de verdad. Lo que los
        // separa es la proporción, no el tamaño.
        if (available < valued.Quantity && !IsRounding(valued.Quantity - available, valued.Quantity))
        {
            inconsistencies.Add(new CalculationInconsistency(
                userId,
                InconsistencyKind.InsufficientLots,
                assetId,
                valued.Transaction.Id,
                valued.OccurredAt,
                valued.Quantity - available));

            return;
        }

        // Se dispone de lo que hay. Registrar la cantidad del movimiento dejaría un
        // resultado que dice haber vendido más de lo que desglosa por lote, y es el
        // desglose lo que hace el cálculo auditable.
        var disposed = available < valued.Quantity ? available : valued.Quantity;

        // El importe de transmisión es lo recibido menos la comisión de venta. La comisión
        // se reparte entre los lotes consumidos en proporción a la cantidad de cada uno.
        //
        // El importe no se toca: ese dinero se recibió de verdad y por esta posición.
        // Recortarlo en proporción a la cienmillonésima que no se consume introduciría
        // una diferencia entre lo que la plataforma ingresó y lo que Kapea dice haber
        // obtenido, que es peor que la diferencia de cantidad que se está tolerando.
        var netProceeds = valued.GrossAmountInEuros - valued.FeeInEuros;
        var consumed = new List<ConsumedLot>();
        var pending = disposed;
        var totalCost = Money.Euros(0m);
        var allocatedProceeds = Money.Euros(0m);

        foreach (var lot in InFifoOrder(openLots))
        {
            if (pending.IsZero)
            {
                break;
            }

            var take = Quantity.Min(pending, lot.RemainingQuantity);
            var cost = lot.Consume(take);
            var share = netProceeds * take.Value / disposed.Value;

            consumed.Add(new ConsumedLot(lot.Id, lot.AcquisitionTransactionId, lot.AcquiredAt, take, cost, share));
            totalCost += cost;
            allocatedProceeds += share;
            pending -= take;
        }

        // El reparto proporcional puede dejar céntimos sueltos por el último decimal;
        // se asignan al último lote para que la suma cuadre exactamente con el importe.
        if (consumed.Count > 0 && allocatedProceeds != netProceeds)
        {
            var last = consumed[^1];
            consumed[^1] = last.WithProceeds(last.ProceedsInEuros + (netProceeds - allocatedProceeds));
        }

        realized.Add(new RealizedResult(
            userId,
            assetId,
            valued.Transaction.Id,
            valued.Transaction.AccountId,
            valued.OccurredAt,
            disposed,
            netProceeds,
            totalCost,
            consumed));
    }

    /// <summary>Lo que falta es el redondeo de la plataforma y no un descuadre.</summary>
    private static bool IsRounding(Quantity missing, Quantity sold) =>
        sold.Value > 0m && missing.Value <= sold.Value * DisposalTolerance;

    private static void Transfer(
        UserId userId,
        Guid assetId,
        ValuedTransaction valued,
        List<Lot> lots,
        List<Lot> openLots,
        List<CalculationInconsistency> inconsistencies)
    {
        var destination = valued.TransferDestinationAccountId!.Value;
        var source = valued.Transaction.AccountId;
        var candidates = InFifoOrder(openLots).Where(lot => lot.AccountId == source).ToList();
        var available = TotalRemaining(candidates);

        if (available < valued.Quantity)
        {
            inconsistencies.Add(new CalculationInconsistency(
                userId,
                InconsistencyKind.InsufficientLotsForTransfer,
                assetId,
                valued.Transaction.Id,
                valued.OccurredAt,
                valued.Quantity - available));

            return;
        }

        var pending = valued.Quantity;
        var moved = new List<Lot>();

        foreach (var lot in candidates)
        {
            if (pending.IsZero)
            {
                break;
            }

            if (lot.RemainingQuantity <= pending)
            {
                pending -= lot.RemainingQuantity;
                lot.TransferTo(destination);
                moved.Add(lot);
                continue;
            }

            var part = lot.SplitOff(pending);
            part.TransferTo(destination);
            lots.Add(part);
            openLots.Add(part);
            moved.Add(part);
            pending = Quantity.Zero;
        }

        // La comisión de red no genera resultado: encarece los lotes que llegan al destino.
        if (!valued.FeeInEuros.IsZero && moved.Count > 0)
        {
            var totalMoved = TotalRemaining(moved);

            foreach (var lot in moved)
            {
                lot.AddTransferFee(valued.FeeInEuros * lot.RemainingQuantity.Value / totalMoved.Value);
            }
        }
    }

    private static void ApplySplit(
        UserId userId,
        Guid assetId,
        ValuedTransaction valued,
        List<Lot> openLots,
        List<CalculationInconsistency> inconsistencies)
    {
        if (valued.SplitRatio is not { } ratio || ratio <= 0m)
        {
            inconsistencies.Add(new CalculationInconsistency(
                userId,
                InconsistencyKind.SplitWithoutRatio,
                assetId,
                valued.Transaction.Id,
                valued.OccurredAt,
                Quantity.Zero));

            return;
        }

        // Solo se ajustan los lotes ya adquiridos: un split no toca lo que aún no existía,
        // ni las ventas anteriores, cuyo resultado quedó fijado en su fecha.
        foreach (var lot in openLots)
        {
            lot.ApplySplit(ratio);
        }
    }

    private static IEnumerable<Lot> InFifoOrder(IEnumerable<Lot> lots) =>
        lots.OrderBy(lot => lot.AcquiredAt.Instant).ThenBy(lot => lot.SequenceNumber);

    private static Quantity TotalRemaining(IEnumerable<Lot> lots) =>
        lots.Aggregate(Quantity.Zero, (total, lot) => total + lot.RemainingQuantity);
}
