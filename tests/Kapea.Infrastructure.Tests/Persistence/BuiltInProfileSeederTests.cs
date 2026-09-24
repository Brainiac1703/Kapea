using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Import.Tabular;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class BuiltInProfileSeederTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_built_in_profile_that_changed_reaches_a_database_that_already_had_it()
    {
        // Sembrar sólo lo que falta dejaba una base con la interpretación del día que se
        // sembró: al corregir cómo se lee una plataforma, el arreglo no llegaba nunca y
        // el fallo volvía en cada importación.
        var owner = new UserId(Guid.NewGuid());
        var name = "Perfil de serie " + Suffix();

        await using (var context = fixture.CreateContext(owner))
        {
            context.ImportProfiles.Add(Outdated(name));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext(owner))
        {
            await Seed(context, [Current(name)]);
        }

        await using (var context = fixture.CreateContext(owner))
        {
            var profile = await context.ImportProfiles.SingleAsync(entity => entity.Name == name);

            Assert.Equal(2, profile.Current.Number);
            Assert.Equal("Purchase Value", profile.Current.Columns[ImportField.OpenAmount]);

            // La anterior se conserva: los movimientos que entraron con ella la siguen
            // apuntando, y así se puede explicar de dónde salió una cifra vieja.
            Assert.NotNull(profile.FindVersion(1));
        }
    }

    [Fact]
    public async Task A_profile_the_user_made_his_own_is_not_touched()
    {
        var owner = new UserId(Guid.NewGuid());
        var name = "Perfil propio " + Suffix();

        await using (var context = fixture.CreateContext(owner))
        {
            context.ImportProfiles.Add(Outdated(name, builtIn: false));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext(owner))
        {
            await Seed(context, [Current(name)]);
        }

        await using (var context = fixture.CreateContext(owner))
        {
            var profile = await context.ImportProfiles.SingleAsync(entity => entity.Name == name);

            Assert.Equal(1, profile.Current.Number);
        }
    }

    [Fact]
    public async Task Seeding_twice_does_not_pile_up_versions()
    {
        var owner = new UserId(Guid.NewGuid());
        var name = "Perfil estable " + Suffix();

        await using (var context = fixture.CreateContext(owner))
        {
            context.ImportProfiles.Add(Current(name));
            await context.SaveChangesAsync();
        }

        for (var round = 0; round < 2; round++)
        {
            await using var context = fixture.CreateContext(owner);
            await Seed(context, [Current(name)]);
        }

        await using (var context = fixture.CreateContext(owner))
        {
            Assert.Equal(1, (await context.ImportProfiles.SingleAsync(entity => entity.Name == name)).Current.Number);
        }
    }

    private static async Task Seed(Kapea.Infrastructure.Persistence.KapeaDbContext context, IReadOnlyList<ImportProfile> profiles)
    {
        // El sembrador de verdad trabaja sobre los perfiles de serie; aquí se le dan unos
        // de prueba para no depender de cómo sea hoy el catálogo.
        var existing = await context.ImportProfiles.ToListAsync();
        var byName = existing.ToLookup(profile => profile.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            var current = byName[profile.Name].FirstOrDefault();

            if (current is null)
            {
                context.ImportProfiles.Add(profile);

                continue;
            }

            if (current.BuiltIn && BuiltInProfileSeeder.HasChanged(current.Current, profile.Current))
            {
                current.Revise(number => BuiltInProfileSeeder.CopyOf(profile.Current, number, Now));
            }
        }

        await context.SaveChangesAsync();
    }

    private static string Suffix() =>
        Random.Shared.Next(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Como estaba antes de corregir de dónde sale el importe.</summary>
    private static ImportProfile Outdated(string name, bool builtIn = true) =>
        Profile(name, withAmountColumns: false, builtIn);

    /// <summary>Como está ahora: el total lo da el informe, ya convertido.</summary>
    private static ImportProfile Current(string name) => Profile(name, withAmountColumns: true, builtIn: true);

    private static ImportProfile Profile(string name, bool withAmountColumns, bool builtIn)
    {
        var columns = new Dictionary<ImportField, string>
        {
            [ImportField.NaturalId] = "Position ID",
            [ImportField.AssetSymbol] = "Ticker",
            [ImportField.Quantity] = "Volume",
            [ImportField.OpenDate] = "Open Time (UTC)",
            [ImportField.OpenPrice] = "Open Price",
            [ImportField.CloseDate] = "Close Time (UTC)",
            [ImportField.ClosePrice] = "Close Price",
        };

        if (withAmountColumns)
        {
            columns[ImportField.OpenAmount] = "Purchase Value";
            columns[ImportField.CloseAmount] = "Sale Value";
        }

        return ImportProfile.Create(
            PlatformCode.Xtb,
            name,
            number => ImportProfileVersion.Create(
                number,
                Now,
                delimiter: ';',
                DecimalConvention.Invariant,
                "Europe/Madrid",
                ["Position ID", "Ticker", "Volume"],
                columns,
                ["yyyy-MM-dd HH:mm:ss"],
                fixedCurrency: "EUR",
                rowShape: RowShape.OpenAndClosePosition,
                amountSource: AmountSource.QuantityTimesPrice,
                fixedAssetClass: "Equity",
                sheet: "Closed Positions"),
            builtIn);
    }
}
