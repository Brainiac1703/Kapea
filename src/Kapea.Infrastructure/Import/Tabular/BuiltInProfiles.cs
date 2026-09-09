using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>
/// Perfiles que Kapea trae configurados.
/// </summary>
/// <remarks>
/// No son «los formatos soportados»: son los que ya vienen puestos. Cualquier otro se
/// da de alta desde la aplicación, y estos también se pueden corregir, porque XTB
/// cambia sus informes sin avisar.
///
/// Traducen conceptos por igualdad y no por fragmentos. Buscar fragmentos parecía más
/// tolerante y no lo era: un concepto inventado que contuviera «venta» se clasificaba
/// como una venta y falseaba un resultado. Lo que no está traducido entra sin
/// clasificar y aparece en revisión, que es un hueco visible en lugar de una cifra
/// equivocada.
/// </remarks>
public static class BuiltInProfiles
{
    /// <summary>Los formatos de xStation5 vienen con la fecha en la zona de la plataforma, sin desfase.</summary>
    private const string TimeZone = "Europe/Madrid";

    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
    ];

    /// <summary>
    /// Conceptos del informe de efectivo.
    /// </summary>
    /// <remarks>
    /// Las compras y las ventas no están, y es deliberado: en este informe aparecen sin
    /// volumen, porque son la contrapartida en dinero de una operación que el informe de
    /// posiciones ya trae entera. Traducirlas aquí crearía lotes de cero unidades y
    /// duplicaría la operación.
    /// </remarks>
    private static readonly Dictionary<string, TransactionType> CashConcepts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dividend"] = TransactionType.Dividend,
        ["Dividendo"] = TransactionType.Dividend,
        ["Withholding tax"] = TransactionType.Dividend,
        ["Retención de dividendos"] = TransactionType.Dividend,
        ["Deposit"] = TransactionType.Deposit,
        ["Depósito"] = TransactionType.Deposit,
        ["Ingreso"] = TransactionType.Deposit,
        ["Withdrawal"] = TransactionType.Withdrawal,
        ["Retirada"] = TransactionType.Withdrawal,
        ["Commission"] = TransactionType.Fee,
        ["Comisión"] = TransactionType.Fee,
        ["Free-funds Interest"] = TransactionType.Interest,
        ["Free funds interest"] = TransactionType.Interest,
        ["Interés de fondos libres"] = TransactionType.Interest,
        ["Split"] = TransactionType.Split,
    };

    public static IReadOnlyList<ImportProfile> All(DateTimeOffset createdAt) =>
    [
        CashOperations(createdAt),
        ClosedPositions(createdAt),
        OpenPositions(createdAt),
    ];

    private static ImportProfile CashOperations(DateTimeOffset createdAt) =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Operaciones de efectivo",
            number => ImportProfileVersion.Create(
                number,
                createdAt,
                delimiter: ';',
                DecimalConvention.European,
                TimeZone,
                ["ID", "Type", "Time", "Amount"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "ID",
                    [ImportField.Concept] = "Type",
                    [ImportField.Date] = "Time",
                    [ImportField.AssetSymbol] = "Symbol",
                    [ImportField.GrossAmount] = "Amount",
                    [ImportField.Currency] = "Currency",
                },
                DateFormats,
                CashConcepts,
                nonFinancialConcepts: null,
                fixedCurrency: "EUR",
                RowShape.SingleMovement,
                AmountSource.Column,
                fixedAssetClass: null,
                amountIsAlwaysPositive: true),
            builtIn: true);

    private static ImportProfile ClosedPositions(DateTimeOffset createdAt) =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Posiciones cerradas",
            number => ImportProfileVersion.Create(
                number,
                createdAt,
                delimiter: ';',
                DecimalConvention.European,
                TimeZone,
                ["Position", "Symbol", "Volume", "Open time", "Open price", "Close time", "Close price"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "Position",
                    [ImportField.AssetSymbol] = "Symbol",
                    [ImportField.Quantity] = "Volume",
                    [ImportField.OpenDate] = "Open time",
                    [ImportField.OpenPrice] = "Open price",
                    [ImportField.CloseDate] = "Close time",
                    [ImportField.ClosePrice] = "Close price",
                    [ImportField.Fee] = "Commission",
                    [ImportField.Currency] = "Currency",
                },
                DateFormats,
                concepts: null,
                nonFinancialConcepts: null,
                fixedCurrency: "EUR",
                RowShape.OpenAndClosePosition,
                AmountSource.QuantityTimesPrice,
                fixedAssetClass: "Equity"),
            builtIn: true);

    private static ImportProfile OpenPositions(DateTimeOffset createdAt) =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Posiciones abiertas",
            number => ImportProfileVersion.Create(
                number,
                createdAt,
                delimiter: ';',
                DecimalConvention.European,
                TimeZone,
                // Sin «Close time» a propósito: es lo que distingue este informe del de
                // posiciones cerradas, que trae exactamente las mismas columnas más esa.
                ["Position", "Symbol", "Volume", "Open time", "Open price"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "Position",
                    [ImportField.AssetSymbol] = "Symbol",
                    [ImportField.Quantity] = "Volume",
                    [ImportField.OpenDate] = "Open time",
                    [ImportField.OpenPrice] = "Open price",
                    [ImportField.Fee] = "Commission",
                    [ImportField.Currency] = "Currency",
                },
                DateFormats,
                concepts: null,
                nonFinancialConcepts: null,
                fixedCurrency: "EUR",
                RowShape.OpenPosition,
                AmountSource.QuantityTimesPrice,
                fixedAssetClass: "Equity"),
            builtIn: true);
}
