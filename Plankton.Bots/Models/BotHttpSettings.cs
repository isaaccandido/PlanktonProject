using System.Net;

namespace Plankton.Bots.Models;

public class BotHttpSettings
{
    public string? BaseUrl { get; set; }
    public string? BearerToken { get; init; }
    public NetworkCredential? BasicAuth { get; init; }
    public Dictionary<string, string>? CustomHeaders { get; init; }
    public int RetryCount { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 2;
    public int CircuitBreakerFailures { get; init; } = 5;
    public int CircuitBreakerDurationSeconds { get; init; } = 15;
}