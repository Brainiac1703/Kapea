using Kapea.Domain.Common;

namespace Kapea.Domain.Accounts;

/// <summary>
/// Plataforma de la que proceden los movimientos.
/// </summary>
/// <remarks>
/// Es una fila y no un valor del código. Dar de alta un bróker que exporta un fichero
/// no debería exigir compilar nada: basta con decir cómo se llama y que sus movimientos
/// llegan en fichero. Las de API siguen necesitando su adaptador, porque su firma, su
/// paginación y sus alias no se deducen.
/// </remarks>
public sealed class Platform
{
    private Platform(PlatformCode code, string name, PlatformImportKind importKind, bool builtIn)
    {
        Code = code;
        Name = name;
        ImportKind = importKind;
        BuiltIn = builtIn;
    }

    private Platform()
    {
        // Para el materializador de EF Core.
        Name = string.Empty;
    }

    public PlatformCode Code { get; private set; }

    public string Name { get; private set; }

    public PlatformImportKind ImportKind { get; private set; }

    /// <summary>
    /// Viene de serie. No se puede borrar: hay movimientos del histórico a su nombre y
    /// adaptadores que la esperan.
    /// </summary>
    public bool BuiltIn { get; private set; }

    public static Platform Create(PlatformCode code, string name, PlatformImportKind importKind) =>
        new(code, Named(name, code), importKind, builtIn: false);

    /// <summary>Las que Kapea trae configuradas. No son «las soportadas»: son las que ya vienen.</summary>
    public static Platform BuiltInPlatform(PlatformCode code, string name, PlatformImportKind importKind) =>
        new(code, Named(name, code), importKind, builtIn: true);

    public void Rename(string name) => Name = Named(name, Code);

    public void EnsureCanBeDeleted(int accountCount)
    {
        if (BuiltIn)
        {
            throw new DomainException(
                $"La plataforma '{Name}' viene de serie y no se puede borrar.");
        }

        if (accountCount > 0)
        {
            throw new DomainException(
                $"La plataforma '{Name}' tiene {accountCount} cuentas: bórralas antes de retirarla.");
        }
    }

    private static string Named(string name, PlatformCode code) =>
        string.IsNullOrWhiteSpace(name) ? code.Value : name.Trim();
}
