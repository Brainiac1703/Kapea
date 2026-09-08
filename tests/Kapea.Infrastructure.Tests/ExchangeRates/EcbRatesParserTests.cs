using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.ExchangeRates;

namespace Kapea.Infrastructure.Tests.ExchangeRates;

public class EcbRatesParserTests
{
    [Fact]
    public void Every_currency_of_every_published_day_is_read()
    {
        var rates = Parse();

        Assert.Equal(8, rates.Count);
        Assert.Equal(3, rates.Count(rate => rate.Date == new DateOnly(2025, 4, 17)));
    }

    [Fact]
    public void Amounts_are_read_with_invariant_culture()
    {
        var rate = Parse().Single(r => r.Currency == Currency.FromCode("USD") && r.Date == new DateOnly(2025, 3, 14));

        Assert.Equal(1.0882m, rate.UnitsPerEuro);
    }

    [Fact]
    public void An_unparseable_rate_is_skipped_without_losing_the_rest_of_the_day()
    {
        // El BCE publica ocasionalmente 'N/A' para una divisa suspendida. Descartarla
        // no puede llevarse por delante las demás del mismo día.
        var day = Parse().Where(rate => rate.Date == new DateOnly(2025, 3, 10)).ToList();

        Assert.Equal(2, day.Count);
        Assert.DoesNotContain(day, rate => rate.UnitsPerEuro <= 0m);
    }

    [Fact]
    public void An_empty_document_yields_no_rates()
    {
        using var stream = new MemoryStream("""
            <?xml version="1.0" encoding="UTF-8"?>
            <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
              <Cube />
            </gesmes:Envelope>
            """u8.ToArray());

        Assert.Empty(EcbRatesParser.Parse(stream));
    }

    private static IReadOnlyList<Kapea.Domain.Exchange.DailyRate> Parse()
    {
        using var stream = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "ExchangeRates", "Recorded", "eurofxref-hist-sample.xml"));

        return EcbRatesParser.Parse(stream);
    }
}
