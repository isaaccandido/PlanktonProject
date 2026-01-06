using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plankton.Bots.Enums;
using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;

namespace Plankton.Bots;

public class BotEngine(IServiceProvider serviceProvider, ILogger<BotEngine> logger)
{
    private const string BotNotFoundMessage = "Bot could not be found.";

    private readonly List<IBot> _bots = [];
    private readonly ConcurrentDictionary<string, BotStatus> _botStatuses = new();
    private readonly ConcurrentDictionary<string, int> _botCrashCounts = new();
    private readonly ConcurrentDictionary<string, DateTime?> _botNextRunTimes = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _botCts = new();
    private readonly ConcurrentDictionary<string, Task> _botTasks = new();

    public async Task RunAsync(CancellationToken ct)
    {
        LoadBots();
        _bots.ForEach(b => StartBot(b.Name));

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Bot engine cancelled");
        }

        logger.LogInformation("Bot engine stopped");
    }

    private void LoadBots()
    {
        logger.LogInformation("Scanning for bots...");

        var botTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IBot).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        foreach (var botType in botTypes)
            try
            {
                var bot = (IBot)ActivatorUtilities.CreateInstance(serviceProvider, botType);

                _bots.Add(bot);
                _botStatuses[bot.Name] = bot.Settings.Enabled ? BotStatus.Idle : BotStatus.Disabled;
                _botCrashCounts[bot.Name] = 0;
                _botNextRunTimes[bot.Name] = bot.Settings.Enabled
                    ? DateTime.UtcNow.Add(bot.Settings.RunInterval ?? TimeSpan.FromMinutes(1))
                    : null;

                logger.LogInformation("Loaded bot {BotName}", bot.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load bot {BotType}", botType.FullName);
            }

        if (_bots.Count == 0) logger.LogWarning("No bots found!");
    }

    public IEnumerable<(string Name, BotStatus Status, int CrashCount)> GetAllBotStatuses()
    {
        return _bots.Select(bot =>
        (
            bot.Name,
            _botStatuses.GetValueOrDefault(bot.Name, BotStatus.Disabled),
            _botCrashCounts.GetValueOrDefault(bot.Name, 0)
        ));
    }

    public BotStatusModel? GetBotStatus(string botName)
    {
        var bot = FindBotByName(botName);

        if (bot is null) return null;

        _botStatuses.TryGetValue(bot.Name, out var status);
        _botNextRunTimes.TryGetValue(bot.Name, out var nextRun);
        _botCrashCounts.TryGetValue(bot.Name, out var crashCount);

        var reason = status switch
        {
            BotStatus.Disabled when !bot.Settings.Enabled => "Bot is disabled in settings",
            BotStatus.Disabled => "Bot is currently disabled",
            BotStatus.PermanentlyStopped =>
                $"Bot exceeded max failures ({bot.Settings.MaxFailures}) and is permanently stopped",
            BotStatus.Crashed =>
                $"Bot crashed ({crashCount}/{bot.Settings.MaxFailures}) and will restart after delay",
            BotStatus.Idle when nextRun.HasValue && nextRun.Value > DateTime.UtcNow =>
                $"Bot is idle, next run scheduled at {nextRun.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
            BotStatus.Idle => "Bot is idle, waiting for next run interval",
            _ => null
        };

        return new BotStatusModel
        {
            Name = bot.Name,
            Status = status,
            CrashCount = crashCount,
            Settings = bot.Settings,
            IsRunning = status == BotStatus.Running,
            NextRun = nextRun,
            Reason = reason
        };
    }

    public BotActionResultModel StartBot(string botName)
    {
        var bot = FindBotByName(botName);

        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        var status = _botStatuses.GetValueOrDefault(bot.Name, BotStatus.Disabled);

        switch (status)
        {
            case BotStatus.Running:
                return new BotActionResultModel(false, "Bot is already running");
            case BotStatus.PermanentlyStopped:
                return new BotActionResultModel(false, "Bot is permanently stopped due to exceeding max failures");
            case BotStatus.Disabled:
                return new BotActionResultModel(false, "Bot is currently disabled");
            case BotStatus.Crashed:
            case BotStatus.Stopped:
            case BotStatus.Idle:
                break;
            default:
                throw new Exception("Unknown status");
        }

        var cts = new CancellationTokenSource();

        if (!_botCts.TryAdd(bot.Name, cts))
            return new BotActionResultModel(false, "Failed to initialize bot supervision task");

        _botTasks[bot.Name] = SuperviseBot(bot, cts.Token);

        logger.LogInformation("Supervision task started for bot {BotName}", bot.Name);

        return new BotActionResultModel(true);
    }

    public BotActionResultModel StopBot(string botName)
    {
        var bot = FindBotByName(botName);

        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        var status = _botStatuses.GetValueOrDefault(bot.Name, BotStatus.Disabled);

        if (status != BotStatus.Running && status != BotStatus.Idle)
            return new BotActionResultModel(false, $"Stop failed: bot is in status '{status}'");

        if (!_botCts.TryRemove(bot.Name, out var cts))
            return new BotActionResultModel(false, $"Bot is not running (status: '{status}')");

        cts.Cancel();

        _botStatuses[bot.Name] = BotStatus.Stopped;

        logger.LogInformation("Bot {BotName} supervision cancelled", bot.Name);

        return new BotActionResultModel(true);
    }

    public BotActionResultModel DisableBot(string botName)
    {
        var bot = FindBotByName(botName);

        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        var stopResult = StopBot(botName);

        if (!stopResult.Success && _botStatuses[bot.Name] != BotStatus.Disabled) return stopResult;

        _botStatuses[bot.Name] = BotStatus.Disabled;

        bot.Settings.Enabled = false;

        logger.LogInformation("Bot {BotName} disabled successfully", bot.Name);

        return new BotActionResultModel(true);
    }

    public BotActionResultModel EnableBot(string botName)
    {
        var bot = FindBotByName(botName);

        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        var currentStatus = _botStatuses.GetValueOrDefault(bot.Name, BotStatus.Disabled);

        if (currentStatus is BotStatus.Running or BotStatus.Idle)
            return new BotActionResultModel(false, "Bot is already enabled");

        bot.Settings.Enabled = true;

        if (currentStatus == BotStatus.Disabled) _botStatuses[bot.Name] = BotStatus.Idle;

        var startResult = StartBot(botName);

        if (!startResult.Success) return new BotActionResultModel(false, $"Failed to start bot: {startResult.Reason}");

        logger.LogInformation("Bot {BotName} enabled successfully", bot.Name);

        return new BotActionResultModel(true);
    }

    public BotActionResultModel RestartBot(string botName)
    {
        logger.LogInformation("Bot {BotName} restarting bot", botName);

        var stopResult = StopBot(botName);

        if (!stopResult.Success) return stopResult;

        _botCrashCounts[botName] = 0;

        return StartBot(botName);
    }

    private async Task SuperviseBot(IBot bot, CancellationToken ct)
    {
        var settings = bot.Settings;
        var threadId = Environment.CurrentManagedThreadId;

        logger.LogInformation("Supervision task started for bot {BotName} on thread {ThreadId}", bot.Name, threadId);

        while (!ct.IsCancellationRequested)
        {
            if (!settings.Enabled)
            {
                _botStatuses[bot.Name] = BotStatus.Disabled;
                logger.LogInformation("Bot {BotName} disabled. Waiting indefinitely.", bot.Name);
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch
                {
                    break;
                }
            }

            if (_botCrashCounts[bot.Name] >= settings.MaxFailures)
            {
                _botStatuses[bot.Name] = BotStatus.PermanentlyStopped;
                logger.LogWarning(
                    "Bot {BotName} exceeded max failures ({MaxFailures}) and is permanently stopped",
                    bot.Name, settings.MaxFailures
                );

                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch
                {
                    break;
                }
            }

            try
            {
                _botStatuses[bot.Name] = BotStatus.Running;
                logger.LogInformation("Bot {BotName} started running (Thread {ThreadId})", bot.Name, threadId);

                await bot.RunAsync(ct);

                _botStatuses[bot.Name] = BotStatus.Idle;
                logger.LogInformation("Bot {BotName} finished running (Thread {ThreadId})", bot.Name, threadId);

                var wait = settings.RunInterval ?? TimeSpan.FromMinutes(1);
                var nextRunAt = DateTime.UtcNow.Add(wait);
                _botNextRunTimes[bot.Name] = nextRunAt;

                logger.LogInformation(
                    "Bot {BotName} will next run at {NextRun} (Thread {ThreadId})",
                    bot.Name, nextRunAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), threadId
                );

                await Task.Delay(wait, ct);
            }
            catch (OperationCanceledException)
            {
                _botStatuses[bot.Name] = BotStatus.Stopped;
                logger.LogInformation("Bot {BotName} stopped (Thread {ThreadId})", bot.Name, threadId);
                break;
            }
            catch (Exception ex)
            {
                _botCrashCounts.AddOrUpdate(bot.Name, 1, (_, old) => old + 1);
                _botStatuses[bot.Name] = BotStatus.Crashed;

                logger.LogError(
                    ex,
                    "Bot {BotName} crashed ({Attempt}/{MaxFailures}) on thread {ThreadId}",
                    bot.Name, _botCrashCounts[bot.Name], settings.MaxFailures, threadId
                );

                await Task.Delay(settings.RestartDelay, ct);
            }
        }

        _botCts.TryRemove(bot.Name, out _);
        _botTasks.TryRemove(bot.Name, out _);
        logger.LogInformation("Supervision task for bot {BotName} exited (Thread {ThreadId})", bot.Name, threadId);
    }

    private IBot? FindBotByName(string name)
    {
        return _bots.FirstOrDefault(b => b.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}