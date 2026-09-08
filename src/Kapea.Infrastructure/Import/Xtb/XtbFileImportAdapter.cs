using System.Globalization;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>
/// Importa la exportación de fichero de xStation5. XTB no ofrece API pública de
/// histórico para minorista, así que el fichero es la única vía.
/// </summary>
/// <remarks>
/// Las fechas del informe vienen sin zona; se interpretan en la zona de la plataforma,
/// que es la que ve el usuario en su extracto.
/// </remarks>
public sealed class XtbFileImportAdapter(ILogger<XtbFileImportAdapter> logger) : IFileImportAdapter
{
    internal const string PlatformTimeZoneId = "Europe/Madrid";

    public Platform Platform => Platform.Xtb;

    public ImportSourceKind SourceKind => ImportSourceKind.UploadedFile;

    public Task<ImportReadResult> ReadAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!TabularReader.IsSupported(fileName))
        {
            throw new UnsupportedImportFileException(Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant());
        }

        var tabular = TabularReader.Read(content, fileName);

        // La detección va antes que cualquier fila: si el informe ha cambiado, se
        // rechaza el fichero entero en lugar de importar la mitad que se entiende.
        var (format, columns) = XtbFormatDetector.Detect(tabular.Headers);

        logger.LogInformation(
            "Fichero de XTB reconocido como formato '{Formato}' con {Filas} filas.", format.Name, tabular.Rows.Count);

        var records = new List<ImportRecord>();
        var rejected = new List<RejectedRecord>();
        var nonFinancial = 0;
        var warnings = new List<string>();

        foreach (var missing in format.Columns.Where(column => !column.IsRequired && !columns.ContainsKey(column.Canonical)))
        {
            warnings.Add($"El fichero no trae la columna opcional '{missing.Aliases[0]}': los movimientos irán sin ese dato.");
        }

        foreach (var row in tabular.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var mapped = format.Kind == XtbReportKind.CashOperations
                    ? MapCashOperation(row, columns)
                    : MapClosedPosition(row, columns);

                if (mapped.Count == 0)
                {
                    nonFinancial++;
                    continue;
                }

                records.AddRange(mapped);
            }
            catch (XtbRowException exception)
            {
                rejected.Add(new RejectedRecord(null, row.Number, row.Raw, exception.Message));
            }
        }

        return Task.FromResult(new ImportReadResult(records, rejected, nonFinancial) { Warnings = warnings });
    }

    private static IReadOnlyList<ImportRecord> MapCashOperation(TabularRow row, IReadOnlyDictionary<string, int> columns)
    {
        var concept = Text(row, columns, "Type");
        var type = XtbTypeMapper.Map(concept);

        if (!XtbValueReader.TryReadDate(Text(row, columns, "Time"), out var occurredAt))
        {
            throw new XtbRowException($"La fecha '{Text(row, columns, "Time")}' no se puede interpretar.");
        }

        if (!XtbValueReader.TryReadDecimal(Text(row, columns, "Amount"), out var amount))
        {
            throw new XtbRowException($"El importe '{Text(row, columns, "Amount")}' no se puede interpretar.");
        }

        // Un apunte informativo sin efecto financiero y sin activo no aporta nada al
        // cálculo; se descarta y se cuenta, pero no se trata como error.
        if (amount == 0m && string.IsNullOrWhiteSpace(Text(row, columns, "Symbol")))
        {
            return [];
        }

        var symbol = Text(row, columns, "Symbol");
        var currency = ReadCurrency(row, columns);

        return
        [
            new ImportRecord(
                NaturalId: Text(row, columns, "Id") is { Length: > 0 } id ? id : null,
                RowNumber: row.Number,
                Type: type,
                AssetSymbol: string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToUpperInvariant(),
                AssetClass: string.IsNullOrWhiteSpace(symbol) ? null : Domain.Assets.AssetClass.Equity,
                Quantity: 0m,
                UnitPrice: null,
                GrossAmount: Math.Abs(amount),
                Currency: currency,
                Fee: 0m,
                Withholding: null,
                OccurredAt: null,
                NaiveOccurredAt: occurredAt,
                SourceTimeZoneId: PlatformTimeZoneId,
                SplitRatio: null,
                RawContent: row.Raw),
        ];
    }

    /// <summary>
    /// Cada posición cerrada es en realidad dos operaciones: la apertura y el cierre.
    /// Se emiten como compra y venta separadas porque el criterio FIFO necesita la
    /// adquisición y la transmisión con sus propias fechas.
    /// </summary>
    private static IReadOnlyList<ImportRecord> MapClosedPosition(TabularRow row, IReadOnlyDictionary<string, int> columns)
    {
        var symbol = Text(row, columns, "Symbol");

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new XtbRowException("La posición no indica símbolo.");
        }

        if (!XtbValueReader.TryReadDecimal(Text(row, columns, "Volume"), out var volume) || volume <= 0m)
        {
            throw new XtbRowException($"El volumen '{Text(row, columns, "Volume")}' no se puede interpretar.");
        }

        if (!XtbValueReader.TryReadDate(Text(row, columns, "OpenTime"), out var openedAt)
            || !XtbValueReader.TryReadDate(Text(row, columns, "CloseTime"), out var closedAt))
        {
            throw new XtbRowException("La posición no trae fechas de apertura y cierre interpretables.");
        }

        if (!XtbValueReader.TryReadDecimal(Text(row, columns, "OpenPrice"), out var openPrice)
            || !XtbValueReader.TryReadDecimal(Text(row, columns, "ClosePrice"), out var closePrice))
        {
            throw new XtbRowException("La posición no trae precios de apertura y cierre interpretables.");
        }

        XtbValueReader.TryReadDecimal(Text(row, columns, "Commission"), out var commission);

        var position = Text(row, columns, "Position");
        var currency = ReadCurrency(row, columns);
        var canonical = symbol.Trim().ToUpperInvariant();

        return
        [
            Leg(TransactionType.Buy, openedAt, openPrice, $"{position}:open", Math.Abs(commission)),
            Leg(TransactionType.Sell, closedAt, closePrice, $"{position}:close", 0m),
        ];

        ImportRecord Leg(TransactionType type, DateTime moment, decimal price, string naturalId, decimal fee) =>
            new(
                NaturalId: string.IsNullOrWhiteSpace(position) ? null : naturalId,
                RowNumber: row.Number,
                Type: type,
                AssetSymbol: canonical,
                AssetClass: AssetClass.Equity,
                Quantity: volume,
                UnitPrice: price,
                GrossAmount: volume * price,
                Currency: currency,
                Fee: fee,
                Withholding: null,
                OccurredAt: null,
                NaiveOccurredAt: moment,
                SourceTimeZoneId: PlatformTimeZoneId,
                SplitRatio: null,
                RawContent: row.Raw);
    }

    private static Currency ReadCurrency(TabularRow row, IReadOnlyDictionary<string, int> columns)
    {
        var text = Text(row, columns, "Currency");

        return string.IsNullOrWhiteSpace(text) ? Currency.Euro : Currency.FromCode(text);
    }

    private static string Text(TabularRow row, IReadOnlyDictionary<string, int> columns, string canonical) =>
        columns.TryGetValue(canonical, out var index)
            ? (row.Cell(index) ?? string.Empty).Trim()
            : string.Empty;

    private sealed class XtbRowException(string message) : Exception(message);
}
