namespace Kapea.Application.Ideas;

/// <summary>Una idea extraída de un texto, antes de aprobarse.</summary>
/// <param name="Symbol">Símbolo tal como aparece en el texto.</param>
/// <param name="Direction">«Buy» o «Sell».</param>
public sealed record ExtractedIdea(
    string Symbol,
    string Direction,
    decimal? Entry,
    decimal? Target,
    decimal? StopLoss,
    string? Note);

/// <summary>
/// Lo que se ha sacado de un texto.
/// </summary>
/// <param name="NotUnderstood">Lo que no se ha sabido extraer, con las palabras del texto.</param>
public sealed record IdeaExtraction(IReadOnlyList<ExtractedIdea> Ideas, IReadOnlyList<string> NotUnderstood);

/// <summary>
/// Saca ideas de un texto pegado.
/// </summary>
/// <remarks>
/// El texto no se guarda: conservar la transcripción de una publicación ajena sería
/// almacenar obra de otro, y lo que hace falta son las ideas y el enlace.
///
/// Lo que no se entiende se señala en lugar de completarse, igual que en la traducción de
/// un sistema. Y nada se guarda sin que una persona lo apruebe.
/// </remarks>
public interface IIdeaExtractor
{
    bool IsAvailable { get; }

    Task<IdeaExtraction?> ExtractAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>El extractor que no extrae nada, para cuando no hay servicio configurado.</summary>
public sealed class UnavailableIdeaExtractor : IIdeaExtractor
{
    public bool IsAvailable => false;

    public Task<IdeaExtraction?> ExtractAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult<IdeaExtraction?>(null);
}

/// <summary>Una publicación vista en una fuente.</summary>
public sealed record SourcePublication(string Title, DateTimeOffset PublishedAt, string Url);

/// <summary>
/// Mira si una fuente tiene algo nuevo.
/// </summary>
/// <remarks>
/// Usa únicamente lo que la fuente ofrezca para ello. Descargar transcripciones por otra
/// vía iría contra los términos de quien las publica, así que Kapea avisa y quien lee
/// pega el texto.
/// </remarks>
public interface ISourceWatcher
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<SourcePublication>> SinceAsync(
        string channel,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default);
}

/// <summary>El vigilante que no vigila, para cuando no hay forma de consultar la fuente.</summary>
public sealed class UnavailableSourceWatcher : ISourceWatcher
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<SourcePublication>> SinceAsync(
        string channel,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SourcePublication>>([]);
}
