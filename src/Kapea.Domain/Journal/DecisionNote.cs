using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Journal;

/// <summary>
/// Por qué se decidió lo que se decidió.
/// </summary>
/// <remarks>
/// Va aparte del movimiento y no dentro: un movimiento importado no se toca, y su
/// correspondencia con el registro de origen es lo que permite defender una cifra. Anotar
/// no puede alterar ni sus importes ni su huella de duplicado.
///
/// Se puede anotar sobre un movimiento o sobre una señal. Sobre una señal que no se
/// siguió es donde más se aprende: dentro de seis meses, saber por qué no se hizo vale
/// más que la señal misma.
/// </remarks>
public sealed class DecisionNote
{
    private DecisionNote()
    {
        Text = null!;
    }

    private DecisionNote(UserId userId, Guid? transactionId, Guid? signalId, string text, DateTimeOffset writtenAt)
    {
        UserId = userId;
        TransactionId = transactionId;
        SignalId = signalId;
        Text = text;
        WrittenAt = writtenAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public UserId UserId { get; private set; }

    /// <summary>Movimiento al que acompaña, si acompaña a uno.</summary>
    public Guid? TransactionId { get; private set; }

    /// <summary>Señal a la que acompaña, si acompaña a una.</summary>
    public Guid? SignalId { get; private set; }

    public string Text { get; private set; }

    /// <summary>
    /// Cuándo se escribió.
    /// </summary>
    /// <remarks>
    /// No es la fecha del movimiento: leer meses después una nota escrita en caliente
    /// junto a otra escrita con perspectiva es justamente lo que enseña algo.
    /// </remarks>
    public DateTimeOffset WrittenAt { get; private set; }

    public static DecisionNote ForTransaction(
        UserId userId,
        Guid transactionId,
        string text,
        DateTimeOffset writtenAt) =>
        new(userId, transactionId, null, Clean(text), writtenAt);

    public static DecisionNote ForSignal(UserId userId, Guid signalId, string text, DateTimeOffset writtenAt) =>
        new(userId, null, signalId, Clean(text), writtenAt);

    /// <summary>Corrige el texto. La fecha de escritura no cambia: es cuándo se pensó eso.</summary>
    public void Rewrite(string text) => Text = Clean(text);

    private static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Una anotación sin texto no explica nada.");
        }

        return text.Trim();
    }
}
