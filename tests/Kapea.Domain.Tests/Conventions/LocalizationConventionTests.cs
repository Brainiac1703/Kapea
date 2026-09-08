using Kapea.Domain.Tests.Architecture;

namespace Kapea.Domain.Tests.Conventions;

public class LocalizationConventionTests
{
    public static TheoryData<string> RazorComponents()
    {
        var data = new TheoryData<string>();

        foreach (var file in EnumerateRazorComponents())
        {
            data.Add(Path.GetRelativePath(RepositoryLayout.Root, file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RazorComponents))]
    public void Razor_component_has_no_hardcoded_user_facing_text(string relativePath)
    {
        var markup = File.ReadAllText(Path.Combine(RepositoryLayout.Root, relativePath));

        var literals = UiLiteralScanner.FindLiterals(markup);

        Assert.True(
            literals.Count == 0,
            $"{relativePath} contiene texto sin localizar: {string.Join(" | ", literals)}");
    }

    [Fact]
    public void There_is_at_least_one_razor_component_to_scan()
    {
        // Sin esta comprobación, un fallo del recorrido de ficheros dejaría el
        // Theory sin casos y la convención pasaría sin haber mirado nada.
        Assert.NotEmpty(EnumerateRazorComponents());
    }

    [Fact]
    public void Scanner_detects_hardcoded_text()
    {
        var literals = UiLiteralScanner.FindLiterals("""
            <h1>Hello, world!</h1>
            <input placeholder="Escribe aquí" />
            """);

        Assert.Contains(literals, literal => literal.Contains("Hello, world!", StringComparison.Ordinal));
        Assert.Contains(literals, literal => literal.Contains("placeholder", StringComparison.Ordinal));
    }

    [Fact]
    public void Scanner_accepts_localized_markup()
    {
        var literals = UiLiteralScanner.FindLiterals("""
            @page "/"
            <h1>@Ui["Home_Heading"]</h1>
            <input placeholder="@Ui["Home_Intro"]" />
            @code {
                private const string NotUserFacing = "diagnostic only";
            }
            """);

        Assert.Empty(literals);
    }

    private static IReadOnlyList<string> EnumerateRazorComponents() =>
        Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.razor", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order()
            .ToList();
}
