using System.Net;
using System.Net.Http.Json;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class JournalEndpointsTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task A_note_on_a_movement_is_stored_without_touching_its_figures()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Con diario", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await SeedTransactionAsync(user, account!.Id);

        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?accountId={account.Id}");

        var transaction = Assert.Single(transactions!);

        var created = await client.PostAsJsonAsync(
            "/api/journal", new WriteNoteRequest(transaction.Id, null, "Entré porque venía de un ingreso."));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Las cifras del movimiento siguen intactas: anotar no lo edita.
        var after = await client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?accountId={account.Id}");

        Assert.Equal(transaction.GrossAmount, Assert.Single(after!).GrossAmount);
    }

    [Fact]
    public async Task The_journal_says_what_the_note_was_about()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Con repaso", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await SeedTransactionAsync(user, account!.Id);

        var transaction = (await client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?accountId={account.Id}"))![0];

        await client.PostAsJsonAsync(
            "/api/journal", new WriteNoteRequest(transaction.Id, null, "Un ingreso para empezar."));

        var journal = await client.GetFromJsonAsync<List<DecisionNoteResponse>>("/api/journal");

        var note = Assert.Single(journal!);

        Assert.Equal("Un ingreso para empezar.", note.Text);
        Assert.Contains("Deposit", note.About!, StringComparison.Ordinal);
        Assert.NotNull(note.Outcome);
    }

    [Fact]
    public async Task A_note_that_accompanies_nothing_is_rejected()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/journal", new WriteNoteRequest(null, null, "Sin destino."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_note_is_rejected()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/journal", new WriteNoteRequest(Guid.NewGuid(), null, "   "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_journal_of_a_new_user_comes_back_empty_rather_than_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        Assert.Empty(await client.GetFromJsonAsync<List<DecisionNoteResponse>>("/api/journal") ?? []);
    }

    private async Task SeedTransactionAsync(Guid user, Guid accountId)
    {
        await using var context = factory.CreateContext(user);

        context.Transactions.Add(Domain.Transactions.Transaction.Imported(
            new Domain.ValueObjects.UserId(user),
            accountId,
            Domain.Transactions.TransactionType.Deposit,
            null,
            Domain.ValueObjects.Quantity.Zero,
            null,
            Domain.ValueObjects.Money.Euros(100m),
            Domain.ValueObjects.Money.Euros(0m),
            Domain.Transactions.Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"),
            Domain.Transactions.TransactionSource.FromImport(
                Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString())));

        await context.SaveChangesAsync();
    }
}
