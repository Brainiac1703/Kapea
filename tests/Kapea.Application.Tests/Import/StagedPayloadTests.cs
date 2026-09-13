using Kapea.Application.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Tests.Import;

public class StagedPayloadTests
{
    [Fact]
    public void A_movement_that_does_not_settle_in_cash_keeps_saying_so_after_being_stored()
    {
        // Entre la vista previa y la confirmación, el registro pasa por la base de datos.
        // Lo que no viaje en ese salto se pierde: las permutas volvían a contarse como
        // movimientos de dinero y descuadraban el saldo de la cuenta.
        var swapLeg = Record(settledInCash: false);

        var restored = StagedPayload.Deserialize(StagedPayload.Serialize(swapLeg), swapLeg.RawContent);

        Assert.False(restored.SettledInCash);
    }

    [Fact]
    public void An_ordinary_movement_comes_back_settling_in_cash()
    {
        var purchase = Record(settledInCash: true);

        var restored = StagedPayload.Deserialize(StagedPayload.Serialize(purchase), purchase.RawContent);

        Assert.True(restored.SettledInCash);
    }

    [Fact]
    public void The_figures_survive_the_round_trip_untouched()
    {
        var purchase = Record(settledInCash: true);

        var restored = StagedPayload.Deserialize(StagedPayload.Serialize(purchase), purchase.RawContent);

        Assert.Equal(purchase.GrossAmount, restored.GrossAmount);
        Assert.Equal(purchase.Fee, restored.Fee);
        Assert.Equal(purchase.Quantity, restored.Quantity);
        Assert.Equal(purchase.NaturalId, restored.NaturalId);
    }

    private static ImportRecord Record(bool settledInCash) =>
        new(
            NaturalId: "op-0001:in",
            RowNumber: null,
            Type: TransactionType.Buy,
            AssetSymbol: "ETH",
            AssetClass: Kapea.Domain.Assets.AssetClass.Crypto,
            Quantity: 0.027m,
            UnitPrice: null,
            GrossAmount: 49.53m,
            Currency: Currency.Euro,
            Fee: 0.47m,
            Withholding: null,
            OccurredAt: new DateTimeOffset(2026, 4, 2, 18, 43, 0, TimeSpan.Zero),
            NaiveOccurredAt: null,
            SourceTimeZoneId: "Europe/Madrid",
            SplitRatio: null,
            RawContent: "{}",
            SettledInCash: settledInCash);
}
