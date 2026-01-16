namespace Plankton.Bots.Implementations.RestReminder;

public record Reminder(
    string Key,
    Func<DateTime, bool> Condition,
    string Message
);