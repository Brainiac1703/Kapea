namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>Columna de un formato de exportación, con los nombres bajo los que aparece.</summary>
/// <param name="Canonical">Nombre con el que el adaptador se refiere a la columna.</param>
/// <param name="Aliases">Cabeceras reales que corresponden a esa columna, en cualquier idioma del informe.</param>
/// <param name="IsRequired">Si falta una columna obligatoria, el formato no encaja.</param>
public sealed record XtbColumn(string Canonical, string[] Aliases, bool IsRequired = true);

/// <summary>Qué representa cada fila del informe.</summary>
public enum XtbReportKind
{
    /// <summary>Operaciones de efectivo: ingresos, retiradas, dividendos, comisiones.</summary>
    CashOperations = 1,

    /// <summary>Posiciones cerradas: cada fila es una compra y su venta.</summary>
    ClosedPositions = 2,
}

/// <summary>
/// Formato conocido de exportación de xStation5.
/// </summary>
/// <remarks>
/// XTB cambia estas cabeceras sin aviso, y por eso el reconocimiento es explícito y
/// el fallo es total: importar a medias un fichero que no se entiende produciría
/// cifras mal calculadas que parecen buenas. Añadir un formato nuevo es añadir una
/// entrada a <see cref="Known"/>.
/// </remarks>
public sealed record XtbFormat(string Name, XtbReportKind Kind, IReadOnlyList<XtbColumn> Columns)
{
    public IEnumerable<XtbColumn> RequiredColumns => Columns.Where(column => column.IsRequired);

    public static IReadOnlyList<XtbFormat> Known { get; } =
    [
        new("Cash operations", XtbReportKind.CashOperations,
        [
            new("Id", ["ID", "Id", "Identificador"]),
            new("Type", ["Type", "Tipo"]),
            new("Time", ["Time", "Hora", "Fecha"]),
            new("Symbol", ["Symbol", "Símbolo", "Simbolo"], IsRequired: false),
            new("Comment", ["Comment", "Comentario"], IsRequired: false),
            new("Amount", ["Amount", "Importe", "Cantidad"]),
            new("Currency", ["Currency", "Divisa", "Moneda"], IsRequired: false),
        ]),
        new("Closed positions", XtbReportKind.ClosedPositions,
        [
            new("Position", ["Position", "Posición", "Posicion"]),
            new("Symbol", ["Symbol", "Símbolo", "Simbolo"]),
            new("Type", ["Type", "Tipo"]),
            new("Volume", ["Volume", "Volumen"]),
            new("OpenTime", ["Open time", "Hora de apertura", "Fecha de apertura"]),
            new("OpenPrice", ["Open price", "Precio de apertura"]),
            new("CloseTime", ["Close time", "Hora de cierre", "Fecha de cierre"]),
            new("ClosePrice", ["Close price", "Precio de cierre"]),
            new("Commission", ["Commission", "Comisión", "Comision"], IsRequired: false),
            new("Currency", ["Currency", "Divisa", "Moneda"], IsRequired: false),
            new("GrossPL", ["Gross P/L", "Beneficio bruto", "Profit"], IsRequired: false),
        ]),
    ];
}

/// <summary>
/// El fichero no se corresponde con ningún formato conocido. El mensaje enumera lo
/// esperado y lo encontrado porque, cuando XTB cambia el informe, eso es exactamente
/// lo que hace falta para arreglar el adaptador.
/// </summary>
public sealed class UnknownXtbFormatException(IReadOnlyList<string> foundHeaders)
    : InvalidOperationException(BuildMessage(foundHeaders))
{
    public IReadOnlyList<string> FoundHeaders { get; } = foundHeaders;

    private static string BuildMessage(IReadOnlyList<string> foundHeaders)
    {
        var expected = string.Join(
            " | ",
            XtbFormat.Known.Select(format =>
                $"{format.Name}: {string.Join(", ", format.RequiredColumns.Select(column => column.Aliases[0]))}"));

        return $"El fichero no encaja con ningún formato de exportación conocido. " +
            $"Columnas esperadas por formato — {expected}. " +
            $"Columnas encontradas: {(foundHeaders.Count == 0 ? "ninguna" : string.Join(", ", foundHeaders))}.";
    }
}
