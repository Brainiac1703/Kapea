using System.Text.Json;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;

namespace Kapea.Infrastructure.Import.Bit2Me;

/// <summary>
/// Vuelve a leer un movimiento de Bit2Me a partir del texto con el que se importó.
/// </summary>
/// <remarks>
/// Bit2Me cambia la forma de sus respuestas sin avisar, así que aparecen movimientos que
/// el adaptador no sabe clasificar. Cuando aprende a hacerlo, esto permite aprovecharlo
/// sobre lo ya importado en lugar de dejarlo sin clasificar para siempre.
/// </remarks>
public sealed class Bit2MeRecordReinterpreter : IRecordReinterpreter
{
    public PlatformCode Platform => PlatformCode.Bit2Me;

    public ImportRecord? Reinterpret(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawContent);

            // Solo las transacciones de monedero: los movimientos de Earn y las
            // operaciones ya vienen clasificados, y un texto que no sea de esta forma no
            // se fuerza a encajar.
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("id", out _))
            {
                return null;
            }

            var transaction = Bit2MeWalletTransaction.From(document.RootElement);

            return Bit2MeImportAdapter.ReadWalletTransaction(transaction).FirstOrDefault();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
