namespace Kapea.Application.Portfolio;

/// <summary>Ejercicio que puede cambiar una corrección, y si ya ha terminado.</summary>
public sealed record CorrectionImpact(int TaxYear, bool IsPastYear)
{
    /// <summary>
    /// El ejercicio más antiguo de las fechas implicadas.
    /// </summary>
    /// <remarks>
    /// Mover la fecha de un movimiento cambia dos ejercicios, el de antes y el de después,
    /// y avisar por el más antiguo es avisar por todos: el FIFO arrastra el cambio hacia
    /// delante. Un ejercicio anterior al actual se trata como posiblemente declarado, porque
    /// Kapea todavía no sabe cuáles se han presentado.
    /// </remarks>
    public static CorrectionImpact For(DateOnly date, DateOnly? previous, DateOnly today)
    {
        var oldest = previous is { } before && before < date ? before : date;

        return new CorrectionImpact(oldest.Year, oldest.Year < today.Year);
    }
}
