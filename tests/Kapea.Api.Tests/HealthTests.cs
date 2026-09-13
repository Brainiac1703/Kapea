using System.Net;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class HealthTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task The_check_answers_without_a_session()
    {
        // El servicio que comprueba la revisión no tiene con qué autenticarse, así que
        // si esto exigiera sesión daría por muerta cada revisión nueva.
        var response = await factory.CreateAnonymousClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
