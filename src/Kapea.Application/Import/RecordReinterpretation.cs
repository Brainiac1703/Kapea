using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>
/// Vuelve a leer el contenido original de un movimiento con las reglas de hoy.
/// </summary>
/// <remarks>
/// Cada movimiento importado guarda el texto tal y como llegó, así que reinterpretarlo
/// no exige volver a pedir nada a la plataforma. Sirve cuando un adaptador aprende a
/// entender algo que antes no entendía, que es lo que pasa cada vez que aparece una
/// forma nueva en una API ajena.
/// </remarks>
public interface IRecordReinterpreter
{
    PlatformCode Platform { get; }

    /// <summary>Devuelve nulo si este contenido no se sabe leer: no todo lo importado viene de una API.</summary>
    ImportRecord? Reinterpret(string rawContent);
}

/// <param name="Reclassified">Movimientos que han pasado a tener un tipo.</param>
/// <param name="StillUnknown">Los que se han vuelto a leer y siguen sin significar nada conocido.</param>
/// <param name="NotSupported">Los que no se pueden reinterpretar porque su origen no lo permite.</param>
public sealed record ReinterpretationResult(int Reclassified, int StillUnknown, int NotSupported)
{
    public int Total => Reclassified + StillUnknown + NotSupported;
}
