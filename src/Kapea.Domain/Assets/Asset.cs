using Kapea.Domain.Common;

namespace Kapea.Domain.Assets;

/// <summary>
/// Activo del catálogo, identificado de forma estable e independiente del nombre que
/// use cada plataforma. Es la pieza que hace que BTC importado de Kraken y BTC
/// importado de Bit2Me sean el mismo activo y compartan una única cola FIFO.
/// </summary>
public sealed class Asset
{
    private Asset(Guid id, string canonicalSymbol, AssetClass assetClass, string? isin, string displayName, bool isVerified)
    {
        Id = id;
        CanonicalSymbol = canonicalSymbol;
        Class = assetClass;
        Isin = isin;
        DisplayName = displayName;
        IsVerified = isVerified;
    }

    public Guid Id { get; }

    /// <summary>Símbolo canónico en mayúsculas: BTC, AAPL. No es el símbolo de ninguna plataforma concreta.</summary>
    public string CanonicalSymbol { get; }

    public AssetClass Class { get; }

    /// <summary>Identificador ISIN. Solo existe en renta variable, y no siempre lo aporta el origen.</summary>
    public string? Isin { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>
    /// Un activo creado automáticamente porque una importación trajo un símbolo
    /// desconocido queda sin verificar: la importación no se bloquea, pero el usuario
    /// tiene que confirmar de qué activo se trata antes de fiarse de sus cifras.
    /// </summary>
    public bool IsVerified { get; private set; }

    public static Asset Create(string canonicalSymbol, AssetClass assetClass, string? displayName = null, string? isin = null) =>
        new(Guid.NewGuid(), NormalizeSymbol(canonicalSymbol), assetClass, NormalizeIsin(isin, assetClass),
            string.IsNullOrWhiteSpace(displayName) ? NormalizeSymbol(canonicalSymbol) : displayName.Trim(), isVerified: true);

    /// <summary>Alta automática desde una importación con un símbolo que no se pudo resolver.</summary>
    public static Asset CreateUnverified(string canonicalSymbol, AssetClass assetClass) =>
        new(Guid.NewGuid(), NormalizeSymbol(canonicalSymbol), assetClass, isin: null,
            NormalizeSymbol(canonicalSymbol), isVerified: false);

    public void Verify(string? displayName = null, string? isin = null)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(isin))
        {
            Isin = NormalizeIsin(isin, Class);
        }

        IsVerified = true;
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new DomainException("Un activo necesita símbolo canónico.");
        }

        return symbol.Trim().ToUpperInvariant();
    }

    private static string? NormalizeIsin(string? isin, AssetClass assetClass)
    {
        if (string.IsNullOrWhiteSpace(isin))
        {
            return null;
        }

        if (assetClass != AssetClass.Equity)
        {
            throw new DomainException("Solo la renta variable tiene ISIN.");
        }

        var normalized = isin.Trim().ToUpperInvariant();

        if (normalized.Length != 12
            || !normalized[..2].All(char.IsAsciiLetterUpper)
            || !normalized[2..].All(char.IsAsciiLetterOrDigit))
        {
            throw new DomainException($"'{isin}' no tiene la forma de un ISIN.");
        }

        return normalized;
    }
}
