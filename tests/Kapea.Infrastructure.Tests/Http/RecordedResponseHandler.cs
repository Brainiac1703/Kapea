using System.Net;

namespace Kapea.Infrastructure.Tests.Http;

/// <summary>
/// Sirve respuestas grabadas en lugar de salir a la red. Permite probar los clientes
/// de terceros —incluidos sus reintentos y su paginación— sin depender de que la
/// plataforma esté disponible ni de sus límites de uso.
/// </summary>
internal sealed class RecordedResponseHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    internal List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Cuerpos ya leídos. El HttpRequestMessage se desecha al terminar la llamada, así
    /// que el contenido hay que capturarlo aquí o deja de estar disponible en el test.
    /// </summary>
    internal List<string> RequestBodies { get; } = [];

    internal RecordedResponseHandler RespondWithFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);

        return Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(File.ReadAllText(path)),
        });
    }

    internal RecordedResponseHandler RespondWithContent(string content) =>
        Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });

    internal RecordedResponseHandler RespondWithStatus(HttpStatusCode status) =>
        Respond(_ => new HttpResponseMessage(status) { Content = new StringContent(string.Empty) });

    internal RecordedResponseHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _responses.Enqueue(respond);

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? string.Empty);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No queda ninguna respuesta grabada para {request.Method} {request.RequestUri}.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}
