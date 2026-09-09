using Kapea.Application.Identity;
using Kapea.Domain.Accounts;
using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence.Stores;
using Kapea.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.Identity;

[Collection(SqlServerCollection.Name)]
public class UserRepositoryTests(SqlServerFixture fixture)
{
    private const string Google = "Google";
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_first_sign_in_creates_the_user()
    {
        var subject = NewSubject();

        var result = await SignInAsync(new ExternalPrincipal(Google, subject, "Nacho", "nacho@ejemplo.com"));

        Assert.Equal(SignInOutcome.Registered, result.Outcome);
        Assert.Equal("Nacho", result.User.DisplayName);
    }

    [Fact]
    public async Task A_second_sign_in_recognises_the_same_user()
    {
        var subject = NewSubject();
        var first = await SignInAsync(new ExternalPrincipal(Google, subject, "Nacho", "nacho@ejemplo.com"));

        var second = await SignInAsync(new ExternalPrincipal(Google, subject, "Nacho", "nacho@ejemplo.com"));

        Assert.Equal(SignInOutcome.SignedIn, second.Outcome);
        Assert.Equal(first.User.Id, second.User.Id);
    }

    [Fact]
    public async Task A_changed_email_still_resolves_to_the_same_user()
    {
        // La identidad es el sujeto. Buscar por correo dejaría entrar a quien controle
        // esa dirección en otro proveedor.
        var subject = NewSubject();
        var first = await SignInAsync(new ExternalPrincipal(Google, subject, "Nacho", "viejo@ejemplo.com"));

        var second = await SignInAsync(new ExternalPrincipal(Google, subject, "Nacho", "nuevo@ejemplo.com"));

        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Equal("nuevo@ejemplo.com", second.User.Email);
    }

    [Fact]
    public async Task Two_people_with_different_subjects_are_two_users()
    {
        var one = await SignInAsync(new ExternalPrincipal(Google, NewSubject(), "Uno", "compartido@ejemplo.com"));
        var other = await SignInAsync(new ExternalPrincipal(Google, NewSubject(), "Otro", "compartido@ejemplo.com"));

        Assert.NotEqual(one.User.Id, other.User.Id);
    }

    [Fact]
    public async Task An_identity_cannot_belong_to_two_users()
    {
        var subject = NewSubject();
        await SignInAsync(new ExternalPrincipal(Google, subject, "Uno", null));
        var other = await SignInAsync(new ExternalPrincipal(Google, NewSubject(), "Otro", null));

        await Assert.ThrowsAsync<IdentityAlreadyLinkedException>(() => LinkAsync(
            other.User.Id, new ExternalPrincipal(Google, subject, "Otro", null)));
    }

    [Fact]
    public async Task A_linked_identity_lets_the_same_user_in()
    {
        var user = await SignInAsync(new ExternalPrincipal(Google, NewSubject(), "Nacho", null));
        var appleSubject = NewSubject();

        await LinkAsync(user.User.Id, new ExternalPrincipal("Apple", appleSubject, "Nacho", null));

        var again = await SignInAsync(new ExternalPrincipal("Apple", appleSubject, "Nacho", null));

        Assert.Equal(SignInOutcome.SignedIn, again.Outcome);
        Assert.Equal(user.User.Id, again.User.Id);
    }

    [Fact]
    public async Task The_database_refuses_the_same_identity_twice()
    {
        var subject = NewSubject();
        var owner = await SignInAsync(new ExternalPrincipal(Google, subject, "Uno", null));

        await using var context = fixture.CreateContext(owner.User.Id);
        var stranger = User.Register(Google, subject, "Otro", null, Now);
        context.Users.Add(stranger);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Ownership_of_existing_data_can_be_reassigned()
    {
        // Los movimientos importados antes de haber usuarios están a nombre del usuario
        // fijo de desarrollo; la adopción los traslada sin perderlos.
        var previous = new UserId(Guid.NewGuid());
        var account = PlatformAccount.Create(previous, Platform.Kraken, "Cuenta previa", Currency.Euro);

        await using (var seed = fixture.CreateContext(previous))
        {
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
        }

        var adopter = await SignInAsync(new ExternalPrincipal(Google, NewSubject(), "Nacho", null));

        await using (var context = fixture.CreateContext(adopter.User.Id))
        {
            var moved = await new UserRepository(context).ReassignOwnershipAsync(previous, adopter.User.Id);

            Assert.True(moved > 0);
        }

        await using var reader = fixture.CreateContext(adopter.User.Id);

        Assert.Contains(await reader.Accounts.ToListAsync(), stored => stored.Id == account.Id);
    }

    private static string NewSubject() => Guid.NewGuid().ToString("N");

    private async Task<SignInResult> SignInAsync(ExternalPrincipal principal)
    {
        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));

        return await Service(context).SignInAsync(principal);
    }

    private async Task<ExternalIdentity> LinkAsync(UserId userId, ExternalPrincipal principal)
    {
        await using var context = fixture.CreateContext(userId);

        return await Service(context).LinkAsync(userId, principal);
    }

    private static UserSignInService Service(Infrastructure.Persistence.KapeaDbContext context) =>
        new(new UserRepository(context), new FakeTimeProvider(Now), NullLogger<UserSignInService>.Instance);
}
