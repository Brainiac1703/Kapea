using System.Text.RegularExpressions;

namespace Kapea.Domain.Tests.Architecture;

/// <summary>
/// Vigila cómo se quitan los filtros globales de los movimientos.
/// </summary>
/// <remarks>
/// Los movimientos llevan dos filtros: el de usuario y el de vigentes. Para ver los
/// anulados basta con quitar el segundo. Un <c>IgnoreQueryFilters()</c> sin argumentos
/// quita también el primero y enseña los movimientos de todos los usuarios; compila,
/// funciona en una prueba con un solo usuario y no avisa de nada.
/// </remarks>
public partial class TransactionFilterTests
{
    /// <summary>
    /// Donde quitarlos todos es deliberado: la adopción de los datos anteriores a los
    /// usuarios, que por definición recorre filas que aún no son de nadie.
    /// </summary>
    private static readonly string[] Allowed = ["UserRepository.cs"];

    [Fact]
    public void No_query_on_transactions_lifts_every_filter()
    {
        var offenders = Offenders(SourceFiles()).ToList();

        Assert.True(
            offenders.Count == 0,
            "Consultas de movimientos que quitan todos los filtros, incluido el de usuario: "
                + string.Join(" | ", offenders));
    }

    [Fact]
    public void The_check_catches_a_query_that_lifts_every_filter()
    {
        // Sin esto, un cambio en la forma de escribir las consultas dejaría la regla sin
        // encontrar nada y pasando siempre.
        const string source = """
            var all = await context.Transactions
                .IgnoreQueryFilters()
                .ToListAsync();
            """;

        Assert.Single(Offenders([("Ejemplo.cs", source)]));
    }

    [Fact]
    public void The_check_lets_through_lifting_only_the_in_force_filter()
    {
        const string source = """
            var all = await context.Transactions
                .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
                .ToListAsync();
            """;

        Assert.Empty(Offenders([("Ejemplo.cs", source)]));
    }

    private static IEnumerable<string> Offenders(IEnumerable<(string Name, string Source)> files) =>
        from file in files
        where !Allowed.Contains(Path.GetFileName(file.Name))
        from Match match in TransactionsLiftingEveryFilter().Matches(file.Source)
        select $"{Path.GetFileName(file.Name)}:{LineOf(file.Source, match.Index)}";

    private static IEnumerable<(string Name, string Source)> SourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (path, File.ReadAllText(path)));

    private static int LineOf(string source, int index) => source[..index].Count(character => character == '\n') + 1;

    /// <summary>
    /// <c>Transactions</c> seguido, en la misma expresión, de <c>IgnoreQueryFilters()</c>
    /// vacío. Se permite lo que haya entre medias salvo un punto y coma, que cerraría la
    /// consulta.
    /// </summary>
    [GeneratedRegex(@"\bTransactions\b[^;]*?\.IgnoreQueryFilters\(\s*\)")]
    private static partial Regex TransactionsLiftingEveryFilter();
}
