namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Traduce el símbolo del bróker al que entiende Yahoo Finance.
/// </summary>
/// <remarks>
/// La regla general sale de cómo nombra XTB los valores: el mercado va como sufijo y
/// coincide con el de Yahoo salvo en Estados Unidos, donde Yahoo no lo pone. Las
/// excepciones se declaran una a una en lugar de intentar una regla más lista: una
/// regla equivocada devuelve el precio de otro valor, y eso no falla, solo miente.
/// </remarks>
public static class YahooSymbols
{
    private static readonly Dictionary<string, string> Exceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // XTB nombra así los ETF de iShares en Ámsterdam; Yahoo usa el sufijo del mercado.
        ["CSPX.UK"] = "CSPX.L",
        ["IWDA.NL"] = "IWDA.AS",
        ["VWCE.DE"] = "VWCE.DE",
    };

    /// <summary>Sufijos que Yahoo no usa porque son su mercado por omisión.</summary>
    private static readonly string[] ImplicitMarkets = [".US"];

    /// <summary>
    /// Símbolo con el que Yahoo nombra la serie de un activo.
    /// </summary>
    /// <remarks>
    /// Yahoo cotiza las criptomonedas contra una divisa y lo dice en el propio símbolo:
    /// bitcoin en euros es «BTC-EUR». Pedirlo sin el sufijo devuelve otra cosa o nada.
    /// </remarks>
    public static string ToYahoo(string canonicalSymbol, Domain.Assets.AssetClass assetClass) =>
        assetClass == Domain.Assets.AssetClass.Crypto
            ? $"{canonicalSymbol.Trim().ToUpperInvariant()}-EUR"
            : ToYahoo(canonicalSymbol);

    public static string ToYahoo(string brokerSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerSymbol);

        var symbol = brokerSymbol.Trim().ToUpperInvariant();

        if (Exceptions.TryGetValue(symbol, out var mapped))
        {
            return mapped;
        }

        foreach (var market in ImplicitMarkets)
        {
            if (symbol.EndsWith(market, StringComparison.OrdinalIgnoreCase))
            {
                return symbol[..^market.Length];
            }
        }

        return symbol;
    }
}
