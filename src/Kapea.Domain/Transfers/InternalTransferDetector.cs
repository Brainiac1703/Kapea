using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Transfers;

/// <summary>
/// Márgenes con los que se busca la pareja de un traspaso.
/// </summary>
/// <param name="Window">
/// Cuánto puede tardar la entrada respecto a la salida. Setenta y dos horas cubren un
/// fin de semana con una red congestionada.
/// </param>
/// <param name="QuantityTolerance">
/// Diferencia admisible entre lo enviado y lo recibido, por la comisión de red. Un dos
/// por ciento absorbe una comisión alta sin llegar a emparejar cantidades distintas.
/// </param>
public sealed record TransferMatchingOptions(TimeSpan Window, decimal QuantityTolerance)
{
    public static TransferMatchingOptions Default { get; } = new(TimeSpan.FromHours(72), 0.02m);
}

/// <summary>Pareja candidata: una salida y la entrada que probablemente le corresponde.</summary>
public sealed record TransferCandidate(Transaction Outgoing, Transaction Incoming);

/// <summary>
/// Busca salidas de una cuenta que se correspondan con entradas del mismo activo en
/// otra cuenta del usuario. Solo propone: la decisión es siempre de una persona.
/// </summary>
public static class InternalTransferDetector
{
    public static IReadOnlyList<TransferCandidate> Propose(
        IEnumerable<Transaction> transactions,
        IReadOnlySet<(Guid Outgoing, Guid Incoming)> alreadyResolved,
        TransferMatchingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(alreadyResolved);

        var settings = options ?? TransferMatchingOptions.Default;
        var all = transactions.ToList();

        var outgoing = all
            .Where(transaction => IsOutgoing(transaction) && transaction.AssetId is not null)
            .OrderBy(transaction => transaction.OccurredAt.Instant)
            .ToList();

        var incoming = all
            .Where(transaction => IsIncoming(transaction) && transaction.AssetId is not null)
            .OrderBy(transaction => transaction.OccurredAt.Instant)
            .ToList();

        var used = new HashSet<Guid>();
        var candidates = new List<TransferCandidate>();

        foreach (var sent in outgoing)
        {
            var match = incoming.FirstOrDefault(received =>
                !used.Contains(received.Id)
                && received.AssetId == sent.AssetId
                && received.AccountId != sent.AccountId
                && IsWithinWindow(sent, received, settings.Window)
                && IsWithinTolerance(sent.Quantity, received.Quantity, settings.QuantityTolerance)
                && !alreadyResolved.Contains((sent.Id, received.Id)));

            if (match is null)
            {
                continue;
            }

            used.Add(match.Id);
            candidates.Add(new TransferCandidate(sent, match));
        }

        return candidates;
    }

    private static bool IsOutgoing(Transaction transaction) =>
        transaction.Type is TransactionType.Withdrawal or TransactionType.Transfer && !transaction.Quantity.IsZero;

    private static bool IsIncoming(Transaction transaction) =>
        transaction.Type is TransactionType.Deposit or TransactionType.Transfer && !transaction.Quantity.IsZero;

    /// <summary>
    /// La entrada nunca precede a la salida: el activo no llega antes de haber salido,
    /// y aceptarlo emparejaría operaciones que no tienen relación.
    /// </summary>
    private static bool IsWithinWindow(Transaction sent, Transaction received, TimeSpan window)
    {
        var elapsed = received.OccurredAt.Instant - sent.OccurredAt.Instant;

        return elapsed >= TimeSpan.Zero && elapsed <= window;
    }

    private static bool IsWithinTolerance(Quantity sent, Quantity received, decimal tolerance)
    {
        if (sent.IsZero || received > sent)
        {
            return false;
        }

        return sent.Value - received.Value <= sent.Value * tolerance;
    }
}
