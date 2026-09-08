namespace Kapea.Domain.Calculation;

/// <summary>
/// Orden de proceso de los movimientos de un activo. El desempate no puede depender
/// del orden en que la base de datos devuelva las filas: si dependiera, dos recálculos
/// del mismo histórico podrían repartir los lotes de forma distinta.
/// </summary>
public sealed class TransactionOrdering : IComparer<ValuedTransaction>
{
    public static readonly TransactionOrdering Instance = new();

    private TransactionOrdering()
    {
    }

    public int Compare(ValuedTransaction? x, ValuedTransaction? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        var byInstant = x.OccurredAt.Instant.CompareTo(y.OccurredAt.Instant);

        if (byInstant != 0)
        {
            return byInstant;
        }

        // Un split del mismo instante se aplica antes que la operación que lo acompaña:
        // en caso contrario la venta consumiría cantidades todavía sin ajustar.
        var bySplitFirst = IsSplit(y).CompareTo(IsSplit(x));

        if (bySplitFirst != 0)
        {
            return bySplitFirst;
        }

        // La huella de deduplicación es estable para el mismo registro de origen,
        // así que sirve de desempate reproducible entre recálculos.
        var byFingerprint = string.CompareOrdinal(
            x.Transaction.Source.Fingerprint,
            y.Transaction.Source.Fingerprint);

        return byFingerprint != 0 ? byFingerprint : x.Transaction.Id.CompareTo(y.Transaction.Id);
    }

    private static int IsSplit(ValuedTransaction transaction) =>
        transaction.Type == Transactions.TransactionType.Split ? 1 : 0;
}
