using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;

namespace Kapea.Domain.Tests.ImportProfiles;

public class ImportProfileVersionTests
{
    [Fact]
    public void A_profile_without_a_date_column_is_rejected_naming_what_is_missing()
    {
        var exception = Assert.Throws<DomainException>(() => Version(columns: new Dictionary<ImportField, string>
        {
            [ImportField.GrossAmount] = "Importe",
        }));

        Assert.Contains("la fecha", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_without_an_amount_column_is_rejected_naming_what_is_missing()
    {
        var exception = Assert.Throws<DomainException>(() => Version(columns: new Dictionary<ImportField, string>
        {
            [ImportField.Date] = "Fecha",
        }));

        Assert.Contains("el importe", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_without_headers_is_rejected()
    {
        var exception = Assert.Throws<DomainException>(() => Version(headers: []));

        Assert.Contains("cabecera", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_without_date_formats_is_rejected()
    {
        var exception = Assert.Throws<DomainException>(() => Version(dateFormats: []));

        Assert.Contains("formato", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_with_neither_a_currency_column_nor_a_fixed_currency_is_rejected()
    {
        var exception = Assert.Throws<DomainException>(() => Version(fixedCurrency: null));

        Assert.Contains("divisa", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_currency_column_makes_the_fixed_currency_unnecessary()
    {
        var version = Version(
            columns: new Dictionary<ImportField, string>
            {
                [ImportField.Date] = "Fecha",
                [ImportField.GrossAmount] = "Importe",
                [ImportField.Currency] = "Divisa",
            },
            fixedCurrency: null);

        Assert.Equal("Divisa", version.Columns[ImportField.Currency]);
    }

    internal static ImportProfileVersion Version(
        int number = 1,
        IEnumerable<string>? headers = null,
        IReadOnlyDictionary<ImportField, string>? columns = null,
        IEnumerable<string>? dateFormats = null,
        string? fixedCurrency = "EUR",
        IReadOnlyDictionary<string, TransactionType>? concepts = null,
        DateTimeOffset? createdAt = null) =>
        ImportProfileVersion.Create(
            number,
            createdAt ?? DateTimeOffset.UnixEpoch,
            delimiter: ';',
            DecimalConvention.European,
            timeZoneId: "Europe/Madrid",
            headers ?? ["Fecha", "Importe"],
            columns ?? new Dictionary<ImportField, string>
            {
                [ImportField.Date] = "Fecha",
                [ImportField.GrossAmount] = "Importe",
            },
            dateFormats ?? ["dd/MM/yyyy"],
            concepts,
            nonFinancialConcepts: null,
            fixedCurrency);
}

public class ImportProfileVersioningTests
{
    [Fact]
    public void Revising_a_profile_keeps_the_previous_version_reachable()
    {
        // Es lo que permite explicar una cifra interpretada hace meses: sus reglas
        // siguen ahí aunque el perfil se haya corregido después.
        var profile = ImportProfile.Create(
            PlatformCode.Xtb, "Efectivo", number => ImportProfileVersionTests.Version(number));

        var first = profile.Current;

        profile.Revise(number => ImportProfileVersionTests.Version(
            number,
            headers: ["Fecha", "Importe", "Comentario"],
            createdAt: DateTimeOffset.UnixEpoch.AddDays(1)));

        Assert.Equal(2, profile.Current.Number);
        Assert.Equal(first.Id, profile.FindVersion(1)!.Id);
        Assert.Equal(2, profile.Versions.Count);
    }

    [Fact]
    public void A_profile_needs_a_name_because_one_platform_exports_several_reports() =>
        Assert.Throws<DomainException>(() => ImportProfile.Create(
            PlatformCode.Xtb, "  ", number => ImportProfileVersionTests.Version(number)));
}

public class ProfileMatchingTests
{
    [Fact]
    public void A_known_format_finds_its_profile()
    {
        var profile = Profile("Efectivo", ["Fecha", "Importe"]);

        var match = ProfileMatching.Match([profile], PlatformCode.Xtb, ["Fecha", "Importe", "Comentario"]);

        Assert.True(match.Found);
        Assert.Equal(profile.Id, match.Profile!.Id);
        Assert.False(match.Ambiguous);
    }

    [Fact]
    public void An_unknown_format_finds_nothing()
    {
        var profile = Profile("Efectivo", ["Fecha", "Importe"]);

        var match = ProfileMatching.Match([profile], PlatformCode.Xtb, ["Date", "Amount"]);

        Assert.False(match.Found);
    }

    [Fact]
    public void A_profile_of_another_platform_is_never_used()
    {
        var profile = Profile("Efectivo", ["Fecha", "Importe"]);

        var match = ProfileMatching.Match([profile], PlatformCode.Kraken, ["Fecha", "Importe"]);

        Assert.False(match.Found);
    }

    [Fact]
    public void With_several_candidates_the_most_specific_wins_and_it_is_recorded()
    {
        var general = Profile("General", ["Fecha", "Importe"]);
        var specific = Profile("Posiciones", ["Fecha", "Importe", "Símbolo"]);

        var match = ProfileMatching.Match(
            [general, specific], PlatformCode.Xtb, ["Fecha", "Importe", "Símbolo"]);

        Assert.Equal(specific.Id, match.Profile!.Id);
        Assert.True(match.Ambiguous);
        Assert.Equal(2, match.Candidates.Count);
    }

    [Fact]
    public void Between_equally_specific_candidates_the_most_recent_wins()
    {
        var old = Profile("Antiguo", ["Fecha", "Importe"], DateTimeOffset.UnixEpoch);
        var recent = Profile("Reciente", ["Fecha", "Importe"], DateTimeOffset.UnixEpoch.AddYears(1));

        var match = ProfileMatching.Match([old, recent], PlatformCode.Xtb, ["Fecha", "Importe"]);

        Assert.Equal(recent.Id, match.Profile!.Id);
        Assert.True(match.Ambiguous);
    }

    private static ImportProfile Profile(
        string name,
        string[] headers,
        DateTimeOffset? createdAt = null) =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            name,
            number => ImportProfileVersionTests.Version(number, headers, createdAt: createdAt));
}
