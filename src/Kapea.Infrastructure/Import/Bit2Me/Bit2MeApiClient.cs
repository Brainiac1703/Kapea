using System.Globalization;
using System.Net;
using System.Text.Json;
using Kapea.Application.Import;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Import.Bit2Me;

/// <summary>
/// Cliente de la API de Bit2Me. Firma cada petición, recorre la paginación —por
/// desplazamiento en contado y por cursor en el monedero— y espera de forma creciente
/// cuando la plataforma señala exceso de uso.
/// </summary>
public sealed class Bit2MeApiClient(HttpClient httpClient, ILogger<Bit2MeApiClient> logger)
{
    internal const int PageSize = 100;
    internal const int MaximumRetries = 5;
    internal const int MaximumPages = 1000;

    /// <summary>Espera entre reintentos. Se sustituye en los tests para que no esperen de verdad.</summary>
    internal Func<int, TimeSpan> BackoffDelay { get; set; } = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>Operaciones de contado. Pagina por desplazamiento hasta alcanzar el total declarado.</summary>
    public async Task<IReadOnlyList<Bit2MeTrade>> GetTradesAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var trades = new List<Bit2MeTrade>();
        var offset = 0;

        for (var page = 0; page < MaximumPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = "/v1/trading/trade?" + Query(
                ("limit", PageSize.ToString(CultureInfo.InvariantCulture)),
                ("offset", offset.ToString(CultureInfo.InvariantCulture)),
                ("startTime", from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
                ("endTime", to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)));

            using var document = await GetAsync(url, credential, cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var count = root.TryGetProperty("count", out var total) && total.TryGetInt32(out var parsed) ? parsed : 0;
            var read = 0;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in data.EnumerateArray())
                {
                    trades.Add(Bit2MeTrade.From(element));
                    read++;
                }
            }

            offset += read;

