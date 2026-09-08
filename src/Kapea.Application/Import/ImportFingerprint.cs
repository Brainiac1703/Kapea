using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>
/// Huella que identifica un registro de origen dentro de una cuenta. Es lo que hace
/// idempotente la importación: reimportar el mismo periodo no duplica nada.
/// </summary>
/// <remarks>
/// Dos niveles, por orden de preferencia:
///
/// 1. El identificador natural del origen (txid de Kraken, id de operación de Bit2Me).
///    Es estable y no depende de nada más.
/// 2. Cuando el origen no lo aporta —el caso de los ficheros de XTB— los datos
///    financieros del registro más su número de fila.
///
/// Incluir el número de fila tiene una consecuencia asumida: reimportar el mismo
/// fichero con las filas reordenadas duplicaría. Se prefiere a la alternativa, que
/// era colapsar en uno dos movimientos reales idénticos del mismo instante, porque
/// perder una operación es peor que un duplicado que se ve en la vista previa.
/// </remarks>
public static class ImportFingerprint
{
    public static string For(Guid accountId, Platform platform, ImportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var payload = record.NaturalId is { Length: > 0 } naturalId
            ? string.Create(CultureInfo.InvariantCulture, $"{accountId:N}|{platform}|id|{naturalId}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{accountId:N}|{platform}|row|{record.RowNumber}|{record.Type}|{record.AssetSymbol}|" +
                $"{record.Quantity}|{record.UnitPrice}|{record.GrossAmount}|{record.Currency.Code}|" +
                $"{record.Fee}|{Instant(record):O}");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Indica si la huella del registro se apoya en el identificador del origen o en sus datos.</summary>
    public static bool UsesNaturalId(ImportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.NaturalId is { Length: > 0 };
    }

    private static DateTimeOffset Instant(ImportRecord record) =>
        record.OccurredAt ?? new DateTimeOffset(record.NaiveOccurredAt ?? default, TimeSpan.Zero);
}
