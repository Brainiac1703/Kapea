namespace Kapea.Client.Layout;

/// <summary>Un periodo de la escala, con el texto que lo nombra.</summary>
/// <param name="Days">Cuántos días atrás. Sin valor, todo lo que haya.</param>
public sealed record ChartPeriod(string Label, int? Days);

/// <summary>
/// La escala de periodos de las gráficas, y los intervalos en que se agrupan.
/// </summary>
/// <remarks>
/// Saltar de un año a todo obliga a mirar años cuando se quieren ver meses. La escala va
/// de una semana a todo el histórico, como las de Bit2Me, XTB o la aplicación de Bolsa.
///
/// El periodo y el intervalo son dos controles y no uno: el primero dice cuánto se mira
/// y el segundo cada cuánto se agrupa lo que se mira. Confundirlos es lo que hace que
/// una escala de «1h, 4h, 1d» signifique cosas distintas según la aplicación.
/// </remarks>
public static class ChartPeriods
{
    /// <summary>Los intervalos que se ofrecen, en el orden en que crecen.</summary>
    public static IReadOnlyList<string> Intervals { get; } = ["Daily", "Weekly", "Monthly"];

    /// <summary>
    /// La escala completa, calculada para un día concreto.
    /// </summary>
    /// <remarks>
    /// «Lo que va de año» depende de la fecha, así que la escala se calcula y no se
    /// declara: el 2 de enero son dos días y el 30 de diciembre son casi trescientos
    /// sesenta y cinco.
    /// </remarks>
    public static IReadOnlyList<ChartPeriod> Scale(DateOnly today) =>
    [
        new("Evolution_Period_Week", 7),
        new("Evolution_Period_Month", 30),
        new("Evolution_Period_Quarter", 90),
        new("Evolution_Period_HalfYear", 180),
        new("Evolution_Period_YearToDate", YearToDate(today)),
        new("Evolution_Period_Year", 365),
        new("Evolution_Period_TwoYears", 730),
        new("Evolution_Period_FiveYears", 1825),
        new("Evolution_Period_All", null),
    ];

    /// <summary>El texto que nombra un intervalo.</summary>
    public static string Label(string interval) => $"Evolution_Interval_{interval}";

    /// <summary>
    /// Cuántos días van de año.
    /// </summary>
    /// <remarks>
    /// Al menos uno: el 1 de enero no van cero días, va el propio día, y pedir un rango
    /// vacío devolvería una gráfica sin nada en lugar de la de hoy.
    /// </remarks>
    private static int YearToDate(DateOnly today) =>
        Math.Max(1, today.DayNumber - new DateOnly(today.Year, 1, 1).DayNumber);
}