            if (read == 0 || offset >= count)
            {
                logger.LogInformation("Recuperadas {Total} operaciones de contado de Bit2Me.", trades.Count);

                return trades;
            }
        }

        throw new Bit2MePaginationException("/v1/trading/trade");
    }

    /// <summary>Movimientos del monedero. Pagina por cursor opaco hasta que la plataforma dice que no hay más.</summary>
    public async Task<IReadOnlyList<Bit2MeWalletTransaction>> GetWalletTransactionsAsync(
        ApiCredential credential,
        CancellationToken cancellationToken = default)
    {
        var transactions = new List<Bit2MeWalletTransaction>();
        string? cursor = null;

        for (var page = 0; page < MaximumPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new List<(string, string)>
            {
                ("limit", PageSize.ToString(CultureInfo.InvariantCulture)),
            };

            if (cursor is { Length: > 0 })
            {
                parameters.Add(("cursor", cursor));
            }

            var url = "/v3/wallet/transaction?" + Query([.. parameters]);

            using var document = await GetAsync(url, credential, cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in data.EnumerateArray())
                {
                    transactions.Add(Bit2MeWalletTransaction.From(element));
                }
            }

            var hasNext = root.TryGetProperty("pageInfo", out var pageInfo)
                && pageInfo.TryGetProperty("hasNextPage", out var next)
                && next.ValueKind == JsonValueKind.True;

            cursor = hasNext && pageInfo.TryGetProperty("endCursor", out var end) ? end.GetString() : null;

            if (!hasNext || string.IsNullOrEmpty(cursor))
            {
                logger.LogInformation("Recuperados {Total} movimientos de monedero de Bit2Me.", transactions.Count);

                return transactions;
            }
        }

        throw new Bit2MePaginationException("/v3/wallet/transaction");
    }

    /// <summary>Monederos de rendimiento del usuario.</summary>
    public async Task<IReadOnlyList<Bit2MeEarnWallet>> GetEarnWalletsAsync(
        ApiCredential credential,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetAsync("/v2/earn/wallets", credential, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. document.RootElement.EnumerateArray()
                .Select(element => new Bit2MeEarnWallet(
                    Bit2MeJson.String(element, "walletId"),
                    Bit2MeJson.String(element, "currency")))
                .Where(wallet => wallet.WalletId.Length > 0),
        ];
    }

    /// <summary>Movimientos de un monedero de rendimiento concreto.</summary>
    public async Task<IReadOnlyList<Bit2MeEarnMovement>> GetEarnMovementsAsync(
        ApiCredential credential,
        string walletId,
        CancellationToken cancellationToken = default)
    {
        var movements = new List<Bit2MeEarnMovement>();
        var offset = 0;

        for (var page = 0; page < MaximumPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = $"/v1/earn/wallets/{Uri.EscapeDataString(walletId)}/movements?" + Query(
                ("limit", PageSize.ToString(CultureInfo.InvariantCulture)),
                ("offset", offset.ToString(CultureInfo.InvariantCulture)));

            using var document = await GetAsync(url, credential, cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var total = root.TryGetProperty("total", out var count) && count.TryGetInt32(out var parsed) ? parsed : 0;
            var read = 0;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in data.EnumerateArray())
                {
                    movements.Add(Bit2MeEarnMovement.From(element));
                    read++;
                }
            }

            offset += read;

            if (read == 0 || offset >= total)
            {
                return movements;
            }
        }

        throw new Bit2MePaginationException("/v1/earn/wallets/{walletId}/movements");
    }

    private async Task<JsonDocument> GetAsync(string url, ApiCredential credential, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            // El nonce viaja en milisegundos, como en el ejemplo de la documentación,
            // y tiene una ventana de validez de cinco segundos.
            var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Add("x-api-key", credential.Key);
            request.Headers.Add("x-nonce", nonce.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add("api-signature", Bit2MeSignature.Compute(nonce, url, body: null, credential.Secret));

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                if (attempt >= MaximumRetries)
                {
                    throw new Bit2MeRateLimitException(attempt);
                }

                logger.LogWarning(
                    "Bit2Me señala exceso de peticiones; se reintenta el intento {Intento} de {Maximo}.",
                    attempt + 1, MaximumRetries);

                await Task.Delay(BackoffDelay(attempt), cancellationToken).ConfigureAwait(false);

                continue;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new Bit2MeAccessDeniedException(url, response.StatusCode);
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return JsonDocument.Parse(payload);
        }
    }

    private static string Query(params (string Name, string Value)[] parameters) =>
        string.Join('&', parameters.Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value)}"));
}

/// <summary>La credencial no puede leer ese recurso: o está revocada, o le falta el ámbito.</summary>
public sealed class Bit2MeAccessDeniedException(string url, HttpStatusCode status)
    : InvalidOperationException($"Bit2Me ha denegado el acceso a {url} ({(int)status})."),
    Kapea.Application.Synchronization.IInvalidCredentialSignal
{
    public string Url { get; } = url;

    public HttpStatusCode Status { get; } = status;

    /// <summary>Un 401 apunta a credencial inválida; un 403, a falta de permisos sobre ese producto.</summary>
    public bool IsInvalidCredential => Status == HttpStatusCode.Unauthorized;
}

/// <summary>Se han agotado los reintentos frente al límite de frecuencia.</summary>
public sealed class Bit2MeRateLimitException(int attempts)
    : InvalidOperationException(
        $"Bit2Me sigue rechazando por exceso de peticiones tras {attempts} reintentos; la importación queda fallida.");

/// <summary>
/// La paginación no ha terminado en un número razonable de páginas. Se corta con error
/// en lugar de girar indefinidamente: una respuesta que nunca dice que ha acabado es
/// un fallo de la plataforma, no un histórico muy largo.
/// </summary>
public sealed class Bit2MePaginationException(string endpoint)
    : InvalidOperationException($"La paginación de {endpoint} no ha terminado tras {Bit2MeApiClient.MaximumPages} páginas.");
