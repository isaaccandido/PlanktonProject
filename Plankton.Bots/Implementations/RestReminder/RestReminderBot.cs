using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.Bots.Utils;
using Plankton.DataAccess.Enums;

namespace Plankton.Bots.Implementations.RestReminder;

public class RestReminderBot(BotWebTools botWebTools) : IBot
{
    public string Name => "RestReminder";
    public DataAccessType StateStorage => DataAccessType.InMemory;

    public BotSettingsModel Settings { get; set; } = new()
    {
        Enabled = true,
        RunInterval = TimeSpan.FromDays(1),
        MaxFailures = 3,
        RestartDelay = TimeSpan.FromSeconds(10)
    };

    private static string NotificationUrl => "https://ntfy.sh/Godofredo";

    private readonly HashSet<string> _sentToday = [];
    private DateOnly _currentDay = DateOnly.FromDateTime(DateTime.Now);

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now;

            ResetIfNewDay(now);

            if (!IsWorkday(now))
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
                continue;
            }

            await HandleBreakReminder(now, ct);
            await HandleBackFromBreak(now, ct);
            await HandleLunch(now, ct);
            await HandleBackFromLunch(now, ct);
            await HandleEndOfDay(now, ct);

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task HandleBreakReminder(DateTime now, CancellationToken ct)
    {
        if (!IsWorkHour(now)) return;
        if (now.Hour == 11) return;           // no break right before lunch
        if (now.Minute < 50) return;

        await SendOncePerHour(
            "break",
            now,
            "Take a break! Back in 10.",
            ct
        );
    }

    private async Task HandleBackFromBreak(DateTime now, CancellationToken ct)
    {
        if (!IsWorkHour(now)) return;
        if (now.Hour == 12) return;           // never during lunch
        if (now.Minute != 0) return;

        await SendOncePerHour(
            "back-from-break",
            now,
            "Back to work, but take it easy!",
            ct
        );
    }

    private async Task HandleLunch(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 12 || now.Minute != 0) return;

        await SendOncePerHour(
            "lunch",
            now,
            "Go eat.",
            ct
        );
    }

    private async Task HandleBackFromLunch(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 13 || now.Minute != 0) return;

        await SendOncePerHour(
            "back-from-lunch",
            now,
            "I hope you've enjoyed your lunch! Take it easy!",
            ct
        );
    }

    private async Task HandleEndOfDay(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 18 || now.Minute != 0) return;

        await SendOncePerHour(
            "end-of-day",
            now,
            "Go rest! Eat something, read something, play something.",
            ct
        );
    }

    private async Task SendOncePerHour(
        string key,
        DateTime now,
        string message,
        CancellationToken ct
    )
    {
        var sentKey = $"{_currentDay}:{key}:{now.Hour}";
        if (_sentToday.Contains(sentKey)) return;

        await SendMessage(message, ct);
        _sentToday.Add(sentKey);
    }

    private void ResetIfNewDay(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (today == _currentDay) return;

        _currentDay = today;
        _sentToday.Clear();
    }

    private static bool IsWorkday(DateTime now) =>
        now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static bool IsWorkHour(DateTime now) =>
        now.Hour is >= 8 and < 18;

    private async Task SendMessage(string message, CancellationToken ct)
    {
        try
        {
            await botWebTools.SendAsync<Dictionary<string, object>>(
                HttpMethod.Post,
                Name,
                NotificationUrl,
                message,
                ct
            );
        }
        catch
        {
            // intentionally ignored
        }
    }
}
