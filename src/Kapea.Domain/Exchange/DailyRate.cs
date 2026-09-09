using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Exchange;

/// <summary>Tipo publicado para una divisa en una fecha concreta.</summary>
public sealed record DailyRate(Currency Currency, DateOnly Date, decimal UnitsPerEuro);
