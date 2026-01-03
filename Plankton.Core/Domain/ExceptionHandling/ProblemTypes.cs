namespace Plankton.Core.Domain.ExceptionHandling;

public static class ProblemTypes
{
    private const string Base = "https://plankton.dev/problems";

    // ─── Command errors ──────────────────────────────────────────────
    public const string InvalidCommand = Base + "/invalid-command";
    public const string MissingHandler = Base + "/missing-handler";

    // ─── Authorization / security ────────────────────────────────────
    public const string Unauthorized = Base + "/unauthorized";
    public const string Forbidden = Base + "/forbidden";
    public const string RateLimited = Base + "/rate-limited";

    // ─── Bot lifecycle ────────────────────────────────────────────────
    public const string BotNotFound = Base + "/bot-not-found";
    public const string BotAlreadyRunning = Base + "/bot-already-running";

    // ─── Infrastructure ───────────────────────────────────────────────
    public const string InternalError = Base + "/internal-error";
    public const string ServiceUnavailable = Base + "/service-unavailable";
}