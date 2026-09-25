namespace Kapea.Domain.Strategies;

/// <summary>
/// Desde cuándo hace falta tener precios para poder evaluar un sistema.
/// </summary>
/// <remarks>
/// Un sistema declara cuántos días necesita su ventana más larga. Hace falta el doble
/// para que esa ventana esté completa desde el primer día evaluado, y un año más para
/// que lo evaluado tenga recorrido suficiente como para significar algo.
///
/// Vive aquí y no dentro del motor porque la descarga de precios necesita la misma
/// cuenta: si bajara menos histórico del que el motor pide, un activo recién añadido
/// nunca llegaría a evaluarse y nadie sabría por qué.
/// </remarks>
public static class StrategyLookback
{
    /// <summary>Lo que se mira hacia atrás cuando no hay ningún sistema declarado.</summary>
    public const int DefaultDays = 365;

    public static DateOnly From(DateOnly today, int requiredDays) =>
        today.AddDays(-Days(requiredDays));

    public static int Days(int requiredDays) =>
        requiredDays <= 0 ? DefaultDays : (requiredDays * 2) + DefaultDays;
}
