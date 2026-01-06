using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using Plankton.Bots.Models;

namespace Plankton.Bots.Utils;

public sealed class BotWebTools(
    ILogger<BotWebTools> logger,
    IOptions<BotsHttpSettings> options,
    HttpClient? httpClient = null)
{
    private readonly BotsHttpSettings _settings = options.Value;
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly ConcurrentDictionary<string, AsyncPolicy<HttpResponseMessage>> _policies = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyTokens = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<T?> SendAsync<T>(
        HttpMethod method,
        string botId,
        string? url = null,
        object? body = null,
        CancellationToken ct = default)
    {
        var settings = GetSettingsForBot(botId);

        var targetUrl = url ?? settings.BaseUrl;
        if (string.IsNullOrWhiteSpace(targetUrl))
            throw new ArgumentException($"No URL provided for bot '{botId}'.");

        using var request = new HttpRequestMessage(method, targetUrl);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        ApplyHeaders(request, settings);
        ApplyIdempotencyToken(request, botId, targetUrl);

        var policyKey = $"{botId}:{new Uri(targetUrl).Host}";
        var policy = _policies.GetOrAdd(policyKey, _ => CreatePolicy(policyKey, settings));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("[{BotId}] Sending {Method} request to {Url}", botId, method, targetUrl);

        var response = await policy.ExecuteAsync(token => _httpClient.SendAsync(request, token), ct);

        sw.Stop();
        
        logger.LogInformation(
            "[{BotId}] Received {StatusCode} from {Url} in {ElapsedMs}ms",
            botId, response.StatusCode, targetUrl, sw.ElapsedMilliseconds);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[{BotId}] Response body: {Body}", botId, responseJson);

        return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
    }

    public void ResetIdempotencyToken(string botId, string? url = null)
    {
        var key = $"{botId}:{url}";
        _idempotencyTokens.TryRemove(key, out _);
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
            BaseUrl = botSettings.BaseUrl ?? _settings.Default.BaseUrl,
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

    private AsyncPolicy<HttpResponseMessage> CreatePolicy(string key, BotHttpSettings settings)
    {
        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                settings.RetryCount,
                _ => TimeSpan.FromSeconds(settings.RetryDelaySeconds),
                (resp, _, retryCount, _) =>
                    logger.LogWarning("[{Key}] Retry {RetryCount} due to {Reason}", key, retryCount, resp.Exception?.Message ?? resp.Result?.StatusCode.ToString())
            );

        var circuitBreaker = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                settings.CircuitBreakerFailures,
                TimeSpan.FromSeconds(settings.CircuitBreakerDurationSeconds),
                (_, _) => logger.LogWarning("[{Key}] Circuit breaker open", key),
                () => logger.LogInformation("[{Key}] Circuit breaker reset", key),
                () => logger.LogInformation("[{Key}] Circuit breaker half-open", key)
            );

        return Policy.WrapAsync(retryPolicy, circuitBreaker);
    }

    private void ApplyHeaders(HttpRequestMessage request, BotHttpSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BearerToken) && settings.BasicAuth is not null)
            throw new InvalidOperationException("Cannot use both BearerToken and BasicAuth for the same bot request.");

        if (!string.IsNullOrWhiteSpace(settings.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken);

        if (settings.BasicAuth is { UserName: not null, Password: not null })
        {
            var value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.BasicAuth.UserName}:{settings.BasicAuth.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", value);
        }

        if (settings.CustomHeaders is null) return;
        
        foreach (var kv in settings.CustomHeaders)
        {
            request.Headers.Remove(kv.Key);
            request.Headers.Add(kv.Key, kv.Value);
        }
    }

    private void ApplyIdempotencyToken(HttpRequestMessage request, string botId, string url)
    {
        if (request.Method != HttpMethod.Post) return;

        var key = $"{botId}:{url}";
        var token = _idempotencyTokens.GetOrAdd(key, _ => Guid.NewGuid().ToString("N"));

        request.Headers.Remove("Idempotency-Key");
        request.Headers.Add("Idempotency-Key", token);
    }
}