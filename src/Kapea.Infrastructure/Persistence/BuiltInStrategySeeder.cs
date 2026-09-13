using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Da de alta un sistema de partida para quien entra por primera vez.
/// </summary>
/// <remarks>
/// No es una recomendación: es el ejemplo más conocido y más simple de sistema seguidor
/// de tendencia, y está para tener algo que simular desde el primer minuto y para que se
/// vea cómo se escribe una regla.
///
/// Se reconoce por el nombre, igual que los perfiles de importación: si el usuario lo
/// corrige, el siguiente arranque no lo pisa ni lo duplica.
/// </remarks>
public static class BuiltInStrategySeeder
{
    internal const string Name = "Cruce de medias con filtro de tendencia";

    public static async Task EnsureAsync(
        KapeaDbContext context,
        UserId userId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (await context.Strategies.AnyAsync(strategy => strategy.Name == Name, cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        context.Strategies.Add(Strategy.Create(
            userId,
            Name,
            number => StrategyVersion.Create(
                number,
                timeProvider.GetUtcNow(),

                // Entra cuando el precio cruza al alza su media de cincuenta días y
                // además está por encima de la de doscientos. El filtro de doscientos es
                // lo que evita comprar rebotes dentro de una tendencia bajista.
                Condition.All(
                    Condition.When(
                        Term.Close, Comparison.CrossesAbove, Term.Indicator(Operand.SimpleMovingAverage, 50)),
                    Condition.When(
                        Term.Close, Comparison.GreaterThan, Term.Indicator(Operand.SimpleMovingAverage, 200))),

                // Sale cuando pierde la media de cincuenta, además de por su nivel de
                // salida y su objetivo.
                exit: Condition.When(
                    Term.Close, Comparison.CrossesBelow, Term.Indicator(Operand.SimpleMovingAverage, 50)),

                // La salida a dos veces lo que el activo se mueve en un día, y el
                // objetivo al doble de esa distancia.
                stopLoss: new Level(LevelKind.RangeMultiple, 2m),
                target: new Level(LevelKind.RiskMultiple, 2m)),
            "Sistema de partida, para tener algo que simular. Entra cuando el precio cruza al alza su media de "
            + "cincuenta días estando por encima de la de doscientos, y sale al perder la de cincuenta."));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
