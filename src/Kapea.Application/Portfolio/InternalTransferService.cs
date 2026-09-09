using Kapea.Application.Abstractions;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Portfolio;

/// <summary>Lo que la detección de traspasos necesita leer y escribir.</summary>
public interface IInternalTransferRepository
{
    Task<IReadOnlyList<Transaction>> ListTransactionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InternalTransfer>> ListTransfersAsync(CancellationToken cancellationToken = default);

    Task AddAsync(IReadOnlyList<InternalTransfer> transfers, CancellationToken cancellationToken = default);

    Task<InternalTransfer?> FindAsync(Guid transferId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Propone traspasos internos y resuelve los que el usuario decide.
/// </summary>
/// <remarks>
/// Se ejecuta después de cada importación, que es cuando pueden aparecer las dos patas
/// de un traspaso. Nunca confirma por su cuenta: propone y espera. Un falso positivo
/// convertiría una venta real en traspaso y borraría un hecho imponible; un falso
/// negativo solo genera una propuesta de más que alguien descarta.
/// </remarks>
public sealed class InternalTransferService(
    IInternalTransferRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<InternalTransferService> logger)
{
    public async Task<int> ProposeAsync(
        TransferMatchingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var transactions = await repository.ListTransactionsAsync(cancellationToken).ConfigureAwait(false);
        var existing = await repository.ListTransfersAsync(cancellationToken).ConfigureAwait(false);

        // Se excluyen todos los emparejamientos ya vistos, no solo los rechazados: uno
        // confirmado tampoco debe volver a proponerse, y uno pendiente ya está en la lista.
        var known = existing
            .Select(transfer => (transfer.OutgoingTransactionId, transfer.IncomingTransactionId))
            .ToHashSet();

        var candidates = InternalTransferDetector.Propose(transactions, known, options);
        var accounts = transactions.ToDictionary(transaction => transaction.Id, transaction => transaction.AccountId);
        var now = timeProvider.GetUtcNow();

        var proposals = candidates
            .Select(candidate => InternalTransfer.Propose(
                currentUser.Id,
                candidate.Outgoing.Id,
                candidate.Incoming.Id,
                accounts[candidate.Outgoing.Id],
                accounts[candidate.Incoming.Id],
                candidate.Outgoing.AssetId!.Value,
                candidate.Outgoing.Quantity,
                candidate.Incoming.Quantity,
                now))
            .ToList();

        if (proposals.Count == 0)
        {
            return 0;
        }

        await repository.AddAsync(proposals, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Propuestos {Total} traspasos internos pendientes de revisión.", proposals.Count);

        return proposals.Count;
    }

    /// <summary>Aplica la decisión del usuario. Devuelve el activo afectado, para recalcularlo.</summary>
    public async Task<Guid?> ResolveAsync(
        Guid transferId,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        var transfer = await repository.FindAsync(transferId, cancellationToken).ConfigureAwait(false);

        if (transfer is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        if (confirm)
        {
            transfer.Confirm(now);
        }
        else
        {
            transfer.Reject(now);
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return transfer.AssetId;
    }
}
