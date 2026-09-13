using Kapea.Domain.Accounts;
using Kapea.Domain.Common;

namespace Kapea.Domain.Tests.Accounts;

public class PlatformTests
{
    [Fact]
    public void A_platform_that_exports_files_needs_nothing_but_a_name_and_its_kind()
    {
        // Es la razón de ser del cambio: dar de alta un bróker que exporta CSV no
        // debería exigir compilar nada.
        var platform = Platform.Create(new PlatformCode("DeGiro"), "DeGiro", PlatformImportKind.File);

        Assert.Equal("DeGiro", platform.Code.Value);
        Assert.Equal(PlatformImportKind.File, platform.ImportKind);
        Assert.False(platform.BuiltIn);
    }

    [Fact]
    public void Without_a_name_the_code_is_the_name()
    {
        var platform = Platform.Create(new PlatformCode("DeGiro"), "  ", PlatformImportKind.File);

        Assert.Equal("DeGiro", platform.Name);
    }

    [Fact]
    public void A_built_in_platform_cannot_be_retired()
    {
        var platform = Platform.BuiltInPlatform(PlatformCode.Xtb, "XTB", PlatformImportKind.File);

        var exception = Assert.Throws<DomainException>(() => platform.EnsureCanBeDeleted(accountCount: 0));

        Assert.Contains("de serie", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_platform_with_accounts_cannot_be_retired()
    {
        var platform = Platform.Create(new PlatformCode("DeGiro"), "DeGiro", PlatformImportKind.File);

        var exception = Assert.Throws<DomainException>(() => platform.EnsureCanBeDeleted(accountCount: 2));

        Assert.Contains("2 cuentas", exception.Message, StringComparison.Ordinal);
    }
}

public class PlatformCodeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_code_cannot_be_empty(string value) =>
        Assert.Throws<DomainException>(() => new PlatformCode(value));

    [Theory]
    [InlineData("De Giro")]
    [InlineData("De-Giro")]
    [InlineData("Bróker")]
    [InlineData("De|Giro")]
    public void A_code_admits_only_letters_and_digits(string value)
    {
        // Va dentro de la huella de deduplicación y de la clave del secreto: un
        // separador ahí podría hacer que dos plataformas produjeran la misma cadena.
        var exception = Assert.Throws<DomainException>(() => new PlatformCode(value));

        Assert.Contains("letras y dígitos", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_code_longer_than_the_column_is_rejected() =>
        Assert.Throws<DomainException>(() => new PlatformCode(new string('a', PlatformCode.MaxLength + 1)));

    [Fact]
    public void Two_codes_that_differ_only_in_case_are_the_same_platform()
    {
        Assert.Equal(new PlatformCode("xtb"), PlatformCode.Xtb);
        Assert.Equal(new PlatformCode("xtb").GetHashCode(), PlatformCode.Xtb.GetHashCode());
    }

    [Fact]
    public void A_code_keeps_the_shape_it_was_given()
    {
        // Se compara sin distinguir mayúsculas, pero lo que se lee en pantalla es lo
        // que se escribió al darla de alta.
        Assert.Equal("DeGiro", new PlatformCode("  DeGiro  ").Value);
    }
}
