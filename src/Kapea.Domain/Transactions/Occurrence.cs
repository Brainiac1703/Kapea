using Kapea.Domain.Common;

namespace Kapea.Domain.Transactions;

/// <summary>
/// Instante de una operación. Se guarda siempre en UTC junto con la zona en la que
/// lo expresó el origen: sin la zona no se puede reconstruir la fecha que vio el
/// usuario, y esa fecha es la que determina el ejercicio fiscal.
/// </summary>
public readonly record struct Occurrence
{
    private Occurrence(DateTimeOffset instant, string sourceTimeZoneId)
    {
        Instant = instant;
        SourceTimeZoneId = sourceTimeZoneId;
    }

    /// <summary>Instante absoluto, siempre en UTC.</summary>
    public DateTimeOffset Instant { get; }

    /// <summary>Identificador de la zona horaria en la que el origen expresó la fecha.</summary>
    public string SourceTimeZoneId { get; }

    /// <summary>Fecha tal y como la vio el usuario en su plataforma.</summary>
    public DateTimeOffset InSourceTimeZone =>
        TimeZoneInfo.ConvertTime(Instant, TimeZoneInfo.FindSystemTimeZoneById(SourceTimeZoneId));

    public static Occurrence FromOffset(DateTimeOffset instant, string sourceTimeZoneId) =>
        new(instant.ToUniversalTime(), EnsureKnownTimeZone(sourceTimeZoneId));

    /// <summary>
    /// Para orígenes que dan la fecha sin zona: se interpreta en la zona que declara
    /// el adaptador y se almacena el instante resultante en UTC.
    /// </summary>
    public static Occurrence FromNaive(DateTime naive, string sourceTimeZoneId)
    {
        if (naive.Kind == DateTimeKind.Local)
        {
            throw new DomainException(
                "Una fecha sin zona no puede llegar como DateTimeKind.Local: la zona del servidor no es la del origen.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(EnsureKnownTimeZone(sourceTimeZoneId));
        var unspecified = DateTime.SpecifyKind(naive, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecified);

        return new Occurrence(new DateTimeOffset(unspecified, offset).ToUniversalTime(), timeZone.Id);
    }

    private static string EnsureKnownTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new DomainException("Un movimiento necesita la zona horaria en la que el origen expresó su fecha.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).Id;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new DomainException($"La zona horaria '{timeZoneId}' no existe en este sistema.");
        }
    }

    public override string ToString() => Instant.ToString("O");
}
