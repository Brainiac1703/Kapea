using System.Net;
using Kapea.Application.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Import.Kraken;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Import.Kraken;

public class KrakenImportAdapterTests
{
    private static readonly ApiCredential Credential = new("clave", Convert.ToBase64String([1, 2, 3, 4]));
    private static readonly DateTimeOffset From = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_paginated_history_is_walked_to_the_end()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("trades-page-1.json"))
            .RespondWithFile(Recorded("trades-page-2.json"))
            .RespondWithFile(Recorded("empty-ledgers.json"));

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        // Kraken declara count 3 y sirve dos páginas: la paginación no puede pararse
        // en la primera ni pedir una tercera que no existe.
        Assert.Equal(3, result.Records.Count);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task A_trade_becomes_a_buy_with_its_price_cost_and_fee()
    {
        var result = await ReadFullHistory();
        var buy = result.Records.Single(record => record.NaturalId == "TCWJEG-FL4SZ-3FKGH6");

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal("BTC", buy.AssetSymbol);
        Assert.Equal(0.1m, buy.Quantity);
        Assert.Equal(38000m, buy.UnitPrice);
        Assert.Equal(3800m, buy.GrossAmount);
        Assert.Equal(6.08m, buy.Fee);
        Assert.Equal(Currency.Euro, buy.Currency);
        Assert.Equal(new DateTimeOffset(2024, 1, 10, 10, 50, 0, TimeSpan.Zero).AddMilliseconds(123), buy.OccurredAt);
    }

    [Fact]
    public async Task A_crypto_to_crypto_pair_keeps_the_asset_and_its_counterparty()
    {
        var result = await ReadFullHistory();
        var swap = result.Records.Single(record => record.NaturalId == "TCWJEG-FL4SZ-3FKGH7");

        Assert.Equal(TransactionType.Sell, swap.Type);
        Assert.Equal("ETH", swap.AssetSymbol);
        Assert.Equal(2m, swap.Quantity);
        Assert.Equal(0.053m, swap.UnitPrice);
    }

    [Fact]
    public async Task Ledger_entries_of_a_trade_are_not_imported_twice()
    {
        var result = await ReadFullHistory();

        // El apunte LGRSVT-QOWLF-3FKGH2 es de tipo trade: ya viene por TradesHistory.
        Assert.DoesNotContain(result.Records, record => record.NaturalId == "LGRSVT-QOWLF-3FKGH2");
    }

    [Fact]
    public async Task A_deposit_a_withdrawal_and_a_staking_reward_are_normalised()
    {
        var result = await ReadFullHistory();

        var deposit = result.Records.Single(record => record.NaturalId == "LGRSVT-QOWLF-3FKGH1");
        var reward = result.Records.Single(record => record.NaturalId == "LGRSVT-QOWLF-3FKGH3");
        var withdrawal = result.Records.Single(record => record.NaturalId == "LGRSVT-QOWLF-3FKGH4");

        Assert.Equal(TransactionType.Deposit, deposit.Type);
        Assert.Equal(5000m, deposit.GrossAmount);
        Assert.Null(deposit.AssetSymbol);

        Assert.Equal(TransactionType.Reward, reward.Type);
        Assert.Equal("ETH", reward.AssetSymbol);
        Assert.Equal(0.0042m, reward.Quantity);

        Assert.Equal(TransactionType.Withdrawal, withdrawal.Type);
        Assert.Equal(0.02m, withdrawal.Quantity);
        Assert.Equal(0.00005m, withdrawal.Fee);
    }

    [Fact]
    public async Task A_pair_that_cannot_be_split_is_rejected_without_stopping_the_import()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithContent("""
                {"error":[],"result":{"trades":{"TX-1":{"pair":"ZZZ","time":1704883800.0,"type":"buy","price":"1","cost":"1","fee":"0","vol":"1"}},"count":1}}
                """)
            .RespondWithFile(Recorded("empty-ledgers.json"));

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        Assert.Empty(result.Records);
        Assert.Contains("ZZZ", Assert.Single(result.Rejected).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rate_limit_is_retried_with_growing_waits_and_the_import_continues()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("rate-limit.json"))
            .RespondWithFile(Recorded("rate-limit.json"))
            .RespondWithContent("""{"error":[],"result":{"trades":{},"count":0}}""")
            .RespondWithFile(Recorded("empty-ledgers.json"));

        var waits = new List<int>();
        var client = Client(handler, waits);

        var result = await new KrakenImportAdapter(client, NullLogger<KrakenImportAdapter>.Instance)
            .ReadAsync(Credential, From, To);

        Assert.Empty(result.Records);
        Assert.Equal([0, 1], waits);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task Exhausted_retries_fail_the_import()
    {
        var handler = new RecordedResponseHandler();

        for (var attempt = 0; attempt <= KrakenApiClient.MaximumRetries; attempt++)
        {
            handler.RespondWithFile(Recorded("rate-limit.json"));
        }

        var client = Client(handler, []);

        await Assert.ThrowsAsync<KrakenRateLimitException>(
            () => client.GetTradesAsync(Credential, From, To));
    }

    [Fact]
    public async Task An_http_rate_limit_status_is_also_retried()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithStatus(HttpStatusCode.TooManyRequests)
            .RespondWithContent("""{"error":[],"result":{"trades":{},"count":0}}""");

        var trades = await Client(handler, []).GetTradesAsync(Credential, From, To);

        Assert.Empty(trades);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task An_invalid_credential_is_reported_as_such()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("invalid-key.json"));

        var exception = await Assert.ThrowsAsync<KrakenApiException>(
            () => Client(handler, []).GetTradesAsync(Credential, From, To));

        Assert.True(exception.IsInvalidCredential);
    }

    [Fact]
    public async Task Every_request_is_signed_and_carries_the_api_key()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithContent("""{"error":[],"result":{"trades":{},"count":0}}""");

        await Client(handler, []).GetTradesAsync(Credential, From, To);

        var request = Assert.Single(handler.Requests);

        Assert.Equal("clave", request.Headers.GetValues("API-Key").Single());
        Assert.NotEmpty(request.Headers.GetValues("API-Sign").Single());
    }

    [Fact]
    public async Task The_requested_range_is_sent_to_the_platform()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithContent("""{"error":[],"result":{"trades":{},"count":0}}""");

        await Client(handler, []).GetTradesAsync(Credential, From, To);

        var body = Assert.Single(handler.RequestBodies);

        Assert.Contains($"start={From.ToUnixTimeSeconds()}", body, StringComparison.Ordinal);
        Assert.Contains($"end={To.ToUnixTimeSeconds()}", body, StringComparison.Ordinal);
    }

    private static async Task<ImportReadResult> ReadFullHistory()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("trades-page-1.json"))
            .RespondWithFile(Recorded("trades-page-2.json"))
            .RespondWithFile(Recorded("ledgers.json"));

        return await Adapter(handler).ReadAsync(Credential, From, To);
    }

    private static string Recorded(string fileName) =>
        Path.Combine("Import", "Kraken", "Recorded", fileName);

    private static KrakenImportAdapter Adapter(RecordedResponseHandler handler) =>
        new(Client(handler, []), NullLogger<KrakenImportAdapter>.Instance);

    private static KrakenApiClient Client(RecordedResponseHandler handler, List<int> waits) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com/") },
            NullLogger<KrakenApiClient>.Instance)
        {
            // Los tests no esperan de verdad: solo comprueban que la espera crece.
            BackoffDelay = attempt =>
            {
                waits.Add(attempt);

                return TimeSpan.Zero;
            },
        };
}
