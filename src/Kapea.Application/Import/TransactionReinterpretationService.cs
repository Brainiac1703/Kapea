using Kapea.Domain.Accounts;
using Kapea.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Import;

/// <summary>Movimientos sin clasificar y con qué se importaron.</summary>
public interface IReinterpretationRepository
{
    Task<IReadOnlyList<(Transaction Transaction, PlatformCode Platform)>> ListUnclassifiedAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Vuelve a interpretar los movimientos que quedaron sin clasificar.
/// </summary>
/// <remarks>
/// Un movimiento sin clasificar es un hueco reconocido: entró tal cual llegó y quedó
/// fuera del cálculo en lugar de adivinarse. Cuando el adaptador aprende a entender esa
/// forma, esto lo aprovecha sin volver a pedir nada a la plataforma ni tocar las cifras,
/// que siguen siendo las que trajo el origen.
/// </remarks>
public sealed class TransactionReinterpretationService(
    IReinterpretationRepository repository,
    IEnumerable<IRecordReinterpreter> reinterpreters,
    ILogger<TransactionReinterpretationService> logger)
{
    private readonly Dictionary<PlatformCode, IRecordReinterpreter> _byPlatform =
        reinterpreters.ToDictionary(reinterpreter => reinterpreter.Platform);

    public async Task<ReinterpretationResult> ReinterpretAsync(CancellationToken cancellationToken = default)
    {
        var pending = await repository.ListUnclassifiedAsync(cancellationToken).ConfigureAwait(false);

        var reclassified = 0;
        var stillUnknown = 0;
        var notSupported = 0;

        foreach (var (transaction, platform) in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (transaction.Source.RawContent is not { Length: > 0 } raw
                || !_byPlatform.TryGetValue(platform, out var reinterpreter))
            {
                notSupported++;

                continue;
            }

            var record = reinterpreter.Reinterpret(raw);

            if (record is null || record.Type == TransactionType.Unknown)
            {
                stillUnknown++;

                continue;
            }

            transaction.Reinterpret(record.Type);
            reclassified++;
        }

        if (reclassified > 0)
        {
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Reinterpretación terminada: {Clasificados} clasificados, {Desconocidos} siguen sin significar nada y {NoAdmitidos} no se pueden releer.",
            reclassified, stillUnknown, notSupported);

        return new ReinterpretationResult(reclassified, stillUnknown, notSupported);
    }
}
