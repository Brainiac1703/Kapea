using Kapea.Client.Models;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Tests;

public class ManualMovementModelTests
{
    private static readonly Guid Account = Guid.NewGuid();

    [Fact]
    public void A_buy_needs_asset_and_quantity()
    {
        var model = new ManualMovementModel { AccountId = Account, Type = "Buy", GrossAmount = 20m };

        Assert.Contains("Common_Asset", model.Missing);
        Assert.Contains("Common_Quantity", model.Missing);
    }

    [Fact]
    public void A_deposit_needs_neither_asset_nor_quantity()
    {
        var model = new ManualMovementModel { AccountId = Account, Type = "Deposit", GrossAmount = 500m };

        Assert.True(model.IsComplete);
    }

    [Fact]
    public void A_new_movement_does_not_need_a_note()
    {
        var model = Complete(ManualMovementMode.New);

        Assert.True(model.IsComplete);
    }

    [Fact]
    public void A_correction_needs_its_reason()
    {
        var model = Complete(ManualMovementMode.Correct);

        Assert.Contains("Movement_Reason", model.Missing);

        model.Text = "la cantidad venía mal";
        Assert.True(model.IsComplete);
    }

    [Fact]
    public void The_request_carries_the_day_at_noon_so_no_offset_moves_it_to_another_day()
    {
        var model = Complete(ManualMovementMode.New);
        model.Date = new DateTime(2025, 1, 1);

        var request = model.ToRequest();

        Assert.Equal(new DateOnly(2025, 1, 1), DateOnly.FromDateTime(request.OccurredAt.DateTime));
        Assert.Equal(12, request.OccurredAt.Hour);
        Assert.Equal(ManualMovementModel.TimeZoneId, request.TimeZoneId);
    }

    [Fact]
    public void A_blank_note_travels_as_no_note()
    {
        var model = Complete(ManualMovementMode.New);
        model.Text = "   ";

        Assert.Null(model.ToRequest().Text);
    }

    [Fact]
    public void A_movement_without_asset_sends_no_asset_class_nor_quantity()
    {
        var model = new ManualMovementModel { AccountId = Account, Type = "Deposit", GrossAmount = 500m, Quantity = 3m, AssetClass = "Equity" };

        var request = model.ToRequest();

        Assert.Null(request.AssetClass);
        Assert.Equal(0m, request.Quantity);
    }

    [Fact]
    public void Editing_keeps_the_note_and_correcting_starts_without_a_reason()
    {
        var movement = new TransactionResponse(
            Guid.NewGuid(), Account, Guid.NewGuid(), "BTC", "Buy", 2m, 10m, 20m, "EUR", 0m,
            new DateTimeOffset(2025, 3, 4, 12, 0, 0, TimeSpan.Zero), "Europe/Madrid", MovementOrigins.Manual, false,
            null, null, null, null, null, null, false, Note: "en papel");

        Assert.Equal("en papel", ManualMovementModel.From(movement, ManualMovementMode.Edit, []).Text);
        Assert.Empty(ManualMovementModel.From(movement, ManualMovementMode.Correct, []).Text);
        Assert.Equal(new DateOnly(2025, 3, 4), ManualMovementModel.From(movement, ManualMovementMode.Edit, []).OriginalDate);
    }

    private static ManualMovementModel Complete(ManualMovementMode mode) =>
        new()
        {
            Mode = mode,
            AccountId = Account,
            Type = "Buy",
            AssetSymbol = "BTC",
            Quantity = 2m,
            GrossAmount = 20m,
        };
}
