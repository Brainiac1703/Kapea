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
    public Task<IReadOnlyList<AuthProviderResponse>> GetAuthProvidersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<AuthProviderResponse>("auth/providers", cancellationToken);

    public Task<IReadOnlyList<PlatformResponse>> GetPlatformsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<PlatformResponse>("api/platforms", cancellationToken);

    public async Task<PlatformResponse> CreatePlatformAsync(
        CreatePlatformRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/platforms", request, cancellationToken);

        return await ReadAsync<PlatformResponse>(response, cancellationToken);
    }

    public async Task DeletePlatformAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/platforms/{Uri.EscapeDataString(code)}", cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<FileInspectionResponse> InspectFileAsync(
        Guid accountId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(content), "file", fileName },
        };

        var response = await http.PostAsync(
            $"api/profiles/inspect?accountId={accountId}", form, cancellationToken);

        return await ReadAsync<FileInspectionResponse>(response, cancellationToken);
    }

    public async Task<MappingProposalResponse> ProposeMappingAsync(
        string platform,
        MappingSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            $"api/profiles/propose?platform={Uri.EscapeDataString(platform)}", request, cancellationToken);

        return await ReadAsync<MappingProposalResponse>(response, cancellationToken);
    }

    public async Task<TransactionPageResponse> SearchTransactionsAsync(
        Guid? accountId = null,
        string? asset = null,
        string? type = null,
        int? year = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };

        if (accountId is { } id)
        {
            query.Add($"accountId={id}");
        }

        if (!string.IsNullOrWhiteSpace(asset))
        {
            query.Add($"asset={Uri.EscapeDataString(asset)}");
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query.Add($"type={Uri.EscapeDataString(type)}");
        }

        if (year is { } chosen)
        {
            query.Add($"year={chosen}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        var response = await http.GetAsync("api/transactions/search?" + string.Join('&', query), cancellationToken);

        return await ReadAsync<TransactionPageResponse>(response, cancellationToken);
    }

    /// <param name="full">Relee el histórico entero en lugar de pedir solo lo nuevo.</param>
    public async Task<SynchronizationResponse> SynchroniseAsync(
        bool full = false,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync(full ? "api/sync?full=true" : "api/sync", content: null, cancellationToken);

        return await ReadAsync<SynchronizationResponse>(response, cancellationToken);
    }

    public async Task<ReinterpretationResponse> ReinterpretAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync("api/transactions/reinterpret", content: null, cancellationToken);

        return await ReadAsync<ReinterpretationResponse>(response, cancellationToken);
    }

    public Task<IReadOnlyList<ImportProfileResponse>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<ImportProfileResponse>("api/profiles", cancellationToken);

    public Task<IReadOnlyList<ImportFieldResponse>> GetImportFieldsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<ImportFieldResponse>("api/profiles/fields", cancellationToken);

    public async Task<ImportProfileResponse> CreateProfileAsync(
        CreateImportProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/profiles", request, cancellationToken);

        return await ReadAsync<ImportProfileResponse>(response, cancellationToken);
    }

    public async Task<ImportProfileResponse> ReviseProfileAsync(
        Guid profileId,
        ReviseImportProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/profiles/{profileId}/versions", request, cancellationToken);

        return await ReadAsync<ImportProfileResponse>(response, cancellationToken);
    }

    public async Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/profiles/{profileId}", cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

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

    /// <param name="days">Cuántos días atrás. Sin valor, el que decida el servidor.</param>
    public async Task<PortfolioHistoryResponse> GetHistoryAsync(
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(Range("api/portfolio/history", days), cancellationToken);

        return await ReadAsync<PortfolioHistoryResponse>(response, cancellationToken);
    }

    public async Task<AssetHistoryResponse> GetAssetHistoryAsync(
        Guid assetId,
        int? days = null,
        int? window = null,
        CancellationToken cancellationToken = default)
    {
        var path = Range($"api/portfolio/history/{assetId}", days);
        var query = window is { } size ? $"{path}{(path.Contains('?', StringComparison.Ordinal) ? "&" : "?")}window={size}" : path;

        var response = await http.GetAsync(query, cancellationToken);

        return await ReadAsync<AssetHistoryResponse>(response, cancellationToken);
    }

    public async Task<PerformanceResponse> GetPerformanceAsync(
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(Range("api/portfolio/performance", days), cancellationToken);

        return await ReadAsync<PerformanceResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<StrategyResponse>> ListStrategiesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/strategies", cancellationToken);

        return await ReadAsync<List<StrategyResponse>>(response, cancellationToken);
    }

    public async Task<StrategyVocabularyResponse> GetStrategyVocabularyAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/strategies/vocabulary", cancellationToken);

        return await ReadAsync<StrategyVocabularyResponse>(response, cancellationToken);
    }

    public async Task<StrategyResponse> CreateStrategyAsync(
        CreateStrategyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/strategies", request, cancellationToken);

        return await ReadAsync<StrategyResponse>(response, cancellationToken);
    }

    public async Task<StrategyResponse> ReviseStrategyAsync(
        Guid strategyId,
        ReviseStrategyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            $"api/strategies/{strategyId}/versions", request, cancellationToken);

        return await ReadAsync<StrategyResponse>(response, cancellationToken);
    }

    public async Task<StrategyProposalResponse> TranslateStrategyAsync(
        string description,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            "api/strategies/translate", new TranslateStrategyRequest(description), cancellationToken);

        return await ReadAsync<StrategyProposalResponse>(response, cancellationToken);
    }

    public async Task<SignalRunResponse> RunStrategiesAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync("api/strategies/run", content: null, cancellationToken);

        return await ReadAsync<SignalRunResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<SignalResponse>> ListSignalsAsync(
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(
            days is { } span ? $"api/strategies/signals?days={span}" : "api/strategies/signals", cancellationToken);

        return await ReadAsync<List<SignalResponse>>(response, cancellationToken);
    }

    public async Task<BacktestResponse> SimulateAsync(
        Guid strategyId,
        Guid assetId,
        decimal capital,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync(
            $"api/strategies/{strategyId}/simulate?assetId={assetId}&capital={capital.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            content: null,
            cancellationToken);

        return await ReadAsync<BacktestResponse>(response, cancellationToken);
    }

    /// <summary>Acota el periodo contando hacia atrás desde hoy.</summary>
    private static string Range(string path, int? days) => days is { } span
        ? $"{path}?from={DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-span):yyyy-MM-dd}"
        : path;

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

    /// <summary>
    /// Quién tiene la sesión. Devuelve null si no hay ninguna, en lugar de fallar: no
    /// estar autenticado no es un error, es el estado inicial de cualquier visita.
    /// </summary>
    public async Task<CurrentUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync("api/me", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return await ReadAsync<CurrentUserResponse>(response, cancellationToken);
    }

    public async Task<CurrentUserResponse> UnlinkIdentityAsync(
        Guid identityId,
        CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/me/identities/{identityId}", cancellationToken);

        return await ReadAsync<CurrentUserResponse>(response, cancellationToken);
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
