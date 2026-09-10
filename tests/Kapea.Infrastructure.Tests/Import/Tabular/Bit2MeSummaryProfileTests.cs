using System.Text;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Tabular;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

/// <summary>
/// Comprueba el perfil del resumen de movimientos que exporta Bit2Me desde su web.
/// </summary>
/// <remarks>
/// Existe porque su API no devuelve todo lo que ese fichero trae: las ventas de cripto a
/// euros no aparecen por ninguna de sus rutas de histórico. Las filas de este fichero de
/// prueba son casos reales, con los identificadores cambiados.
/// </remarks>
public class Bit2MeSummaryProfileTests
{
    [Fact]
    public void Paying_with_euros_is_a_purchase_of_what_arrives()
    {
        var record = Leer().Records.Single(r => r.NaturalId == "op-0001");

        Assert.Equal(TransactionType.Buy, record.Type);
        Assert.Equal("XRP", record.AssetSymbol);
        Assert.Equal(49.591735m, record.Quantity);
        Assert.Equal(100m, record.GrossAmount);
        Assert.Equal("EUR", record.Currency.Code);
        Assert.Equal(1.99m, record.Fee);
    }

    [Fact]
    public void Receiving_euros_is_a_sale_of_what_leaves()
    {
        // Es la operación que la API no devuelve por ninguna vía, y la razón de que se
        // pueda importar este fichero.
        var record = Leer().Records.Single(r => r.NaturalId == "op-0004");

        Assert.Equal(TransactionType.Sell, record.Type);
        Assert.Equal("EURC", record.AssetSymbol);
        Assert.Equal(99.0595m, record.Quantity);
        Assert.Equal(98.10821983m, record.GrossAmount);
    }

    [Fact]
    public void Swapping_one_crypto_for_another_is_a_sale_and_a_purchase()
    {
        var records = Leer().Records.Where(r => r.NaturalId!.StartsWith("op-0005", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, records.Count);

        var venta = records.Single(r => r.Type == TransactionType.Sell);
        var compra = records.Single(r => r.Type == TransactionType.Buy);

        Assert.Equal("POL", venta.AssetSymbol);
        Assert.Equal(254.54242979m, venta.Quantity);
        Assert.Equal("ETH", compra.AssetSymbol);
        Assert.Equal(0.01234836m, compra.Quantity);

        // Sin euros por medio no hay importe que leer, y no se inventa ninguno.
        Assert.Equal(0m, venta.GrossAmount);
        Assert.NotEqual(venta.NaturalId, compra.NaturalId);
    }

    [Fact]
    public void A_staking_reward_is_income_and_not_a_purchase()
    {
        // El par de monedas diría «compra» porque solo entra algo. Lo que lo distingue es
        // el concepto, y confundirlos inventaría un coste que nadie pagó.
        var record = Leer().Records.Single(r => r.NaturalId == "op-0003");

        Assert.Equal(TransactionType.Reward, record.Type);
        Assert.Equal("B2M", record.AssetSymbol);
        Assert.Equal(0.79504755m, record.Quantity);
        Assert.Equal(0m, record.GrossAmount);
    }

    [Fact]
    public void A_deposit_of_euros_moves_no_asset()
    {
        var record = Leer().Records.Single(r => r.NaturalId == "op-0002");

        Assert.Equal(TransactionType.Deposit, record.Type);
        Assert.Null(record.AssetSymbol);
        Assert.Equal(100m, record.GrossAmount);
    }

    [Fact]
    public void The_file_is_recognised_by_its_own_profile()
    {
        var cabeceras = File.ReadLines(Fichero).First().Split(',');

        var match = ProfileMatching.Match(
            BuiltInProfiles.All(DateTimeOffset.UnixEpoch), PlatformCode.Bit2Me, cabeceras);

        Assert.Equal("Bit2Me · Resumen de movimientos", match.Profile!.Name);
    }

    private static string Fichero =>
        Path.Combine(AppContext.BaseDirectory, "Import", "Tabular", "Recorded", "bit2me-summary.csv");

    private static ImportReadResult Leer()
    {
        var perfil = BuiltInProfiles.All(DateTimeOffset.UnixEpoch)
            .Single(p => p.Name == "Bit2Me · Resumen de movimientos");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(Fichero)));
        var content = TabularReader.Read(stream, "summary.csv", perfil.Current.Delimiter);

        return new ProfileFileImportAdapter().Read(perfil, perfil.Current, content);
    }
}
