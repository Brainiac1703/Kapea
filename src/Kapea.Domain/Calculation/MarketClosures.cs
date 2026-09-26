using Kapea.Domain.MarketData;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Qué días no cotizó cada mercado.
/// </summary>
/// <remarks>
/// Sale de los propios datos: si ninguna acción del catálogo tiene precio el 4 de julio,
/// la bolsa estaba cerrada. Si cinco lo tienen y una no, a esa una le falta el dato.
///
/// La alternativa sería un calendario de festivos por bolsa, que habría que mantener con
/// sus fiestas móviles, que obligaría a saber a qué bolsa pertenece cada activo, y que se
/// equivocaría en silencio el día que cambiara. Los datos ya lo dicen.
///
/// La unidad es el mercado y no la clase, porque dentro de la renta variable conviven
/// bolsas con calendarios distintos: el 3 de julio de 2026 la estadounidense cerró por la
/// fiesta del 4 y la alemana operó. Agrupando por clase, ese día parecía abierto y a las
/// estadounidenses les faltaba el dato.
/// </remarks>
public sealed class MarketClosures
{
    /// <summary>
    /// Cuántos activos de un mercado hacen fiable la deducción.
    /// </summary>
    /// <remarks>
    /// Con uno solo no hay deducción posible: el día que le falte el dato no habría con
    /// qué contrastarlo y cualquier laguna pasaría por festivo. Antes de eso, se prefiere
    /// tratarlo como lo que era: un dato que falta. Es el límite conocido, y afinar a
    /// mercado lo hace más fácil de alcanzar que agrupando por clase.
    /// </remarks>
    private const int Enough = 2;

    private readonly Dictionary<string, HashSet<DateOnly>> _closed;

    private MarketClosures(Dictionary<string, HashSet<DateOnly>> closed) => _closed = closed;

    /// <summary>Nada cerrado: lo que se usa cuando no se conocen los mercados.</summary>
    public static MarketClosures None { get; } = new([]);

    /// <summary>Ese mercado no cotizó ese día.</summary>
    public bool WasClosed(string market, DateOnly date) =>
        _closed.TryGetValue(market, out var days) && days.Contains(date);

    /// <summary>
    /// Deduce los cierres a partir de qué activos de cada mercado tienen precio cada día.
    /// </summary>
    /// <param name="prices">Serie por activo, del rango que se va a mirar.</param>
    /// <param name="markets">Mercado de cada activo. Lo que no esté aquí no cuenta.</param>
    public static MarketClosures Of(
        IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>> prices,
        IReadOnlyDictionary<Guid, string> markets,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(markets);

        var closed = new Dictionary<string, HashSet<DateOnly>>();

        // Sólo cuentan los activos que tienen alguna cotización en el rango: uno que el
        // proveedor no cubre en absoluto no dice nada de si la bolsa abrió.
        var byMarket = prices
            .Where(entry => entry.Value.Count > 0 && markets.ContainsKey(entry.Key))
            .GroupBy(entry => markets[entry.Key], StringComparer.Ordinal);

        foreach (var group in byMarket)
        {
            var assets = group.ToList();

            if (assets.Count < Enough)
            {
                continue;
            }

            var quoted = new HashSet<DateOnly>(assets.SelectMany(entry => entry.Value).Select(price => price.Date));
            var days = new HashSet<DateOnly>();

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (!quoted.Contains(day))
                {
                    days.Add(day);
                }
            }

            closed[group.Key] = days;
        }

        return new MarketClosures(closed);
    }
}
