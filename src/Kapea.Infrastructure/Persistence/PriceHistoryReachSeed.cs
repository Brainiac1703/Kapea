namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Lo que se da por pedido de lo que ya está descargado.
/// </summary>
/// <remarks>
/// Vive fuera de la migración para poder comprobarlo: es la sentencia que decide si la
/// primera ejecución tras migrar vuelve a pedir la serie entera de cada activo o sólo lo
/// que falta por debajo.
/// </remarks>
internal static class PriceHistoryReachSeed
{
    internal const string Sql =
        """
        INSERT INTO PriceHistoryReaches (AssetId, RequestedFrom, RequestedTo, RequestedWith)
        SELECT p.AssetId, MIN(p.Date), MAX(p.Date), MIN(a.ProviderId)
        FROM DailyPrices p
        INNER JOIN Assets a ON a.Id = p.AssetId
        LEFT JOIN PriceHistoryReaches r ON r.AssetId = p.AssetId
        WHERE r.AssetId IS NULL
        GROUP BY p.AssetId;
        """;
}
