using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plankton.Bots.Enums;
using Plankton.Bots.Interfaces;
using Plankton.Bots.Models;
using Plankton.DataAccess;
using Plankton.DataAccess.Enums;
using Plankton.DataAccess.Interfaces;

namespace Plankton.Bots;

public class BotEngine
{
    private const string BotNotFoundMessage = "Bot could not be found.";

    private readonly DataAccessEngine _dataAccessEngine;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotEngine> _logger;
    private readonly BotEngineOptions _options;

    private readonly List<IBot> _bots = [];
    private readonly ConcurrentDictionary<string, BotRuntimeStateModel> _runtimeStates = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _botCts = new();
    private readonly ConcurrentDictionary<string, Task> _botTasks = new();

    private readonly IDataStore<BotSettingsModel> _settingsStore;
    private readonly IDataStore<BotRuntimeStateModel> _defaultRuntimeStore;

    public BotEngine(
        DataAccessEngine dataAccessEngine,
        IServiceProvider serviceProvider,
        ILogger<BotEngine> logger,
        IOptions<BotEngineOptions> options
    )
    {
        _dataAccessEngine = dataAccessEngine;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;

        _settingsStore = _dataAccessEngine.Resolve<BotSettingsModel>(_options.RuntimeStateStorage);
        _defaultRuntimeStore = _dataAccessEngine.Resolve<BotRuntimeStateModel>(_options.RuntimeStateStorage);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await LoadBotsAsync();

        foreach (var bot in _bots)
        {
            var state = await GetBotRuntimeStateAsync(bot);
            if (state.Status == BotStatus.Idle) StartBot(bot.Name);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bot engine cancelled");
        }

        _logger.LogInformation("Bot engine stopped");
    }

    private async Task LoadBotsAsync()
    {
        _logger.LogInformation("Scanning for bots...");

        var botTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IBot).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        foreach (var botType in botTypes)
            try
            {
                var bot = (IBot)ActivatorUtilities.CreateInstance(_serviceProvider, botType);

                var persistedSettings = await _settingsStore.GetAsync(bot.Name);
                if (persistedSettings != null)
                    bot.Settings = persistedSettings;
                else
                    await _settingsStore.SetAsync(bot.Name, bot.Settings);

                _bots.Add(bot);

                var runtimeState = await GetBotRuntimeStateAsync(bot);
                runtimeState.Status = bot.Settings.Enabled ? BotStatus.Idle : BotStatus.Disabled;
                _runtimeStates[bot.Name] = runtimeState;

                _logger.LogInformation("Loaded bot {BotName}", bot.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load bot {BotType}", botType.FullName);
            }

        if (_bots.Count == 0) _logger.LogWarning("No bots found!");
    }

    private IDataStore<BotRuntimeStateModel> GetRuntimeStore(IBot bot)
    {
        return bot.StateStorage != DataAccessType.InMemory
            ? _dataAccessEngine.Resolve<BotRuntimeStateModel>(bot.StateStorage)
            : _defaultRuntimeStore;
    }

    private async Task<BotRuntimeStateModel> GetBotRuntimeStateAsync(IBot bot)
    {
        var store = GetRuntimeStore(bot);
        var runtimeState = await store.GetAsync(bot.Name);
        if (runtimeState != null) return runtimeState;

        runtimeState = new BotRuntimeStateModel
        {
            BotName = bot.Name,
            Status = bot.Settings.Enabled ? BotStatus.Idle : BotStatus.Disabled,
            CrashCount = 0,
            NextRunUtc = DateTime.UtcNow
        };

        await store.SetAsync(bot.Name, runtimeState);
        return runtimeState;
    }

    private async Task PersistRuntimeStateAsync(IBot bot, BotRuntimeStateModel state)
    {
        var store = GetRuntimeStore(bot);
        await store.SetAsync(bot.Name, state);
        _runtimeStates[bot.Name] = state;
    }

    public BotActionResultModel GetAllBotStatuses()
    {
        return new BotActionResultModel(true, Result:
            _bots.Select(bot =>
            {
                _runtimeStates.TryGetValue(bot.Name, out var state);
                return (
                    bot.Name,
                    state?.Status ?? BotStatus.Disabled,
                    state?.CrashCount ?? 0
                );
            })
        );
    }

    public BotStatusModel? GetBotStatus(string botName)
    {
        var bot = FindBotByName(botName);
        if (bot == null) return null;

        _runtimeStates.TryGetValue(bot.Name, out var state);

        var status = state?.Status ?? BotStatus.Disabled;
        var crashCount = state?.CrashCount ?? 0;
        var nextRun = state?.NextRunUtc;

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

        _runtimeStates.TryGetValue(bot.Name, out var state);
        var status = state?.Status ?? BotStatus.Disabled;

        switch (status)
        {
            case BotStatus.Running:
                return new BotActionResultModel(false, "Bot is already running");
            case BotStatus.PermanentlyStopped:
                return new BotActionResultModel(false, "Bot is permanently stopped due to exceeding max failures");
            case BotStatus.Disabled:
                return new BotActionResultModel(false, "Bot is currently disabled");
        }

        var cts = new CancellationTokenSource();
        if (!_botCts.TryAdd(bot.Name, cts))
            return new BotActionResultModel(false, "Failed to initialize bot supervision task");

        _botTasks[bot.Name] = SuperviseBot(bot, cts.Token);

        _logger.LogInformation("Supervision task started for bot {BotName}", bot.Name);

        return new BotActionResultModel(true);
    }

