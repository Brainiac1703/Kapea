using Kapea.Domain.Common;

namespace Kapea.Domain.Identity;

/// <summary>
/// Identidad de una persona en un proveedor externo.
/// </summary>
/// <remarks>
/// La identidad es el par proveedor y sujeto, nunca el correo. Un proveedor puede
/// entregar un correo enmascarado o distinto del real, y dos proveedores pueden
/// entregar el mismo: identificar por correo dejaría entrar a quien controle esa
/// dirección en otro proveedor.
/// </remarks>
public sealed class ExternalIdentity
{
    private ExternalIdentity()
    {
        Provider = null!;
        Subject = null!;
    }

    private ExternalIdentity(Guid id, string provider, string subject, DateTimeOffset linkedAt)
    {
        Id = id;
        Provider = provider;
        Subject = subject;
        LinkedAt = linkedAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Proveedor que emitió la identidad. Es un dato, para poder añadir uno nuevo sin tocar el modelo.</summary>
    public string Provider { get; private set; }

    /// <summary>Identificador de sujeto que emite el proveedor. Estable a lo largo del tiempo.</summary>
    public string Subject { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    public DateTimeOffset? LastSignedInAt { get; private set; }

    public static ExternalIdentity Link(string provider, string subject, DateTimeOffset linkedAt)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainException("Una identidad externa necesita saber de qué proveedor viene.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainException("Una identidad externa necesita el identificador de sujeto del proveedor.");
        }

        return new ExternalIdentity(Guid.NewGuid(), provider.Trim(), subject.Trim(), linkedAt);
    }

    public void RecordSignIn(DateTimeOffset signedInAt) => LastSignedInAt = signedInAt;

    public bool Matches(string provider, string subject) =>
        string.Equals(Provider, provider?.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(Subject, subject?.Trim(), StringComparison.Ordinal);
}
