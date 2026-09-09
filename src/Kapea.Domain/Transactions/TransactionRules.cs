using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Transactions;

/// <summary>
/// Invariantes de un movimiento que se pueden comprobar sin tenerlo construido.
/// </summary>
/// <remarks>
/// Existen aparte para que la fase de preparación de una importación pueda decir si un
/// registro llegará a ser un movimiento válido. Sin esto la vista previa prometía
/// importar registros que después reventaban al confirmar, y el usuario veía un
/// recuento que no se cumplía.
/// </remarks>
public static class TransactionRules
{
    /// <summary>Devuelve el motivo por el que el registro no puede ser un movimiento, o null si puede.</summary>
    public static string? Validate(
        TransactionType type,
        bool hasAsset,
        Quantity quantity,
        Currency currency,
        Currency? unitPriceCurrency,
        Currency feeCurrency,
        decimal fee,
        Currency? withholdingCurrency,
        decimal? withholding)
    {
        if (type is TransactionType.Buy or TransactionType.Sell)
        {
            if (!hasAsset)
            {
                return "Una compra o una venta necesita activo.";
            }

            if (quantity.IsZero)
            {
                return "Una compra o una venta necesita cantidad.";
            }
        }

        if (unitPriceCurrency is { } price && price != currency)
        {
            return $"El precio unitario está en {price.Code} y la operación en {currency.Code}.";
        }

        if (feeCurrency != currency)
        {
            return $"La comisión está en {feeCurrency.Code} y la operación en {currency.Code}.";
        }

        if (fee < 0m)
        {
            return "Una comisión no puede ser negativa; su signo lo decide el cálculo, no el dato.";
        }

        if (withholding is { } amount)
        {
            if (withholdingCurrency is { } retention && retention != currency)
            {
                return $"La retención está en {retention.Code} y la operación en {currency.Code}.";
            }

            if (amount < 0m)
            {
                return "Una retención no puede ser negativa.";
            }
        }

        return null;
    }

    private static void EnsureSameCurrency(Currency? candidate, Currency expected)
    {
        if (candidate is { } currency && currency != expected)
        {
            throw new CurrencyMismatchException(currency, expected, "combinar");
        }
    }

    /// <summary>Lanza si el movimiento no cumple sus invariantes. Es la vía que usa la entidad.</summary>
    internal static void Ensure(
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Money? withholdingTax)
    {
        // Las divisas se comprueban aquí y no a través de Validate para conservar el tipo
        // de excepción: quien captura una discrepancia de divisa necesita distinguirla de
        // cualquier otra violación de invariante.
        EnsureSameCurrency(unitPrice?.Currency, grossAmount.Currency);
        EnsureSameCurrency(fee.Currency, grossAmount.Currency);
        EnsureSameCurrency(withholdingTax?.Currency, grossAmount.Currency);

        var reason = Validate(
            type,
            assetId is not null,
            quantity,
            grossAmount.Currency,
            unitPrice?.Currency,
            fee.Currency,
            fee.Amount,
            withholdingTax?.Currency,
            withholdingTax?.Amount);

        if (reason is not null)
        {
            throw new DomainException(reason);
        }
    }
}
