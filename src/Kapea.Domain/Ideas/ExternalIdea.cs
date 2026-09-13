using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Ideas;

/// <summary>Qué propone la idea.</summary>
public enum IdeaDirection
{
    Buy = 1,

    Sell = 2,
}

/// <summary>Cómo acabó una idea.</summary>
public enum IdeaOutcome
{
    /// <summary>Todavía no ha llegado a ninguno de sus niveles.</summary>
    Open = 1,

    /// <summary>Llegó a su objetivo.</summary>
    Reached = 2,

    /// <summary>Llegó a su nivel de salida.</summary>
    Stopped = 3,

    /// <summary>Pasó su plazo sin llegar a ninguno.</summary>
    Expired = 4,
}

/// <summary>
/// Una fuente externa que se sigue.
/// </summary>
/// <remarks>
/// Guarda hasta dónde se ha mirado para poder avisar de lo nuevo sin volver a leer lo
/// viejo. No guarda el contenido de nadie: solo el enlace y la fecha.
/// </remarks>
public sealed class IdeaSource
{
    private IdeaSource()
    {
        Name = null!;
    }

    private IdeaSource(UserId userId, string name, string? channel)
    {
        UserId = userId;
        Name = name;
        Channel = channel;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public UserId UserId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Identificador en la plataforma de la fuente, cuando se puede vigilar.</summary>
    public string? Channel { get; private set; }

    /// <summary>Fecha de la última publicación conocida, para avisar solo de lo nuevo.</summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Enlace de esa última publicación.</summary>
    public string? LastSeenUrl { get; private set; }

    public static IdeaSource Create(UserId userId, string name, string? channel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new IdeaSource(userId, name.Trim(), string.IsNullOrWhiteSpace(channel) ? null : channel.Trim());
    }

    /// <summary>Anota hasta dónde se ha mirado.</summary>
    public void Seen(DateTimeOffset publishedAt, string? url)
    {
        if (LastSeenAt is { } last && publishedAt <= last)
        {
            return;
        }

        LastSeenAt = publishedAt;
        LastSeenUrl = url;
    }
}

/// <summary>
/// Una idea que llega de fuera sobre un valor concreto.
/// </summary>
/// <remarks>
/// No es un sistema: es una opinión con fecha. Se guarda con sus niveles y se sigue hasta
/// el final, porque eso es lo que convierte «suele acertar» en un número.
///
/// De la publicación se guarda el enlace y nada más. Conservar la transcripción sería
/// almacenar obra ajena, y lo que hace falta son las ideas.
/// </remarks>
public sealed class ExternalIdea
{
    private ExternalIdea()
    {
        Symbol = null!;
    }

    private ExternalIdea(
        UserId userId,
        Guid sourceId,
        string symbol,
        IdeaDirection direction,
        DateOnly publishedOn)
    {
        UserId = userId;
        SourceId = sourceId;
        Symbol = symbol;
        Direction = direction;
        PublishedOn = publishedOn;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public UserId UserId { get; private set; }

    public Guid SourceId { get; private set; }

    /// <summary>Símbolo tal como lo nombró la fuente. Puede no estar en el catálogo.</summary>
    public string Symbol { get; private set; }

    public IdeaDirection Direction { get; private set; }

    public DateOnly PublishedOn { get; private set; }

    /// <summary>Activo del catálogo, cuando el símbolo se ha podido resolver.</summary>
    public Guid? AssetId { get; private set; }

    public Money? Entry { get; private set; }

    public Money? Target { get; private set; }

    public Money? StopLoss { get; private set; }

    public string? Url { get; private set; }

    /// <summary>Lo que la fuente dijo, resumido en una línea por quien la anotó.</summary>
    public string? Note { get; private set; }

    public IdeaOutcome Outcome { get; private set; } = IdeaOutcome.Open;

    /// <summary>Cuándo se resolvió, si se resolvió.</summary>
    public DateOnly? ResolvedOn { get; private set; }

    public static ExternalIdea Create(
        UserId userId,
        Guid sourceId,
        string symbol,
        IdeaDirection direction,
        DateOnly publishedOn,
        Guid? assetId = null,
        Money? entry = null,
        Money? target = null,
        Money? stopLoss = null,
        string? url = null,
        string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        // La comprobación depende del sentido: en una venta el objetivo va por debajo de
        // la entrada y la salida por encima, justo al revés que en una compra.
        if (target is { } up && stopLoss is { } down)
        {
            var wrong = direction == IdeaDirection.Buy
                ? up.Amount <= down.Amount
                : up.Amount >= down.Amount;

            if (wrong)
            {
                throw new DomainException(
                    "El objetivo y el nivel de salida están al revés para el sentido de la idea.");
            }
        }

        return new ExternalIdea(userId, sourceId, symbol.Trim().ToUpperInvariant(), direction, publishedOn)
        {
            AssetId = assetId,
            Entry = entry,
            Target = target,
            StopLoss = stopLoss,
            Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
    }

    /// <summary>Marca cómo acabó. Una idea ya resuelta no se vuelve a resolver.</summary>
    public void Resolve(IdeaOutcome outcome, DateOnly on)
    {
        if (Outcome != IdeaOutcome.Open || outcome == IdeaOutcome.Open)
        {
            return;
        }

        Outcome = outcome;
        ResolvedOn = on;
    }

    /// <summary>Se puede seguir contra la serie si sabemos de qué activo habla y a qué niveles.</summary>
    public bool CanBeTracked => AssetId is not null && (Target is not null || StopLoss is not null);
}
