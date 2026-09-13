using Kapea.Domain.Assets;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Lo que se sabe de un activo con posición abierta, antes de compararlo con el resto
/// de la cartera.
/// </summary>
/// <param name="FeesInEuros">Comisiones pagadas por ese activo desde el principio.</param>
/// <param name="RealizedResultInEuros">Resultado ya realizado del activo, de todos los ejercicios.</param>
public sealed record PortfolioAsset(
    AssetClass Class,
    OpenPosition Position,
    Money FeesInEuros,
    Money RealizedResultInEuros);

/// <summary>Una posición abierta ya situada dentro de la cartera.</summary>
/// <param name="Weight">
/// Parte del valor de la cartera que representa. Nula sin precio: el peso de lo que no
/// se sabe cuánto vale no es cero.
/// </param>
public sealed record PortfolioPosition(
    AssetClass Class,
    OpenPosition Position,
    Money FeesInEuros,
    Money RealizedResultInEuros,
    decimal? Weight);

/// <summary>Las posiciones de una clase de activo con sus subtotales.</summary>
public sealed record PortfolioGroup(
    AssetClass Class,
    IReadOnlyList<PortfolioPosition> Positions,
    Money CostInEuros,
    Money MarketValueInEuros,
    decimal? Weight);

/// <summary>
/// Resultado acumulado de la cartera desde el principio, no el de un ejercicio.
/// </summary>
/// <remarks>
/// El realizado suma todas las transmisiones de todos los años, así que no coincide
/// con lo que se declara en un ejercicio concreto aunque salga de los mismos datos.
/// </remarks>
public sealed record AccumulatedResult(Money RealizedInEuros, Money UnrealisedInEuros)
{
    public Money TotalInEuros => RealizedInEuros + UnrealisedInEuros;
}

/// <summary>Rendimientos cobrados de una clase de activo, con su retención.</summary>
public sealed record IncomeByClass(AssetClass Class, Money GrossInEuros, Money WithholdingInEuros)
{
    public Money NetInEuros => GrossInEuros - WithholdingInEuros;
}

/// <summary>
/// El patrimonio: lo que valen las posiciones más el dinero en cuenta.
/// </summary>
/// <remarks>
/// Se da siempre, aunque falte algo, pero diciendo qué falta. Una cifra que se calla
/// que le faltan precios se lee como el patrimonio entero y no lo es.
/// </remarks>
public sealed record Wealth(Money TotalInEuros, bool MissingPrices, bool MissingCash)
{
    public bool IsComplete => !MissingPrices && !MissingCash;
}

/// <summary>
/// La cartera entera: posiciones agrupadas por clase, patrimonio, resultado acumulado
/// y rendimientos cobrados.
/// </summary>
/// <remarks>
/// Se compone en el dominio y no en la consulta para que las mismas cifras salgan
/// igual desde cualquier sitio, y para poder comprobarlas sin base de datos.
///
/// Los pesos se reparten sobre el valor de las posiciones que tienen precio. Repartir
/// sobre el total incluyendo las que no lo tienen daría pesos que no suman cien y que
/// cambiarían solos en cuanto apareciera un precio.
/// </remarks>
public sealed record PortfolioSummary(
    IReadOnlyList<PortfolioGroup> Groups,
    Money CostInEuros,
    Money MarketValueInEuros,
    Wealth Wealth,
    AccumulatedResult Result,
    IReadOnlyList<IncomeByClass> Income,
    bool WeightsArePartial)
{
    public IEnumerable<PortfolioPosition> Positions => Groups.SelectMany(group => group.Positions);

    /// <param name="realizedSinceInception">
    /// Todo lo realizado, incluido lo de activos que ya no se tienen.
    /// </param>
    /// <remarks>
    /// Llega aparte porque no se puede sacar de las posiciones abiertas: un activo
    /// vendido entero no tiene posición, y su ganancia o su pérdida desaparecerían del
    /// acumulado justo cuando se materializan.
    /// </remarks>
    public static PortfolioSummary Build(
        IEnumerable<PortfolioAsset> assets,
        CashTotal cash,
        CashBalances cashBalances,
        IEnumerable<IncomeByClass> income,
        Money realizedSinceInception)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(cash);
        ArgumentNullException.ThrowIfNull(cashBalances);
        ArgumentNullException.ThrowIfNull(income);

        var all = assets.ToList();
        var valued = all.Where(asset => asset.Position.MarketValueInEuros is not null).ToList();
        var missingPrices = valued.Count < all.Count;

        var marketValue = valued.Aggregate(
            Money.Euros(0m), (total, asset) => total + asset.Position.MarketValueInEuros!.Value);

        var groups = all
            .GroupBy(asset => asset.Class)
            .Select(group => Group(group.Key, [.. group], marketValue))
            .OrderBy(group => group.Class)
            .ToList();

        var cost = all.Aggregate(Money.Euros(0m), (total, asset) => total + asset.Position.CostInEuros);

        var unrealised = valued.Aggregate(
            Money.Euros(0m), (total, asset) => total + asset.Position.UnrealisedResultInEuros!.Value);

        return new PortfolioSummary(
            groups,
            cost,
            marketValue,
            new Wealth(
                marketValue + cash.Total,
                missingPrices,
                !cash.IsComplete || !cashBalances.IsComplete),
            new AccumulatedResult(realizedSinceInception, unrealised),
            [.. income.OrderBy(entry => entry.Class)],
            missingPrices);
    }

    private static PortfolioGroup Group(AssetClass assetClass, IReadOnlyList<PortfolioAsset> assets, Money total)
    {
        var positions = assets
            .Select(asset => new PortfolioPosition(
                asset.Class,
                asset.Position,
                asset.FeesInEuros,
                asset.RealizedResultInEuros,
                Weight(asset.Position.MarketValueInEuros, total)))
            .ToList();

        var value = assets.Aggregate(
            Money.Euros(0m),
            (running, asset) => running + (asset.Position.MarketValueInEuros ?? Money.Euros(0m)));

        return new PortfolioGroup(
            assetClass,
            positions,
            assets.Aggregate(Money.Euros(0m), (running, asset) => running + asset.Position.CostInEuros),
            value,
            Weight(value, total));
    }

    private static decimal? Weight(Money? value, Money total) =>
        value is { } amount && total.Amount != 0m ? amount.Amount / total.Amount : null;
}
