using Kapea.Domain.Common;

namespace Kapea.Domain.Assets;

/// <summary>
/// Activo del catálogo, identificado de forma estable e independiente del nombre que
/// use cada plataforma. Es la pieza que hace que BTC importado de Kraken y BTC
/// importado de Bit2Me sean el mismo activo y compartan una única cola FIFO.
/// </summary>
public sealed class Asset
{
    // El parámetro se llama @class para que coincida con la propiedad Class: EF enlaza
    // el constructor por nombre y, sin esa correspondencia, no puede materializar el activo.
    private Asset(
        Guid id,
        string canonicalSymbol,
        AssetClass @class,
        string? isin,
        string displayName,
        bool isVerified,
        string? providerId = null)
    {
        ProviderId = providerId;
        Id = id;
        CanonicalSymbol = canonicalSymbol;
        Class = @class;
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

    /// <summary>
    /// Identificador con el que conoce este activo el proveedor que da sus precios.
    /// </summary>
    /// <remarks>
    /// No es el símbolo: el símbolo identifica el activo dentro de Kapea, y esto sólo
    /// sirve para pedirle datos al proveedor. Existe porque varios activos pueden
    /// compartir símbolo, y deducirlo traería el precio de otra cosa sin que nada
    /// fallara.
    ///
    /// Vacío en todo lo que entró importando movimientos, que se sigue resolviendo por
    /// su símbolo.
    /// </remarks>
    public string? ProviderId { get; private set; }

    public static Asset Create(
        string canonicalSymbol,
        AssetClass assetClass,
        string? displayName = null,
        string? isin = null,
        string? providerId = null) =>
        new(Guid.NewGuid(), NormalizeSymbol(canonicalSymbol), assetClass, NormalizeIsin(isin, assetClass),
            string.IsNullOrWhiteSpace(displayName) ? NormalizeSymbol(canonicalSymbol) : displayName.Trim(),
            isVerified: true,
            string.IsNullOrWhiteSpace(providerId) ? null : providerId.Trim());

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

    /// <summary>
    /// Deja dicho con qué identificador lo conoce su proveedor.
    /// </summary>
    /// <remarks>
    /// Se usa cuando un activo que ya existía se reconoce en una búsqueda: a partir de
    /// ahí sus precios se piden por el identificador y no por su símbolo, que es lo que
    /// permite distinguirlo de otro que se llame igual.
    /// </remarks>
    public void KnownAs(string providerId, string? displayName = null)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            ProviderId = providerId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }
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
