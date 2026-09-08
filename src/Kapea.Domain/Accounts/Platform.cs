namespace Kapea.Domain.Accounts;

/// <summary>
/// Plataforma de la que proceden los movimientos. Es la clave que selecciona el
/// adaptador de importación, así que añadir una plataforma empieza siempre aquí.
/// </summary>
public enum Platform
{
    Xtb = 1,
    Kraken = 2,
    Bit2Me = 3,
}
