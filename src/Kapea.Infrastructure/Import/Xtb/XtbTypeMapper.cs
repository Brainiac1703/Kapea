using Kapea.Domain.Transactions;

namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>
/// Traduce los conceptos de operación de XTB a los tipos normalizados. Lo que no
/// tiene equivalencia acaba en Unknown y queda fuera del cálculo hasta que una
/// persona lo clasifica: inventar un tipo aquí falsearía un resultado fiscal.
/// </summary>
public static class XtbTypeMapper
{
    private static readonly (string Fragment, TransactionType Type)[] Rules =
    [
        ("STOCKS SALE", TransactionType.Sell),
        ("STOCKS PURCHASE", TransactionType.Buy),
        ("SALE", TransactionType.Sell),
        ("VENTA", TransactionType.Sell),
        ("PURCHASE", TransactionType.Buy),
        ("COMPRA", TransactionType.Buy),
        ("DIVIDEND", TransactionType.Dividend),
        ("DIVIDENDO", TransactionType.Dividend),
        ("WITHHOLDING TAX", TransactionType.Dividend),
        ("RETENCION", TransactionType.Dividend),
        ("DEPOSIT", TransactionType.Deposit),
        ("DEPOSITO", TransactionType.Deposit),
        ("INGRESO", TransactionType.Deposit),
        ("WITHDRAWAL", TransactionType.Withdrawal),
        ("RETIRADA", TransactionType.Withdrawal),
        ("COMMISSION", TransactionType.Fee),
        ("COMISION", TransactionType.Fee),
        ("FEE", TransactionType.Fee),
        ("INTEREST", TransactionType.Interest),
        ("INTERES", TransactionType.Interest),
        ("SPLIT", TransactionType.Split),
        ("FREE FUNDS", TransactionType.Interest),
    ];

    public static TransactionType Map(string? concept)
    {
        if (string.IsNullOrWhiteSpace(concept))
        {
            return TransactionType.Unknown;
        }

        var normalized = Normalize(concept);

        foreach (var (fragment, type) in Rules)
        {
            if (normalized.Contains(fragment, StringComparison.Ordinal))
            {
                return type;
            }
        }

        return TransactionType.Unknown;
    }

    private static string Normalize(string concept)
    {
        var upper = concept.Trim().ToUpperInvariant();
        var withoutAccents = string.Concat(upper.Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                != System.Globalization.UnicodeCategory.NonSpacingMark));

        return withoutAccents.Normalize(System.Text.NormalizationForm.FormC);
    }
}
