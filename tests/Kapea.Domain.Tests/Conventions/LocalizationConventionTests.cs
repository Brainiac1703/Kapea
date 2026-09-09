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
    public void Scanner_still_catches_text_next_to_control_flow()
    {
        // El escáner ignora if, else y foreach para no confundir el control de flujo con
        // texto. Esta prueba vigila que esa concesión no se lleve por delante el texto
        // de verdad que hay al lado.
        var literals = UiLiteralScanner.FindLiterals("""
            @if (_cuentas.Count == 0)
            {
                <p>Todavía no hay ninguna cuenta.</p>
            }
            else
            {
                <FluentButton OnClick="@(() => Borrar(context))">@Ui["Common_Delete"]</FluentButton>
            }
            """);

        Assert.Contains(literals, literal => literal.Contains("Todavía no hay ninguna cuenta.", StringComparison.Ordinal));
        Assert.DoesNotContain(literals, literal => literal.Contains("Borrar", StringComparison.Ordinal));
    }

    [Fact]
    public void Scanner_does_not_mistake_a_format_string_for_visible_text()
    {
        var literals = UiLiteralScanner.FindLiterals("""
            <span>@context.OccurredAt.ToLocalTime().ToString("d")</span>
            """);

        Assert.Empty(literals);
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
