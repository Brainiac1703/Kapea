namespace Kapea.Client.Models;

/// <summary>Alta de una plataforma que no viene de serie.</summary>
/// <remarks>
/// Solo de fichero. Una plataforma de API necesita además su adaptador, que es código,
/// y ofrecerla aquí prometería algo que después no se puede usar.
/// </remarks>
public sealed class NewPlatformModel
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsComplete => !string.IsNullOrWhiteSpace(Code);
}
