using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>Dinero disponible en una cuenta y una divisa.</summary>
public sealed record CashBalance(Guid AccountId, Currency Currency, Money Amount);

/// <summary>
/// Los saldos de efectivo y lo que ha quedado fuera de ellos.
/// </summary>
/// <param name="Balances">Un saldo por cada cuenta y divisa con movimientos.</param>
/// <param name="UnresolvedMovements">Movimientos que el cálculo no ha podido tratar.</param>
public sealed record CashBalances(IReadOnlyList<CashBalance> Balances, int UnresolvedMovements)
{
    /// <summary>
    /// El saldo está completo cuando ningún movimiento se ha quedado fuera.
    /// </summary>
    /// <remarks>
    /// Un saldo incompleto se enseña igual, pero diciéndolo. Una cifra que parece el
    /// dinero que hay y le faltan movimientos es peor que no dar ninguna.
    /// </remarks>
    public bool IsComplete => UnresolvedMovements == 0;
}

/// <summary>Total del efectivo en euros y lo que no se ha podido convertir.</summary>
/// <param name="Total">Suma en euros de las divisas con tipo disponible.</param>
/// <param name="CurrenciesWithoutRate">Divisas dejadas fuera por no tener tipo.</param>
public sealed record CashTotal(Money Total, IReadOnlyList<Currency> CurrenciesWithoutRate)
{
    public bool IsComplete => CurrenciesWithoutRate.Count == 0;
}

/// <summary>
/// Suma el efectivo por cuenta y divisa a partir del efecto en caja de cada movimiento.
/// </summary>
/// <remarks>
/// Las divisas no se suman entre sí en ningún paso intermedio: cada una lleva su
/// saldo hasta el final y solo el total se convierte, al tipo del día. Convertir antes
/// dejaría el saldo de una cuenta en dólares expresado en euros de una fecha que no
/// significa nada.
/// </remarks>
public static class CashBalanceCalculator
{
    public static CashBalances Calculate(IEnumerable<ValuedTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var byAccountAndCurrency = new Dictionary<(Guid Account, Currency Currency), Money>();
        var unresolved = 0;

        foreach (var valued in transactions)
        {
            // Un movimiento sin clasificar o con un traspaso sin confirmar no entra: si
            // resulta ser una venta mueve el saldo, y si resulta ser un traspaso no.
            if (valued.IsUnresolved)
            {
                unresolved++;

                continue;
            }

            var effect = CashEffect.Of(valued.Transaction);

            if (effect is not { } amount)
            {
                unresolved++;

                continue;
            }

            var key = (valued.Transaction.AccountId, amount.Currency);

            byAccountAndCurrency[key] = byAccountAndCurrency.TryGetValue(key, out var running)
                ? running + amount
                : amount;
        }

        var balances = byAccountAndCurrency
            .Select(entry => new CashBalance(entry.Key.Account, entry.Key.Currency, entry.Value))
            .OrderBy(balance => balance.AccountId)
            .ThenBy(balance => balance.Currency.Code, StringComparer.Ordinal)
            .ToList();

        return new CashBalances(balances, unresolved);
    }

    /// <summary>
    /// Convierte los saldos a euros al tipo del día y deja fuera lo que no se puede
    /// convertir.
    /// </summary>
    /// <remarks>
    /// Una divisa sin tipo no se cuenta como cero ni se convierte a ojo: se nombra, y
    /// el total queda marcado como incompleto.
    /// </remarks>
    public static CashTotal InEuros(CashBalances balances, IReadOnlyDictionary<Currency, ExchangeRate> rates)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(rates);

        var total = Money.Euros(0m);
        var missing = new List<Currency>();

        foreach (var balance in balances.Balances)
        {
            if (balance.Currency.IsEuro)
            {
                total += balance.Amount;

                continue;
            }

            if (rates.TryGetValue(balance.Currency, out var rate))
            {
                total += rate.ToEuros(balance.Amount);

                continue;
            }

            if (!missing.Contains(balance.Currency))
            {
                missing.Add(balance.Currency);
            }
        }

        return new CashTotal(total, missing);
    }
}
