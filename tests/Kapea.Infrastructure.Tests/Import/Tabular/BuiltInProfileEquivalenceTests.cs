using System.Text;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Tabular;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

/// <summary>
/// Fija lo que los perfiles de serie tienen que producir a partir de cada informe de
/// xStation5.
/// </summary>
/// <remarks>
/// Los valores están escritos a mano y no calculados: son los mismos que producía el
/// adaptador que estos perfiles sustituyen, y se comprobaron uno a uno antes de
/// retirarlo. Escribirlos aquí es lo que impide que una corrección posterior del perfil
/// cambie en silencio la interpretación del histórico.
///
/// La huella de deduplicación se calcula sobre estos campos. Cualquier diferencia haría
/// que lo ya importado dejara de reconocerse y entrara por segunda vez.
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

    [Fact]
    public void A_cash_operations_export_is_normalised()
    {
        var records = Read(CashOperations).Records;

        Assert.Equal(3, records.Count);

        // Sin volumen no puede ser una compra: la operación entera la trae el informe de
        // posiciones, y clasificarla aquí la duplicaría con un lote de cero unidades. El
        // perfil no traduce ese concepto, así que entra sin clasificar.
        Assert.Equal(TransactionType.Unknown, records[0].Type);
        Assert.Equal(1234.56m, records[0].GrossAmount);
        Assert.Equal("SAN.ES", records[0].AssetSymbol);
        Assert.Equal(new DateTime(2024, 1, 10, 9, 30, 0), records[0].NaiveOccurredAt);

        Assert.Equal(TransactionType.Dividend, records[1].Type);
        Assert.Equal(45.20m, records[1].GrossAmount);

        Assert.Equal(TransactionType.Interest, records[2].Type);
        Assert.Equal(1.15m, records[2].GrossAmount);
    }

    [Fact]
    public void A_closed_position_becomes_a_buy_and_a_sell_with_their_own_dates()
    {
        var records = Read(ClosedPositions).Records;

        Assert.Equal(2, records.Count);

        var buy = records[0];
        var sell = records[1];

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal("AAPL.US", buy.AssetSymbol);
        Assert.Equal(10m, buy.Quantity);
        Assert.Equal(180.50m, buy.UnitPrice);
        Assert.Equal(1805m, buy.GrossAmount);
        Assert.Equal(2.50m, buy.Fee);
        Assert.Equal(new DateTime(2024, 2, 5, 15, 30, 0), buy.NaiveOccurredAt);

        Assert.Equal(TransactionType.Sell, sell.Type);
        Assert.Equal(2257.5m, sell.GrossAmount);
        Assert.Equal(0m, sell.Fee);
        Assert.Equal(new DateTime(2025, 9, 20, 16, 0, 0), sell.NaiveOccurredAt);
        Assert.Equal("USD", sell.Currency.Code);

        // Con el mismo identificador, la venta se descartaría como duplicado de la compra.
        Assert.NotEqual(buy.NaturalId, sell.NaturalId);
    }

    [Fact]
    public void An_open_position_becomes_only_its_acquisition()
    {
        // El informe de cerradas nunca deja nada abierto, así que sin este formato un
        // usuario de XTB no tendría ninguna posición en la cartera.
        var buy = Assert.Single(Read(OpenPositions).Records);

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal("SAN.ES", buy.AssetSymbol);
        Assert.Equal(50m, buy.Quantity);
        Assert.Equal(4.10m, buy.UnitPrice);
        Assert.Equal(205m, buy.GrossAmount);
        Assert.Equal(1m, buy.Fee);
        Assert.Equal(new DateTime(2025, 4, 1, 9, 0, 0), buy.NaiveOccurredAt);
    }

    [Fact]
    public void The_market_price_of_an_open_position_is_not_imported()
    {
        // Es el precio del momento en que se exportó el informe. El valor de hoy lo
        // resuelve el proveedor de precios, y guardar aquel lo dejaría congelado.
        var buy = Assert.Single(Read(OpenPositions).Records);

        Assert.NotEqual(4.55m, buy.UnitPrice);
    }

    [Theory]
    [InlineData(CashOperations, "XTB · Operaciones de efectivo", false)]
    [InlineData(ClosedPositions, "XTB · Posiciones cerradas", true)]
    [InlineData(OpenPositions, "XTB · Posiciones abiertas", false)]
    public void Each_report_finds_its_profile(string csv, string expected, bool ambiguous)
    {
        // Las cabeceras de posiciones abiertas son un subconjunto de las de cerradas, así
        // que con un fichero de cerradas encajan las dos. Gana el más específico.
        var match = ProfileMatching.Match(
            BuiltInProfiles.All(DateTimeOffset.UnixEpoch), PlatformCode.Xtb, Headers(csv));

        Assert.Equal(expected, match.Profile!.Name);
        Assert.Equal(ambiguous, match.Ambiguous);
    }

    [Fact]
    public void A_file_no_profile_recognises_finds_nothing()
    {
        var match = ProfileMatching.Match(
            BuiltInProfiles.All(DateTimeOffset.UnixEpoch), PlatformCode.Xtb, ["Fecha", "Concepto", "Saldo"]);

        Assert.False(match.Found);
    }

    private static string[] Headers(string csv) => csv.Split('\n')[0].Trim().Split(';');

    private static ImportReadResult Read(string csv)
    {
        var profiles = BuiltInProfiles.All(DateTimeOffset.UnixEpoch);
        var match = ProfileMatching.Match(profiles, PlatformCode.Xtb, Headers(csv));

        Assert.True(match.Found);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var content = TabularReader.Read(stream, "extracto.csv", match.Profile!.Current.Delimiter);

        return new ProfileFileImportAdapter().Read(match.Profile, match.Profile.Current, content);
    }
}
