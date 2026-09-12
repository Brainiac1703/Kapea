using System.Text.Json;
using Kapea.Domain.Strategies;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kapea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Guarda un árbol de condiciones como JSON.
/// </summary>
/// <remarks>
/// Las reglas se consultan enteras y nunca por partes, así que desmenuzarlas en tablas
/// solo añadiría uniones para volver a juntarlas al leer.
/// </remarks>
internal sealed class ConditionConverter() : ValueConverter<Condition?, string?>(
    condition => StrategyJson.Write(condition),
    stored => StrategyJson.ReadCondition(stored));

/// <summary>Guarda un nivel como JSON, por lo mismo que las condiciones.</summary>
internal sealed class LevelConverter() : ValueConverter<Level?, string?>(
    level => StrategyJson.Write(level),
    stored => StrategyJson.ReadLevel(stored));

internal static class StrategyJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static string? Write(Condition? condition) =>
        condition == null ? null : JsonSerializer.Serialize(condition, Options);

    internal static string? Write(Level? level) =>
        level == null ? null : JsonSerializer.Serialize(level, Options);

    internal static Condition? ReadCondition(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? null : JsonSerializer.Deserialize<Condition>(stored, Options);

    internal static Level? ReadLevel(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? null : JsonSerializer.Deserialize<Level>(stored, Options);
}
