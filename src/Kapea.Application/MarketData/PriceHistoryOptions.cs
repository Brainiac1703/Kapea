namespace Kapea.Application.MarketData;

/// <summary>
/// Hasta dónde se quiere la serie de precios.
/// </summary>
/// <remarks>
/// Una fecha y no «sin límite» porque los proveedores piden un rango: sin suelo acabaría
/// habiendo uno igualmente, escondido dentro de cada adaptador y distinto en cada uno.
/// Configurada, además, se puede mover sin tocar el programa.
///
/// No es lo que hay que cubrir sino hasta dónde interesa llegar. El mínimo —la primera
/// adquisición y lo que los sistemas declarados necesiten— se garantiza aparte y manda
/// cuando es más antiguo.
/// </remarks>
public sealed class PriceHistoryOptions
{
    public const string SectionName = "PriceHistory";

    /// <summary>
    /// Anterior a la cotización de cualquier activo que pueda interesar, para que el
    /// límite lo ponga el proveedor y no esta fecha.
    /// </summary>
    public DateOnly EarliestFrom { get; set; } = new(2000, 1, 1);
}
