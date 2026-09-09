using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kapea.Application.Import;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Import.Kraken;

/// <summary>
/// Cliente de la API privada de Kraken. Firma cada petición, pagina hasta agotar el
/// histórico y espera de forma creciente cuando la plataforma señala exceso de uso.
/// </summary>
/// <remarks>
/// Kraken firma con HMAC-SHA512 sobre la ruta más el SHA-256 del nonce concatenado
/// con el cuerpo, y la clave secreta viaja en base64. El nonce tiene que crecer
/// siempre, y por eso se usa el reloj en milisegundos con un contador que evita
/// repetirlo si dos peticiones caen en el mismo milisegundo.
/// </remarks>
public sealed class KrakenApiClient(HttpClient httpClient, ILogger<KrakenApiClient> logger)
{
    internal const int PageSize = 50;
    internal const int MaximumRetries = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private long _lastNonce;

    /// <summary>Espera entre reintentos. Se expone para que los tests no tarden lo que tardaría en producción.</summary>
    internal Func<int, TimeSpan> BackoffDelay { get; set; } = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>Recorre TradesHistory paginando por desplazamiento hasta agotar el total que declara Kraken.</summary>
    public async Task<IReadOnlyList<KrakenTrade>> GetTradesAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var trades = new List<KrakenTrade>();
        var offset = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = await PostAsync(
                "0/private/TradesHistory",
                credential,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["start"] = Seconds(from),
                    ["end"] = Seconds(to),
                    ["ofs"] = offset.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken).ConfigureAwait(false);

            var result = document.RootElement.GetProperty("result");
            var page = result.TryGetProperty("trades", out var element) ? element : default;
            var count = result.TryGetProperty("count", out var total) ? total.GetInt32() : 0;
            var read = 0;

            if (page.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in page.EnumerateObject())
                {
                    trades.Add(KrakenTrade.From(entry.Name, entry.Value));
                    read++;
                }
            }

            offset += read;

            // Se para cuando la página viene vacía o ya se ha alcanzado el total que
            // declara Kraken; sin ese corte la paginación no terminaría nunca.
            if (read == 0 || offset >= count)
            {
                logger.LogInformation("Recuperadas {Total} operaciones de Kraken.", trades.Count);

                return trades;
            }
        }
    }

    /// <summary>Recorre Ledgers: ingresos, retiradas, comisiones y recompensas de staking.</summary>
    public async Task<IReadOnlyList<KrakenLedgerEntry>> GetLedgersAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<KrakenLedgerEntry>();
        var offset = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = await PostAsync(
                "0/private/Ledgers",
                credential,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["start"] = Seconds(from),
                    ["end"] = Seconds(to),
                    ["ofs"] = offset.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken).ConfigureAwait(false);

            var result = document.RootElement.GetProperty("result");
            var page = result.TryGetProperty("ledger", out var element) ? element : default;
            var count = result.TryGetProperty("count", out var total) ? total.GetInt32() : 0;
            var read = 0;

            if (page.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in page.EnumerateObject())
                {
                    entries.Add(KrakenLedgerEntry.From(entry.Name, entry.Value));
                    read++;
                }
            }

            offset += read;

            if (read == 0 || offset >= count)
            {
                logger.LogInformation("Recuperados {Total} apuntes de Kraken.", entries.Count);

                return entries;
            }
        }
    }

    private async Task<JsonDocument> PostAsync(
        string path,
        ApiCredential credential,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var nonce = NextNonce();
            var body = new Dictionary<string, string>(parameters, StringComparer.Ordinal)
            {
                ["nonce"] = nonce.ToString(CultureInfo.InvariantCulture),
            };

            var content = string.Join('&', body.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/x-www-form-urlencoded"),
            };

            request.Headers.Add("API-Key", credential.Key);
            request.Headers.Add("API-Sign", Sign("/" + path, nonce, content, credential.Secret));

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable
                || IsRateLimited(payload))
            {
                if (attempt >= MaximumRetries)
                {
                    throw new KrakenRateLimitException(attempt);
                }

                logger.LogWarning(
                    "Kraken señala exceso de peticiones; se reintenta el intento {Intento} de {Maximo}.",
                    attempt + 1, MaximumRetries);

                await Task.Delay(BackoffDelay(attempt), cancellationToken).ConfigureAwait(false);

                continue;
            }

            response.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(payload);
            var errors = ReadErrors(document);

            if (errors.Count > 0)
            {
                document.Dispose();

                throw new KrakenApiException(errors);
            }

            return document;
        }
    }

    private static bool IsRateLimited(string payload) =>
        payload.Contains("EAPI:Rate limit exceeded", StringComparison.Ordinal)
        || payload.Contains("EGeneral:Too many requests", StringComparison.Ordinal);

    private static IReadOnlyList<string> ReadErrors(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("error", out var errors) || errors.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. errors.EnumerateArray().Select(error => error.GetString() ?? string.Empty).Where(error => error.Length > 0)];
    }

    private static string Sign(string path, long nonce, string content, string secret)
    {
        var payload = SHA256.HashData(Encoding.UTF8.GetBytes(
            nonce.ToString(CultureInfo.InvariantCulture) + content));

        var message = Encoding.UTF8.GetBytes(path).Concat(payload).ToArray();

        using var hmac = new HMACSHA512(Convert.FromBase64String(secret));

        return Convert.ToBase64String(hmac.ComputeHash(message));
    }

    private long NextNonce()
    {
        var candidate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var previous = Interlocked.Read(ref _lastNonce);
        var nonce = candidate > previous ? candidate : previous + 1;

        Interlocked.Exchange(ref _lastNonce, nonce);

        return nonce;
    }

    private static string Seconds(DateTimeOffset moment) =>
        moment.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    internal static JsonSerializerOptions SerializerOptions => JsonOptions;
}

/// <summary>Kraken ha devuelto errores propios en el cuerpo, aunque el código HTTP fuera 200.</summary>
public sealed class KrakenApiException(IReadOnlyList<string> errors)
    : InvalidOperationException($"Kraken ha rechazado la petición: {string.Join("; ", errors)}."),
    Kapea.Application.Synchronization.IInvalidCredentialSignal
{
    public IReadOnlyList<string> Errors { get; } = errors;

    /// <summary>Una credencial inválida o revocada en la plataforma, que obliga a desactivarla.</summary>
    public bool IsInvalidCredential => Errors.Any(error =>
        error.Contains("EAPI:Invalid key", StringComparison.Ordinal)
        || error.Contains("EAPI:Invalid signature", StringComparison.Ordinal)
        || error.Contains("EGeneral:Permission denied", StringComparison.Ordinal));
}

/// <summary>Se han agotado los reintentos frente al límite de frecuencia de Kraken.</summary>
public sealed class KrakenRateLimitException(int attempts)
    : InvalidOperationException(
        $"Kraken sigue rechazando por exceso de peticiones tras {attempts} reintentos; la importación queda fallida.");
