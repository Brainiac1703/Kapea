using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class ImportProfilePersistenceTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_profile_survives_the_round_trip_with_all_its_rules()
    {
        // Las reglas van en JSON dentro de una columna. Un viaje de ida y vuelta
        // completo es lo único que demuestra que no se pierde ninguna por el camino.
        var profile = ImportProfile.Create(
            PlatformCode.Xtb,
            "Efectivo",
            number => ImportProfileVersion.Create(
                number,
                DateTimeOffset.UnixEpoch,
                delimiter: '\t',
                DecimalConvention.Invariant,
                timeZoneId: "Europe/Madrid",
                ["ID", "Type", "Time", "Amount"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "ID",
                    [ImportField.Concept] = "Type",
                    [ImportField.Date] = "Time",
                    [ImportField.GrossAmount] = "Amount",
                },
                ["dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd"],
                new Dictionary<string, TransactionType>
                {
                    ["Dividend"] = TransactionType.Dividend,
                    ["Stocks/ETF purchase"] = TransactionType.Buy,
                },
                ["Free funds interest tax"],
                fixedCurrency: "eur"));

        await using (var writer = fixture.CreateContext(new UserId(Guid.NewGuid())))
        {
            writer.ImportProfiles.Add(profile);
            await writer.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(new UserId(Guid.NewGuid()));

        var stored = await reader.ImportProfiles
            .SingleAsync(entity => entity.Id == profile.Id);

        var version = stored.Current;

        Assert.Equal(PlatformCode.Xtb, stored.Platform);
        Assert.Equal('\t', version.Delimiter);
        Assert.Equal(DecimalConvention.Invariant, version.DecimalConvention);
        Assert.Equal("EUR", version.FixedCurrency);
        Assert.Equal(["ID", "Type", "Time", "Amount"], version.RecognizedHeaders);
        Assert.Equal(["dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd"], version.DateFormats);
        Assert.Equal(["Free funds interest tax"], version.NonFinancialConcepts);
        Assert.Equal("Time", version.Columns[ImportField.Date]);
        Assert.Equal(TransactionType.Dividend, version.Concepts["Dividend"]);
        Assert.Equal(TransactionType.Buy, version.Concepts["Stocks/ETF purchase"]);
    }

    [Fact]
    public async Task A_revision_is_stored_beside_the_version_it_replaces()
    {
        var profile = ImportProfile.Create(
            PlatformCode.Xtb, "Posiciones", Version);

        await using (var writer = fixture.CreateContext(new UserId(Guid.NewGuid())))
        {
            writer.ImportProfiles.Add(profile);
            await writer.SaveChangesAsync();

            var tracked = await writer.ImportProfiles.SingleAsync(entity => entity.Id == profile.Id);
            tracked.Revise(number => Version(number));
            await writer.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(new UserId(Guid.NewGuid()));

        var stored = await reader.ImportProfiles.SingleAsync(entity => entity.Id == profile.Id);

        Assert.Equal(2, stored.Versions.Count);
        Assert.Equal(2, stored.Current.Number);
        Assert.NotNull(stored.FindVersion(1));
    }

    [Fact]
    public async Task A_transaction_says_with_which_profile_and_version_it_was_read()
    {
        // Es la trazabilidad que hace falta si una cifra fiscal se cuestiona: de qué
        // fila salió, con qué reglas se leyó, y en qué versión de esas reglas.
        var userId = new UserId(Guid.NewGuid());
        var profile = ImportProfile.Create(PlatformCode.Xtb, "Efectivo", Version);

        await using var context = fixture.CreateContext(userId);

        var account = PlatformAccount.Create(userId, PlatformCode.Xtb, "XTB", Currency.Euro);
        var transaction = Kapea.Domain.Transactions.Transaction.Imported(
            userId,
            account.Id,
            TransactionType.Dividend,
            assetId: null,
            new Quantity(0m),
            unitPrice: null,
            new Money(12.50m, Currency.Euro),
            Money.Zero(Currency.Euro),
            Occurrence.FromNaive(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), "Europe/Madrid"),
            TransactionSource.FromImport(
                Guid.NewGuid(),
                naturalId: null,
                rowNumber: 7,
                fingerprint: Guid.NewGuid().ToString("N"),
                rawContent: "fila original",
                profileId: profile.Id,
                profileVersion: 1));

        context.ImportProfiles.Add(profile);
        context.Accounts.Add(account);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        var queries = new Kapea.Infrastructure.Persistence.Stores.PortfolioQueries(
            context,
            new NoPrices(),
            new NoRates(),
            new Kapea.Infrastructure.Persistence.Stores.PriceHistoryStore(context));
        var stored = (await queries.ListTransactionsAsync(account.Id, onlyRequiringReview: false))
            .Single(entity => entity.Id == transaction.Id);

        Assert.Equal(profile.Id, stored.ProfileId);
        Assert.Equal("Efectivo", stored.ProfileName);
        Assert.Equal(1, stored.ProfileVersion);
        Assert.Equal(7, stored.SourceRowNumber);
        Assert.Equal("fila original", stored.RawContent);
    }

    /// <summary>Aquí no se miran precios de mercado: lo que se comprueba es el rastro hasta el perfil.</summary>
    private sealed class NoRates : Kapea.Application.Abstractions.IExchangeRateProvider
    {
        public Task<Kapea.Domain.Exchange.ExchangeRate?> ResolveAsync(
            Kapea.Domain.ValueObjects.Currency currency,
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Kapea.Domain.Exchange.ExchangeRate?>(null);
    }

    private sealed class NoPrices : Kapea.Application.Abstractions.IMarketPriceProvider
    {
        public Task<IReadOnlyDictionary<string, Kapea.Application.Abstractions.MarketPrice>> GetPricesAsync(
            IReadOnlyCollection<string> canonicalSymbols,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, Kapea.Application.Abstractions.MarketPrice>>(
                new Dictionary<string, Kapea.Application.Abstractions.MarketPrice>());
    }

    private static ImportProfileVersion Version(int number) =>
        ImportProfileVersion.Create(
            number,
            DateTimeOffset.UnixEpoch.AddDays(number),
            delimiter: ';',
            DecimalConvention.European,
            timeZoneId: "Europe/Madrid",
            ["Fecha", "Importe"],
            new Dictionary<ImportField, string>
            {
                [ImportField.Date] = "Fecha",
                [ImportField.GrossAmount] = "Importe",
            },
            ["dd/MM/yyyy"],
            fixedCurrency: "EUR");
}
