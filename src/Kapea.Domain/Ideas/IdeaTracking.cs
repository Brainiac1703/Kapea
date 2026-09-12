using Kapea.Domain.MarketData;

namespace Kapea.Domain.Ideas;

/// <summary>Cómo acabó una idea y cuándo.</summary>
public sealed record IdeaResolution(IdeaOutcome Outcome, DateOnly? On);

/// <summary>
/// Sigue una idea contra la serie de precios hasta que acabe.
/// </summary>
/// <remarks>
/// Mira día a día desde la publicación. Si un día toca los dos niveles, gana el de salida:
/// con un cierre diario no se sabe qué se tocó primero, y darle el objetivo sería
/// contarse una historia favorable, la misma razón por la que el simulador hace lo mismo.
/// </remarks>
public static class IdeaTracking
{
    /// <summary>Días tras los que una idea sin resolver se considera caducada.</summary>
    public const int DefaultExpiryDays = 90;

    public static IdeaResolution Resolve(
        ExternalIdea idea,
        IReadOnlyList<DailyPrice> series,
        DateOnly today,
        int expiryDays = DefaultExpiryDays)
    {
        ArgumentNullException.ThrowIfNull(idea);
        ArgumentNullException.ThrowIfNull(series);

        if (!idea.CanBeTracked)
        {
            return new IdeaResolution(IdeaOutcome.Open, null);
        }

        var expires = idea.PublishedOn.AddDays(expiryDays);

        foreach (var price in series.Where(price => price.Date >= idea.PublishedOn).OrderBy(price => price.Date))
        {
            if (price.Date > expires)
            {
                break;
            }

            if (Stopped(idea, price.PriceInEuros))
            {
                return new IdeaResolution(IdeaOutcome.Stopped, price.Date);
            }

            if (Reached(idea, price.PriceInEuros))
            {
                return new IdeaResolution(IdeaOutcome.Reached, price.Date);
            }
        }

        // Caducada solo cuando el plazo ya pasó: mientras no haya pasado, sigue viva.
        return today > expires
            ? new IdeaResolution(IdeaOutcome.Expired, expires)
            : new IdeaResolution(IdeaOutcome.Open, null);
    }

    private static bool Stopped(ExternalIdea idea, decimal price) => idea.StopLoss is { } stop
        && (idea.Direction == IdeaDirection.Buy ? price <= stop.Amount : price >= stop.Amount);

    private static bool Reached(ExternalIdea idea, decimal price) => idea.Target is { } target
        && (idea.Direction == IdeaDirection.Buy ? price >= target.Amount : price <= target.Amount);
}

/// <summary>Lo que una fuente ha dado hasta ahora.</summary>
/// <param name="Reached">Ideas que llegaron a su objetivo.</param>
/// <param name="Stopped">Ideas que saltaron por su nivel de salida.</param>
/// <param name="ReturnNetOfFees">
/// Lo que habría rendido seguirlas todas con el mismo capital, ya descontadas las
/// comisiones de entrada y salida. Nulo mientras ninguna se haya resuelto.
/// </param>
public sealed record SourceBalance(
    Guid SourceId,
    string SourceName,
    int Reached,
    int Stopped,
    int Expired,
    int Open,
    decimal? ReturnNetOfFees);

/// <summary>
/// Resume lo que ha dado una fuente.
/// </summary>
/// <remarks>
/// Con las comisiones dentro, porque seguir una idea cuesta lo mismo que seguir una señal:
/// una fuente que acierta dos de cada tres puede salir perdiendo si cada operación se
/// lleva un dos por ciento.
/// </remarks>
public static class SourceBalanceCalculator
{
    public static SourceBalance Of(
        Guid sourceId,
        string sourceName,
        IReadOnlyList<ExternalIdea> ideas,
        decimal feeRatePerSide)
    {
        ArgumentNullException.ThrowIfNull(ideas);

        var resolved = ideas.Where(idea => idea.Outcome is IdeaOutcome.Reached or IdeaOutcome.Stopped).ToList();

        return new SourceBalance(
            sourceId,
            sourceName,
            ideas.Count(idea => idea.Outcome == IdeaOutcome.Reached),
            ideas.Count(idea => idea.Outcome == IdeaOutcome.Stopped),
            ideas.Count(idea => idea.Outcome == IdeaOutcome.Expired),
            ideas.Count(idea => idea.Outcome == IdeaOutcome.Open),
            resolved.Count == 0 ? null : Return(resolved, feeRatePerSide));
    }

    /// <summary>
    /// Lo que habría rendido seguirlas todas con el mismo capital en cada una.
    /// </summary>
    /// <remarks>
    /// Se mide entre el nivel de entrada y el que se alcanzó. Sin entrada declarada la
    /// idea no entra en el cálculo: sin saber a qué precio se habría comprado, el
    /// rendimiento sería inventado.
    /// </remarks>
    private static decimal Return(List<ExternalIdea> resolved, decimal feeRatePerSide)
    {
        var total = 0m;
        var counted = 0;

        foreach (var idea in resolved)
        {
            if (idea.Entry is not { } entry || entry.Amount <= 0m)
            {
                continue;
            }

            var exit = idea.Outcome == IdeaOutcome.Reached ? idea.Target : idea.StopLoss;

            if (exit is not { } level)
            {
                continue;
            }

            var move = idea.Direction == IdeaDirection.Buy
                ? (level.Amount - entry.Amount) / entry.Amount
                : (entry.Amount - level.Amount) / entry.Amount;

            total += move - (2m * feeRatePerSide);
            counted++;
        }

        return counted == 0 ? 0m : total / counted;
    }
}
