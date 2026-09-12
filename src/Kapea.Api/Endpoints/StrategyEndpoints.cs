using Kapea.Application.Abstractions;
using Kapea.Application.Strategies;
using Kapea.Domain.Common;
using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Api.Endpoints;

/// <summary>
/// Sistemas de especulación, señales y simulaciones.
/// </summary>
/// <remarks>
/// Kapea evalúa las reglas que el usuario ha escrito. No recomienda comprar ni vender, y
/// la pantalla lo dice: lo que viaja por aquí es el resultado de aplicar unas reglas, no
/// un consejo.
/// </remarks>
internal static class StrategyEndpoints
{
    /// <summary>Cuántos días atrás se consideran vigentes las señales.</summary>
    private const int RecentSignalDays = 30;

    internal static void MapStrategies(this RouteGroupBuilder api)
    {
        var strategies = api.MapGroup("/strategies");

        strategies.MapGet("/", async (
            IStrategyRepository repository,
            KapeaDbContext context,
            ICurrentUser user,
            TimeProvider time,
            CancellationToken token) =>
        {
            // El sistema de partida se da de alta si falta, por lo mismo que los perfiles
            // de importación: sin ninguno, la pantalla no enseñaría cómo se escribe uno.
            await BuiltInStrategySeeder.EnsureAsync(context, user.Id, time, token);

            var all = await repository.ListAsync(token);

            return all.Select(StrategyMapping.ToResponse).ToList();
        });

        strategies.MapGet("/vocabulary", () => Vocabulary());

        strategies.MapPost("/", async (
            CreateStrategyRequest request,
            IStrategyRepository repository,
            ICurrentUser user,
            TimeProvider time,
            CancellationToken token) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            var strategy = Strategy.Create(
                user.Id,
                request.Name,
                number => StrategyMapping.ToDomain(request.Rules, number, time.GetUtcNow()),
                request.Description);

            await repository.AddAsync(strategy, token);
            await repository.SaveChangesAsync(token);

            return Results.Created($"/api/strategies/{strategy.Id}", StrategyMapping.ToResponse(strategy));
        });

