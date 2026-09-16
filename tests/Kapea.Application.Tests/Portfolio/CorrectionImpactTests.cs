using Kapea.Application.Portfolio;

namespace Kapea.Application.Tests.Portfolio;

public class CorrectionImpactTests
{
    private static readonly DateOnly Today = new(2026, 9, 16);

    [Fact]
    public void A_date_of_a_past_year_warns() =>
        Assert.Equal(new CorrectionImpact(2025, true), CorrectionImpact.For(new DateOnly(2025, 6, 1), null, Today));

    [Fact]
    public void A_date_of_the_current_year_does_not_warn() =>
        Assert.False(CorrectionImpact.For(new DateOnly(2026, 2, 1), null, Today).IsPastYear);

    [Fact]
    public void The_first_of_january_belongs_to_the_current_year() =>
        Assert.False(CorrectionImpact.For(new DateOnly(2026, 1, 1), null, new DateOnly(2026, 1, 1)).IsPastYear);

    [Fact]
    public void The_thirty_first_of_december_of_last_year_is_past() =>
        Assert.True(CorrectionImpact.For(new DateOnly(2025, 12, 31), null, new DateOnly(2026, 1, 1)).IsPastYear);

    [Fact]
    public void Moving_a_date_warns_about_the_oldest_of_both()
    {
        var impact = CorrectionImpact.For(new DateOnly(2026, 3, 1), previous: new DateOnly(2024, 11, 2), Today);

        Assert.Equal(2024, impact.TaxYear);
        Assert.True(impact.IsPastYear);
    }

    [Fact]
    public void Moving_a_date_into_the_past_warns_about_the_new_one() =>
        Assert.Equal(2023, CorrectionImpact.For(new DateOnly(2023, 5, 5), previous: new DateOnly(2026, 3, 1), Today).TaxYear);
}
