using Kapea.Application.Import;
using Kapea.Domain.Assets;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Tests.Import;

internal static class ImportRecords
{
    internal static ImportRecord Buy(
        string? naturalId = null,
        int? rowNumber = null,
        decimal quantity = 10m,
        decimal grossAmount = 1000m,
        decimal fee = 0m,
        string date = "2024-01-10",
        string symbol = "BTC") =>
        new(
            naturalId,
            rowNumber,
            TransactionType.Buy,
            symbol,
            AssetClass.Crypto,
            quantity,
            grossAmount / quantity,
            grossAmount,
            Currency.Euro,
            fee,
            null,
            null,
            DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            "Europe/Madrid",
            null,
            $"raw:{naturalId ?? rowNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
}
