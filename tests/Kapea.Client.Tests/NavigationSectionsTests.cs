using Kapea.Client.Layout;

namespace Kapea.Client.Tests;

public class NavigationSectionsTests
{
    [Theory]
    [InlineData("portfolio", NavigationSection.Portfolio)]
    [InlineData("evolution", NavigationSection.Portfolio)]
    [InlineData("transactions", NavigationSection.Portfolio)]
    [InlineData("strategies", NavigationSection.Strategy)]
    [InlineData("signals", NavigationSection.Strategy)]
    [InlineData("ideas", NavigationSection.Strategy)]
    [InlineData("journal", NavigationSection.Strategy)]
    [InlineData("import", NavigationSection.Import)]
    [InlineData("imports", NavigationSection.Import)]
    [InlineData("review", NavigationSection.Import)]
    [InlineData("platforms", NavigationSection.Settings)]
    [InlineData("accounts", NavigationSection.Settings)]
    [InlineData("credentials", NavigationSection.Settings)]
    [InlineData("profiles", NavigationSection.Settings)]
    public void Each_page_opens_its_group(string path, NavigationSection expected) =>
        Assert.Equal(expected, NavigationSections.Of(path));

    [Theory]
    [InlineData("", NavigationSection.None)]
    [InlineData("results", NavigationSection.None)]
    [InlineData("identities", NavigationSection.None)]
    public void Direct_links_open_no_group(string path, NavigationSection expected) =>
        Assert.Equal(expected, NavigationSections.Of(path));

    [Fact]
    public void A_detail_page_opens_the_group_of_the_page_it_hangs_from()
    {
        // La evolución de un activo se abre desde Cartera, y volver a ella con el grupo
        // cerrado es perder el sitio.
        Assert.Equal(
            NavigationSection.Portfolio,
            NavigationSections.Of("evolution/6f1a7c1e-0000-0000-0000-000000000001"));

        Assert.Equal(
            NavigationSection.Strategy,
            NavigationSections.Of("strategies/6f1a7c1e-0000-0000-0000-000000000001/simulate"));
    }

    [Fact]
    public void The_query_does_not_change_the_group() =>
        Assert.Equal(NavigationSection.Import, NavigationSections.Of("imports?run=42#detalle"));

    [Fact]
    public void Every_routed_page_has_a_place_in_the_menu()
    {
        // Una página nueva que nadie añada aquí entraría con su grupo cerrado sin que
        // se note. Las que se enumeran aparte son enlaces directos o no van en el menú.
        string[] outsideTheGroups = ["", "results", "identities", "signin", "not-found"];

        var routes = typeof(NavigationSections).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), false))
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(route => route.Template.Trim('/').Split('/')[0])
            .Distinct()
            .Where(first => !outsideTheGroups.Contains(first));

        Assert.All(routes, first => Assert.NotEqual(NavigationSection.None, NavigationSections.Of(first)));
    }
}
