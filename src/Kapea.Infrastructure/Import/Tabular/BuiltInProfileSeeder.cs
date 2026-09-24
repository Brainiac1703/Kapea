using System.Text.Json;
using Kapea.Domain.ImportProfiles;
using Kapea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>
/// Da de alta los perfiles que vienen de serie y los pone al día si han cambiado.
/// </summary>
/// <remarks>
/// Va aquí y no en la migración porque un perfil lleva sus reglas en columnas JSON y
/// una fila sembrada a mano en SQL sería ilegible e imposible de mantener al día.
///
/// Se reconoce por el nombre y no por el identificador: así, un perfil de serie que el
/// usuario haya corregido no se duplica ni se pisa en el siguiente arranque. Corregirlo
/// es la vía prevista cuando el bróker cambia sus cabeceras.
/// </remarks>
public static class BuiltInProfileSeeder
{
    public static async Task EnsureAsync(
        KapeaDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        var builtIn = BuiltInProfiles.All(now);

        var existing = await context.ImportProfiles
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Por nombre, pero tolerando repetidos: nada impide que el usuario llame a un
        // perfil suyo como uno de serie, y reventar por eso dejaría la aplicación sin
        // arrancar.
        var byName = existing.ToLookup(profile => profile.Name, StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var revised = 0;

        foreach (var profile in builtIn)
        {
            var current = byName[profile.Name].FirstOrDefault(candidate => candidate.BuiltIn)
                ?? byName[profile.Name].FirstOrDefault();

            if (current is null)
            {
                context.ImportProfiles.Add(profile);
                added++;

                continue;
            }

            // Un perfil de serie que cambia —porque la plataforma cambió su exportación o
            // porque se corrigió cómo se leía— tiene que llegar a quien ya lo tenía. Sin
            // esto, una base sembrada ayer se queda con la interpretación de ayer para
            // siempre, y el fallo reaparece en cada importación nueva.
            if (!current.BuiltIn || !HasChanged(current.Current, profile.Current))
            {
                continue;
            }

            current.Revise(number => CopyOf(profile.Current, number, now));
            revised++;
        }

        if (added == 0 && revised == 0)
        {
            return;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lo que define cómo se lee un fichero, para poder comparar dos versiones.
    /// </summary>
    /// <remarks>
    /// No entra la fecha de creación, que cambia en cada arranque y haría que el perfil
    /// se revisara eternamente sin que nada hubiera cambiado de verdad.
    /// </remarks>
    internal static bool HasChanged(ImportProfileVersion stored, ImportProfileVersion builtIn) =>
        Signature(stored) != Signature(builtIn);

    private static string Signature(ImportProfileVersion version) =>
        JsonSerializer.Serialize(new
        {
            version.Delimiter,
            version.DecimalConvention,
            version.TimeZoneId,
            version.FixedCurrency,
            version.RowShape,
            version.AmountSource,
            version.FixedAssetClass,
            version.AmountIsAlwaysPositive,
            version.AmountIsNetOfFee,
            version.Sheet,
            Headers = version.RecognizedHeaders.Order(StringComparer.Ordinal),
            Columns = version.Columns.OrderBy(column => column.Key),
            Concepts = version.Concepts.OrderBy(concept => concept.Key, StringComparer.Ordinal),
            NonFinancial = version.NonFinancialConcepts.Order(StringComparer.Ordinal),
            Dates = version.DateFormats,
            Fiat = version.FiatCurrencies.Order(StringComparer.Ordinal),
        });

    internal static ImportProfileVersion CopyOf(ImportProfileVersion source, int number, DateTimeOffset createdAt) =>
        ImportProfileVersion.Create(
            number,
            createdAt,
            source.Delimiter,
            source.DecimalConvention,
            source.TimeZoneId,
            source.RecognizedHeaders,
            source.Columns,
            source.DateFormats,
            source.Concepts,
            source.NonFinancialConcepts,
            source.FixedCurrency,
            source.RowShape,
            source.AmountSource,
            source.FixedAssetClass,
            source.AmountIsAlwaysPositive,
            source.FiatCurrencies,
            source.AmountIsNetOfFee,
            source.Sheet);
}
