using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kapea.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones de los tipos de valor del dominio a columnas.
/// </summary>
/// <remarks>
/// Las precisiones no son decorativas: 18 decimales en cantidades cubren un token
/// ERC-20 completo y 8 en importes dejan margen para que el redondeo a céntimos
/// ocurra solo al presentar. Ninguna columna monetaria puede ser de coma flotante.
/// </remarks>
public static class ValueObjectConverters
{
    public const int QuantityPrecision = 38;
    public const int QuantityScale = 18;
    public const int MoneyPrecision = 28;
    public const int MoneyScale = 8;
    public const int RatePrecision = 28;
    public const int RateScale = 12;

    public sealed class CurrencyConverter()
        : ValueConverter<Currency, string>(currency => currency.Code, code => Currency.FromCode(code));

    public sealed class QuantityConverter()
        : ValueConverter<Quantity, decimal>(quantity => quantity.Value, value => new Quantity(value));

    public sealed class UserIdConverter()
        : ValueConverter<UserId, Guid>(userId => userId.Value, value => new UserId(value));

    public sealed class DateOnlyConverter()
        : ValueConverter<DateOnly, DateTime>(
            date => date.ToDateTime(TimeOnly.MinValue),
            value => DateOnly.FromDateTime(value));
}
