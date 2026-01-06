using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.Commands.Sources;
using Plankton.Core.Domain.Commands.Validation;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;
using Plankton.Core.Services;
using Serilog;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Plankton.Bots;
using Plankton.Bots.Utils;
using Plankton.Core.Domain.Commands.Handlers;
using Plankton.Core.Domain.Startup;
using Plankton.Core.Enums;

namespace Plankton.Core;

public sealed class Startup
{
    private static readonly Lazy<Startup> Instance = new(() => new Startup());

    private Startup()
    {
    }

    public static Startup GetInstance()
    {
        return Instance.Value;
    }

    public async Task Boot(string[] args)
    {
        using var host = CreateHost(args);

        var logger = host.Services.GetRequiredService<ILogger<Startup>>();
        var schema = host.Services.GetRequiredService<CliSchemaModel>();
        var parser = host.Services.GetRequiredService<CliParserService>();
        var help = host.Services.GetRequiredService<CliHelpPrinterService>();
        var banner = host.Services.GetRequiredService<BannerProcessor>();
        var engine = host.Services.GetRequiredService<PlanktonHostEngine>();

        logger.LogInformation("Initializing application...");
        banner.PrintBanner();

        var result = parser.Parse(args, schema);
        if (result.HasHelp)
        {
            help.Print(schema);
            return;
        }

        logger.LogInformation("Dependencies initialization complete.");
        logger.LogInformation("Running engine...");

        engine.CliArgs = result;

        var commandSources = host.Services.GetServices<ICommandSource>();


        foreach (var source in commandSources)
        {
            if (source is TelegramCommandSource s) s.Token = GetTelegramBotTokenFromEnvironment();

            engine.RegisterCommandSource(source);
        }

        await engine.RunAsync();
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((ctx, services, cfg) =>
            {
                cfg.ReadFrom.Configuration(ctx.Configuration)
                    .ReadFrom.Services(services);
            })
            .ConfigureServices((ctx, services) => { AddServices(services, ctx.Configuration); })
            .Build();
    }

    private static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        configuration["application:baseAddress"] = GetBaseAddressFromEnvironment();

        services.AddSingleton<CliParserService>();
        services.AddSingleton<CliHelpPrinterService>();
        services.AddSingleton<CliSchemaFactory>();
        services.AddSingleton<CliSchemaModel>(sp => sp.GetRequiredService<CliSchemaFactory>().Build());
        services.AddSingleton<BannerProcessor>();
        services.AddSingleton<PlanktonHostEngine>();
        services.AddSingleton<Func<PlanktonHostEngine>>(sp => sp.GetRequiredService<PlanktonHostEngine>);

        var commandSourceSettings = configuration.GetSection("commandSources")
            .Get<CommandSourceSettingsModel>() ?? new CommandSourceSettingsModel();

        if (commandSourceSettings is { Http.Enabled: false, Telegram.Enabled: false })
            throw new InvalidOperationException("At least one command source must be enabled (HTTP or Telegram).");

        var allSourceTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(ICommandSource).IsAssignableFrom(t));

        foreach (var type in allSourceTypes)
        {
            var enabled =
                (type == typeof(HttpCommandSource) && commandSourceSettings.Http.Enabled) ||
                (type == typeof(TelegramCommandSource) && commandSourceSettings.Telegram.Enabled);

            if (!enabled) continue;

            services.AddSingleton(type);
            services.AddSingleton(typeof(ICommandSource), sp => sp.GetRequiredService(type));
        }

        services.AddSingleton<ICommandValidator, BasicCommandValidator>();
        services.AddSingleton<ICommandAuthorizer>(_ => new TokenCommandAuthorizer(GetSuiteTokens()));
        services.AddSingleton<CommandRateLimiter>();
        services.AddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        services.AddSingleton<CommandBus>();

        services.AddSingleton<ShutdownSuiteCommandHandler>();
        services.AddSingleton<ICommandHandler>(sp => sp.GetRequiredService<ShutdownSuiteCommandHandler>());
        AddCommandHandlers(services);

        services.AddSingleton<BotEngine>();
        services.AddSingleton<BotWebTools>();
    }

    private static void AddCommandHandlers(IServiceCollection services)
    {
        var handlerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract &&
                        typeof(ICommandHandler).IsAssignableFrom(t) &&
                        t != typeof(ShutdownSuiteCommandHandler));

        foreach (var handlerType in handlerTypes)
        {
            services.AddSingleton(typeof(ICommandHandler), handlerType);
            services.AddSingleton(handlerType);
        }
    }

    private static Dictionary<SourceType, string> GetSuiteTokens()
    {
        var httpId = Environment.GetEnvironmentVariable("PLANKTON_HTTP_ID")
                     ?? throw new InvalidOperationException("Environment variable BOT_ADMIN_TOKEN is not defined.");

        var telegramId = Environment.GetEnvironmentVariable("PLANKTON_TELEGRAM_ID")
                         ?? throw new InvalidOperationException(
                             "Environment variable PLANKTON_TELEGRAM_TOKEN is not defined.");

        return new Dictionary<SourceType, string>
        {
            { SourceType.Http, httpId },
            { SourceType.Telegram, telegramId }
        };
    }

    private static string GetBaseAddressFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("PLANKTON_BASE_ADDRESS")
               ?? throw new InvalidOperationException("Environment variable PLANKTON_BASE_ADDRESS is not defined.");
    }

    private static string GetTelegramBotTokenFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("PLANKTON_TELEGRAM_TOKEN")
               ?? throw new InvalidOperationException("Environment variable PLANKTON_TELEGRAM_TOKEN is not defined.");
    }
}