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

    public PlatformCode Platform => PlatformCode.Kraken;

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

        // Las compras instantáneas no salen por TradesHistory: llegan al libro como un
        // par de apuntes, lo que se gasta y lo que se recibe, enlazados por referencia.
        var instant = InstantPurchases(ledgers);
        var pocketMoves = MovesBetweenPockets(ledgers);
        var withoutEffect = 0;

        foreach (var entry in ledgers)
        {
            if (pocketMoves.Contains(entry.LedgerId))
            {
                withoutEffect++;

                continue;
            }

            if (LedgerTypesCoveredByTrades.Contains(entry.Type))
            {
                // El apunte de una compraventa ya llega por TradesHistory con su precio;
                // importarlo dos veces duplicaría el movimiento.
                continue;
            }

            if (instant.TryGetValue(entry.LedgerId, out var purchase))
            {
                records.Add(purchase);

                continue;
            }

            if (instant.ContainsKey($"omitir:{entry.LedgerId}"))
            {
                // Es la otra pata del par, que ya viaja dentro del movimiento anterior.
                continue;
            }

            records.Add(FromLedger(entry));
        }

        logger.LogInformation(
            "Kraken: {Operaciones} operaciones y {Apuntes} apuntes normalizados, {Rechazados} rechazados, {SinEfecto} sin efecto.",
            trades.Count, records.Count - trades.Count + rejected.Count, rejected.Count, withoutEffect);

        return new ImportReadResult(records, rejected, withoutEffect);
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
            // El libro de Kraken no valora en euros lo que no es dinero: solo dice
            // cuántas unidades entran o salen. Poner esa cantidad como importe hacía que
            // una recompensa de trescientas mil unidades pareciera costar trescientos mil
            // euros. Sin valoración, el importe se queda a cero y se ve como pendiente.
            GrossAmount: isFiat ? Math.Abs(entry.Amount) : 0m,
            Currency: isFiat ? Currency.FromCode(canonical) : Currency.Euro,
            Fee: Math.Abs(entry.Fee),
            Withholding: null,
            OccurredAt: entry.Time,
            NaiveOccurredAt: null,
            SourceTimeZoneId: PlatformTimeZoneId,
            SplitRatio: null,
            RawContent: entry.RawContent);
    }

    /// <summary>
    /// Reconstruye a partir de sus dos apuntes lo que en el libro no se ve: las compras
    /// y ventas instantáneas y las permutas de un activo por otro.
    /// </summary>
    /// <remarks>
    /// Comprar desde la aplicación de Kraken no genera una operación de mercado, así que
    /// no aparece en TradesHistory. En el libro quedan dos apuntes con la misma
    /// referencia: lo que sale y lo que entra. Leídos por separado parecen dos traspasos
    /// y no crean ninguna posición, que es como una compra desaparecía de la cartera.
    ///
    /// Cuando una de las patas es dinero, el importe en euros sale de ahí, que es lo que
    /// costó de verdad. Cuando ninguna lo es —cambiar una cripto por otra— el libro no
    /// da ningún euro, así que las dos patas salen sin valorar y las valora el motor con
    /// el precio de cierre del día. Antes caían en el mapeo por tipo y se importaban como
    /// dos traspasos sueltos: ni restaban del activo entregado ni creaban lote del
    /// recibido, y una venta posterior se quedaba sin las unidades que había recibido.
    /// </remarks>
    private static Dictionary<string, ImportRecord> InstantPurchases(IReadOnlyList<KrakenLedgerEntry> ledgers)
    {
        var pairs = ledgers
            .Where(entry => entry.Type.Equals("spend", StringComparison.OrdinalIgnoreCase)
                || entry.Type.Equals("receive", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ReferenceId))
            .GroupBy(entry => entry.ReferenceId, StringComparer.Ordinal);

        var byLedgerId = new Dictionary<string, ImportRecord>(StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            var money = pair.FirstOrDefault(entry => KrakenSymbols.IsFiat(entry.Asset));
            var assets = pair.Where(entry => !KrakenSymbols.IsFiat(entry.Asset)).ToList();

            if (money is null && assets.Count == 2)
            {
                var outgoing = assets.Find(entry => entry.Amount < 0m);
                var incoming = assets.Find(entry => entry.Amount > 0m);

                // Dos patas en el mismo sentido no son una permuta. Se dejan como
                // estaban, que es visible en revisión, en lugar de inventar un cambio.
                if (outgoing is null || incoming is null)
                {
                    continue;
                }

                byLedgerId[outgoing.LedgerId] = SwapLeg(TransactionType.Sell, outgoing);
                byLedgerId[incoming.LedgerId] = SwapLeg(TransactionType.Buy, incoming);

                continue;
            }

            var asset = assets.Count == 1 ? assets[0] : null;

            // Sin las dos patas no se sabe qué costó: se deja como estaba y se ve en
            // revisión, en lugar de inventar un precio.
            if (money is null || asset is null)
            {
                continue;
            }

            var buying = asset.Amount > 0m;

            byLedgerId[asset.LedgerId] = new ImportRecord(
                NaturalId: asset.LedgerId,
                RowNumber: null,
                Type: buying ? TransactionType.Buy : TransactionType.Sell,
                AssetSymbol: KrakenSymbols.ToCanonical(asset.Asset),
                AssetClass: AssetClass.Crypto,
                Quantity: Math.Abs(asset.Amount),
                UnitPrice: Math.Abs(asset.Amount) == 0m ? null : Math.Abs(money.Amount) / Math.Abs(asset.Amount),
                GrossAmount: Math.Abs(money.Amount),
                Currency: Currency.FromCode(KrakenSymbols.ToCanonical(money.Asset)),
                Fee: Math.Abs(money.Fee) + Math.Abs(asset.Fee),
                Withholding: null,
                OccurredAt: asset.Time,
                NaiveOccurredAt: null,
                SourceTimeZoneId: PlatformTimeZoneId,
                SplitRatio: null,
                RawContent: asset.RawContent);

            byLedgerId[$"omitir:{money.LedgerId}"] = byLedgerId[asset.LedgerId];
        }

        return byLedgerId;
    }

    /// <summary>
    /// Una de las dos patas de una permuta, sin valorar y sin pasar por caja.
    /// </summary>
    /// <remarks>
    /// Conserva su propio identificador de apunte en lugar de inventar sufijos: el libro
    /// ya da uno por pata, y es lo que hace que releer el histórico las reconozca en vez
    /// de duplicarlas.
    ///
    /// La cantidad va neta de comisión porque Kraken la cobra en el propio activo: entra
    /// lo recibido menos la comisión y sale lo entregado más ella. Sumando la cantidad
    /// bruta quedaban unas milésimas que nadie tenía, y la posición de un activo vendido
    /// por completo seguía apareciendo abierta por ese residuo. Por eso tampoco se
    /// declara comisión: ya está dentro de la cantidad, y repetirla como importe la
    /// contaría además en euros, que no es la moneda en la que se cobró.
    /// </remarks>
    private static ImportRecord SwapLeg(TransactionType type, KrakenLedgerEntry entry) =>
        new(
            NaturalId: entry.LedgerId,
            RowNumber: null,
            Type: type,
            AssetSymbol: KrakenSymbols.ToCanonical(entry.Asset),
            AssetClass: AssetClass.Crypto,
            Quantity: type == TransactionType.Buy
                ? Math.Abs(entry.Amount) - Math.Abs(entry.Fee)
                : Math.Abs(entry.Amount) + Math.Abs(entry.Fee),
            UnitPrice: null,
            GrossAmount: 0m,
            Currency: Currency.Euro,
            Fee: 0m,
            Withholding: null,
            OccurredAt: entry.Time,
            NaiveOccurredAt: null,
            SourceTimeZoneId: PlatformTimeZoneId,
            SplitRatio: null,
            RawContent: entry.RawContent,
            SettledInCash: false,
            NeedsValuation: true);

    /// <summary>
    /// Apuntes de mover un activo a Earn y de recuperarlo, que no son movimientos.
    /// </summary>
    /// <remarks>
    /// Kraken guarda lo que está en Earn como una variante del mismo activo —SOL.F frente
    /// a SOL—, así que el paso queda apuntado como un traspaso que sale de uno y entra en
    /// el otro. Canónicamente son el mismo activo: la cantidad total no cambia y lo único
    /// que se importaba eran dos líneas iguales que parecían un traspaso duplicado.
    ///
    /// Se reconocen por la forma —misma referencia, mismo activo canónico, una entrada y
    /// una salida que se anulan— y no por el nombre del subtipo, que la plataforma ha ido
    /// cambiando y no hay motivo para perseguir.
    /// </remarks>
    private static HashSet<string> MovesBetweenPockets(IReadOnlyList<KrakenLedgerEntry> ledgers)
    {
        var moves = new HashSet<string>(StringComparer.Ordinal);

        var pairs = ledgers
            .Where(entry => entry.Type.Equals("transfer", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ReferenceId))
            .GroupBy(entry => entry.ReferenceId, StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            var sides = pair.ToList();

            if (sides.Count != 2
                || !KrakenSymbols.ToCanonical(sides[0].Asset).Equals(KrakenSymbols.ToCanonical(sides[1].Asset), StringComparison.Ordinal)
                || sides[0].Amount + sides[1].Amount != 0m)
            {
                continue;
            }

            moves.Add(sides[0].LedgerId);
            moves.Add(sides[1].LedgerId);
        }

        return moves;
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
