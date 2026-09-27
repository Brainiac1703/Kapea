namespace Kapea.Domain.MarketData;

/// <summary>
/// Precio de cierre de un activo en un día, en euros, y su recorrido si se conoce.
/// </summary>
/// <remarks>
/// Es un catálogo global y no un dato de cada usuario: lo que valió bitcoin el martes
/// es lo mismo para todos, y guardarlo por duplicado multiplicaría las peticiones a los
/// proveedores sin añadir nada.
///
/// El origen viaja con el precio porque no todas las fuentes cubren los mismos activos
/// ni el mismo alcance: saber de dónde salió cada cifra permite volver a pedir solo lo
/// que trajo una fuente concreta si resulta estar mal.
///
/// El recorrido —apertura, máximo y mínimo— es opcional a propósito. Hay proveedores que
/// no lo dan, y sin él sólo se sabe dónde acabó el día: uno que subió un ocho por ciento
/// y volvió al punto de partida es indistinguible de uno plano. Nulo significa que ese
/// proveedor no lo da, que no es lo mismo que un recorrido de cero.
/// </remarks>
/// <param name="AssetId">Activo del catálogo al que corresponde.</param>
/// <param name="Date">Día del cierre.</param>
/// <param name="PriceInEuros">Precio de cierre en euros.</param>
/// <param name="Source">Proveedor del que se obtuvo.</param>
/// <param name="OpenInEuros">Primer precio del día.</param>
/// <param name="HighInEuros">Mayor precio del día.</param>
/// <param name="LowInEuros">Menor precio del día.</param>
public sealed record DailyPrice(
    Guid AssetId,
    DateOnly Date,
    decimal PriceInEuros,
    string Source,
    decimal? OpenInEuros = null,
    decimal? HighInEuros = null,
    decimal? LowInEuros = null)
{
    /// <summary>Se conoce lo que el activo se movió ese día, y no sólo dónde acabó.</summary>
    public bool HasRange => OpenInEuros is not null && HighInEuros is not null && LowInEuros is not null;

    /// <summary>
    /// El mismo precio con su recorrido, o sin él si lo que llega no se sostiene.
    /// </summary>
    /// <remarks>
    /// Un máximo menor que el mínimo, o un cierre fuera de los dos, es un dato roto. Se
    /// descarta el recorrido y se conserva el cierre: perder el día entero por un
    /// extremo mal traído sería peor que quedarse sin dibujar la vela.
    /// </remarks>
    public DailyPrice WithRange(decimal? open, decimal? high, decimal? low)
    {
        if (open is not { } o || high is not { } h || low is not { } l)
        {
            return this;
        }

        var coherent = l <= h
            && o >= l && o <= h
            && PriceInEuros >= l && PriceInEuros <= h
            && l > 0m;

        return coherent ? this with { OpenInEuros = o, HighInEuros = h, LowInEuros = l } : this;
    }
}
