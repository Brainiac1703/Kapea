using Kapea.Shared.Contracts;

namespace Kapea.Application.Strategies;

/// <summary>
/// Una propuesta de reglas a partir de una descripción en palabras.
/// </summary>
/// <param name="Rules">Las reglas propuestas, para revisar antes de guardar.</param>
/// <param name="NotUnderstood">
/// Lo que no se ha sabido traducir, dicho tal cual.
/// </param>
/// <param name="Confidence">Cuánta seguridad declara el traductor, entre cero y uno.</param>
/// <remarks>
/// Lo que no se entiende se señala en lugar de completarse. Una regla inventada para
/// rellenar un hueco parece buena y no lo es, que es justo el problema que ya se resolvió
/// así en el mapeo de importaciones.
/// </remarks>
public sealed record StrategyProposal(
    string Name,
    StrategyRulesRequest Rules,
    IReadOnlyList<string> NotUnderstood,
    double Confidence);

/// <summary>
/// Traduce a reglas un método descrito en palabras, y explica en castellano unas reglas.
/// </summary>
/// <remarks>
/// No emite señales, no fija umbrales por su cuenta y no toma imágenes: un modelo
/// estimando niveles sobre un dibujo da una respuesta distinta cada vez y no se puede
/// auditar ni simular, cuando los números exactos están en la base de datos.
/// </remarks>
public interface IStrategyTranslator
{
    /// <summary>Falso cuando no hay servicio configurado. Entonces todo se declara a mano.</summary>
    bool IsAvailable { get; }

    Task<StrategyProposal?> TranslateAsync(string description, CancellationToken cancellationToken = default);

    /// <summary>Qué dicen unas reglas, en castellano corriente.</summary>
    Task<string?> ExplainAsync(StrategyRulesRequest rules, CancellationToken cancellationToken = default);
}

/// <summary>
/// El traductor que no traduce nada.
/// </summary>
/// <remarks>
/// Se registra cuando no hay servicio configurado, en lugar de no registrar ninguno: así
/// el resto del código no pregunta si existe antes de cada llamada, y la aplicación
/// funciona igual declarando las reglas a mano.
/// </remarks>
public sealed class UnavailableStrategyTranslator : IStrategyTranslator
{
    public bool IsAvailable => false;

    public Task<StrategyProposal?> TranslateAsync(
        string description,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<StrategyProposal?>(null);

    public Task<string?> ExplainAsync(
        StrategyRulesRequest rules,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
