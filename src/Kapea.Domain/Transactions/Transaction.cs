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
/// Sus datos financieros no tienen mutadores públicos a propósito. Editar un movimiento
/// importado rompería la correspondencia con el registro de origen, que es
/// justamente lo que permite defender una cifra ante una gestoría. Un importado se
/// corrige anulándolo y registrando un ajuste; sólo un movimiento apuntado a mano, que
/// no tiene registro de origen, se revisa en sitio.
/// </remarks>
public sealed class Transaction
{
    /// <summary>
    /// Constructor sin parámetros para el materializador de EF Core: no sabe enlazar
    /// tipos complejos —importes y fechas— a parámetros de constructor, así que los
    /// asigna por propiedad. No amplía la superficie pública: sigue sin haber forma de
    /// construir ni mutar un movimiento desde fuera.
    /// </summary>
    private Transaction()
    {
        Source = null!;
    }

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
        ExchangeRate? appliedExchangeRate,
        bool settledInCash = true,
        bool amountIsEstimated = false)
    {
        SettledInCash = settledInCash;
        AmountIsEstimated = amountIsEstimated;
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

    public Guid AccountId { get; private set; }

    public TransactionType Type { get; private set; }

    /// <summary>Activo operado. Nulo en movimientos puramente dinerarios (una comisión de cuenta, un ingreso).</summary>
    public Guid? AssetId { get; private set; }

    public Quantity Quantity { get; private set; }

    /// <summary>Precio por unidad en la divisa de la operación. Nulo cuando el origen no lo aporta.</summary>
    public Money? UnitPrice { get; private set; }

    /// <summary>Importe bruto de la operación en su divisa, antes de comisiones y retenciones.</summary>
    public Money GrossAmount { get; private set; }

    public Money Fee { get; private set; }

    /// <summary>Retención practicada en origen. Solo la aportan algunos dividendos.</summary>
    public Money? WithholdingTax { get; private set; }

    public Occurrence OccurredAt { get; private set; }

    public Currency Currency => GrossAmount.Currency;

    public TransactionOrigin Origin { get; }

    public TransactionSource Source { get; private set; }

    /// <summary>Motivo del ajuste. Obligatorio en un ajuste manual, siempre nulo en un importado.</summary>
    public string? AdjustmentReason { get; }

    /// <summary>Nota libre de un movimiento apuntado a mano. Nula en los demás orígenes.</summary>
    public string? Note { get; private set; }

    /// <summary>Cuándo se apuntó a mano. Nulo en los demás orígenes.</summary>
    public DateTimeOffset? RegisteredAt { get; private set; }

    /// <summary>Cuándo se editó por última vez un movimiento apuntado a mano.</summary>
    public DateTimeOffset? RevisedAt { get; private set; }

    /// <summary>
    /// Cuándo se anuló. Nulo mientras está vigente.
    /// </summary>
    /// <remarks>
    /// Anular no borra: el movimiento conserva su registro de origen y su huella, y por eso
    /// volver a importar no lo resucita. Sólo deja de contar en el cálculo.
    /// </remarks>
    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Por qué se anuló. Obligatorio al anular.</summary>
    public string? VoidReason { get; private set; }

    public bool IsVoided => VoidedAt is not null;

    /// <summary>
    /// Importado con el que el usuario confirmó que este apunte manual no coincide.
    /// </summary>
    /// <remarks>
    /// Guarda el identificador y no una marca suelta: si más tarde llega otro importado
    /// igual, esa coincidencia es nueva y tiene que volver a verse.
    /// </remarks>
    public Guid? DistinctFrom { get; private set; }

    /// <summary>
    /// Tipo de cambio congelado en el momento de importar. Nulo cuando la operación ya
    /// estaba en euros. Es el que usa cualquier recálculo posterior: la fuente de tipos
    /// no se vuelve a consultar.
    /// </summary>
    public ExchangeRate? AppliedExchangeRate { get; private set; }

    /// <summary>
    /// El movimiento se liquidó con dinero de la cuenta.
    /// </summary>
    /// <remarks>
    /// Falso en una permuta de un activo por otro: se valora en euros para saber lo que
    /// costó, pero ningún euro entró ni salió. Contarla como una venta y una compra
    /// dejaba en el saldo la diferencia entre los dos cambios, que no es dinero de nadie.
    /// </remarks>
    public bool SettledInCash { get; private set; } = true;

    /// <summary>
    /// El importe no lo dio el origen: lo estimó Kapea con el precio de cierre del día.
    /// </summary>
    /// <remarks>
    /// Se marca para que la cifra se pueda distinguir de una tomada del extracto y se
    /// pueda corregir. No excluye el movimiento del cálculo: una permuta sin valorar
    /// descuadraría la cartera entera, y una estimación visible es mejor que un hueco.
    /// </remarks>
    public bool AmountIsEstimated { get; private set; }

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
        ExchangeRate? appliedExchangeRate = null,
        bool settledInCash = true,
        bool amountIsEstimated = false)
    {
        EnsureConsistent(type, assetId, quantity, unitPrice, grossAmount, fee, withholdingTax);
        EnsureRateMatchesCurrency(grossAmount, appliedExchangeRate);

        if (source.ImportRunId is null)
        {
            throw new DomainException("Un movimiento importado tiene que referenciar su ejecución de importación.");
        }

        return new Transaction(Guid.NewGuid(), userId, accountId, type, assetId, quantity, unitPrice, grossAmount,
            fee, withholdingTax, occurredAt, TransactionOrigin.Imported, source, adjustmentReason: null,
            appliedExchangeRate, settledInCash, amountIsEstimated);
    }

    /// <summary>Un movimiento apuntado a mano: el dato normal de una cuenta cuando no llega por otra vía.</summary>
    public static Transaction FromManualEntry(
        UserId userId,
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Occurrence occurredAt,
        DateTimeOffset registeredAt,
        string? note = null,
        ExchangeRate? appliedExchangeRate = null)
    {
        EnsureManualType(type);
        EnsureConsistent(type, assetId, quantity, unitPrice, grossAmount, fee, withholdingTax: null);
        EnsureRateMatchesCurrency(grossAmount, appliedExchangeRate);

        var id = Guid.NewGuid();

        return new Transaction(id, userId, accountId, type, assetId, quantity, unitPrice, grossAmount,
            fee, withholdingTax: null, occurredAt, TransactionOrigin.Manual,
            TransactionSource.ForManualEntry(id), adjustmentReason: null, appliedExchangeRate)
        {
            Note = CleanNote(note),
            RegisteredAt = registeredAt,
        };
    }

    /// <summary>
    /// Cambia los datos de un movimiento apuntado a mano.
    /// </summary>
    /// <remarks>
    /// Sólo a mano, y a propósito: un importado tiene detrás el registro de la plataforma
    /// y un ajuste tiene su motivo; cambiar cualquiera de los dos en sitio borraría lo que
    /// defiende la cifra. Un apunte manual no tiene nada de eso que conservar.
    /// </remarks>
    public void Revise(
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Occurrence occurredAt,
        string? note,
        ExchangeRate? appliedExchangeRate,
        DateTimeOffset revisedAt)
    {
        if (Origin != TransactionOrigin.Manual)
        {
            throw new DomainException(Origin == TransactionOrigin.Imported
                ? "Un movimiento importado no se edita: se corrige o se anula."
                : "Un ajuste no se edita: se borra y se corrige de nuevo.");
        }

        EnsureManualType(type);
        EnsureConsistent(type, assetId, quantity, unitPrice, grossAmount, fee, withholdingTax: null);
        EnsureRateMatchesCurrency(grossAmount, appliedExchangeRate);

        AccountId = accountId;
        Type = type;
        AssetId = assetId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        GrossAmount = grossAmount;
        Fee = fee;
        OccurredAt = occurredAt;
        Note = CleanNote(note);
        AppliedExchangeRate = appliedExchangeRate;
        RevisedAt = revisedAt;
    }

    /// <summary>Deja un importado fuera del cálculo sin borrarlo.</summary>
    public void Void(string reason, DateTimeOffset at)
    {
        if (Origin != TransactionOrigin.Imported)
        {
            throw new DomainException(
                "Sólo se anula un movimiento importado. Uno apuntado a mano o un ajuste se borra.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Anular un movimiento necesita motivo: sin él no se sabe por qué falta.");
        }

        if (IsVoided)
        {
            throw new DomainException("El movimiento ya está anulado.");
        }

        VoidedAt = at;
        VoidReason = reason.Trim();
    }

    /// <summary>Devuelve al cálculo un movimiento anulado, con los mismos datos que tenía.</summary>
    public void Restore()
    {
        if (!IsVoided)
        {
            throw new DomainException("El movimiento no está anulado.");
        }

        VoidedAt = null;
        VoidReason = null;
    }

    /// <summary>
    /// Anota que este apunte manual y un importado que coincide con él son movimientos
    /// distintos, para que la coincidencia deje de señalarse.
    /// </summary>
    public void MarkDistinctFrom(Transaction imported)
    {
        ArgumentNullException.ThrowIfNull(imported);

        if (Origin != TransactionOrigin.Manual || imported.Origin != TransactionOrigin.Imported)
        {
            throw new DomainException("La distinción se anota en un movimiento manual frente a uno importado.");
        }

        DistinctFrom = imported.Id;
    }

    /// <summary>
    /// Vuelve a clasificar un movimiento que quedó sin clasificar.
    /// </summary>
    /// <remarks>
    /// Solo desde <see cref="TransactionType.Unknown"/>, y a propósito. Un movimiento ya
    /// clasificado puede haber entrado en un ejercicio presentado, y cambiarle el tipo
    /// alteraría cifras que alguien ya dio por buenas. Uno sin clasificar está fuera del
    /// cálculo, así que interpretarlo mejor no reescribe nada.
    ///
    /// Las cifras no se tocan: son las que trajo el origen, y lo que se corrige aquí es
    /// solo qué significan.
    /// </remarks>
    public void Reinterpret(TransactionType type)
    {
        if (Type != TransactionType.Unknown)
        {
            throw new DomainException(
                "Solo se reinterpreta un movimiento sin clasificar: cambiar uno ya clasificado alteraría cifras dadas por buenas.");
        }

        if (type == TransactionType.Unknown)
        {
            return;
        }

        EnsureConsistent(type, AssetId, Quantity, UnitPrice, GrossAmount, Fee, WithholdingTax);

        Type = type;
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
        EnsureManualType(type);
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

    /// <summary>
    /// Un apunte manual o un ajuste tiene que decir qué es.
    /// </summary>
    /// <remarks>
    /// Un movimiento sin clasificar existe porque la plataforma trajo un concepto que no se
    /// entendió. A mano no hay concepto que entender: quien lo apunta sabe qué es.
    /// </remarks>
    private static void EnsureManualType(TransactionType type)
    {
        if (type == TransactionType.Unknown)
        {
            throw new DomainException("Un movimiento apuntado a mano tiene que indicar su tipo.");
        }
    }

    private static string? CleanNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>Las invariantes viven en TransactionRules para que la importación pueda comprobarlas antes.</summary>
    private static void EnsureConsistent(
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Money? withholdingTax) =>
        TransactionRules.Ensure(type, assetId, quantity, unitPrice, grossAmount, fee, withholdingTax);

}
