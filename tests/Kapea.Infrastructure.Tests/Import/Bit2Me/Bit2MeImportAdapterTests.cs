using System.Net;
using Kapea.Application.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Import.Bit2Me;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Import.Bit2Me;

public class Bit2MeImportAdapterTests
{
    private static readonly ApiCredential Credential = new("clave", "secreto");
    private static readonly DateTimeOffset From = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_paginated_history_is_walked_to_the_end()
    {
        var handler = FullHistory();

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        // Dos páginas de contado, dos de monedero, la lista de monederos Earn y los
        // movimientos de cada uno: siete peticiones y ninguna página perdida.
        Assert.Equal(7, handler.Requests.Count);
        Assert.Equal(3, result.Records.Count(record => record.NaturalId!.StartsWith("a1b2c3d4", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_spot_trade_against_a_fiat_pair_keeps_its_own_currency()
    {
        var result = await ReadFullHistory();
        var buy = result.Records.Single(record => record.NaturalId == "a1b2c3d4-0001-4000-8000-000000000001");

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal("BTC", buy.AssetSymbol);
        Assert.Equal(0.1m, buy.Quantity);
        Assert.Equal(38000m, buy.UnitPrice);
        Assert.Equal(3800m, buy.GrossAmount);
        Assert.Equal(Currency.Euro, buy.Currency);
        Assert.Equal(6.08m, buy.Fee);
    }

    [Fact]
    public async Task A_crypto_to_crypto_trade_is_valued_with_the_euro_amount_the_platform_reports()
    {
        // La contrapartida es BTC, así que el coste en su divisa no sirve para el FIFO:
        // se usa costEuro, que es la valoración en euros de la propia plataforma.
        var result = await ReadFullHistory();
        var trade = result.Records.Single(record => record.NaturalId == "a1b2c3d4-0001-4000-8000-000000000002");

        Assert.Equal(TransactionType.Sell, trade.Type);
        Assert.Equal("ETH", trade.AssetSymbol);
        Assert.Equal(4200m, trade.GrossAmount);
        Assert.Equal(Currency.Euro, trade.Currency);
    }

    [Fact]
    public async Task A_swap_becomes_a_linked_sell_and_buy_valued_in_euros()
    {
        var result = await ReadFullHistory();
        var sell = result.Records.Single(record => record.NaturalId == "w-0002:out");
        var buy = result.Records.Single(record => record.NaturalId == "w-0002:in");

        Assert.Equal(TransactionType.Sell, sell.Type);
        Assert.Equal("BTC", sell.AssetSymbol);
        Assert.Equal(0.025m, sell.Quantity);
        Assert.Equal(1500m, sell.GrossAmount);

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal("ETH", buy.AssetSymbol);
        Assert.Equal(0.6m, buy.Quantity);
        Assert.Equal(1500m, buy.GrossAmount);

        Assert.Equal(sell.OccurredAt, buy.OccurredAt);
    }

    [Fact]
    public async Task A_fiat_deposit_carries_no_asset()
    {
        var result = await ReadFullHistory();
        var deposit = result.Records.Single(record => record.NaturalId == "w-0001");

        Assert.Equal(TransactionType.Deposit, deposit.Type);
        Assert.Null(deposit.AssetSymbol);
        Assert.Equal(5000m, deposit.GrossAmount);
        Assert.Equal(Currency.Euro, deposit.Currency);
    }

    [Fact]
    public async Task A_network_fee_travels_with_the_withdrawal()
    {
        var result = await ReadFullHistory();
        var withdrawal = result.Records.Single(record => record.NaturalId == "w-0003");

        Assert.Equal(TransactionType.Withdrawal, withdrawal.Type);
        Assert.Equal(0.01m, withdrawal.Quantity);
        Assert.Equal(0.00005m, withdrawal.Fee);
    }

    [Fact]
    public async Task A_cancelled_transaction_is_not_imported()
    {
        var result = await ReadFullHistory();

        Assert.DoesNotContain(result.Records, record => record.NaturalId == "w-0004");
    }

    [Fact]
    public async Task The_earn_products_are_read_from_the_object_that_wraps_them()
    {
        // Bit2Me devuelve los productos dentro de «data», no como un array suelto.
        // Esperando un array se leían cero productos y con ellos se perdían todas las
        // recompensas: ni un error ni un aviso, simplemente no había nada que importar.
        var result = await ReadFullHistory();

        Assert.Contains(result.Records, record => record.NaturalId == "em-0001");
    }

    [Fact]
    public async Task A_reward_is_worth_what_it_was_worth_and_not_how_many_units_it_paid()
    {
        // Bit2Me manda el valor en euros de cada movimiento de rendimiento. Sin usarlo,
        // la cantidad de cripto acababa de importe: dos mil setecientos B2M de recompensa
        // parecían dos mil setecientos euros de rendimiento, y con ellos de coste.
        var result = await ReadFullHistory();

        var reward = result.Records.Single(record => record.NaturalId == "em-0001");

        Assert.Equal(0.0042m, reward.Quantity);
        Assert.Equal(12.50m, reward.GrossAmount);
        Assert.Equal("EUR", reward.Currency.Code);
    }

    [Fact]
    public async Task Earn_rewards_and_contributions_are_told_apart()
    {
        var result = await ReadFullHistory();

        var reward = result.Records.Single(record => record.NaturalId == "em-0001");
        var contribution = result.Records.Single(record => record.NaturalId == "em-0002");

        Assert.Equal(TransactionType.Reward, reward.Type);
        Assert.Equal("ETH", reward.AssetSymbol);
        Assert.Equal(0.0042m, reward.Quantity);

        // Aportar a un producto de ahorro no es comprar: es mover el activo de sitio.
        Assert.Equal(TransactionType.Transfer, contribution.Type);
    }

    [Fact]
    public async Task A_purchase_costs_what_was_paid_and_not_what_the_rate_recalculates()
    {
        // Comprar cien euros de cripto cuesta cien euros. Multiplicar la cantidad por el
        // cambio da una cifra parecida y distinta, y el coste de adquisición dejaría de
        // ser el de la operación.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-purchase-rate.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        var purchase = Assert.Single(result.Records);

        Assert.Equal(TransactionType.Buy, purchase.Type);
        Assert.Equal(99.0595m, purchase.Quantity);

        // Lo pagado son cien euros, repartidos entre lo que costó la moneda y lo que se
        // quedó la plataforma dentro del precio.
        Assert.Equal(100m, decimal.Round(purchase.GrossAmount + purchase.Fee, 2));
    }

    [Fact]
    public async Task A_purchase_paid_with_a_card_brings_its_money_into_the_account()
    {
        // El dinero va de la tarjeta a la moneda sin pasar por el saldo en euros. Sin el
        // ingreso que la acompaña, la compra descuenta un efectivo que nunca estuvo allí
        // y el saldo de la cuenta arranca en negativo para siempre.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-purchase-card.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        var funding = result.Records.Single(record => record.NaturalId == "wc-0001:pago");
        var purchase = result.Records.Single(record => record.NaturalId == "wc-0001");

        Assert.Equal(TransactionType.Deposit, funding.Type);
        Assert.Equal(100m, funding.GrossAmount);
        Assert.Null(funding.AssetSymbol);
        Assert.Equal(0m, funding.Quantity);

        // El ingreso no cambia lo que costó la compra ni cuántas unidades trajo.
        Assert.Equal(TransactionType.Buy, purchase.Type);
        Assert.Equal(100m, purchase.GrossAmount);
        Assert.Equal(49.591735m, purchase.Quantity);
    }

    [Fact]
    public async Task A_purchase_paid_from_the_wallet_brings_no_money_in()
    {
        // El dinero ya estaba en la cuenta. Ingresarlo otra vez lo contaría dos veces.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-purchase-card.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        Assert.DoesNotContain(result.Records, record => record.NaturalId == "wc-0002:pago");
        Assert.Equal(TransactionType.Buy, result.Records.Single(record => record.NaturalId == "wc-0002").Type);
    }

    [Fact]
    public async Task The_fee_hidden_in_the_price_is_worked_out_from_the_published_rate()
    {
        // Bit2Me no manda comisión en sus movimientos: la cobra dando peor cambio que el
        // publicado. Como manda los dos datos, la diferencia es la comisión, y es la
        // misma cifra que trae la columna del fichero exportado.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-purchase-spread.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var purchase = Assert.Single((await Adapter(handler).ReadAsync(Credential, From, To)).Records);

        // Cincuenta euros al cambio publicado son 49,53; los cuarenta y siete céntimos
        // que faltan se los quedó la plataforma.
        Assert.Equal(0.47m, decimal.Round(purchase.Fee, 2));
        Assert.Equal(49.53m, decimal.Round(purchase.GrossAmount, 2));

        // Lo pagado no cambia: sigue costando los cincuenta euros que salieron.
        Assert.Equal(50m, decimal.Round(purchase.GrossAmount + purchase.Fee, 2));
        Assert.Equal(0.00084628m, purchase.Quantity);
    }

    [Fact]
    public async Task A_swap_of_crypto_for_crypto_is_valued_in_euros()
    {
        // Bit2Me valora el movimiento entero en la moneda de origen, no en euros, pero
        // cada lado trae su cambio contra el euro. Sin usarlo, la venta entraba con cero
        // de ingreso y el ejercicio salía con una pérdida del importe entero.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-swap-rates.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        var sale = result.Records.Single(record => record.Type == TransactionType.Sell);
        var purchase = result.Records.Single(record => record.Type == TransactionType.Buy);

        // 12,47227153 ATOM a 2,71572845 €
        Assert.Equal("ATOM", sale.AssetSymbol);
        Assert.Equal(33.87m, Math.Round(sale.GrossAmount, 2));
        Assert.Equal("EUR", sale.Currency.Code);

        // 0,01004612 ETH a 3.339,87 €
        Assert.Equal("ETH", purchase.AssetSymbol);
        Assert.Equal(33.55m, Math.Round(purchase.GrossAmount, 2));
    }

    [Fact]
    public async Task A_value_that_is_not_in_euros_is_not_taken_for_euros()
    {
        // Bit2Me valora el movimiento en «denomination», y no siempre en euros: a veces
        // viene en la propia moneda del activo. Tomarlo sin mirar la divisa convertía una
        // cantidad de cripto en un importe, y la cifra que se veía era disparatada.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-denomination.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        var record = Assert.Single(result.Records);

        Assert.Equal(1500m, record.Quantity);
        Assert.Equal(0m, record.GrossAmount);
        Assert.Equal("EUR", record.Currency.Code);
    }

    [Fact]
    public async Task Moving_funds_to_and_from_Earn_is_a_transfer_and_not_a_withdrawal()
    {
        // Bit2Me reparte el significado entre dos campos: una retirada hacia Earn llega
        // como type «withdrawal» y subtype «earn», y no es sacar dinero de la cuenta sino
        // moverlo a otro producto del mismo usuario. Leyendo solo uno de los dos, estos
        // movimientos entraban sin clasificar.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-earn.json"))
            .RespondWithContent("""{"total":0,"data":[]}""")
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        var byId = result.Records.ToDictionary(record => record.NaturalId!);


        Assert.Equal(TransactionType.Transfer, byId["we-0001"].Type);
        Assert.Equal(TransactionType.Transfer, byId["we-0002"].Type);

        // Sin subtipo siguen siendo lo que dicen ser: dinero que entra o sale de verdad.
        Assert.Equal(TransactionType.Withdrawal, byId["we-0003"].Type);
        Assert.Equal(TransactionType.Deposit, byId["we-0004"].Type);

        Assert.DoesNotContain(result.Records, record => record.Type == TransactionType.Unknown);
    }

    [Fact]
    public async Task An_inaccessible_product_is_recorded_and_the_rest_is_still_imported()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("trades-page-1.json"))
            .RespondWithFile(Recorded("trades-page-2.json"))
            .RespondWithStatus(HttpStatusCode.Forbidden)
            .RespondWithFile(Recorded("earn-wallets.json"))
            .RespondWithFile(Recorded("earn-movements-eth.json"))
            .RespondWithContent("""{"total":0,"data":[]}""");

        var result = await Adapter(handler).ReadAsync(Credential, From, To);

        Assert.Contains(result.Warnings, warning => warning.Contains("monedero", StringComparison.Ordinal));
        Assert.Equal(3, result.Records.Count(record => record.NaturalId!.StartsWith("a1b2c3d4", StringComparison.Ordinal)));
        Assert.Contains(result.Records, record => record.NaturalId == "em-0001");
    }

    [Fact]
    public async Task An_invalid_credential_is_not_swallowed_as_a_missing_product()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<Bit2MeAccessDeniedException>(
            () => Adapter(handler).ReadAsync(Credential, From, To));

        Assert.True(exception.IsInvalidCredential);
    }

    [Fact]
    public async Task A_rate_limit_is_retried_with_growing_waits()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithStatus(HttpStatusCode.TooManyRequests)
            .RespondWithStatus(HttpStatusCode.TooManyRequests)
            .RespondWithFile(Recorded("empty-trades.json"));

        var waits = new List<int>();

        var trades = await Client(handler, waits).GetTradesAsync(Credential, From, To);

        Assert.Empty(trades);
        Assert.Equal([0, 1], waits);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Exhausted_retries_fail_the_import()
    {
        var handler = new RecordedResponseHandler();

        for (var attempt = 0; attempt <= Bit2MeApiClient.MaximumRetries; attempt++)
        {
            handler.RespondWithStatus(HttpStatusCode.TooManyRequests);
        }

        await Assert.ThrowsAsync<Bit2MeRateLimitException>(
            () => Client(handler, []).GetTradesAsync(Credential, From, To));
    }

    [Fact]
    public async Task Every_request_carries_the_key_the_nonce_and_the_signature()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("empty-trades.json"));

