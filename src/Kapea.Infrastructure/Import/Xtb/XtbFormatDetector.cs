namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>
/// Reconoce el formato del fichero por sus cabeceras, antes de tocar ninguna fila.
/// Es la primera línea de defensa frente a un cambio de informe de XTB.
/// </summary>
public static class XtbFormatDetector
{
    /// <summary>
    /// Devuelve el formato que encaja y la correspondencia entre columna canónica e
    /// índice de columna del fichero. Lanza si ninguno encaja.
    /// </summary>
    public static (XtbFormat Format, IReadOnlyDictionary<string, int> ColumnIndexes) Detect(IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var normalized = headers.Select(Normalize).ToList();

        foreach (var format in XtbFormat.Known)
        {
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var missesRequired = false;

            foreach (var column in format.Columns)
            {
                var index = normalized.FindIndex(header =>
                    column.Aliases.Any(alias => string.Equals(Normalize(alias), header, StringComparison.Ordinal)));

                if (index >= 0)
                {
                    indexes[column.Canonical] = index;
                }
                else if (column.IsRequired)
                {
                    missesRequired = true;
                    break;
                }
            }

            if (!missesRequired)
            {
                // Las columnas que el fichero trae de más simplemente no se mapean:
                // ignorarlas es lo correcto, no motivo para rechazar el fichero.
                return (format, indexes);
            }
        }

        throw new UnknownXtbFormatException(headers);
    }

    private static string Normalize(string header) =>
        header.Trim().ToUpperInvariant().Replace(".", string.Empty, StringComparison.Ordinal);
}
