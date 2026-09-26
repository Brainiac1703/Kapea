using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Qué días no cotizó el mercado de cada clase de activo.
/// </summary>
/// <remarks>
/// Sale de los propios datos: si ninguna acción del catálogo tiene precio el 4 de julio,
/// la bolsa estaba cerrada. Si cinco lo tienen y una no, a esa una le falta el dato.
///
/// La alternativa sería un calendario de festivos por bolsa, que habría que mantener con
/// sus fiestas móviles, que obligaría a saber a qué bolsa pertenece cada activo, y que se
/// equivocaría en silencio el día que cambiara. Los datos ya lo dicen.
///
/// La clase es la unidad porque es lo que Kapea distingue hoy. Si algún día conviven
/// dentro de la renta variable bolsas con calendarios muy distintos, habrá que afinar a
/// mercado en lugar de a clase.
/// </remarks>
public sealed class MarketClosures
{
    /// <summary>
    /// Cuántos activos de una clase hacen fiable la deducción.
    /// </summary>
    /// <remarks>
    /// Con uno solo no hay deducción posible: el día que le falte el dato no habría con
    /// qué contrastarlo y cualquier laguna pasaría por festivo. Antes de eso, se prefiere
    /// tratarlo como lo que era: un dato que falta.
    /// </remarks>
    private const int Enough = 2;

    private readonly Dictionary<AssetClass, HashSet<DateOnly>> _closed;

    private MarketClosures(Dictionary<AssetClass, HashSet<DateOnly>> closed) => _closed = closed;

    /// <summary>Nada cerrado: lo que se usa cuando no se conocen las clases.</summary>
    public static MarketClosures None { get; } = new([]);

    /// <summary>El mercado de ese activo no cotizó ese día.</summary>
    public bool WasClosed(AssetClass assetClass, DateOnly date) =>
        _closed.TryGetValue(assetClass, out var days) && days.Contains(date);

    /// <summary>
    /// Deduce los cierres a partir de qué activos de cada clase tienen precio cada día.
    /// </summary>
    /// <param name="prices">Serie por activo, del rango que se va a mirar.</param>
    /// <param name="classes">Clase de cada activo. Lo que no esté aquí no cuenta.</param>
    public static MarketClosures Of(
        IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>> prices,
        IReadOnlyDictionary<Guid, AssetClass> classes,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(classes);

        var closed = new Dictionary<AssetClass, HashSet<DateOnly>>();

        // Sólo cuentan los activos que tienen alguna cotización en el rango: uno que el
        // proveedor no cubre en absoluto no dice nada de si la bolsa abrió.
        var byClass = prices
            .Where(entry => entry.Value.Count > 0 && classes.ContainsKey(entry.Key))
            .GroupBy(entry => classes[entry.Key]);

        foreach (var group in byClass)
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
