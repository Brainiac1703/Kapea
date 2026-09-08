using Kapea.Domain.Calculation;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

/// <summary>
/// Construye movimientos ya valorados en euros para el motor. Las fechas se expresan
/// como día del calendario porque es la granularidad con la que se razona un caso
/// fiscal, y la hora solo importa para desempatar.
/// </summary>
internal sealed class Ledger
{
    private readonly List<ValuedTransaction> _transactions = [];
    private int _fingerprint;

    internal UserId Owner { get; } = new(Guid.NewGuid());

    internal Guid AssetId { get; } = Guid.NewGuid();

    internal Guid AccountId { get; } = Guid.NewGuid();

    internal Guid OtherAccountId { get; } = Guid.NewGuid();

    internal IReadOnlyList<ValuedTransaction> Transactions => _transactions;

    internal Ledger Buy(string date, decimal quantity, decimal grossEuros, decimal feeEuros = 0m, Guid? accountId = null) =>
        Add(TransactionType.Buy, date, quantity, grossEuros, feeEuros, accountId);

    internal Ledger Sell(string date, decimal quantity, decimal grossEuros, decimal feeEuros = 0m, Guid? accountId = null) =>
        Add(TransactionType.Sell, date, quantity, grossEuros, feeEuros, accountId);

    /// <summary>Venta ejecutada desde la cuenta de destino, tras un traspaso.</summary>
    internal Ledger SellFromOtherAccount(string date, decimal quantity, decimal grossEuros) =>
        Add(TransactionType.Sell, date, quantity, grossEuros, 0m, OtherAccountId);

    internal Ledger Split(string date, decimal ratio) =>
        Add(TransactionType.Split, date, 0m, 0m, 0m, splitRatio: ratio);

    internal Ledger Dividend(string date, decimal grossEuros, decimal withholdingEuros = 0m) =>
        Add(TransactionType.Dividend, date, 0m, grossEuros, 0m, withholdingEuros: withholdingEuros);

    internal Ledger Reward(string date, decimal quantity, decimal grossEuros) =>
        Add(TransactionType.Reward, date, quantity, grossEuros, 0m);

    internal Ledger Unclassified(string date) =>
        Add(TransactionType.Unknown, date, 0m, 0m, 0m);

    internal Ledger PendingTransfer(string date, decimal quantity, decimal grossEuros) =>
        Add(TransactionType.Withdrawal, date, quantity, grossEuros, 0m, pendingTransferReview: true);

    internal Ledger TransferOut(string date, decimal quantity, decimal feeEuros = 0m, Guid? transferId = null)
    {
        var id = transferId ?? Guid.NewGuid();

        Add(TransactionType.Withdrawal, date, quantity, 0m, feeEuros, AccountId,
            internalTransferId: id, destinationAccountId: OtherAccountId);

        return Add(TransactionType.Deposit, date, quantity, 0m, 0m, OtherAccountId, internalTransferId: id);
    }

    internal AssetCalculationResult Calculate() => FifoCalculator.Calculate(Owner, AssetId, _transactions);

    /// <summary>Devuelve los movimientos en orden inverso, para comprobar que el motor los reordena.</summary>
    internal AssetCalculationResult CalculateInReverse() =>
        FifoCalculator.Calculate(Owner, AssetId, Enumerable.Reverse(_transactions));

    private Ledger Add(
        TransactionType type,
        string date,
        decimal quantity,
        decimal grossEuros,
        decimal feeEuros,
        Guid? accountId = null,
        decimal? splitRatio = null,
        decimal withholdingEuros = 0m,
        Guid? internalTransferId = null,
        Guid? destinationAccountId = null,
        bool pendingTransferReview = false)
    {
        var occurredAt = Occurrence.FromNaive(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), "Europe/Madrid");
        var needsAsset = type is TransactionType.Buy or TransactionType.Sell;

        var transaction = Transaction.Imported(
            Owner,
            accountId ?? AccountId,
            type,
            needsAsset || quantity > 0m ? AssetId : null,
            new Quantity(quantity),
            quantity > 0m && grossEuros > 0m ? Money.Euros(grossEuros / quantity) : null,
            Money.Euros(grossEuros),
            Money.Euros(feeEuros),
            occurredAt,
            TransactionSource.FromImport(Guid.NewGuid(), $"src-{++_fingerprint:D4}", null, $"fp-{_fingerprint:D4}"),
            withholdingEuros > 0m ? Money.Euros(withholdingEuros) : null);

        _transactions.Add(new ValuedTransaction(
            transaction,
            Money.Euros(grossEuros),
            Money.Euros(feeEuros),
            withholdingEuros > 0m ? Money.Euros(withholdingEuros) : null,
            splitRatio,
            internalTransferId,
            destinationAccountId,
            pendingTransferReview));

        return this;
    }
}
