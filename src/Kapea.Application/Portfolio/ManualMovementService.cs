using Kapea.Application.Abstractions;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Portfolio;

/// <summary>Los datos de un movimiento tal como se escriben en el formulario.</summary>
/// <param name="AssetSymbol">Símbolo del activo, o nulo en un movimiento sólo de dinero.</param>
/// <param name="AssetClass">Clase del activo, para darlo de alta si aún no existe.</param>
/// <param name="Text">Nota de un movimiento manual o motivo de una corrección, según el caso.</param>
public sealed record ManualMovementInput(
    Guid AccountId,
    TransactionType Type,
    string? AssetSymbol,
    AssetClass? AssetClass,
    decimal Quantity,
    decimal? UnitPrice,
    decimal GrossAmount,
    string Currency,
    decimal Fee,
    DateTimeOffset OccurredAt,
    string TimeZoneId,
    string? Text);

/// <summary>El movimiento que se pide está en un traspaso confirmado y no se puede tocar.</summary>
public sealed class MovementInConfirmedTransferException(Guid transferId)
    : DomainException($"El movimiento forma parte del traspaso confirmado {transferId}. Deshacer traspasos no está disponible.")
{
    public Guid TransferId { get; } = transferId;
}

/// <summary>Lo que el servicio de movimientos manuales necesita leer y escribir.</summary>
public interface IManualMovementRepository
{
    Task<PlatformAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Un movimiento del usuario, anulado o no.</summary>
    Task<Transaction?> FindTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>El traspaso confirmado en el que participa el movimiento, si lo hay.</summary>
    Task<Guid?> FindConfirmedTransferAsync(Guid transactionId, CancellationToken cancellationToken = default);

    void Add(Transaction transaction);

    void Remove(Transaction transaction);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Movimientos apuntados a mano y correcciones de importados.
/// </summary>
/// <remarks>
/// No recalcula: devuelve el control y quien lo llama recalcula, igual que tras confirmar
/// una importación. Así un recálculo lento no queda dentro de la misma transacción que la
/// escritura, y un fallo del cálculo no deshace un dato que sí es correcto.
/// </remarks>
public sealed class ManualMovementService(
    IManualMovementRepository repository,
    IAssetCatalog assets,
    IExchangeRateProvider exchangeRates,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ManualMovementService> logger)
{
    public async Task<Transaction> RegisterAsync(ManualMovementInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var data = await ResolveAsync(input, cancellationToken).ConfigureAwait(false);

        var movement = Transaction.FromManualEntry(
            currentUser.Id, data.AccountId, input.Type, data.AssetId, data.Quantity, data.UnitPrice, data.Gross,
            data.Fee, data.OccurredAt, timeProvider.GetUtcNow(), input.Text, data.Rate);

        repository.Add(movement);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Movimiento manual {Id} registrado en la cuenta {Cuenta}.", movement.Id, data.AccountId);

        return movement;
    }

    public async Task<Transaction> ReviseAsync(
        Guid transactionId,
        ManualMovementInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var movement = await RequireAsync(transactionId, cancellationToken).ConfigureAwait(false);
        var data = await ResolveAsync(input, cancellationToken).ConfigureAwait(false);

        movement.Revise(
            data.AccountId, input.Type, data.AssetId, data.Quantity, data.UnitPrice, data.Gross, data.Fee,
            data.OccurredAt, input.Text, data.Rate, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Movimiento manual {Id} editado.", movement.Id);

        return movement;
    }

    /// <summary>Borra un movimiento manual o un ajuste. Un importado se anula, no se borra.</summary>
    public async Task DeleteAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var movement = await RequireAsync(transactionId, cancellationToken).ConfigureAwait(false);

        if (movement.Origin == TransactionOrigin.Imported)
        {
            throw new DomainException(
                "Un movimiento importado no se borra suelto: se corrige, se anula o se elimina con su importación completa.");
        }

        repository.Remove(movement);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Movimiento {Origen} {Id} borrado.", movement.Origin, movement.Id);
    }

    public async Task<Transaction> VoidAsync(Guid transactionId, string reason, CancellationToken cancellationToken = default)
    {
        var movement = await RequireNotInConfirmedTransferAsync(transactionId, cancellationToken).ConfigureAwait(false);

        movement.Void(reason, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Movimiento {Id} anulado.", movement.Id);

        return movement;
    }

    public async Task<Transaction> RestoreAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var movement = await RequireAsync(transactionId, cancellationToken).ConfigureAwait(false);

        movement.Restore();
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Anulación del movimiento {Id} deshecha.", movement.Id);

        return movement;
    }

    /// <summary>
    /// Anula un importado y registra el ajuste con los datos corregidos, en un solo guardado.
    /// </summary>
    /// <remarks>
    /// El ajuste se construye antes de anular nada: si sus datos no pasan las reglas, la
    /// excepción sale con el importado todavía vigente y sin nada guardado.
    /// </remarks>
    public async Task<Transaction> CorrectAsync(
        Guid transactionId,
        ManualMovementInput corrected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corrected);

        var imported = await RequireNotInConfirmedTransferAsync(transactionId, cancellationToken).ConfigureAwait(false);

        if (imported.Origin != TransactionOrigin.Imported)
        {
            throw new DomainException("Sólo se corrige un movimiento importado. Uno manual se edita y un ajuste se borra.");
        }

        var data = await ResolveAsync(corrected, cancellationToken).ConfigureAwait(false);
        var reason = corrected.Text ?? string.Empty;

        var adjustment = Transaction.FromManualAdjustment(
            currentUser.Id, data.AccountId, corrected.Type, data.AssetId, data.Quantity, data.UnitPrice, data.Gross,
            data.Fee, data.OccurredAt, Guid.NewGuid(), reason, data.Rate);

        imported.Void(reason, timeProvider.GetUtcNow());
        repository.Add(adjustment);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Movimiento {Id} corregido con el ajuste {Ajuste}.", imported.Id, adjustment.Id);

        return adjustment;
    }

