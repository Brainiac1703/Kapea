using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Lote consumido por una transmisión, con la parte del importe y del coste que le
/// corresponde. Es el nivel de detalle que permite defender una cifra ante una gestoría.
/// </summary>
/// <remarks>
/// Es una clase con constructor privado sin parámetros, y no un record posicional,
/// porque EF Core no sabe enlazar tipos complejos —importes y fechas— a parámetros de
/// constructor. Desde fuera sigue siendo inmutable.
/// </remarks>
public sealed class ConsumedLot
{
    private ConsumedLot()
    {
    }

    public ConsumedLot(
        Guid lotId,
        Guid acquisitionTransactionId,
        Occurrence acquiredAt,
        Quantity quantity,
        Money acquisitionCostInEuros,
        Money proceedsInEuros)
    {
        LotId = lotId;
        AcquisitionTransactionId = acquisitionTransactionId;
        AcquiredAt = acquiredAt;
        Quantity = quantity;
        AcquisitionCostInEuros = acquisitionCostInEuros;
        ProceedsInEuros = proceedsInEuros;
    }

    public Guid LotId { get; private set; }

    public Guid AcquisitionTransactionId { get; private set; }

    public Occurrence AcquiredAt { get; private set; }

    public Quantity Quantity { get; private set; }

    public Money AcquisitionCostInEuros { get; private set; }

    public Money ProceedsInEuros { get; private set; }

    public Money ResultInEuros => ProceedsInEuros - AcquisitionCostInEuros;

    /// <summary>Copia con otro importe, para asignar al último lote el resto del reparto.</summary>
    public ConsumedLot WithProceeds(Money proceedsInEuros) =>
        new(LotId, AcquisitionTransactionId, AcquiredAt, Quantity, AcquisitionCostInEuros, proceedsInEuros);
}

/// <summary>
/// Resultado realizado de una transmisión: lo que se ingresó, lo que costó y de qué
/// lotes salió. Se devenga en la fecha de la transmisión, que es la que determina el
/// ejercicio fiscal.
/// </summary>
public sealed class RealizedResult
{
    private readonly List<ConsumedLot> _consumedLots = [];

    private RealizedResult()
    {
    }

    public RealizedResult(
        UserId userId,
        Guid assetId,
        Guid disposalTransactionId,
        Guid accountId,
        Occurrence disposedAt,
        Quantity quantity,
        Money proceedsInEuros,
        Money acquisitionCostInEuros,
        IReadOnlyList<ConsumedLot> consumedLots)
    {
        ArgumentNullException.ThrowIfNull(consumedLots);

        UserId = userId;
        AssetId = assetId;
        DisposalTransactionId = disposalTransactionId;
        AccountId = accountId;
        DisposedAt = disposedAt;
        Quantity = quantity;
        ProceedsInEuros = proceedsInEuros;
        AcquisitionCostInEuros = acquisitionCostInEuros;
        _consumedLots.AddRange(consumedLots);
    }

    public UserId UserId { get; private set; }

    public Guid AssetId { get; private set; }

    public Guid DisposalTransactionId { get; private set; }

    public Guid AccountId { get; private set; }

    public Occurrence DisposedAt { get; private set; }

    public Quantity Quantity { get; private set; }

    public Money ProceedsInEuros { get; private set; }

    public Money AcquisitionCostInEuros { get; private set; }

    public IReadOnlyList<ConsumedLot> ConsumedLots => _consumedLots;

    /// <summary>Ganancia o pérdida patrimonial de la transmisión.</summary>
    public Money ResultInEuros => ProceedsInEuros - AcquisitionCostInEuros;

    /// <summary>
    /// Ejercicio de devengo. Se toma de la fecha en la zona del origen, no en UTC:
    /// una venta del 31 de diciembre por la noche en Madrid es del ejercicio que
    /// vio el usuario, no del siguiente.
    /// </summary>
    public int TaxYear => DisposedAt.InSourceTimeZone.Year;
}