    public BotActionResultModel StopBot(string botName)
    {
        var bot = FindBotByName(botName);
        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        _runtimeStates.TryGetValue(bot.Name, out var state);
        var status = state?.Status ?? BotStatus.Disabled;

        if (status != BotStatus.Running && status != BotStatus.Idle)
            return new BotActionResultModel(false, $"Stop failed: bot is in status '{status}'");

        if (!_botCts.TryRemove(bot.Name, out var cts))
            return new BotActionResultModel(false, $"Bot is not running (status: '{status}')");

        cts.Cancel();

        state?.Status = BotStatus.Stopped;
        _logger.LogInformation("Bot {BotName} supervision cancelled", bot.Name);

        PersistRuntimeStateAsync(bot, state!).GetAwaiter().GetResult();

        return new BotActionResultModel(true);
    }

    public BotActionResultModel EnableBot(string botName)
    {
        var bot = FindBotByName(botName);
        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        _runtimeStates.TryGetValue(bot.Name, out var state);
        var status = state?.Status ?? BotStatus.Disabled;

        if (status == BotStatus.Running || status == BotStatus.Idle)
            return new BotActionResultModel(false, "Bot is already enabled");

        bot.Settings.Enabled = true;
        _settingsStore.SetAsync(bot.Name, bot.Settings).GetAwaiter().GetResult();

        state?.Status = BotStatus.Idle;
        PersistRuntimeStateAsync(bot, state!).GetAwaiter().GetResult();

        return StartBot(botName);
    }

    public BotActionResultModel DisableBot(string botName)
    {
        var stopResult = StopBot(botName);
        if (!stopResult.Success) return stopResult;

        var bot = FindBotByName(botName)!;
        bot.Settings.Enabled = false;
        _settingsStore.SetAsync(bot.Name, bot.Settings).GetAwaiter().GetResult();

        _runtimeStates.TryGetValue(bot.Name, out var state);
        state?.Status = BotStatus.Disabled;
        PersistRuntimeStateAsync(bot, state!).GetAwaiter().GetResult();

        return new BotActionResultModel(true);
    }

    public BotActionResultModel RestartBot(string botName)
    {
        StopBot(botName);
        _runtimeStates.TryGetValue(botName, out var state);
        state?.CrashCount = 0;
        PersistRuntimeStateAsync(FindBotByName(botName)!, state!).GetAwaiter().GetResult();
        return StartBot(botName);
    }

    public BotActionResultModel ResetBotState(string botName)
    {
        var bot = FindBotByName(botName);
        if (bot == null) return new BotActionResultModel(false, BotNotFoundMessage);

        var store = GetRuntimeStore(bot);
        store.DeleteAsync(bot.Name).GetAwaiter().GetResult();

        _runtimeStates[bot.Name] = new BotRuntimeStateModel
        {
            BotName = bot.Name,
            Status = BotStatus.Idle,
            CrashCount = 0,
            NextRunUtc = DateTime.UtcNow
        };

        return new BotActionResultModel(true, "Bot runtime state reset successfully");
    }

    private async Task SuperviseBot(IBot bot, CancellationToken ct)
    {
        var threadId = Environment.CurrentManagedThreadId;

        while (!ct.IsCancellationRequested)
        {
            var state = await GetBotRuntimeStateAsync(bot);

            if (!bot.Settings.Enabled)
            {
                state.Status = BotStatus.Disabled;
                await PersistRuntimeStateAsync(bot, state);

                _logger.LogInformation("Bot {BotName} disabled. Waiting indefinitely.", bot.Name);

                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch
                {
                    break;
                }
            }

            if (state.CrashCount >= bot.Settings.MaxFailures)
            {
                state.Status = BotStatus.PermanentlyStopped;
                await PersistRuntimeStateAsync(bot, state);

                _logger.LogWarning("Bot {BotName} exceeded max failures and is permanently stopped", bot.Name);

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
                state.Status = BotStatus.Running;
                await PersistRuntimeStateAsync(bot, state);

                _logger.LogInformation("Bot {BotName} started running (Thread {ThreadId})",
                    bot.Name,
                    threadId
                );

                await bot.RunAsync(ct);

                state.Status = BotStatus.Idle;
                state.NextRunUtc = DateTime.UtcNow + (bot.Settings.RunInterval ?? TimeSpan.FromMinutes(1));
                state.CrashCount = 0;

                await PersistRuntimeStateAsync(bot, state);

                _logger.LogInformation("Bot {BotName} finished running, next run at {NextRun}",
                    bot.Name,
                    state.NextRunUtc?.ToLocalTime()
                );

                await Task.Delay(bot.Settings.RunInterval ?? TimeSpan.FromMinutes(1), ct);
            }
            catch (OperationCanceledException)
            {
                state.Status = BotStatus.Stopped;
                await PersistRuntimeStateAsync(bot, state);

                _logger.LogInformation("Bot {BotName} stopped (Thread {ThreadId})",
                    bot.Name,
                    threadId
                );

                break;
            }
            catch (Exception ex)
            {
                state.CrashCount++;
                state.Status = BotStatus.Crashed;
                await PersistRuntimeStateAsync(bot, state);

                _logger.LogError(ex, "Bot {BotName} crashed ({Attempt}/{MaxFailures})",
                    bot.Name,
                    state.CrashCount,
                    bot.Settings.MaxFailures
                );

                await Task.Delay(bot.Settings.RestartDelay, ct);
            }
        }

        _botCts.TryRemove(bot.Name, out _);
        _botTasks.TryRemove(bot.Name, out _);
        _logger.LogInformation("Supervision task for bot {BotName} exited (Thread {ThreadId})", bot.Name, threadId);
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