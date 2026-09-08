using Kapea.Domain.Calculation;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

/// <summary>
/// Casos fiscales de referencia con cifras calculadas a mano. Son la red de seguridad
/// del núcleo: cualquier cambio en el motor que altere una de estas cifras rompe aquí
/// antes de llegar a una declaración.
/// </summary>
/// <remarks>
/// Los importes llegan ya convertidos a euros, como los recibe el motor. En los casos
/// en divisa se documenta el tipo aplicado para que la cifra sea reconstruible a mano.
/// </remarks>
public class TaxReferenceCasesTests
{
    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();

        foreach (var name in Cases.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Reference_case_matches_the_hand_calculated_figures(string name)
    {
        var reference = Cases[name];
        var result = reference.Calculate();

        Assert.Empty(result.Inconsistencies);

        Assert.Equal(
            Money.Euros(reference.ExpectedProceeds),
            Total(result.RealizedResults.Select(r => r.ProceedsInEuros)));

        Assert.Equal(
            Money.Euros(reference.ExpectedAcquisitionCost),
            Total(result.RealizedResults.Select(r => r.AcquisitionCostInEuros)));

        Assert.Equal(
            Money.Euros(reference.ExpectedRealisedResult),
            Total(result.RealizedResults.Select(r => r.ResultInEuros)));

        Assert.Equal(
            reference.ExpectedOpenQuantity,
            result.OpenLots.Aggregate(Quantity.Zero, (total, lot) => total + lot.RemainingQuantity).Value);

        Assert.Equal(
            Money.Euros(reference.ExpectedOpenCost),
            Total(result.OpenLots.Select(lot => lot.RemainingCost)));

        Assert.Equal(
            Money.Euros(reference.ExpectedNetIncome),
            Total(result.CapitalIncomes.Select(income => income.NetAmountInEuros)));
    }

    [Fact]
    public void Every_disposal_breaks_down_into_lots_that_add_up_to_its_own_figures()
    {
        foreach (var (name, reference) in Cases)
        {
            foreach (var realized in reference.Calculate().RealizedResults)
            {
                Assert.Equal(realized.ProceedsInEuros, Total(realized.ConsumedLots.Select(lot => lot.ProceedsInEuros)));
                Assert.Equal(realized.AcquisitionCostInEuros, Total(realized.ConsumedLots.Select(lot => lot.AcquisitionCostInEuros)));
                Assert.Equal(
                    realized.Quantity.Value,
                    realized.ConsumedLots.Aggregate(Quantity.Zero, (total, lot) => total + lot.Quantity).Value);
                Assert.True(realized.ConsumedLots.Count > 0, $"El caso '{name}' emite un resultado sin lotes.");
            }
        }
    }

    private static readonly Dictionary<string, TaxReferenceCase> Cases = new()
    {
        // Permuta cripto-cripto: se vende 0,5 BTC (coste 10.000 de un lote de 1 BTC a
        // 20.000) por un valor en euros de 15.000 en la fecha. Ganancia 5.000.
        // La pata de compra del otro activo va en su propia cola FIFO (caso siguiente).
        ["Permuta cripto-cripto: pata de salida"] = new(
            () => new Ledger()
                .Buy("2023-03-01", quantity: 1m, grossEuros: 20000m)
                .Sell("2025-04-10", quantity: 0.5m, grossEuros: 15000m)
                .Calculate(),
            ExpectedProceeds: 15000m,
            ExpectedAcquisitionCost: 10000m,
            ExpectedRealisedResult: 5000m,
            ExpectedOpenQuantity: 0.5m,
            ExpectedOpenCost: 10000m,
            ExpectedNetIncome: 0m),

        // La pata de entrada de la misma permuta: 4 ETH adquiridos por los 15.000 de
        // valoración. Ese importe es su coste de adquisición, no cero.
        ["Permuta cripto-cripto: pata de entrada"] = new(
            () => new Ledger()
                .Buy("2025-04-10", quantity: 4m, grossEuros: 15000m)
                .Calculate(),
            ExpectedProceeds: 0m,
            ExpectedAcquisitionCost: 0m,
            ExpectedRealisedResult: 0m,
            ExpectedOpenQuantity: 4m,
            ExpectedOpenCost: 15000m,
            ExpectedNetIncome: 0m),

        // Compra en USD: 2.200 USD a 1,10 USD/EUR = 2.000 €, comisión 11 USD = 10 €.
        // Coste del lote 2.010 €. Venta 2.750 USD a 1,25 = 2.200 €, comisión 12,50 USD
        // = 10 €. Importe de transmisión 2.190 €. Resultado 180 €.
        ["Compra en USD y venta con otro tipo de cambio"] = new(
            () => new Ledger()
                .Buy("2024-02-01", quantity: 10m, grossEuros: 2000m, feeEuros: 10m)
                .Sell("2025-09-15", quantity: 10m, grossEuros: 2200m, feeEuros: 10m)
                .Calculate(),
            ExpectedProceeds: 2190m,
            ExpectedAcquisitionCost: 2010m,
            ExpectedRealisedResult: 180m,
            ExpectedOpenQuantity: 0m,
            ExpectedOpenCost: 0m,
            ExpectedNetIncome: 0m),

        // Venta que barre tres lotes con comisión: lotes de 10 a 1.000, 1.500 y 2.000.
        // Se venden 25 por 5.000 con 25 de comisión: 4.975 netos.
        // Coste 1.000 + 1.500 + 1.000 (medio lote) = 3.500. Resultado 1.475.
        ["Venta que barre tres lotes con comisión"] = new(
            () => new Ledger()
                .Buy("2023-01-10", quantity: 10m, grossEuros: 1000m)
                .Buy("2023-07-10", quantity: 10m, grossEuros: 1500m)
                .Buy("2024-01-10", quantity: 10m, grossEuros: 2000m)
                .Sell("2025-05-20", quantity: 25m, grossEuros: 5000m, feeEuros: 25m)
                .Calculate(),
            ExpectedProceeds: 4975m,
            ExpectedAcquisitionCost: 3500m,
            ExpectedRealisedResult: 1475m,
            ExpectedOpenQuantity: 5m,
            ExpectedOpenCost: 1000m,
            ExpectedNetIncome: 0m),

        // Split retroactivo: venta de 4 sobre un lote de 10 a 1.000 (coste 400, importe
        // 600, resultado 200) y después split 2:1. La venta no se recalcula; las 6
        // unidades restantes pasan a 12 conservando los 600 de coste.
        ["Split posterior a una venta"] = new(
            () => new Ledger()
                .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
                .Sell("2024-03-01", quantity: 4m, grossEuros: 600m)
                .Split("2024-06-01", ratio: 2m)
                .Calculate(),
            ExpectedProceeds: 600m,
            ExpectedAcquisitionCost: 400m,
            ExpectedRealisedResult: 200m,
            ExpectedOpenQuantity: 12m,
            ExpectedOpenCost: 600m,
            ExpectedNetIncome: 0m),

        // Dividendo con retención del 19 %: 50 brutos, 9,50 retenidos, 40,50 netos.
        // Los lotes no se tocan: cobrar un dividendo no cambia lo que costó la acción.
        ["Dividendo con retención"] = new(
            () => new Ledger()
                .Buy("2024-01-10", quantity: 100m, grossEuros: 400m)
                .Dividend("2025-06-01", grossEuros: 50m, withholdingEuros: 9.5m)
                .Calculate(),
            ExpectedProceeds: 0m,
            ExpectedAcquisitionCost: 0m,
            ExpectedRealisedResult: 0m,
            ExpectedOpenQuantity: 100m,
            ExpectedOpenCost: 400m,
            ExpectedNetIncome: 40.5m),
    };

    private static Money Total(IEnumerable<Money> amounts) =>
        amounts.Aggregate(Money.Euros(0m), (total, amount) => total + amount);

    internal sealed record TaxReferenceCase(
        Func<AssetCalculationResult> Calculate,
        decimal ExpectedProceeds,
        decimal ExpectedAcquisitionCost,
        decimal ExpectedRealisedResult,
        decimal ExpectedOpenQuantity,
        decimal ExpectedOpenCost,
        decimal ExpectedNetIncome);
}
