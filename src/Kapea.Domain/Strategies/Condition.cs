using Kapea.Domain.Common;

namespace Kapea.Domain.Strategies;

/// <summary>De dónde sale el número que una condición compara.</summary>
public enum Operand
{
    /// <summary>Cierre del día.</summary>
    Price = 1,

    SimpleMovingAverage = 2,

    ExponentialMovingAverage = 3,

    RelativeStrengthIndex = 4,

    MacdLine = 5,

    MacdSignal = 6,

    /// <summary>Línea menos señal. Su cambio de signo es el cruce.</summary>
    MacdDistance = 7,

    BollingerUpper = 8,

    BollingerMiddle = 9,

    BollingerLower = 10,

    /// <summary>Cuánto se mueve el activo en un día, en media.</summary>
    AverageDailyRange = 11,

    /// <summary>Un número fijo declarado en la propia regla.</summary>
    Constant = 12,
}

/// <summary>Cómo se comparan los dos lados de una condición.</summary>
public enum Comparison
{
    GreaterThan = 1,

    LessThan = 2,

    /// <summary>Hoy está por encima y ayer no. Es lo que distingue un cruce de un estado.</summary>
    CrossesAbove = 3,

    CrossesBelow = 4,
}

/// <summary>Cómo se combinan varias condiciones.</summary>
public enum Junction
{
    /// <summary>Una comparación suelta.</summary>
    Comparison = 1,

    /// <summary>Todas sus hijas.</summary>
    All = 2,

    /// <summary>Cualquiera de sus hijas.</summary>
    Any = 3,
}

/// <summary>
/// Un lado de una comparación.
/// </summary>
/// <param name="Window">Días del indicador. Nulo en el precio y en una constante.</param>
/// <param name="Value">Número fijo. Solo en una constante.</param>
public sealed record Term(Operand Operand, int? Window = null, decimal? Value = null)
{
    public static Term Of(decimal value) => new(Operand.Constant, Value: value);

    public static Term Close => new(Operand.Price);

    public static Term Indicator(Operand operand, int window) => new(operand, window);

    internal string Describe() => Operand switch
    {
        Operand.Constant => Value?.ToString("0.####", System.Globalization.CultureInfo.CurrentCulture) ?? "?",
        Operand.Price => "precio",
        _ => Window is { } window ? $"{Operand} de {window} días" : Operand.ToString(),
    };
}

/// <summary>
/// Una condición de un sistema, sola o combinada con otras.
/// </summary>
/// <remarks>
/// Es un árbol plano con un discriminador en lugar de una jerarquía de tipos: así se
/// guarda y se recupera como JSON sin configurar polimorfismo, y el motor puede recorrerlo
/// sin conocer subtipos. Una regla que el motor no sepa evaluar se rechaza al guardarla,
/// no al ejecutarla.
/// </remarks>
public sealed record Condition(
    Junction Junction,
    Term? Left = null,
    Comparison? Comparison = null,
    Term? Right = null,
    IReadOnlyList<Condition>? Children = null)
{
    public static Condition When(Term left, Comparison comparison, Term right) =>
        new(Strategies.Junction.Comparison, left, comparison, right);

    public static Condition All(params Condition[] children) =>
        new(Strategies.Junction.All, Children: children);

    public static Condition Any(params Condition[] children) =>
        new(Strategies.Junction.Any, Children: children);

    /// <summary>Días de histórico que la condición necesita para poder evaluarse.</summary>
    public int RequiredDays => Junction == Strategies.Junction.Comparison
        ? Math.Max(Window(Left), Window(Right))
        : (Children ?? []).Select(child => child.RequiredDays).DefaultIfEmpty(0).Max();

    /// <summary>Lanza si la condición está mal formada.</summary>
    /// <remarks>
    /// Se comprueba al guardar y no al evaluar: una regla incompleta descubierta en medio
    /// de una simulación deja media cartera calculada y la otra mitad no.
    /// </remarks>
    public void Ensure()
    {
        if (Junction == Strategies.Junction.Comparison)
        {
            if (Left is null || Right is null || Comparison is null)
            {
                throw new DomainException("Una comparación necesita sus dos lados y su operador.");
            }

            EnsureTerm(Left);
            EnsureTerm(Right);

            return;
        }

        if (Children is not { Count: > 0 } children)
        {
            throw new DomainException("Una combinación de condiciones necesita al menos una condición.");
        }

        foreach (var child in children)
        {
            child.Ensure();
        }
    }

    /// <summary>Cómo se lee la condición en castellano, para poder explicar una señal.</summary>
    public string Describe() => Junction switch
    {
        Strategies.Junction.Comparison =>
            $"{Left!.Describe()} {Comparison!.Value.Describe()} {Right!.Describe()}",
        Strategies.Junction.All => string.Join(" y ", (Children ?? []).Select(child => child.Describe())),
        _ => string.Join(" o ", (Children ?? []).Select(child => child.Describe())),
    };

    private static void EnsureTerm(Term term)
    {
        if (term.Operand == Operand.Constant)
        {
            if (term.Value is null)
            {
                throw new DomainException("Una constante necesita su valor.");
            }

            return;
        }

        if (term.Operand == Operand.Price)
        {
            return;
        }

        if (term.Window is not { } window || window < 2)
        {
            throw new DomainException($"El indicador {term.Operand} necesita una ventana de al menos dos días.");
        }
    }

    private static int Window(Term? term) => term?.Window ?? 0;
}

internal static class ComparisonText
{
    internal static string Describe(this Comparison comparison) => comparison switch
    {
        Strategies.Comparison.GreaterThan => "por encima de",
        Strategies.Comparison.LessThan => "por debajo de",
        Strategies.Comparison.CrossesAbove => "cruza al alza",
        _ => "cruza a la baja",
    };
}
