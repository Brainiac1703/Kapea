namespace Kapea.Domain.Assets;

/// <summary>
/// En qué mercado cotiza un activo, para saber cuándo abre y cuándo no.
/// </summary>
/// <remarks>
/// Sale del sufijo del símbolo canónico, que es el del bróker: NOW.US cotiza en Estados
/// Unidos y VVSM.DE en Alemania. No es un dato nuevo, es leer el que ya hay.
///
/// Hace falta afinar hasta aquí porque la clase no basta. El 3 de julio de 2026 la bolsa
/// estadounidense cerró por la fiesta del 4 y la alemana operó: tratando toda la renta
/// variable como un solo mercado, el día parecía abierto y a las estadounidenses les
/// faltaba el dato.
///
/// Las criptomonedas no llevan sufijo y cotizan siempre, así que caen todas en el mismo
/// mercado y ninguno de sus días sale cerrado.
/// </remarks>
public static class Market
{
    /// <summary>El mercado de un activo, tal y como se deduce de su símbolo y su clase.</summary>
    public static string Of(string canonicalSymbol, AssetClass assetClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSymbol);

        var dot = canonicalSymbol.LastIndexOf('.');

        // Sin sufijo, el mercado es la clase entera: es lo que ocurre con las
        // criptomonedas y con la renta variable que el bróker nombra sin él.
        return dot > 0 && dot < canonicalSymbol.Length - 1
            ? $"{assetClass}:{canonicalSymbol[(dot + 1)..].ToUpperInvariant()}"
            : assetClass.ToString();
    }
}
