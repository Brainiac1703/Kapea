using System.Text;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Infrastructure.Import.Tabular;
using Kapea.Infrastructure.Import.Xtb;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

/// <summary>
/// Comprueba que los perfiles de serie leen los ficheros de XTB exactamente igual que
/// el adaptador escrito a mano.
/// </summary>
/// <remarks>
/// No basta con que produzcan movimientos parecidos. La huella de deduplicación se
/// calcula sobre estos campos, así que cualquier diferencia haría que el histórico ya
/// importado dejara de reconocerse y entrara por segunda vez.
/// </remarks>
public class BuiltInProfileEquivalenceTests
{
    private const string CashOperations = """
        ID;Type;Time;Symbol;Comment;Amount;Currency
        1001;Stocks purchase;10.01.2024 09:30:00;SAN.ES;OPEN BUY;-1.234,56;EUR
        1002;Dividend;15.05.2024 00:00:00;SAN.ES;DIV;45,20;EUR
        1003;Free funds interest;30.06.2024 00:00:00;;;1,15;EUR
        """;

    private const string ClosedPositions = """
        Position;Symbol;Type;Volume;Open time;Open price;Close time;Close price;Commission;Currency
        77001;AAPL.US;BUY;10;05.02.2024 15:30:00;180,50;20.09.2025 16:00:00;225,75;-2,50;USD
        """;

    private const string OpenPositions = """
        Position;Symbol;Type;Volume;Open time;Open price;Market price;Commission;Currency
        77003;SAN.ES;BUY;50;01.04.2025 09:00:00;4,10;4,55;-1,00;EUR
        """;

    [Theory]
    [InlineData(CashOperations)]
    [InlineData(ClosedPositions)]
    [InlineData(OpenPositions)]
    public async Task The_built_in_profiles_read_the_same_records_as_the_hand_written_adapter(string csv)
    {
        var expected = await ReadWithAdapter(csv);
        var actual = ReadWithProfile(csv);

        Assert.Equal(expected.Records.Count, actual.Records.Count);

        foreach (var (left, right) in expected.Records.Zip(actual.Records))
        {
            Assert.Equal(left.Type, right.Type);
            Assert.Equal(left.AssetSymbol, right.AssetSymbol);
            Assert.Equal(left.Quantity, right.Quantity);
            Assert.Equal(left.UnitPrice, right.UnitPrice);
            Assert.Equal(left.GrossAmount, right.GrossAmount);
            Assert.Equal(left.Currency, right.Currency);
            Assert.Equal(left.Fee, right.Fee);
            Assert.Equal(left.NaiveOccurredAt, right.NaiveOccurredAt);
            Assert.Equal(left.RowNumber, right.RowNumber);
            Assert.Equal(left.SourceTimeZoneId, right.SourceTimeZoneId);
        }
    }

    [Theory]
    [InlineData(CashOperations)]
    [InlineData(ClosedPositions)]
    [InlineData(OpenPositions)]
    public async Task Reimporting_with_a_profile_discards_everything_imported_before_as_duplicate(string csv)
    {
        // Es la condición que impide que este cambio duplique el histórico: las huellas
        // que produce el perfil tienen que ser las mismas que las ya guardadas.
        var accountId = Guid.NewGuid();
        var before = await ReadWithAdapter(csv);
        var after = ReadWithProfile(csv);

        var stored = before.Records
            .Select(record => ImportFingerprint.For(accountId, PlatformCode.Xtb, record))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            after.Records,
            record => Assert.Contains(ImportFingerprint.For(accountId, PlatformCode.Xtb, record), stored));
    }

    [Fact]
    public void A_closed_positions_file_matches_the_closed_positions_profile_and_not_the_open_one()
    {
        // Las cabeceras de posiciones abiertas son un subconjunto de las de cerradas, así
        // que ambos perfiles encajan. Gana el más específico, que es el correcto.
        var headers = Headers(ClosedPositions);

        var match = ProfileMatching.Match(
            BuiltInProfiles.All(DateTimeOffset.UnixEpoch), PlatformCode.Xtb, headers);

        Assert.Equal("XTB · Posiciones cerradas", match.Profile!.Name);
        Assert.True(match.Ambiguous);
    }

    [Fact]
    public void An_open_positions_file_matches_only_the_open_positions_profile()
    {
        var match = ProfileMatching.Match(
            BuiltInProfiles.All(DateTimeOffset.UnixEpoch), PlatformCode.Xtb, Headers(OpenPositions));

        Assert.Equal("XTB · Posiciones abiertas", match.Profile!.Name);
        Assert.False(match.Ambiguous);
    }

    [Fact]
    public void A_cash_operations_file_matches_its_profile()
    {
        var match = ProfileMatching.Match(
            BuiltInProfiles.All(DateTimeOffset.UnixEpoch), PlatformCode.Xtb, Headers(CashOperations));

        Assert.Equal("XTB · Operaciones de efectivo", match.Profile!.Name);
        Assert.False(match.Ambiguous);
    }

    private static string[] Headers(string csv) =>
        csv.Split('\n')[0].Trim().Split(';');

    private static Task<ImportReadResult> ReadWithAdapter(string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        return new XtbFileImportAdapter(NullLogger<XtbFileImportAdapter>.Instance)
            .ReadAsync(stream, "extracto.csv");
    }

    private static ImportReadResult ReadWithProfile(string csv)
    {
        var profiles = BuiltInProfiles.All(DateTimeOffset.UnixEpoch);
        var match = ProfileMatching.Match(profiles, PlatformCode.Xtb, Headers(csv));

        Assert.True(match.Found);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var content = TabularReader.Read(stream, "extracto.csv", match.Profile!.Current.Delimiter);

        return new ProfileFileImportAdapter().Read(match.Profile, match.Profile.Current, content);
    }
}
