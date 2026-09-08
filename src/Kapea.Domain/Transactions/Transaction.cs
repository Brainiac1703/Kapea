using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Transactions;

/// <summary>
/// Movimiento normalizado: la unidad mínima de información financiera de Kapea y la
/// única fuente de verdad del cálculo. Lotes, posiciones y resultados son proyecciones
/// que se recalculan a partir de estos movimientos.
/// </summary>
/// <remarks>
/// Sus datos financieros no tienen mutadores a propósito. Editar un movimiento
/// importado rompería la correspondencia con el registro de origen, que es
/// justamente lo que permite defender una cifra ante una gestoría. Las correcciones
/// se expresan como ajuste manual, y deshacer una importación es eliminar su
/// ejecución completa.
/// </remarks>
public sealed class Transaction
{
    private Transaction(
        Guid id,
        UserId userId,
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Money? withholdingTax,
        Occurrence occurredAt,
        TransactionOrigin origin,
        TransactionSource source,
        string? adjustmentReason,
        ExchangeRate? appliedExchangeRate)
    {
        Id = id;
        UserId = userId;
        AccountId = accountId;
        Type = type;
        AssetId = assetId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        GrossAmount = grossAmount;
        Fee = fee;
        WithholdingTax = withholdingTax;
        OccurredAt = occurredAt;
        Origin = origin;
        Source = source;
        AdjustmentReason = adjustmentReason;
        AppliedExchangeRate = appliedExchangeRate;
    }

    public Guid Id { get; }

    public UserId UserId { get; }

    public Guid AccountId { get; }

    public TransactionType Type { get; }

    /// <summary>Activo operado. Nulo en movimientos puramente dinerarios (una comisión de cuenta, un ingreso).</summary>
    public Guid? AssetId { get; }

    public Quantity Quantity { get; }

    /// <summary>Precio por unidad en la divisa de la operación. Nulo cuando el origen no lo aporta.</summary>
    public Money? UnitPrice { get; }

    /// <summary>Importe bruto de la operación en su divisa, antes de comisiones y retenciones.</summary>
    public Money GrossAmount { get; }

    public Money Fee { get; }

    /// <summary>Retención practicada en origen. Solo la aportan algunos dividendos.</summary>
    public Money? WithholdingTax { get; }

    public Occurrence OccurredAt { get; }

    public Currency Currency => GrossAmount.Currency;

    public TransactionOrigin Origin { get; }

    public TransactionSource Source { get; }

    /// <summary>Motivo del ajuste. Obligatorio en un ajuste manual, siempre nulo en un importado.</summary>
    public string? AdjustmentReason { get; }

    /// <summary>
    /// Tipo de cambio congelado en el momento de importar. Nulo cuando la operación ya
    /// estaba en euros. Es el que usa cualquier recálculo posterior: la fuente de tipos
    /// no se vuelve a consultar.
    /// </summary>
    public ExchangeRate? AppliedExchangeRate { get; }

    /// <summary>Un movimiento sin clasificar no participa en el cálculo hasta que una persona lo resuelve.</summary>
    public bool RequiresReview => Type == TransactionType.Unknown;

    public bool IsAcquisition => Type is TransactionType.Buy;

    public bool IsDisposal => Type is TransactionType.Sell;

    public static Transaction Imported(
        UserId userId,
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Occurrence occurredAt,
        TransactionSource source,
        Money? withholdingTax = null,
        ExchangeRate? appliedExchangeRate = null)
    {
        EnsureConsistent(type, assetId, quantity, unitPrice, grossAmount, fee, withholdingTax);
        EnsureRateMatchesCurrency(grossAmount, appliedExchangeRate);

        if (source.ImportRunId is null)
        {
            throw new DomainException("Un movimiento importado tiene que referenciar su ejecución de importación.");
        }

        return new Transaction(Guid.NewGuid(), userId, accountId, type, assetId, quantity, unitPrice, grossAmount,
            fee, withholdingTax, occurredAt, TransactionOrigin.Imported, source, adjustmentReason: null,
            appliedExchangeRate);
    }

    public static Transaction FromManualAdjustment(
        UserId userId,
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Occurrence occurredAt,
        Guid adjustmentId,
        string reason,
        ExchangeRate? appliedExchangeRate = null)
    {
        EnsureConsistent(type, assetId, quantity, unitPrice, grossAmount, fee, withholdingTax: null);
        EnsureRateMatchesCurrency(grossAmount, appliedExchangeRate);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Un ajuste manual necesita motivo: sin él la cifra no es defendible.");
        }

        return new Transaction(Guid.NewGuid(), userId, accountId, type, assetId, quantity, unitPrice, grossAmount,
            fee, withholdingTax: null, occurredAt, TransactionOrigin.ManualAdjustment,
            TransactionSource.ForManualAdjustment(adjustmentId), reason.Trim(), appliedExchangeRate);
    }

    /// <summary>Convierte a euros un importe de este movimiento con el tipo congelado.</summary>
    public Money ToEuros(Money amount) =>
        amount.Currency.IsEuro
            ? amount
            : (AppliedExchangeRate ?? throw new DomainException(
                "El movimiento está en divisa y no tiene tipo de cambio congelado."))
                .ToEuros(amount);

    public Money GrossAmountInEuros => ToEuros(GrossAmount);

    public Money FeeInEuros => ToEuros(Fee);

    public Money? WithholdingTaxInEuros => WithholdingTax is { } withholding ? ToEuros(withholding) : null;

    private static void EnsureRateMatchesCurrency(Money grossAmount, ExchangeRate? rate)
    {
        if (grossAmount.Currency.IsEuro)
        {
            if (rate is not null)
            {
                throw new DomainException("Una operación en euros no lleva tipo de cambio.");
            }

            return;
        }

        if (rate is null)
        {
            throw new DomainException(
                $"Una operación en {grossAmount.Currency.Code} necesita el tipo de cambio aplicado, congelado en el movimiento.");
        }

        if (rate.Currency != grossAmount.Currency)
        {
            throw new CurrencyMismatchException(rate.Currency, grossAmount.Currency, "combinar");
        }
    }

    private static void EnsureConsistent(
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Money? withholdingTax)
    {
        if (type is TransactionType.Buy or TransactionType.Sell)
        {
            if (assetId is null)
            {
                throw new DomainException("Una compra o una venta necesita activo.");
            }

            if (quantity.IsZero)
            {
                throw new DomainException("Una compra o una venta necesita cantidad.");
            }
        }

        if (unitPrice is { } price && price.Currency != grossAmount.Currency)
        {
            throw new CurrencyMismatchException(price.Currency, grossAmount.Currency, "combinar");
        }

        if (fee.Currency != grossAmount.Currency)
        {
            throw new CurrencyMismatchException(fee.Currency, grossAmount.Currency, "combinar");
        }

        if (fee.IsNegative)
        {
            throw new DomainException("Una comisión no puede ser negativa; su signo lo decide el cálculo, no el dato.");
        }

        if (withholdingTax is { } withholding)
        {
            if (withholding.Currency != grossAmount.Currency)
            {
                throw new CurrencyMismatchException(withholding.Currency, grossAmount.Currency, "combinar");
            }

            if (withholding.IsNegative)
            {
                throw new DomainException("Una retención no puede ser negativa.");
            }
        }
    }
}
