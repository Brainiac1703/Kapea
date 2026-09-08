using System.Globalization;
using System.Xml.Linq;
using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.ExchangeRates;

/// <summary>
/// Lee el fichero de tipos de referencia del BCE. Su forma es un Cube por fecha con
/// un Cube por divisa dentro, y los importes vienen siempre con punto decimal, así
/// que se parsean con cultura invariante pase lo que pase en el servidor.
/// </summary>
public static class EcbRatesParser
{
    private static readonly XNamespace EurofxrefNamespace = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";

    public static IReadOnlyList<DailyRate> Parse(Stream xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var document = XDocument.Load(xml);
        var rates = new List<DailyRate>();

        foreach (var day in document.Descendants(EurofxrefNamespace + "Cube").Where(cube => cube.Attribute("time") is not null))
        {
            if (!DateOnly.TryParse(day.Attribute("time")!.Value, CultureInfo.InvariantCulture, out var date))
            {
                continue;
            }

            foreach (var entry in day.Elements(EurofxrefNamespace + "Cube"))
            {
                var code = entry.Attribute("currency")?.Value;
                var rate = entry.Attribute("rate")?.Value;

                if (code is null || rate is null
                    || !decimal.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out var unitsPerEuro)
                    || unitsPerEuro <= 0m)
                {
                    continue;
                }

                rates.Add(new DailyRate(Currency.FromCode(code), date, unitsPerEuro));
            }
        }

        return rates;
    }
}
