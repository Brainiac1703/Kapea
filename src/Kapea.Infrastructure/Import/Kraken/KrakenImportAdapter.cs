using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Import.Kraken;

/// <summary>
/// Adaptador de Kraken: traduce operaciones y apuntes a registros normalizados.
/// </summary>
public sealed class KrakenImportAdapter(KrakenApiClient client, ILogger<KrakenImportAdapter> logger) : IApiImportAdapter
{
    internal const string PlatformTimeZoneId = "UTC";

    /// <summary>Tipos de apunte que ya vienen recogidos en una operación de compraventa.</summary>
    private static readonly HashSet<string> LedgerTypesCoveredByTrades = new(StringComparer.OrdinalIgnoreCase)
    {
        "trade", "margin", "rollover", "settled",
    };

    public Platform Platform => Platform.Kraken;

    public ImportSourceKind SourceKind => ImportSourceKind.RemoteApi;

    public async Task<ImportReadResult> ReadAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var trades = await client.GetTradesAsync(credential, from, to, cancellationToken).ConfigureAwait(false);
        var ledgers = await client.GetLedgersAsync(credential, from, to, cancellationToken).ConfigureAwait(false);

        var records = new List<ImportRecord>();
        var rejected = new List<RejectedRecord>();

        foreach (var trade in trades)
        {
            if (!KrakenSymbols.TrySplitPair(trade.Pair, out var baseAsset, out var quoteAsset))
            {
                rejected.Add(new RejectedRecord(
                    trade.TradeId, null, trade.RawContent,
                    $"El par '{trade.Pair}' no se puede descomponer en activo y contrapartida."));

                continue;
            }

            records.Add(FromTrade(trade, baseAsset, quoteAsset));
        }

        foreach (var entry in ledgers)
        {
            if (LedgerTypesCoveredByTrades.Contains(entry.Type))
            {
                // El apunte de una compraventa ya llega por TradesHistory con su precio;
                // importarlo dos veces duplicaría el movimiento.
                continue;
            }

            records.Add(FromLedger(entry));
        }

        logger.LogInformation(
            "Kraken: {Operaciones} operaciones y {Apuntes} apuntes normalizados, {Rechazados} rechazados.",
            trades.Count, records.Count - trades.Count + rejected.Count, rejected.Count);

        return new ImportReadResult(records, rejected);
    }

    private static ImportRecord FromTrade(KrakenTrade trade, string baseAsset, string quoteAsset)
    {
        var quoteCurrency = KrakenSymbols.QuoteCurrency(quoteAsset);

        return new ImportRecord(
            NaturalId: trade.TradeId,
            RowNumber: null,
            Type: trade.IsBuy ? TransactionType.Buy : TransactionType.Sell,
            AssetSymbol: baseAsset,
            AssetClass: AssetClass.Crypto,
            Quantity: trade.Volume,
            UnitPrice: trade.Price,
            GrossAmount: trade.Cost,
            // Una permuta cripto-cripto no tiene divisa: se valora en euros aguas
            // arriba, y hasta entonces se marca con la contrapartida en euros a cero.
            Currency: quoteCurrency ?? Currency.Euro,
            Fee: trade.Fee,
            Withholding: null,
            OccurredAt: trade.Time,
            NaiveOccurredAt: null,
            SourceTimeZoneId: PlatformTimeZoneId,
            SplitRatio: null,
            RawContent: trade.RawContent);
    }

    private static ImportRecord FromLedger(KrakenLedgerEntry entry)
    {
        var canonical = KrakenSymbols.ToCanonical(entry.Asset);
        var isFiat = KrakenSymbols.IsFiat(entry.Asset);

        return new ImportRecord(
            NaturalId: entry.LedgerId,
            RowNumber: null,
            Type: MapLedgerType(entry),
            AssetSymbol: isFiat ? null : canonical,
            AssetClass: isFiat ? null : AssetClass.Crypto,
            Quantity: isFiat ? 0m : Math.Abs(entry.Amount),
            UnitPrice: null,
            GrossAmount: Math.Abs(entry.Amount),
            Currency: isFiat ? Currency.FromCode(canonical) : Currency.Euro,
            Fee: Math.Abs(entry.Fee),
            Withholding: null,
            OccurredAt: entry.Time,
            NaiveOccurredAt: null,
            SourceTimeZoneId: PlatformTimeZoneId,
            SplitRatio: null,
            RawContent: entry.RawContent);
    }

    private static TransactionType MapLedgerType(KrakenLedgerEntry entry) => entry.Type.ToUpperInvariant() switch
    {
        "DEPOSIT" => TransactionType.Deposit,
        "WITHDRAWAL" => TransactionType.Withdrawal,
        "TRANSFER" => TransactionType.Transfer,
        "STAKING" or "EARN" or "REWARD" => TransactionType.Reward,
        "DIVIDEND" => TransactionType.Dividend,
        "CREDIT" => TransactionType.Interest,
        "SPEND" or "RECEIVE" => TransactionType.Transfer,
        "ADJUSTMENT" => TransactionType.Unknown,
        _ => TransactionType.Unknown,
    };
}
