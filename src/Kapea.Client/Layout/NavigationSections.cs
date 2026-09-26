namespace Kapea.Client.Layout;

/// <summary>Grupos del menú.</summary>
public enum NavigationSection
{
    None,
    Portfolio,
    Strategy,
    Import,
    Settings,
}

/// <summary>
/// A qué grupo del menú pertenece cada ruta.
/// </summary>
/// <remarks>
/// Fuera del componente para poder probarlo: una página nueva que no se añada aquí
/// deja su grupo cerrado al entrar, y eso no lo detecta nadie mirando el menú.
/// Las rutas de detalle cuelgan de su página: la evolución de un activo abre Cartera y
/// la simulación de un sistema abre Estrategia.
/// </remarks>
public static class NavigationSections
{
    public static NavigationSection Of(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var first = relativePath
            .Split(['?', '#'], 2)[0]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .ToLowerInvariant();

        return first switch
        {
            "portfolio" or "evolution" or "transactions" => NavigationSection.Portfolio,
            "watchlist" or "strategies" or "signals" or "ideas" or "journal" => NavigationSection.Strategy,
            "import" or "imports" or "review" => NavigationSection.Import,
            "platforms" or "accounts" or "credentials" or "profiles" => NavigationSection.Settings,
            _ => NavigationSection.None,
        };
    }
}
