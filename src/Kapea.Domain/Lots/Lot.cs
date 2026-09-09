using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Lots;

/// <summary>
/// Lote de adquisición: la cantidad comprada de un activo con su coste en euros y su
/// fecha. Es entidad de primer nivel y no un detalle del cálculo porque un informe
/// fiscal tiene que poder enseñar de qué lotes salió cada resultado.
/// </summary>
/// <remarks>
/// Un lote es parte de la proyección que se reconstruye desde los movimientos; su
/// identidad no sobrevive a un recálculo, pero su contenido sí es reproducible.
/// </remarks>
public sealed class Lot
{
    /// <summary>Constructor para el materializador de EF Core; ver la nota en Transaction.</summary>
    private Lot()
    {
    }

    private Lot(
        Guid id,
        UserId userId,
        Guid assetId,
        Guid accountId,
        Guid acquisitionTransactionId,
        Quantity originalQuantity,
        Money acquisitionCost,
        Occurrence acquiredAt,
        long sequenceNumber)
    {
        Id = id;
        UserId = userId;
        AssetId = assetId;
        AccountId = accountId;
        AcquisitionTransactionId = acquisitionTransactionId;
        OriginalQuantity = originalQuantity;
        RemainingQuantity = originalQuantity;
        AcquisitionCost = acquisitionCost;
        AcquiredAt = acquiredAt;
        SequenceNumber = sequenceNumber;
    }

    public Guid Id { get; }

    public UserId UserId { get; }

    public Guid AssetId { get; }

    /// <summary>Cuenta donde está el lote. Cambia con un traspaso interno; el coste y la fecha no.</summary>
    public Guid AccountId { get; private set; }

    public Guid AcquisitionTransactionId { get; }

    public Quantity OriginalQuantity { get; private set; }

    public Quantity RemainingQuantity { get; private set; }

    /// <summary>Coste total de la adquisición en euros, comisiones de compra incluidas.</summary>
    public Money AcquisitionCost { get; private set; }

    public Occurrence AcquiredAt { get; private set; }

    /// <summary>
    /// Desempate entre lotes adquiridos en el mismo instante. Sin él, el orden de
    /// consumo dependería del orden en que la base de datos devuelva las filas y el
    /// recálculo dejaría de ser reproducible.
    /// </summary>
    public long SequenceNumber { get; }

    public bool IsExhausted => RemainingQuantity.IsZero;

    /// <summary>Coste unitario en euros. Es un derivado: el dato que manda es el coste total.</summary>
    public Money UnitCost => OriginalQuantity.IsZero
        ? Money.Zero(AcquisitionCost.Currency)
        : AcquisitionCost / OriginalQuantity.Value;

    public static Lot Create(
        UserId userId,
        Guid assetId,
        Guid accountId,
        Guid acquisitionTransactionId,
        Quantity quantity,
        Money acquisitionCost,
        Occurrence acquiredAt,
        long sequenceNumber)
    {
        if (quantity.IsZero)
        {
            throw new DomainException("Un lote sin cantidad no representa ninguna adquisición.");
        }

        if (!acquisitionCost.Currency.IsEuro)
        {
            throw new DomainException("El coste de un lote se guarda siempre en euros, ya convertido.");
        }

        if (acquisitionCost.IsNegative)
        {
            throw new DomainException("El coste de adquisición de un lote no puede ser negativo.");
        }

        return new Lot(Guid.NewGuid(), userId, assetId, accountId, acquisitionTransactionId, quantity,
            acquisitionCost, acquiredAt, sequenceNumber);
    }

    /// <summary>
    /// Consume cantidad del lote y devuelve el coste correspondiente, repartido en
    /// proporción a lo consumido sobre la cantidad original.
    /// </summary>
    public Money Consume(Quantity quantity)
    {
        if (quantity > RemainingQuantity)
        {
            throw new LotOverconsumptionException(Id, RemainingQuantity, quantity);
        }

        // Se multiplica antes de dividir: dividir primero introduce un decimal periódico
        // que no cuadra al volver a sumar los trozos.
        var consumedCost = OriginalQuantity.IsZero
            ? Money.Euros(0m)
            : AcquisitionCost * quantity.Value / OriginalQuantity.Value;

        RemainingQuantity -= quantity;

        return consumedCost;
    }

    /// <summary>
    /// Aplica un split: la cantidad se multiplica y el coste total no varía, de modo
    /// que el coste unitario baja en la misma proporción. La fecha de adquisición se
    /// mantiene, porque el split no reinicia la antigüedad del lote.
    /// </summary>
    public void ApplySplit(decimal ratio)
    {
        if (ratio <= 0m)
        {
            throw new DomainException($"La proporción de un split tiene que ser positiva: {ratio}.");
        }

        OriginalQuantity *= ratio;
        RemainingQuantity *= ratio;
    }

    /// <summary>Traslada el lote a otra cuenta conservando coste y fecha: un traspaso interno no es una venta.</summary>
    public void TransferTo(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new DomainException("Un traspaso necesita cuenta de destino.");
        }

        AccountId = accountId;
    }

    /// <summary>
    /// Parte el lote en dos: este se queda con el resto y se devuelve uno nuevo con la
    /// cantidad indicada y su coste proporcional. Hace falta cuando un traspaso interno
    /// se lleva parte de un lote, porque las dos mitades acaban en cuentas distintas.
    /// </summary>
    public Lot SplitOff(Quantity quantity)
    {
        if (quantity.IsZero)
        {
            throw new DomainException("Partir un lote por una cantidad nula no tiene sentido.");
        }

        if (quantity >= RemainingQuantity)
        {
            throw new DomainException(
                $"No se puede partir el lote {Id} por {quantity}: solo le restan {RemainingQuantity}.");
        }

        // Se reparte por coste unitario, no por proporción del resto: así las dos
        // mitades conservan exactamente el mismo coste unitario que el lote original.
        var movedCost = UnitCost * quantity.Value;

        var moved = new Lot(Guid.NewGuid(), UserId, AssetId, AccountId, AcquisitionTransactionId,
            quantity, movedCost, AcquiredAt, SequenceNumber);

        AcquisitionCost -= movedCost;
        OriginalQuantity -= quantity;
        RemainingQuantity -= quantity;

        return moved;
    }

    /// <summary>Coste que queda por consumir, proporcional a la cantidad restante.</summary>
    public Money RemainingCost => OriginalQuantity.IsZero
        ? Money.Zero(AcquisitionCost.Currency)
        : AcquisitionCost * RemainingQuantity.Value / OriginalQuantity.Value;

    /// <summary>Suma al coste la comisión de un traspaso interno, que no genera resultado pero sí encarece el lote.</summary>
    public void AddTransferFee(Money fee)
    {
        if (fee.IsNegative)
        {
            throw new DomainException("La comisión de un traspaso no puede ser negativa.");
        }

        AcquisitionCost += fee;
    }
}

/// <summary>
/// Intento de consumir de un lote más de lo que le resta. Señala datos incompletos
/// —una compra que falta por importar—, nunca un resultado parcial que se pueda emitir.
/// </summary>
public sealed class LotOverconsumptionException(Guid lotId, Quantity remaining, Quantity requested)
    : DomainException($"El lote {lotId} solo tiene {remaining} y se han intentado consumir {requested}.")
{
    public Guid LotId { get; } = lotId;

    public Quantity Remaining { get; } = remaining;

    public Quantity Requested { get; } = requested;
}
