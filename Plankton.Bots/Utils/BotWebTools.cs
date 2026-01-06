using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using Plankton.Bots.Models;

namespace Plankton.Bots.Utils;

public sealed class BotWebTools(ILogger<BotWebTools> logger, IOptions<BotsHttpSettings> options)
{
    private readonly HttpClient _httpClient = new();
    private readonly BotsHttpSettings _settings = options.Value;

    private readonly ConcurrentDictionary<string, AsyncPolicy<HttpResponseMessage>> _policies = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyTokens = new();

    public async Task<T?> SendAsync<T>(
        string url,
        HttpMethod method,
        string botId,
        object? body = null,
        CancellationToken ct = default
    )
    {
        var settings = GetSettingsForBot(botId);
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        ApplyHeaders(request, settings);
        ApplyIdempotencyToken(request, botId);

        var host = request.RequestUri!.Host;
        var policy = _policies.GetOrAdd(host, _ => CreatePolicy(host, settings));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("[{BotId}] Sending {Method} request to {Url}", botId, method, url);

        var response = await policy.ExecuteAsync(token => _httpClient.SendAsync(request, token), ct);

        sw.Stop();
        logger.LogInformation(
            "[{BotId}] Received {StatusCode} from {Url} in {ElapsedMs}ms",
            botId,
            response.StatusCode,
            url,
            sw.ElapsedMilliseconds
        );

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[{BotId}] Response body: {Body}", botId, responseJson);

        return JsonSerializer.Deserialize<T>(responseJson);
    }

    public void ResetIdempotencyToken(string botId)
    {
        _idempotencyTokens.TryRemove(botId, out _);
    }

    private BotHttpSettings GetSettingsForBot(string botId)
    {
        return _settings.Bots.TryGetValue(botId, out var botSettings)
            ? MergeWithDefault(botSettings)
            : _settings.Default;
    }

    private BotHttpSettings MergeWithDefault(BotHttpSettings botSettings)
    {
        return new BotHttpSettings
        {
            BearerToken = botSettings.BearerToken ?? _settings.Default.BearerToken,
            BasicAuth = botSettings.BasicAuth ?? _settings.Default.BasicAuth,
            CustomHeaders = botSettings.CustomHeaders ?? _settings.Default.CustomHeaders,
            RetryCount = botSettings.RetryCount != 0
                ? botSettings.RetryCount
                : _settings.Default.RetryCount,
            RetryDelaySeconds = botSettings.RetryDelaySeconds != 0
                ? botSettings.RetryDelaySeconds
                : _settings.Default.RetryDelaySeconds,
            CircuitBreakerFailures = botSettings.CircuitBreakerFailures != 0
                ? botSettings.CircuitBreakerFailures
                : _settings.Default.CircuitBreakerFailures,
            CircuitBreakerDurationSeconds = botSettings.CircuitBreakerDurationSeconds != 0
                ? botSettings.CircuitBreakerDurationSeconds
                : _settings.Default.CircuitBreakerDurationSeconds
        };
    }

    private AsyncPolicy<HttpResponseMessage> CreatePolicy(string host, BotHttpSettings settings)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                settings.RetryCount,
                _ => TimeSpan.FromSeconds(settings.RetryDelaySeconds),
                (resp, _, retryCount, _) =>
                    logger.LogWarning(
                        "[{Host}] Retry {RetryCount} due to {Reason}",
                        host,
                        retryCount,
                        resp.Exception?.Message ?? resp.Result?.StatusCode.ToString()
                    )
            );

        var circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                settings.CircuitBreakerFailures,
                TimeSpan.FromSeconds(settings.CircuitBreakerDurationSeconds),
                (_, _) => logger.LogWarning("[{Host}] Circuit breaker open", host),
                () => logger.LogInformation("[{Host}] Circuit breaker reset", host),
                () => logger.LogInformation("[{Host}] Circuit breaker half-open", host)
            );

        return Policy.WrapAsync(retryPolicy, circuitBreaker);
    }

    private void ApplyHeaders(HttpRequestMessage request, BotHttpSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken);

        if (settings.BasicAuth is { UserName: not null, Password: not null })
        {
            var value = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{settings.BasicAuth.UserName}:{settings.BasicAuth.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", value);
        }

        if (settings.CustomHeaders is not null)
            foreach (var kv in settings.CustomHeaders)
            {
                request.Headers.Remove(kv.Key);
                request.Headers.Add(kv.Key, kv.Value);
            }
    }

    private void ApplyIdempotencyToken(HttpRequestMessage request, string botId)
    {
        if (request.Method != HttpMethod.Post) return;

        var token = _idempotencyTokens.GetOrAdd(botId, _ => Guid.NewGuid().ToString("N"));
        request.Headers.Remove("Idempotency-Key");
        request.Headers.Add("Idempotency-Key", token);
    }
}