        await Client(handler, []).GetTradesAsync(Credential, From, To);

        var request = Assert.Single(handler.Requests);

        Assert.Equal("clave", request.Headers.GetValues("x-api-key").Single());
        Assert.NotEmpty(request.Headers.GetValues("api-signature").Single());
        Assert.True(long.TryParse(request.Headers.GetValues("x-nonce").Single(), out var nonce) && nonce > 0);
    }

    [Fact]
    public async Task The_wallet_cursor_of_a_page_is_sent_on_the_next_one()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("wallet-page-1.json"))
            .RespondWithFile(Recorded("wallet-page-2.json"));

        await Client(handler, []).GetWalletTransactionsAsync(Credential);

        Assert.DoesNotContain("cursor=", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("cursor=Y3Vyc29yLTE%3D", handler.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transactions_outside_the_requested_range_are_left_out()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("empty-trades.json"))
            .RespondWithFile(Recorded("wallet-page-1.json"))
            .RespondWithFile(Recorded("wallet-page-2.json"))
            .RespondWithContent("[]");

        var result = await Adapter(handler).ReadAsync(
            Credential, new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), To);

        Assert.DoesNotContain(result.Records, record => record.NaturalId == "w-0001");
        Assert.Contains(result.Records, record => record.NaturalId == "w-0003");
    }

    private static async Task<ImportReadResult> ReadFullHistory() =>
        await Adapter(FullHistory()).ReadAsync(Credential, From, To);

    private static RecordedResponseHandler FullHistory() =>
        new RecordedResponseHandler()
            .RespondWithFile(Recorded("trades-page-1.json"))
            .RespondWithFile(Recorded("trades-page-2.json"))
            .RespondWithFile(Recorded("wallet-page-1.json"))
            .RespondWithFile(Recorded("wallet-page-2.json"))
            .RespondWithFile(Recorded("earn-wallets.json"))
            .RespondWithFile(Recorded("earn-movements-eth.json"))
            .RespondWithContent("""{"total":0,"data":[]}""");

    private static string Recorded(string fileName) =>
        Path.Combine("Import", "Bit2Me", "Recorded", fileName);

    private static Bit2MeImportAdapter Adapter(RecordedResponseHandler handler) =>
        new(Client(handler, []), NullLogger<Bit2MeImportAdapter>.Instance);

    private static Bit2MeApiClient Client(RecordedResponseHandler handler, List<int> waits) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://gateway.bit2me.com/") },
            NullLogger<Bit2MeApiClient>.Instance)
        {
            BackoffDelay = attempt =>
            {
                waits.Add(attempt);

                return TimeSpan.Zero;
            },
        };
}
