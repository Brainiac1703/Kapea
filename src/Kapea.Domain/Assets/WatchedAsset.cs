using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Assets;

/// <summary>
/// Un activo que el usuario vigila: quiere su precio y que sus sistemas lo evalúen,
/// tenga posición en él o no.
/// </summary>
/// <remarks>
/// Va por usuario y no como una marca del activo porque el catálogo es global: el mismo
/// BTC vale para cualquiera, y lo que uno decida seguir no puede decidirlo por el resto.
///
/// La lista no se reconcilia con la cartera. Tener posición ya implica seguimiento, se
/// consulte como se consulte, así que nadie tiene que acordarse de añadir al comprar ni
/// de no quitar al vender.
/// </remarks>
public sealed class WatchedAsset
{
    private WatchedAsset()
    {
    }

    private WatchedAsset(UserId userId, Guid assetId, DateTimeOffset addedAt)
    {
        UserId = userId;
        AssetId = assetId;
        AddedAt = addedAt;
    }

    public UserId UserId { get; private set; }

    public Guid AssetId { get; private set; }

    /// <summary>Cuándo empezó a vigilarse. Sirve para ordenar la lista por lo más reciente.</summary>
    public DateTimeOffset AddedAt { get; private set; }

    public static WatchedAsset Of(UserId userId, Guid assetId, DateTimeOffset addedAt) =>
        new(userId, assetId, addedAt);
}
