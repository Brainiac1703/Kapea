namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Nombres de los filtros globales de los movimientos.
/// </summary>
/// <remarks>
/// <c>IgnoreQueryFilters()</c> sin argumentos quita también el de usuario y deja ver los
/// movimientos de todos. Para ver los anulados se quita sólo <see cref="InForce"/>, y una
/// prueba de arquitectura vigila que nadie lo haga de la otra forma.
/// </remarks>
public static class TransactionQueryFilters
{
    /// <summary>Sólo los movimientos del usuario de la sesión.</summary>
    public const string Owner = "Owner";

    /// <summary>Sólo los movimientos no anulados.</summary>
    public const string InForce = "InForce";

    /// <summary>Para quitar únicamente el filtro de vigentes.</summary>
    public static readonly IReadOnlyCollection<string> OnlyInForce = [InForce];
}
