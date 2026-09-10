using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Import.Bit2Me;

/// <summary>
/// Adaptador de Bit2Me: recorre los productos que la credencial permita leer —contado,
/// monedero y rendimiento— y deja constancia de los que no.
/// </summary>
public sealed class Bit2MeImportAdapter(Bit2MeApiClient client, ILogger<Bit2MeImportAdapter> logger) : IApiImportAdapter
{
    internal const string PlatformTimeZoneId = "UTC";

    /// <summary>Divisas fiduciarias, para distinguir un ingreso de euros de la entrada de un activo.</summary>
    private static readonly HashSet<string> FiatCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EUR", "USD", "GBP", "CHF", "JPY", "CAD", "AUD",
    };

    public PlatformCode Platform => PlatformCode.Bit2Me;

    public ImportSourceKind SourceKind => ImportSourceKind.RemoteApi;

    public async Task<ImportReadResult> ReadAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var records = new List<ImportRecord>();
        var rejected = new List<RejectedRecord>();
        var warnings = new List<string>();

        var trades = await ReadProductAsync(
            "contado",
            () => client.GetTradesAsync(credential, from, to, cancellationToken),
            warnings).ConfigureAwait(false);

        foreach (var trade in trades)
        {
            var record = FromTrade(trade, rejected);

            if (record is not null)
            {
                records.Add(record);
            }
        }

        var wallet = await ReadProductAsync(
            "monedero",
            () => client.GetWalletTransactionsAsync(credential, cancellationToken),
            warnings).ConfigureAwait(false);

        foreach (var transaction in wallet.Where(transaction => transaction.IsCompleted))
        {
            if (transaction.Date < from || transaction.Date > to)
            {
                continue;
            }

            records.AddRange(FromWalletTransaction(transaction));
        }

        records.AddRange(await ReadEarnAsync(credential, from, to, warnings, cancellationToken).ConfigureAwait(false));

        logger.LogInformation(
            "Bit2Me: {Registros} registros normalizados, {Rechazados} rechazados, {Avisos} productos omitidos.",
            records.Count, rejected.Count, warnings.Count);

        return new ImportReadResult(records, rejected) { Warnings = warnings };
    }

    /// <summary>
    /// Lee un producto y, si la credencial no llega a él, sigue con el resto dejando
    /// constancia. Un permiso que falta no puede impedir importar lo que sí es legible.
    /// </summary>
    private static async Task<IReadOnlyList<T>> ReadProductAsync<T>(
        string product,
        Func<Task<IReadOnlyList<T>>> read,
        List<string> warnings)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Bit2MeAccessDeniedException exception) when (!exception.IsInvalidCredential)
        {
            warnings.Add($"No se ha podido leer el producto '{product}': {exception.Message}");

            return [];
        }
    }

    private async Task<IReadOnlyList<ImportRecord>> ReadEarnAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var wallets = await ReadProductAsync(
            "rendimiento",
            () => client.GetEarnWalletsAsync(credential, cancellationToken),
            warnings).ConfigureAwait(false);

        var records = new List<ImportRecord>();

        foreach (var earnWallet in wallets)
        {
            var movements = await ReadProductAsync(
                $"rendimiento {earnWallet.Currency}",
                () => client.GetEarnMovementsAsync(credential, earnWallet.WalletId, cancellationToken),
                warnings).ConfigureAwait(false);

            records.AddRange(movements
                .Where(movement => movement.CreatedAt >= from && movement.CreatedAt <= to)
                .Select(FromEarnMovement));
        }

        return records;
    }

    private static ImportRecord? FromTrade(Bit2MeTrade trade, List<RejectedRecord> rejected)
    {
        if (trade.AmountCurrency.Length == 0 || trade.PriceCurrency.Length == 0)
        {
            rejected.Add(new RejectedRecord(
                trade.Id, null, trade.RawContent, $"La operación '{trade.Symbol}' no indica las divisas del par."));

            return null;
        }

        var quoteIsFiat = FiatCodes.Contains(trade.PriceCurrency);

        return new ImportRecord(
            NaturalId: trade.Id,
            RowNumber: null,
            Type: trade.IsBuy ? TransactionType.Buy : TransactionType.Sell,
            AssetSymbol: trade.AmountCurrency.ToUpperInvariant(),
            AssetClass: AssetClass.Crypto,
            Quantity: trade.Amount,
            UnitPrice: trade.Price,
            // Cuando la contrapartida no es fiduciaria, Bit2Me aporta el valor en euros
            // de la operación, que es exactamente lo que el criterio FIFO necesita.
            GrossAmount: quoteIsFiat ? trade.Cost : trade.CostEuro,
            Currency: quoteIsFiat ? Currency.FromCode(trade.PriceCurrency) : Currency.Euro,
            Fee: trade.FeeAmount,
            Withholding: null,
            OccurredAt: trade.CreatedAt,
            NaiveOccurredAt: null,
            SourceTimeZoneId: PlatformTimeZoneId,
            SplitRatio: null,
            RawContent: trade.RawContent);
    }

    /// <summary>
    /// Una permuta se emite como venta del activo de origen y compra del de destino,
    /// enlazadas por el identificador de la transacción. Tratarla como un único
    /// movimiento escondería el hecho imponible de la pata de salida.
    /// </summary>
    /// <summary>
    /// Vuelve a leer una transacción de monedero ya guardada.
    /// </summary>
    /// <remarks>
    /// Se usa para reinterpretar movimientos que quedaron sin clasificar cuando el
    /// adaptador todavía no entendía su forma. Es el mismo camino que sigue una
    /// importación, así que lo reinterpretado y lo importado no pueden divergir.
    /// </remarks>
    internal static IReadOnlyList<ImportRecord> ReadWalletTransaction(Bit2MeWalletTransaction transaction) =>
        FromWalletTransaction(transaction);

    private static IReadOnlyList<ImportRecord> FromWalletTransaction(Bit2MeWalletTransaction transaction)
    {
        var candidates = transaction.Operations
            .Select(name => name.ToUpperInvariant())
            .ToList();

        if (candidates.Count == 0)
        {
            candidates.Add(transaction.Operation.ToUpperInvariant());
        }

        // Bit2Me valora el movimiento en «denomination», pero no siempre en euros: a
        // veces viene en la propia moneda del activo. Tomarlo sin mirar la divisa
        // convertía una cantidad de cripto en un importe en euros.
        //
        // Cuando sí viene en euros es lo que se pagó, y manda sobre cualquier cálculo:
        // multiplicar la cantidad por el cambio da una cifra parecida pero no la real, y
        // una compra de cien euros dejaría de costar cien euros.
        decimal? paidInEuros = transaction.Denomination is { } denomination && IsEuro(denomination.Currency)
            ? denomination.Value
            : null;

        if (candidates.Contains("SWAP") && transaction.Origin is { } origin && transaction.Destination is { } destination)
        {
            // Cada lado trae su propio cambio contra el euro, y es lo único que da valor
            // a una permuta de cripto por cripto: lo que valora el movimiento entero
            // viene en la moneda de origen. Sin esto, la venta entraba con cero de
            // ingreso y el ejercicio salía con una pérdida que no existió.
            var sold = origin.ValueInEuros ?? destination.ValueInEuros ?? paidInEuros ?? 0m;
            var bought = destination.ValueInEuros ?? origin.ValueInEuros ?? paidInEuros ?? 0m;

            return
            [
                Leg(TransactionType.Sell, origin, sold, $"{transaction.Id}:out"),
                Leg(TransactionType.Buy, destination, bought, $"{transaction.Id}:in"),
            ];
        }

        // Gana el primero que se reconozca, que es el más específico: «withdrawal-earn»
        // antes que «withdrawal», porque mover fondos a Earn no es sacarlos de la cuenta.
        var type = candidates
            .Select(MapOperation)
            .FirstOrDefault(mapped => mapped != TransactionType.Unknown, TransactionType.Unknown);

        var amount = transaction.Destination ?? transaction.Origin ?? transaction.Denomination;

        if (amount is null)
        {
            return [];
        }

        return [Leg(type, amount, paidInEuros ?? amount.ValueInEuros ?? 0m, transaction.Id)];

        ImportRecord Leg(TransactionType type, Bit2MeAmount amount, decimal euros, string naturalId)
        {
            var isFiat = FiatCodes.Contains(amount.Currency);

            return new ImportRecord(
                NaturalId: naturalId,
                RowNumber: null,
                Type: type,
                AssetSymbol: isFiat ? null : amount.Currency.ToUpperInvariant(),
                AssetClass: isFiat ? null : AssetClass.Crypto,
                Quantity: isFiat ? 0m : Math.Abs(amount.Value),
                UnitPrice: null,
                GrossAmount: isFiat ? Math.Abs(amount.Value) : Math.Abs(euros),
                Currency: isFiat ? Currency.FromCode(amount.Currency) : Currency.Euro,
                Fee: transaction.NetworkFee is { } fee ? Math.Abs(fee.Value) : 0m,
                Withholding: null,
                OccurredAt: transaction.Date,
                NaiveOccurredAt: null,
                SourceTimeZoneId: PlatformTimeZoneId,
                SplitRatio: null,
                RawContent: transaction.RawContent);
        }
    }

    private static ImportRecord FromEarnMovement(Bit2MeEarnMovement movement)
    {
        var isFiat = FiatCodes.Contains(movement.Amount.Currency);

        return new ImportRecord(
            NaturalId: movement.MovementId,
            RowNumber: null,
            Type: MapEarnType(movement.Type),
            AssetSymbol: isFiat ? null : movement.Amount.Currency.ToUpperInvariant(),
            AssetClass: isFiat ? null : AssetClass.Crypto,
            Quantity: isFiat ? 0m : Math.Abs(movement.Amount.Value),
            UnitPrice: null,
            // Lo que valía al cobrarlo, no cuántas unidades eran. Usar la cantidad como
            // importe hacía que dos mil setecientos B2M de recompensa parecieran dos mil
            // setecientos euros de rendimiento, y con ellos de coste.
            GrossAmount: isFiat ? Math.Abs(movement.Amount.Value) : movement.ValueInEuros ?? 0m,
            Currency: isFiat ? Currency.FromCode(movement.Amount.Currency) : Currency.Euro,
            Fee: 0m,
            Withholding: null,
            OccurredAt: movement.CreatedAt,
            NaiveOccurredAt: null,
            SourceTimeZoneId: PlatformTimeZoneId,
            SplitRatio: null,
            RawContent: movement.RawContent);
    }

    private static TransactionType MapOperation(string operation) => operation switch
    {
        "PURCHASE" => TransactionType.Buy,
        "SELL" => TransactionType.Sell,
        "DEPOSIT" or "RECEIVE" or "RECEIVE-PAY" => TransactionType.Deposit,
        "WITHDRAWAL" or "SEND" or "SEND-PAY" => TransactionType.Withdrawal,
        "DEPOSIT-EARN" or "WITHDRAWAL-EARN" or "DEPOSIT-TRADING" or "WITHDRAWAL-TRADING" => TransactionType.Transfer,
        _ => TransactionType.Unknown,
    };

    /// <summary>
    /// Una recompensa es rendimiento; una aportación o una retirada de Earn son
    /// traspasos entre productos del mismo usuario, no compras ni ventas.
    /// </summary>
    private static TransactionType MapEarnType(string type) => type.ToUpperInvariant() switch
    {
        "REWARD" or "REWARDS" or "INTEREST" => TransactionType.Reward,
        "DEPOSIT" or "WITHDRAWAL" => TransactionType.Transfer,
        _ => TransactionType.Unknown,
    };

    private static bool IsEuro(string currency) => string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase);
}
