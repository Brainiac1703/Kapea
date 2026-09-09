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
                    Transfer(assetId, valued, lots, openLots, inconsistencies);
                    break;

                case TransactionType.Split:
                    ApplySplit(assetId, valued, openLots, inconsistencies);
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

        if (available < valued.Quantity)
        {
            inconsistencies.Add(new CalculationInconsistency(
                InconsistencyKind.InsufficientLots,
                assetId,
                valued.Transaction.Id,
                valued.OccurredAt,
                valued.Quantity - available,
                $"La transmisión de {valued.Quantity} solo encuentra {available} en lotes: falta importar alguna adquisición."));

            return;
        }

        // El importe de transmisión es lo recibido menos la comisión de venta. La comisión
        // se reparte entre los lotes consumidos en proporción a la cantidad de cada uno.
        var netProceeds = valued.GrossAmountInEuros - valued.FeeInEuros;
        var consumed = new List<ConsumedLot>();
        var pending = valued.Quantity;
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
            var share = netProceeds * take.Value / valued.Quantity.Value;

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
            valued.Quantity,
            netProceeds,
            totalCost,
            consumed));
    }

    private static void Transfer(
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
                InconsistencyKind.InsufficientLotsForTransfer,
                assetId,
                valued.Transaction.Id,
                valued.OccurredAt,
                valued.Quantity - available,
                $"El traspaso de {valued.Quantity} solo encuentra {available} en la cuenta de origen."));

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
        Guid assetId,
        ValuedTransaction valued,
        List<Lot> openLots,
        List<CalculationInconsistency> inconsistencies)
    {
        if (valued.SplitRatio is not { } ratio || ratio <= 0m)
        {
            inconsistencies.Add(new CalculationInconsistency(
                InconsistencyKind.SplitWithoutRatio,
                assetId,
                valued.Transaction.Id,
                valued.OccurredAt,
                Quantity.Zero,
                "El split no trae proporción; aplicarlo a ciegas alteraría las cantidades."));

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
