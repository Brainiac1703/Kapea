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

        // La retención sobre el interés llega en su propia fila, con otro identificador
        // y sin más nexo que el periodo escrito en el comentario. Entra como gasto: el
        // dinero cuadra exacto y no se inventa el emparejamiento con su rendimiento.
        ["Free-funds Interest Tax"] = TransactionType.Fee,
        ["Free funds interest tax"] = TransactionType.Fee,
        ["SEC fee"] = TransactionType.Fee,
        ["Sec fee"] = TransactionType.Fee,
    };

    /// <summary>
    /// Lo que la hoja de efectivo cuenta y otra hoja ya aporta mejor.
    /// </summary>
    /// <remarks>
    /// Una compra o una venta aparecen en las dos hojas: en la de posiciones con su
    /// cantidad y su precio, y aquí sólo como un importe. Importar las dos contaría el
    /// dinero dos veces, y sin cantidad no hay lote ni FIFO, así que la que se queda es
    /// la otra.
    /// </remarks>
    private static readonly string[] CoveredByPositions =
    [
        "Stock purchase",
        "Stock sell",
        "Compra de acciones",
        "Venta de acciones",
    ];

    public static IReadOnlyList<ImportProfile> All(DateTimeOffset createdAt) =>
    [
        CashOperations(createdAt),
        ClosedPositions(createdAt),
        OpenPositions(createdAt),
        ReportCashOperations(createdAt),
        ReportClosedPositions(createdAt),
        Bit2MeSummary(createdAt),
    ];

    /// <summary>
    /// La hoja de efectivo del informe que XTB descarga hoy.
    /// </summary>
    /// <remarks>
    /// Va aparte del perfil de siempre y no como una versión suya porque los dos
    /// formatos conviven: un perfil reconoce un fichero sólo si están todas las
    /// cabeceras que declara, así que cambiárselas dejaría ilegibles las exportaciones
    /// anteriores que el usuario aún conserve.
    /// </remarks>
    private static ImportProfile ReportCashOperations(DateTimeOffset createdAt) =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Informe · Operaciones de efectivo",
            number => ImportProfileVersion.Create(
                number,
                createdAt,
                delimiter: ';',
                DecimalConvention.Invariant,
                TimeZone,
                ["Type", "Time", "Amount", "ID", "Ticker", "Comment"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "ID",
                    [ImportField.Concept] = "Type",
                    [ImportField.Date] = "Time",
                    [ImportField.AssetSymbol] = "Ticker",
                    [ImportField.GrossAmount] = "Amount",
                },
                DateFormats,
                CashConcepts,
                CoveredByPositions,
                fixedCurrency: "EUR",
                RowShape.SingleMovement,
                AmountSource.Column,
                fixedAssetClass: "Equity",
                amountIsAlwaysPositive: true,
                sheet: "Cash Operations"),
            builtIn: true);

    /// <summary>Las posiciones ya cerradas del mismo informe, con su cantidad y sus precios.</summary>
    private static ImportProfile ReportClosedPositions(DateTimeOffset createdAt) =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Informe · Posiciones cerradas",
            number => ImportProfileVersion.Create(
                number,
                createdAt,
                delimiter: ';',
                DecimalConvention.Invariant,
                TimeZone,
                ["Position ID", "Ticker", "Volume", "Open Price", "Open Time (UTC)", "Close Price", "Close Time (UTC)"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "Position ID",
                    [ImportField.AssetSymbol] = "Ticker",
                    [ImportField.Quantity] = "Volume",
                    [ImportField.OpenDate] = "Open Time (UTC)",
                    [ImportField.OpenPrice] = "Open Price",
                    [ImportField.CloseDate] = "Close Time (UTC)",
                    [ImportField.ClosePrice] = "Close Price",
                    [ImportField.Fee] = "Commission",

                    // XTB cotiza en la divisa del mercado y da aparte el total ya pasado
                    // a euros con el cambio que aplicó. Multiplicando cantidad por precio
                    // salían dólares contados como euros.
                    [ImportField.OpenAmount] = "Purchase Value",
                    [ImportField.CloseAmount] = "Sale Value",
                },
                DateFormats,
                concepts: null,
                nonFinancialConcepts: null,
                fixedCurrency: "EUR",
                RowShape.OpenAndClosePosition,
                AmountSource.QuantityTimesPrice,
                fixedAssetClass: "Equity",
                sheet: "Closed Positions"),
            builtIn: true);

    /// <summary>
    /// Resumen anual de movimientos que exporta Bit2Me desde su web.
    /// </summary>
    /// <remarks>
    /// Existe porque su API no devuelve todo lo que este fichero sí trae: las ventas de
    /// cripto a euros no aparecen por ninguna de sus rutas de histórico. Poder importarlo
    /// es lo que permite cuadrar una cartera que la sincronización deja incompleta.
    ///
    /// Lo que cada fila significa lo dicen sus dos monedas, no su texto: pagar con euros
    /// es comprar y cobrar euros es vender. El concepto solo manda para lo que el par no
    /// puede decir, como que una entrega fue una recompensa.
    /// </remarks>
    private static ImportProfile Bit2MeSummary(DateTimeOffset createdAt) =>
        ImportProfile.Create(
            PlatformCode.Bit2Me,
            "Bit2Me · Resumen de movimientos",
            number => ImportProfileVersion.Create(
                number,
                createdAt,
                delimiter: ',',
                DecimalConvention.Invariant,
                TimeZone,
                ["Tipo de operación", "Cantidad de destino", "Moneda de destino", "Cantidad de origen", "Moneda de origen"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.Concept] = "Tipo de operación",
                    [ImportField.Date] = "Fecha",
                    [ImportField.DestinationAmount] = "Cantidad de destino",
                    [ImportField.DestinationCurrency] = "Moneda de destino",
                    [ImportField.OriginAmount] = "Cantidad de origen",
                    [ImportField.OriginCurrency] = "Moneda de origen",
                    [ImportField.Fee] = "Comisión de la operación",
                    [ImportField.NaturalId] = "Descripción",
                },
                ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd"],
                new Dictionary<string, TransactionType>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Staking"] = TransactionType.Reward,
                    ["Deposit"] = TransactionType.Deposit,
                    ["Withdrawal"] = TransactionType.Withdrawal,
                },
                nonFinancialConcepts: null,
                fixedCurrency: "EUR",
                RowShape.ExchangePair,
                AmountSource.Column,
                fixedAssetClass: "Crypto",
                amountIsAlwaysPositive: true,
                fiatCurrencies: ["EUR", "USD", "GBP", "CHF"],

                // Bit2Me exporta el dinero que se movió, no el bruto de la operación:
                // una venta de cien euros con una comisión de uno aparece como noventa y
                // nueve. Restarla otra vez dejaba el saldo corto y la venta rindiendo
                // menos de lo que rindió.
                amountIsNetOfFee: true),
            builtIn: true);

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
