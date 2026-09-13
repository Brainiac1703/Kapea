using Kapea.Application.Import;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;

namespace Kapea.Application.Tests.Import;

public class MappingSampleTests
{
    [Fact]
    public void A_sample_never_carries_more_than_three_rows()
    {
        // Un extracto es un dato personal. Para deducir qué columna es la fecha no hace
        // falta ver el año entero, y lo que no sale no se puede filtrar.
        var sample = new MappingSample(
            ["Fecha", "Importe"],
            [.. Enumerable.Range(1, 50).Select(day => (IReadOnlyList<string>)[$"{day:00}/03/2026", "10,00"])]);

        Assert.Equal(MappingSample.MaximumRows, sample.Rows.Count);
    }

    [Fact]
    public void A_sample_with_fewer_rows_keeps_them_all()
    {
        var sample = new MappingSample(["Fecha"], [["01/03/2026"]]);

        Assert.Single(sample.Rows);
    }
}

public class MappingReviewTests
{
    private const double Threshold = 0.8;

    [Fact]
    public void A_proposal_that_agrees_with_the_sample_needs_no_confirmation()
    {
        var review = MappingReview.Review(Proposal(), Sample(), Threshold);

        Assert.True(review.IsConclusive);
        Assert.Empty(review.Doubts);
    }

    [Fact]
    public void A_missing_required_field_is_asked_about()
    {
        var proposal = Proposal(fields:
        [
            new FieldProposal(ImportField.Date, "Fecha", 0.99),
            new FieldProposal(ImportField.Concept, "Concepto", 0.99),
        ]);

        var doubt = Assert.Single(MappingReview.Review(proposal, Sample(), Threshold).Doubts);

        Assert.Equal(MappingDoubt.RequiredFieldMissing, doubt.Doubt);
        Assert.Contains("el importe", doubt.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Confidence_below_the_threshold_is_asked_about()
    {
        var proposal = Proposal(fields:
        [
            new FieldProposal(ImportField.Date, "Fecha", 0.99),
            new FieldProposal(ImportField.GrossAmount, "Importe", 0.4),
            new FieldProposal(ImportField.Concept, "Concepto", 0.99),
        ]);

        var doubt = Assert.Single(MappingReview.Review(proposal, Sample(), Threshold).Doubts);

        Assert.Equal(MappingDoubt.LowConfidence, doubt.Doubt);
    }

    [Fact]
    public void The_threshold_is_a_setting_and_not_a_constant()
    {
        var proposal = Proposal(fields:
        [
            new FieldProposal(ImportField.Date, "Fecha", 0.6),
            new FieldProposal(ImportField.GrossAmount, "Importe", 0.6),
            new FieldProposal(ImportField.Concept, "Concepto", 0.99),
        ]);

        Assert.False(MappingReview.Review(proposal, Sample(), confidenceThreshold: 0.8).IsConclusive);
        Assert.True(MappingReview.Review(proposal, Sample(), confidenceThreshold: 0.5).IsConclusive);
    }

    [Fact]
    public void A_date_column_that_holds_no_dates_is_refused_however_sure_the_proposal_is()
    {
        // El error más caro y más probable: dos columnas parecidas confundidas. La
        // seguridad declarada no lo detecta, porque un modelo puede estar seguro y
        // equivocado; contrastar con las filas sí.
        var proposal = Proposal(fields:
        [
            new FieldProposal(ImportField.Date, "Importe", 1.0),
            new FieldProposal(ImportField.GrossAmount, "Importe", 1.0),
            new FieldProposal(ImportField.Concept, "Concepto", 1.0),
        ]);

        var doubt = Assert.Single(MappingReview.Review(proposal, Sample(), Threshold).Doubts);

        Assert.Equal(MappingDoubt.SampleDisagrees, doubt.Doubt);
        Assert.Contains("no se lee como fecha", doubt.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void An_amount_column_that_holds_no_numbers_is_refused()
    {
        var proposal = Proposal(fields:
        [
            new FieldProposal(ImportField.Date, "Fecha", 1.0),
            new FieldProposal(ImportField.GrossAmount, "Concepto", 1.0),
            new FieldProposal(ImportField.Concept, "Concepto", 1.0),
        ]);

        var doubts = MappingReview.Review(proposal, Sample(), Threshold).Doubts;

        Assert.Contains(doubts, doubt => doubt.Doubt == MappingDoubt.SampleDisagrees
            && doubt.Explanation.Contains("no se lee como número", StringComparison.Ordinal));
    }

    [Fact]
    public void The_wrong_decimal_convention_is_caught_by_the_sample()
    {
        // «1.234,56» leído con punto decimal no es un número. Sin contrastar, el perfil
        // entraría y produciría cifras equivocadas en cada fila.
        var proposal = Proposal(convention: DecimalConvention.Invariant);

        var sample = new MappingSample(
            ["Fecha", "Concepto", "Importe"],
            [["01/03/2026", "Dividendo", "1.234,56"]]);

        Assert.Contains(
            MappingReview.Review(proposal, sample, Threshold).Doubts,
            doubt => doubt.Doubt == MappingDoubt.SampleDisagrees);
    }

    [Fact]
    public void A_concept_in_the_sample_that_the_proposal_does_not_translate_is_asked_about()
    {
        var proposal = Proposal(concepts: []);

        var doubt = Assert.Single(MappingReview.Review(proposal, Sample(), Threshold).Doubts);

        Assert.Equal(MappingDoubt.UntranslatedConcept, doubt.Doubt);
        Assert.Contains("Dividendo", doubt.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_positions_proposal_needs_its_own_fields()
    {
        var proposal = Proposal(
            fields: [new FieldProposal(ImportField.Date, "Fecha", 1.0)],
            rowShape: RowShape.OpenAndClosePosition);

        var doubts = MappingReview.Review(proposal, Sample(), Threshold).Doubts;

        Assert.Contains(doubts, doubt => doubt.Explanation.Contains("la fecha de apertura", StringComparison.Ordinal));
        Assert.Contains(doubts, doubt => doubt.Explanation.Contains("el precio de cierre", StringComparison.Ordinal));
    }

    private static MappingSample Sample() =>
        new(
            ["Fecha", "Concepto", "Importe"],
            [
                ["01/03/2026", "Dividendo", "45,20"],
                ["02/03/2026", "Dividendo", "1.234,56"],
            ]);

    private static MappingProposal Proposal(
        IReadOnlyList<FieldProposal>? fields = null,
        IReadOnlyList<ConceptProposal>? concepts = null,
        DecimalConvention convention = DecimalConvention.European,
        RowShape rowShape = RowShape.SingleMovement) =>
        new(
            fields ??
            [
                new FieldProposal(ImportField.Date, "Fecha", 0.99),
                new FieldProposal(ImportField.Concept, "Concepto", 0.95),
                new FieldProposal(ImportField.GrossAmount, "Importe", 0.97),
            ],
            concepts ?? [new ConceptProposal("Dividendo", TransactionType.Dividend, 0.9)],
            convention,
            ["dd/MM/yyyy"],
            rowShape,
            AmountSource.Column,
            ';',
            "EUR");
}
