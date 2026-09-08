using Kapea.Domain.Assets;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.Import.Kraken;

/// <summary>
/// Traduce la nomenclatura propia de Kraken a los activos y divisas del catálogo.
/// </summary>
/// <remarks>
/// Kraken arrastra alias históricos de cuando prefijaba los activos: X para las
/// criptomonedas y Z para las divisas fiduciarias (XXBT, ZEUR), y sigue llamando XBT
/// a bitcoin. Sin esta traducción, el mismo activo importado en dos momentos acabaría
/// en dos entradas distintas del catálogo y en dos colas FIFO separadas.
/// </remarks>
public static class KrakenSymbols
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["XBT"] = "BTC",
        ["XXBT"] = "BTC",
        ["XETH"] = "ETH",
        ["XXRP"] = "XRP",
        ["XLTC"] = "LTC",
        ["XXLM"] = "XLM",
        ["XXMR"] = "XMR",
        ["XZEC"] = "ZEC",
        ["XETC"] = "ETC",
        ["XREP"] = "REP",
        ["ZEUR"] = "EUR",
        ["ZUSD"] = "USD",
        ["ZGBP"] = "GBP",
        ["ZCAD"] = "CAD",
        ["ZJPY"] = "JPY",
        ["ZAUD"] = "AUD",
    };

    private static readonly HashSet<string> FiatCodes = new(StringComparer.Ordinal)
    {
        "EUR", "USD", "GBP", "CHF", "JPY", "CAD", "AUD",
    };

    /// <summary>Símbolo canónico de un activo de Kraken. Deshace el alias histórico y los sufijos de variante.</summary>
    public static string ToCanonical(string krakenAsset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(krakenAsset);

        var code = krakenAsset.Trim().ToUpperInvariant();

        // Kraken sufija variantes del mismo activo (staking, earn) con .S, .M, .F.
        // Son el mismo activo a efectos de coste y de cola FIFO.
        var dot = code.IndexOf('.', StringComparison.Ordinal);

        if (dot > 0)
        {
            code = code[..dot];
        }

        return Aliases.TryGetValue(code, out var canonical) ? canonical : code;
    }

    public static bool IsFiat(string krakenAsset) => FiatCodes.Contains(ToCanonical(krakenAsset));

    public static AssetClass ClassOf(string krakenAsset) =>
        IsFiat(krakenAsset) ? AssetClass.Equity : AssetClass.Crypto;

    /// <summary>
    /// Parte un par de negociación en activo operado y contrapartida. Kraken no separa
    /// los dos lados con ningún carácter, así que se prueban las longitudes posibles
    /// contra los códigos conocidos en lugar de partir por la mitad.
    /// </summary>
    public static bool TrySplitPair(string pair, out string baseAsset, out string quoteAsset)
    {
        baseAsset = string.Empty;
        quoteAsset = string.Empty;

        if (string.IsNullOrWhiteSpace(pair))
        {
            return false;
        }

        var code = pair.Trim().ToUpperInvariant();

        if (code.Contains('/', StringComparison.Ordinal))
        {
            var parts = code.Split('/', 2);
            baseAsset = ToCanonical(parts[0]);
            quoteAsset = ToCanonical(parts[1]);

            return true;
        }

        // Se prueba la contrapartida de más larga a más corta: XXBTZEUR parte en XXBT
        // y ZEUR, no en XXBTZ y EUR. Cortar por la mitad o probar al revés daría un
        // activo inexistente que acabaría creado como no verificado en el catálogo.
        for (var rightLength = Math.Min(5, code.Length - 3); rightLength >= 3; rightLength--)
        {
            var right = code[^rightLength..];

            if (!Aliases.ContainsKey(right) && !FiatCodes.Contains(right))
            {
                continue;
            }

            baseAsset = ToCanonical(code[..^rightLength]);
            quoteAsset = ToCanonical(right);

            return true;
        }

        return false;
    }

    /// <summary>Divisa de la contrapartida, cuando es fiduciaria. Nulo en una permuta cripto-cripto.</summary>
    public static Currency? QuoteCurrency(string quoteAsset) =>
        FiatCodes.Contains(quoteAsset) ? Currency.FromCode(quoteAsset) : null;
}
