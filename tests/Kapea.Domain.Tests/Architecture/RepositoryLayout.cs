using System.Xml.Linq;

namespace Kapea.Domain.Tests.Architecture;

/// <summary>
/// Localiza la raíz del repositorio y lee las referencias declaradas en los .csproj.
/// Se inspecciona el proyecto y no los ensamblados cargados porque una referencia
/// prohibida debe detectarse aunque el código todavía no la use.
/// </summary>
internal static class RepositoryLayout
{
    internal static string Root { get; } = FindRoot();

    internal static IReadOnlyList<string> ProjectReferencesOf(string projectName)
    {
        var projectFile = Path.Combine(Root, "src", projectName, projectName + ".csproj");

        if (!File.Exists(projectFile))
        {
            throw new FileNotFoundException($"No se encuentra el proyecto {projectName}.", projectFile);
        }

        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', Path.DirectorySeparatorChar)))
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList()!;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kapea.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se ha encontrado Kapea.slnx desde el directorio de ejecución.");
    }
}
