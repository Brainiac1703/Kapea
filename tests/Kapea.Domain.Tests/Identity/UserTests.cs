using Kapea.Domain.Common;
using Kapea.Domain.Identity;

namespace Kapea.Domain.Tests.Identity;

public class UserTests
{
    private const string Google = "Google";
    private const string Apple = "Apple";
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_user_gets_an_identifier_of_its_own()
    {
        // El identificador no puede salir del proveedor: si los datos colgaran de él,
        // enlazar otro obligaría a reasignar todo el histórico.
        var user = User.Register(Google, "115784...", "Nacho", "nacho@ejemplo.com", Now);

        Assert.NotEqual(Guid.Empty, user.Id.Value);
        Assert.NotEqual("115784...", user.Id.Value.ToString());
    }

    [Fact]
    public void Registering_links_the_identity_that_created_the_user()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", "nacho@ejemplo.com", Now);

        var identity = Assert.Single(user.Identities);

        Assert.Equal(Google, identity.Provider);
        Assert.Equal("sujeto-1", identity.Subject);
        Assert.Equal(Now, identity.LinkedAt);
    }

    [Fact]
    public void The_email_is_kept_for_display_and_normalised()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", "  Nacho@Ejemplo.COM ", Now);

        Assert.Equal("nacho@ejemplo.com", user.Email);
    }

    [Fact]
    public void A_user_without_display_name_falls_back_to_the_email()
    {
        var user = User.Register(Google, "sujeto-1", null, "nacho@ejemplo.com", Now);

        Assert.Equal("nacho@ejemplo.com", user.DisplayName);
    }

    [Fact]
    public void A_user_without_name_or_email_still_has_something_to_show()
    {
        // Apple permite ocultar el correo, así que puede no llegar ninguno de los dos.
        var user = User.Register(Apple, "sujeto-1", null, null, Now);

        Assert.False(string.IsNullOrWhiteSpace(user.DisplayName));
        Assert.Null(user.Email);
    }

    [Theory]
    [InlineData("", "sujeto")]
    [InlineData("   ", "sujeto")]
    [InlineData("Google", "")]
    [InlineData("Google", "   ")]
    public void An_identity_without_provider_or_subject_is_rejected(string provider, string subject) =>
        Assert.Throws<DomainException>(() => ExternalIdentity.Link(provider, subject, Now));

    [Fact]
    public void An_identity_is_matched_by_provider_and_subject()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);

        Assert.NotNull(user.FindIdentity(Google, "sujeto-1"));
        Assert.Null(user.FindIdentity(Google, "otro-sujeto"));
        Assert.Null(user.FindIdentity(Apple, "sujeto-1"));
    }

    [Fact]
    public void The_provider_is_matched_regardless_of_letter_case()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);

        Assert.NotNull(user.FindIdentity("google", "sujeto-1"));
    }

    [Fact]
    public void The_subject_is_matched_exactly()
    {
        // El sujeto es opaco: dos que solo difieren en mayúsculas son distintos.
        var user = User.Register(Google, "Sujeto-1", "Nacho", null, Now);

        Assert.Null(user.FindIdentity(Google, "sujeto-1"));
    }

    [Fact]
    public void Linking_another_provider_gives_a_second_way_in()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);

        user.LinkIdentity(Apple, "sujeto-apple", Now.AddDays(1));

        Assert.Equal(2, user.Identities.Count);
        Assert.NotNull(user.FindIdentity(Apple, "sujeto-apple"));
        Assert.NotNull(user.FindIdentity(Google, "sujeto-1"));
    }

    [Fact]
    public void Linking_the_same_identity_twice_is_rejected()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);

        Assert.Throws<DomainException>(() => user.LinkIdentity(Google, "sujeto-1", Now));
    }

    [Fact]
    public void Unlinking_the_only_identity_is_refused()
    {
        // Sin contraseña propia, quitar la última identidad deja al usuario sin ninguna
        // forma de volver a entrar en su propio histórico.
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);
        var identity = user.Identities[0];

        var exception = Assert.Throws<DomainException>(() => user.UnlinkIdentity(identity.Id));

        Assert.Contains("única forma de acceder", exception.Message, StringComparison.Ordinal);
        Assert.Single(user.Identities);
    }

    [Fact]
    public void Unlinking_one_of_two_identities_is_allowed()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);
        var apple = user.LinkIdentity(Apple, "sujeto-apple", Now);

        user.UnlinkIdentity(apple.Id);

        Assert.Single(user.Identities);
        Assert.NotNull(user.FindIdentity(Google, "sujeto-1"));
    }

    [Fact]
    public void Unlinking_an_identity_of_another_user_is_refused()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);

        Assert.Throws<DomainException>(() => user.UnlinkIdentity(Guid.NewGuid()));
    }

    [Fact]
    public void Signing_in_records_the_moment_on_the_user_and_on_the_identity()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);
        var later = Now.AddDays(30);

        user.RecordSignIn(user.Identities[0], "Nacho", null, later);

        Assert.Equal(later, user.LastSignedInAt);
        Assert.Equal(later, user.Identities[0].LastSignedInAt);
    }

    [Fact]
    public void A_changed_email_at_the_provider_updates_what_is_shown()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", "viejo@ejemplo.com", Now);

        user.RecordSignIn(user.Identities[0], "Nacho", "nuevo@ejemplo.com", Now.AddDays(1));

        Assert.Equal("nuevo@ejemplo.com", user.Email);
        Assert.Equal(user.Id, user.Id);
    }

    [Fact]
    public void A_hidden_email_does_not_erase_the_one_already_known()
    {
        // Apple puede dejar de enviar el correo en accesos posteriores.
        var user = User.Register(Apple, "sujeto-1", "Nacho", "nacho@ejemplo.com", Now);

        user.RecordSignIn(user.Identities[0], null, null, Now.AddDays(1));

        Assert.Equal("nacho@ejemplo.com", user.Email);
        Assert.Equal("Nacho", user.DisplayName);
    }

    [Fact]
    public void The_internal_identifier_survives_linking_and_unlinking()
    {
        var user = User.Register(Google, "sujeto-1", "Nacho", null, Now);
        var original = user.Id;

        var apple = user.LinkIdentity(Apple, "sujeto-apple", Now);
        user.UnlinkIdentity(apple.Id);

        Assert.Equal(original, user.Id);
    }
}
