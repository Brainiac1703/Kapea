using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Transactions;

/// <summary>
/// Si un movimiento apuntado a mano y uno importado parecen el mismo.
/// </summary>
/// <remarks>
/// Un apunte manual no tiene huella de origen, así que la deduplicación no puede
/// reconocerlo cuando después llega la exportación o la sincronización que lo incluye.
/// Se compara por los datos: misma cuenta, tipo, activo, cantidad y día. Es un aviso y no
/// un descarte, porque dos compras iguales el mismo día son perfectamente posibles.
///
/// El día se toma en la zona horaria de cada movimiento: una compra apuntada el 3 a las
/// 23:30 en Madrid y exportada en UTC como el 3 a las 21:30 es el mismo día para quien la
/// hizo.
/// </remarks>
public static class ManualCoincidence
{
    public static bool Matches(Transaction manual, Transaction imported)
    {
        ArgumentNullException.ThrowIfNull(manual);
        ArgumentNullException.ThrowIfNull(imported);

        return imported.Origin == TransactionOrigin.Imported
            && !imported.IsVoided
            && manual.DistinctFrom != imported.Id
            && Matches(manual, imported.AccountId, imported.Type, imported.AssetId, imported.Quantity, Day(imported));
    }

    /// <summary>
    /// Si un apunte manual coincide con un registro que todavía no es movimiento, como una
    /// fila de la vista previa de una importación.
    /// </summary>
    public static bool Matches(
        Transaction manual,
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(manual);

        return manual.Origin == TransactionOrigin.Manual
            && !manual.IsVoided
            && manual.AccountId == accountId
            && manual.Type == type
            && manual.AssetId == assetId
            && manual.Quantity == quantity
            && Day(manual) == day;
    }

    public static DateOnly Day(Transaction transaction) =>
        DateOnly.FromDateTime(transaction.OccurredAt.InSourceTimeZone.DateTime);
}
