using Kapea.Application.Abstractions;
using Kapea.Domain.Calculation;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Portfolio;

/// <summary>Lo que el cálculo necesita leer para reconstruir la cartera.</summary>
public interface IPortfolioCalculationRepository
{
    /// <summary>Movimientos del usuario, opcionalmente de un solo activo.</summary>
    Task<IReadOnlyList<Transaction>> ListTransactionsAsync(
        Guid? assetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InternalTransfer>> ListTransfersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Recalcula la proyección de la cartera a partir de los movimientos.
/// </summary>
/// <remarks>
/// Se recalcula desde cero por activo en lugar de conciliar diferencias. A la escala de
/// un usuario particular el coste es despreciable y desaparece toda una clase de
/// errores: un movimiento con fecha anterior a otros ya calculados no obliga a deshacer
/// nada, simplemente entra en el orden que le toca.
/// </remarks>
public sealed class PortfolioCalculationService(
    IPortfolioCalculationRepository repository,
    IPortfolioProjectionStore projections,
    ICurrentUser currentUser,
    ILogger<PortfolioCalculationService> logger)
{
    public async Task<IReadOnlyList<AssetCalculationResult>> RecalculateAsync(
        Guid? assetId = null,
        CancellationToken cancellationToken = default)
    {
        var transactions = await repository.ListTransactionsAsync(assetId, cancellationToken).ConfigureAwait(false);
        var transfers = await repository.ListTransfersAsync(cancellationToken).ConfigureAwait(false);

        var valued = Value(transactions, transfers);
        var results = new List<AssetCalculationResult>();

        foreach (var group in valued.Where(item => item.AssetId is not null).GroupBy(item => item.AssetId!.Value))
        {
            var result = FifoCalculator.Calculate(currentUser.Id, group.Key, group);

            await projections.ReplaceAsync(group.Key, result, cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        logger.LogInformation(
            "Cartera recalculada: {Activos} activos, {Movimientos} movimientos.", results.Count, transactions.Count);

        return results;
    }

    /// <summary>
    /// Empareja cada movimiento con lo que el motor necesita saber de él: su valoración
    /// en euros con el tipo congelado y, si procede, el traspaso interno al que pertenece.
    /// </summary>
    public static IReadOnlyList<ValuedTransaction> Value(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<InternalTransfer> transfers)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(transfers);

        var byOutgoing = transfers.ToDictionary(transfer => transfer.OutgoingTransactionId);
        var byIncoming = transfers.ToDictionary(transfer => transfer.IncomingTransactionId);
        var accountsById = transactions.ToDictionary(transaction => transaction.Id, transaction => transaction.AccountId);

        return
        [
            .. transactions.Select(transaction =>
            {
                if (byOutgoing.TryGetValue(transaction.Id, out var outgoing))
                {
                    return ValuedTransaction.From(
                        transaction,
                        internalTransferId: outgoing.IsConfirmed ? outgoing.Id : null,
                        transferDestinationAccountId: outgoing.IsConfirmed
                            ? accountsById.GetValueOrDefault(outgoing.IncomingTransactionId, outgoing.DestinationAccountId)
                            : null,
                        isPendingTransferReview: outgoing.IsPending);
                }

                if (byIncoming.TryGetValue(transaction.Id, out var incoming))
                {
                    return ValuedTransaction.From(
                        transaction,
                        internalTransferId: incoming.IsConfirmed ? incoming.Id : null,
                        isPendingTransferReview: incoming.IsPending);
                }

                return ValuedTransaction.From(transaction);
            }),
        ];
    }
}