    /// <summary>Anota que un apunte manual y un importado que coinciden son movimientos distintos.</summary>
    public async Task MarkDistinctAsync(Guid manualId, Guid importedId, CancellationToken cancellationToken = default)
    {
        var manual = await RequireAsync(manualId, cancellationToken).ConfigureAwait(false);
        var imported = await RequireAsync(importedId, cancellationToken).ConfigureAwait(false);

        manual.MarkDistinctFrom(imported);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Transaction> RequireAsync(Guid transactionId, CancellationToken cancellationToken) =>
        await repository.FindTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false)
        ?? throw new ImportTargetException($"El movimiento {transactionId} no existe.");

    private async Task<Transaction> RequireNotInConfirmedTransferAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var movement = await RequireAsync(transactionId, cancellationToken).ConfigureAwait(false);

        if (await repository.FindConfirmedTransferAsync(transactionId, cancellationToken).ConfigureAwait(false) is { } transfer)
        {
            throw new MovementInConfirmedTransferException(transfer);
        }

        return movement;
    }

    /// <summary>Traduce el formulario a valores del dominio: cuenta del usuario, activo, importes y tipo de cambio.</summary>
    private async Task<ResolvedMovement> ResolveAsync(ManualMovementInput input, CancellationToken cancellationToken)
    {
        // Cuenta ajena o inexistente dan la misma respuesta: el filtro por usuario la deja
        // fuera y no hay forma de saber desde aquí que existe.
        var account = await repository.FindAccountAsync(input.AccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new ImportTargetException($"La cuenta {input.AccountId} no existe.");

        var currency = Currency.FromCode(input.Currency);
        var occurredAt = Occurrence.FromOffset(input.OccurredAt, input.TimeZoneId);

        Guid? assetId = null;

        if (!string.IsNullOrWhiteSpace(input.AssetSymbol))
        {
            var asset = await assets
                .ResolveAsync(input.AssetSymbol.Trim(), input.AssetClass ?? AssetClass.Crypto, cancellationToken)
                .ConfigureAwait(false);

            assetId = asset.Id;
        }

        return new ResolvedMovement(
            account.Id,
            assetId,
            new Quantity(input.Quantity),
            input.UnitPrice is { } price ? new Money(price, currency) : null,
            new Money(input.GrossAmount, currency),
            new Money(input.Fee, currency),
            occurredAt,
            await ResolveRateAsync(currency, occurredAt, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// El tipo de la fecha del movimiento, con el mismo criterio y el mismo mensaje que al importar.
    /// </summary>
    private async Task<ExchangeRate?> ResolveRateAsync(Currency currency, Occurrence occurredAt, CancellationToken cancellationToken)
    {
        if (currency.IsEuro)
        {
            return null;
        }

        var date = DateOnly.FromDateTime(occurredAt.InSourceTimeZone.Date);

        return await exchangeRates.ResolveAsync(currency, date, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException(
                $"No hay tipo de cambio de {currency.Code} para el {date:yyyy-MM-dd}: ingesta los tipos antes de importar.");
    }

    private sealed record ResolvedMovement(
        Guid AccountId,
        Guid? AssetId,
        Quantity Quantity,
        Money? UnitPrice,
        Money Gross,
        Money Fee,
        Occurrence OccurredAt,
        ExchangeRate? Rate);
}
