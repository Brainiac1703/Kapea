using System.Net;
using System.Net.Http.Json;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Services;

/// <summary>
/// Acceso a la API desde el cliente. Es la única puerta: el navegador no habla con
/// ninguna plataforma de inversión ni ve credencial alguna.
/// </summary>
public sealed class KapeaApiClient(HttpClient http)
{
    public Task<IReadOnlyList<AccountResponse>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<AccountResponse>("api/accounts", cancellationToken);

    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/accounts", request, cancellationToken);

        return await ReadAsync<AccountResponse>(response, cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/accounts/{accountId}", cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<BrokerCredentialResponse>> GetCredentialsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<BrokerCredentialResponse>("api/credentials", cancellationToken);

    public async Task<BrokerCredentialResponse> RegisterCredentialAsync(
        RegisterBrokerCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/credentials", request, cancellationToken);

        return await ReadAsync<BrokerCredentialResponse>(response, cancellationToken);
    }

    public async Task<BrokerCredentialResponse> RotateCredentialAsync(
        Guid credentialId,
        RotateBrokerCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/credentials/{credentialId}", request, cancellationToken);

        return await ReadAsync<BrokerCredentialResponse>(response, cancellationToken);
    }

    public async Task<BrokerCredentialResponse> RevokeCredentialAsync(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/credentials/{credentialId}", cancellationToken);

        return await ReadAsync<BrokerCredentialResponse>(response, cancellationToken);
    }

    public Task<IReadOnlyList<ImportRunResponse>> GetImportRunsAsync(
        Guid? accountId = null,
        CancellationToken cancellationToken = default) =>
        GetListAsync<ImportRunResponse>(
            accountId is null ? "api/imports" : $"api/imports?accountId={accountId}", cancellationToken);

    /// <summary>
    /// Sube el fichero y devuelve la vista previa. No persiste ningún movimiento: hasta
    /// que el usuario confirma, lo único que existe es la clasificación de los registros.
    /// </summary>
    public async Task<ImportPreviewResponse> UploadImportFileAsync(
        Guid accountId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);

        form.Add(file, "file", fileName);

        var response = await http.PostAsync($"api/imports/file?accountId={accountId}", form, cancellationToken);

        return await ReadAsync<ImportPreviewResponse>(response, cancellationToken);
    }

    public async Task<ImportRunResponse> ConfirmImportAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/imports/{runId}/confirm", content: null, cancellationToken);

        return await ReadAsync<ImportRunResponse>(response, cancellationToken);
    }

    public async Task DiscardImportAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/imports/{runId}/discard", content: null, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteImportAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/imports/{runId}", cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<TransactionResponse>> GetTransactionsAsync(
        Guid? accountId = null,
        bool onlyRequiringReview = false,
        CancellationToken cancellationToken = default) =>
        GetListAsync<TransactionResponse>(
            $"api/transactions?requiresReview={onlyRequiringReview}"
                + (accountId is null ? string.Empty : $"&accountId={accountId}"),
            cancellationToken);

    public Task<IReadOnlyList<InternalTransferResponse>> GetTransfersAsync(
        bool onlyPending = true,
        CancellationToken cancellationToken = default) =>
        GetListAsync<InternalTransferResponse>($"api/transfers?onlyPending={onlyPending}", cancellationToken);

    /// <summary>Vuelve a buscar traspasos. Solo propone: la decisión sigue siendo del usuario.</summary>
    public async Task<IReadOnlyList<InternalTransferResponse>> DetectTransfersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync("api/transfers/detect", content: null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<InternalTransferResponse>>(cancellationToken) ?? [];
    }

    public async Task ResolveTransferAsync(
        Guid transferId,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        var action = confirm ? "confirm" : "reject";
        var response = await http.PostAsync($"api/transfers/{transferId}/{action}", content: null, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PortfolioResponse> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/portfolio", cancellationToken);

        return await ReadAsync<PortfolioResponse>(response, cancellationToken);
    }

    public async Task<PortfolioResponse> RecalculatePortfolioAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync("api/portfolio/recalculate", content: null, cancellationToken);

        return await ReadAsync<PortfolioResponse>(response, cancellationToken);
    }

    public async Task<TaxYearResultsResponse> GetTaxYearResultsAsync(
        int taxYear,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/results/{taxYear}", cancellationToken);

        return await ReadAsync<TaxYearResultsResponse>(response, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string url, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new KapeaApiException("La API ha respondido sin contenido.", response.StatusCode);
    }

    /// <summary>
    /// Convierte el error de la API en algo que se le pueda enseñar al usuario. La API
    /// responde ProblemDetails con el motivo real —una invariante del dominio, un
    /// rechazo de la plataforma— y perderlo dejaría un "algo ha fallado" inútil.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;

        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken)
                .ConfigureAwait(false);

            detail = string.IsNullOrWhiteSpace(problem?.Detail) ? problem?.Title : problem.Detail;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // La respuesta no era ProblemDetails; queda el código de estado.
        }

        throw new KapeaApiException(detail ?? $"La API ha respondido {(int)response.StatusCode}.", response.StatusCode);
    }

    private sealed record ProblemDetailsResponse(string? Title, string? Detail, int? Status);
}

/// <summary>Error de la API con el motivo que dio, ya listo para enseñar.</summary>
public sealed class KapeaApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
