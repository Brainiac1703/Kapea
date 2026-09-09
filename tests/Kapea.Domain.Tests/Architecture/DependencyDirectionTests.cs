namespace Kapea.Domain.Tests.Architecture;

public class DependencyDirectionTests
{
    [Fact]
    public void Domain_has_no_project_references()
    {
        var references = RepositoryLayout.ProjectReferencesOf("Kapea.Domain");

        Assert.Empty(references);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        var references = RepositoryLayout.ProjectReferencesOf("Kapea.Application");

        Assert.DoesNotContain("Kapea.Infrastructure", references);
    }

    [Fact]
    public void Application_does_not_reference_api_or_client()
    {
        var references = RepositoryLayout.ProjectReferencesOf("Kapea.Application");

        Assert.DoesNotContain("Kapea.Api", references);
        Assert.DoesNotContain("Kapea.Client", references);
    }

    [Fact]
    public void Shared_has_no_project_references()
    {
        // Shared es una hoja: la consumen Api, Client y Application, así que cualquier
        // referencia saliente introduciría un ciclo o arrastraría infraestructura al cliente.
        var references = RepositoryLayout.ProjectReferencesOf("Kapea.Shared");

        Assert.Empty(references);
    }

    [Fact]
    public void Client_only_references_shared()
    {
        var references = RepositoryLayout.ProjectReferencesOf("Kapea.Client");

        Assert.Equal(["Kapea.Shared"], references);
    }
}
