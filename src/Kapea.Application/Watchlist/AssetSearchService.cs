using Kapea.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Watchlist;

/// <summary>
/// Busca activos preguntando a todos los proveedores a la vez.
/// </summary>
/// <remarks>
/// Se pregunta a los dos sin que el usuario elija antes el tipo: quien busca «cardano»
/// no tiene por qué saber que es una criptomoneda, y quien busca «ServiceNow» tampoco
/// que cotiza en Nueva York.
///
/// Un proveedor que no responde no vacía la pantalla: se devuelve lo del otro y se dice
/// que la búsqueda está incompleta, que es la misma regla que ya sigue la descarga de
/// precios.
/// </remarks>
public sealed class AssetSearchService(
    IEnumerable<IAssetSearchProvider> providers,
    ILogger<AssetSearchService> logger)
{
    public async Task<AssetSearch> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AssetSearch.Empty;
        }

        var searches = providers
            .Select(provider => SafeSearchAsync(provider, text, cancellationToken))
            .ToList();

        var answers = await Task.WhenAll(searches).ConfigureAwait(false);

        return new AssetSearch(
            [.. answers.SelectMany(answer => answer.Results)],
            answers.All(answer => answer.Answered));
    }

    private async Task<(IReadOnlyList<AssetSearchResult> Results, bool Answered)> SafeSearchAsync(
        IAssetSearchProvider provider,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await provider.SearchAsync(text, cancellationToken).ConfigureAwait(false), true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "El proveedor '{Proveedor}' no ha podido buscar.", provider.Name);

            return ([], false);
        }
    }
}
