namespace Kapea.Domain.Accounts;

/// <summary>Forma en la que llegan los movimientos de una plataforma.</summary>
public enum PlatformImportKind
{
    /// <summary>Fichero que sube el usuario, porque la plataforma no ofrece API de histórico.</summary>
    File = 1,

    /// <summary>API remota que se consulta desde el servidor con una credencial de solo lectura.</summary>
    Api = 2,
}
