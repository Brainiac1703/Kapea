using System.Text.RegularExpressions;
using Kapea.Client.Layout;

namespace Kapea.Client.Tests;

/// <summary>
/// Las direcciones que una pantalla enlaza a otra.
/// </summary>
public class RoutesTests
{
    [Fact]
    public void The_asset_chart_is_a_segment_and_not_a_question()
    {
        // La lista de seguimiento enlazaba a «evolution?asset=…». Esa dirección casa con
        // la del patrimonio, así que el enlace no fallaba: llevaba a otro sitio.
        var route = Routes.AssetChart(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        Assert.Equal("evolution/11111111-2222-3333-4444-555555555555", route);
        Assert.DoesNotContain('?', route);
    }

    [Fact]
    public void The_asset_chart_hangs_from_the_portfolio_one() =>
        Assert.StartsWith($"{Routes.PortfolioChart}/", Routes.AssetChart(Guid.NewGuid()), StringComparison.Ordinal);

    [Fact]
    public void It_matches_the_route_the_page_declares()
    {
        // Si alguien cambia «@page» sin tocar esto, o al revés, el enlace deja de llevar
        // donde dice. Se comprueba contra la propia página.
        var page = File.ReadAllText(PageFile("AssetEvolution.razor"));
        var declared = Regex.Match(page, @"@page ""/(?<route>[^""]+)""").Groups["route"].Value;

        Assert.Equal("evolution/{AssetId:guid}", declared);
        Assert.StartsWith(
            declared[..declared.IndexOf('{', StringComparison.Ordinal)],
            Routes.AssetChart(Guid.NewGuid()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void No_page_builds_that_link_by_hand()
    {
        // Construirla en cada sitio es lo que permitió que una quedara distinta.
        var pages = Directory.GetFiles(Path.GetDirectoryName(PageFile("AssetEvolution.razor"))!, "*.razor");

        Assert.All(pages, page =>
            Assert.DoesNotContain("evolution?asset", File.ReadAllText(page), StringComparison.Ordinal));
    }

    private static string PageFile(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "Kapea.Client", "Pages", name);
    }
}
