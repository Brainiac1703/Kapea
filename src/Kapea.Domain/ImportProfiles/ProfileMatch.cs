using Kapea.Domain.Accounts;

namespace Kapea.Domain.ImportProfiles;

/// <summary>Resultado de buscar el perfil con el que leer un fichero.</summary>
/// <param name="Profile">El perfil elegido, o nulo si ninguno reconoce esas cabeceras.</param>
/// <param name="Ambiguous">Varios perfiles encajaban. Se eligió uno, y conviene decirlo.</param>
/// <param name="Candidates">Todos los que encajaban, para poder explicarlo.</param>
public sealed record ProfileMatch(
    ImportProfile? Profile,
    bool Ambiguous,
    IReadOnlyList<ImportProfile> Candidates)
{
    public static ProfileMatch None { get; } = new(null, Ambiguous: false, []);

    public bool Found => Profile is not null;
}

/// <summary>Empareja un fichero con el perfil que sabe leerlo, por sus cabeceras.</summary>
public static class ProfileMatching
{
    /// <summary>
    /// Elige el perfil de una plataforma que reconoce estas cabeceras.
    /// </summary>
    /// <remarks>
    /// Con varios candidatos gana el que declara más cabeceras, y a igualdad el más
    /// reciente. El más específico es el que menos probablemente esté acertando por
    /// casualidad; la fecha solo desempata.
    ///
    /// La ambigüedad se devuelve en lugar de rechazarse: el fichero se puede importar
    /// igual, y decir con cuál se leyó permite corregirlo si se eligió mal.
    /// </remarks>
    public static ProfileMatch Match(
        IEnumerable<ImportProfile> profiles,
        PlatformCode platform,
        IReadOnlyCollection<string> headers)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(headers);

        var candidates = profiles
            .Where(profile => profile.Platform == platform && profile.Matches(headers))
            .OrderByDescending(profile => profile.Current.RecognizedHeaders.Count)
            .ThenByDescending(profile => profile.Current.CreatedAt)
            .ToList();

        return candidates.Count == 0
            ? ProfileMatch.None
            : new ProfileMatch(candidates[0], candidates.Count > 1, candidates);
    }
}