        strategies.MapPost("/{strategyId:guid}/versions", async (
            Guid strategyId,
            ReviseStrategyRequest request,
            IStrategyRepository repository,
            TimeProvider time,
            CancellationToken token) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            if (await repository.FindAsync(strategyId, token) is not { } strategy)
            {
                return Results.NotFound();
            }

            strategy.Revise(
                number => StrategyMapping.ToDomain(request.Rules, number, time.GetUtcNow()),
                request.Name,
                request.Description);

            await repository.SaveChangesAsync(token);

            return Results.Ok(StrategyMapping.ToResponse(strategy));
        });

        strategies.MapPost("/run", async (StrategyService service, CancellationToken token) =>
        {
            var run = await service.RunAsync(token);

            return new SignalRunResponse(run.Assets, run.Emitted, run.New);
        });

        strategies.MapGet("/signals", async (
            int? days,
            IStrategyRepository repository,
            KapeaDbContext context,
            TimeProvider time,
            CancellationToken token) =>
        {
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var signals = await repository.ListSignalsAsync(today.AddDays(-(days ?? RecentSignalDays)), token);

            var names = await context.Strategies
                .ToDictionaryAsync(strategy => strategy.Id, strategy => strategy.Name, token);

            var symbols = await context.Assets
                .ToDictionaryAsync(asset => asset.Id, asset => asset.CanonicalSymbol, token);

            return signals
                .Select(signal => new SignalResponse(
                    signal.Id,
                    signal.StrategyId,
                    names.GetValueOrDefault(signal.StrategyId, string.Empty),
                    signal.StrategyVersion,
                    signal.AssetId,
                    symbols.GetValueOrDefault(signal.AssetId, string.Empty),
                    signal.Date,
                    signal.Direction.ToString(),
                    signal.PriceInEuros.Amount,
                    signal.Reason,
                    signal.Target?.Amount,
                    signal.StopLoss?.Amount,
                    today.DayNumber - signal.Date.DayNumber))
                .ToList();
        });

        strategies.MapPost("/{strategyId:guid}/simulate", async (
            Guid strategyId,
            Guid assetId,
            decimal? capital,
            int? version,
            StrategyService service,
            IStrategyRepository repository,
            IStrategyDataSource data,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            var money = Money.Euros(capital is > 0m ? capital.Value : 1000m);
            var result = await service.SimulateAsync(strategyId, assetId, money, version, token);

            if (result is null)
            {
                return Results.NotFound();
            }

            var strategy = await repository.FindAsync(strategyId, token)
                ?? throw new DomainException("El sistema ha desaparecido a mitad de la simulación.");

            var symbol = await context.Assets
                .Where(asset => asset.Id == assetId)
                .Select(asset => asset.CanonicalSymbol)
                .FirstOrDefaultAsync(token) ?? string.Empty;

            var cost = await data.CostAsync(token);

            return Results.Ok(new BacktestResponse(
                strategyId,
                strategy.Name,
                version ?? strategy.CurrentVersion,
                assetId,
                symbol,
                money.Amount,
                result.Result.Amount,
                result.ResultAfterTax.Amount,
                result.Fees.Amount,
                result.Tax.Amount,
                result.BuyAndHold.Amount,
                result.MaximumDrawdown,
                result.Trades.Count,
                result.Closed,
                result.Winners,
                cost.RateInsidePrice,
                [
                    .. result.Trades.Select(trade => new SimulatedTradeResponse(
                        trade.EntryDate,
                        trade.EntryPrice.Amount,
                        trade.ExitDate,
                        trade.ExitPrice?.Amount,
                        trade.Quantity.Value,
                        trade.Result.Amount,
                        trade.Fees.Amount,
                        trade.EntryReason,
                        trade.ExitReason)),
                ]));
        });
    }

    /// <summary>
    /// Lo que una regla puede nombrar.
    /// </summary>
    /// <remarks>
    /// Lo da el servidor y no lo trae escrito el cliente: añadir un indicador es tocar un
    /// sitio, y ninguna pantalla debería necesitar una versión nueva para verlo.
    /// </remarks>
    private static StrategyVocabularyResponse Vocabulary() => new(
        [
            new OperandResponse(nameof(Operand.Price), "Precio de cierre", false),
            new OperandResponse(nameof(Operand.SimpleMovingAverage), "Media móvil simple", true),
            new OperandResponse(nameof(Operand.ExponentialMovingAverage), "Media móvil exponencial", true),
            new OperandResponse(nameof(Operand.RelativeStrengthIndex), "Fuerza relativa", true),
            new OperandResponse(nameof(Operand.MacdLine), "MACD, línea", true),
            new OperandResponse(nameof(Operand.MacdSignal), "MACD, señal", true),
            new OperandResponse(nameof(Operand.MacdDistance), "MACD, distancia a su señal", true),
            new OperandResponse(nameof(Operand.BollingerUpper), "Banda superior", true),
            new OperandResponse(nameof(Operand.BollingerMiddle), "Banda central", true),
            new OperandResponse(nameof(Operand.BollingerLower), "Banda inferior", true),
            new OperandResponse(nameof(Operand.AverageDailyRange), "Recorrido medio diario", true),
            new OperandResponse(nameof(Operand.Constant), "Un número fijo", false),
        ],
        [
            new NamedValueResponse(nameof(Comparison.GreaterThan), "por encima de"),
            new NamedValueResponse(nameof(Comparison.LessThan), "por debajo de"),
            new NamedValueResponse(nameof(Comparison.CrossesAbove), "cruza al alza"),
            new NamedValueResponse(nameof(Comparison.CrossesBelow), "cruza a la baja"),
        ],
        [
            new NamedValueResponse(nameof(Junction.Comparison), "una comparación"),
            new NamedValueResponse(nameof(Junction.All), "todas"),
            new NamedValueResponse(nameof(Junction.Any), "cualquiera"),
        ],
        [
            new NamedValueResponse(nameof(LevelKind.Percentage), "porcentaje sobre la entrada"),
            new NamedValueResponse(nameof(LevelKind.RangeMultiple), "múltiplo del recorrido diario"),
            new NamedValueResponse(nameof(LevelKind.RiskMultiple), "múltiplo del riesgo"),
        ]);
}
