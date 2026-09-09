using System.Collections.ObjectModel;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;

namespace Kapea.Domain.ImportProfiles;

/// <summary>
/// Cómo se lee un formato de fichero concreto de una plataforma.
/// </summary>
/// <remarks>
/// Va ligado a la plataforma y no a la persona: el formato de un extracto es del
/// bróker, y deducirlo una vez debería servir para todos. Una plataforma puede tener
/// varios perfiles, porque un mismo bróker exporta informes distintos.
/// </remarks>
public sealed class ImportProfile
{
    private readonly List<ImportProfileVersion> _versions = [];

    private ImportProfile(Guid id, PlatformCode platform, string name, bool builtIn)
    {
        Id = id;
        Platform = platform;
        Name = name;
        BuiltIn = builtIn;
    }

    private ImportProfile()
    {
        // Para el materializador de EF Core.
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public PlatformCode Platform { get; private set; }

    public string Name { get; private set; }

    /// <summary>Viene de serie. Se puede editar, y editarlo crea una versión como cualquier otro.</summary>
    public bool BuiltIn { get; private set; }

    public IReadOnlyList<ImportProfileVersion> Versions => new ReadOnlyCollection<ImportProfileVersion>(_versions);

    /// <summary>La versión con la que se importa hoy. Es siempre la de número más alto.</summary>
    public ImportProfileVersion Current =>
        _versions.MaxBy(version => version.Number)
        ?? throw new DomainException($"El perfil '{Name}' no tiene ninguna versión.");

    public static ImportProfile Create(
        PlatformCode platform,
        string name,
        Func<int, ImportProfileVersion> firstVersion,
        bool builtIn = false)
    {
        ArgumentNullException.ThrowIfNull(firstVersion);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Un perfil necesita nombre: una plataforma puede tener varios formatos.");
        }

        var profile = new ImportProfile(Guid.NewGuid(), platform, name.Trim(), builtIn);
        profile._versions.Add(firstVersion(1));

        return profile;
    }

    /// <summary>
    /// Guarda unas reglas corregidas como versión nueva.
    /// </summary>
    /// <remarks>
    /// La anterior se conserva. Los movimientos que se importaron con ella siguen
    /// apuntándola, que es lo que permite explicar de dónde salió una cifra que se
    /// interpretó hace meses.
    /// </remarks>
    public ImportProfileVersion Revise(Func<int, ImportProfileVersion> nextVersion)
    {
        ArgumentNullException.ThrowIfNull(nextVersion);

        var version = nextVersion(Current.Number + 1);
        _versions.Add(version);

        return version;
    }

    public ImportProfileVersion? FindVersion(int number) =>
        _versions.SingleOrDefault(version => version.Number == number);

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Un perfil necesita nombre: una plataforma puede tener varios formatos.");
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Indica si estas cabeceras son las que este perfil sabe leer.
    /// </summary>
    /// <remarks>
    /// Basta con que estén las que el perfil declara: un fichero con columnas de más
    /// sigue siendo el mismo informe, y un bróker que añade una columna no debería
    /// dejar de reconocerse.
    /// </remarks>
    public bool Matches(IReadOnlyCollection<string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var present = headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(header => header.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Current.RecognizedHeaders.Count > 0
            && Current.RecognizedHeaders.All(present.Contains);
    }
}
