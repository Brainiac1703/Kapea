using System.Text.RegularExpressions;
using Kapea.Domain.Transactions;

namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>
/// Traduce los conceptos de operación de XTB a los tipos normalizados. Lo que no
/// tiene equivalencia acaba en Unknown y queda fuera del cálculo hasta que una
/// persona lo clasifica: inventar un tipo aquí falsearía un resultado fiscal.
/// </summary>
public static partial class XtbTypeMapper
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

    /// <summary>
    /// Conceptos de compraventa. En el informe de efectivo aparecen sin volumen, porque
    /// son la contrapartida en dinero de una operación que el informe de posiciones ya
    /// trae completa. Clasificarlos como compra o venta crearía lotes de cero unidades y
    /// duplicaría la operación, así que allí quedan sin clasificar y los revisa el usuario.
    /// </summary>
    private static readonly TransactionType[] TradeTypes = [TransactionType.Buy, TransactionType.Sell];

    /// <summary>Mapeo para el informe de operaciones de efectivo, que no trae volumen.</summary>
    public static TransactionType MapCashOperation(string? concept)
    {
        var type = Map(concept);

        return TradeTypes.Contains(type) ? TransactionType.Unknown : type;
    }

    public static TransactionType Map(string? concept)
    {
        if (string.IsNullOrWhiteSpace(concept))
        {
            return TransactionType.Unknown;
        }

        var words = Words(Normalize(concept));

        foreach (var (fragment, type) in Rules)
        {
            // Se compara por palabras completas y no por subcadena. Buscando trozos,
            // "bonificacion inventada" contiene "venta" y acabaría clasificada como una
            // venta sin activo, que revienta la importación entera al confirmarla.
            if (ContainsPhrase(words, fragment))
            {
                return type;
            }
        }

        return TransactionType.Unknown;
    }

    /// <summary>Comprueba que las palabras del fragmento aparezcan seguidas en el concepto.</summary>
    private static bool ContainsPhrase(IReadOnlyList<string> words, string fragment)
    {
        var expected = fragment.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var start = 0; start + expected.Length <= words.Count; start++)
        {
            var matches = true;

            for (var offset = 0; offset < expected.Length; offset++)
            {
                if (!string.Equals(words[start + offset], expected[offset], StringComparison.Ordinal))
                {
                    matches = false;

                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> Words(string concept) =>
        [.. NonLetters().Split(concept).Where(word => word.Length > 0)];

    [GeneratedRegex(@"[^A-Z0-9]+")]
    private static partial Regex NonLetters();

    private static string Normalize(string concept)
    {
        var upper = concept.Trim().ToUpperInvariant();
        var withoutAccents = string.Concat(upper.Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                != System.Globalization.UnicodeCategory.NonSpacingMark));

        return withoutAccents.Normalize(System.Text.NormalizationForm.FormC);
    }
}
