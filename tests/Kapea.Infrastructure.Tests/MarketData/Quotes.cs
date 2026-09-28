using Kapea.Application.Abstractions;

namespace Kapea.Infrastructure.Tests.MarketData;

/// <summary>
/// Atajo para las pruebas que sólo miran símbolos.
/// </summary>
/// <remarks>
/// La mayoría de los casos no tienen nada que ver con el identificador del proveedor y
/// se escribían con una lista de símbolos. Escribirlos ahora activo a activo los llenaría
/// de ruido sin decir nada nuevo; las pruebas que sí van del identificador construyen sus
/// propios activos.
/// </remarks>
internal static class Quotes
{
    internal static QuotedAsset[] Quoted(params string[] symbols) =>
        [.. symbols.Select(symbol => new QuotedAsset(symbol))];
}
