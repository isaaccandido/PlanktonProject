using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI;
using Plankton.Core.Domain.Commands.Handlers;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.Commands.Sources;
using Plankton.Core.Domain.Commands.Validation;
using Plankton.Core.Domain.Models;
using Plankton.Core.Interfaces;
using Plankton.Core.Services;
using Serilog;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Plankton.Core.Domain.Startup;

namespace Plankton.Core;

public sealed class Startup
{
    private static readonly Lazy<Startup> Instance = new(() => new Startup());

    private Startup()
    {
    }

    public static Startup GetInstance() => Instance.Value;

    public async Task Boot(string[] args)
    {
        using var host = CreateHost(args);

        var logger = host.Services.GetRequiredService<ILogger<Startup>>();
        var schema = host.Services.GetRequiredService<CliSchemaModel>();
        var parser = host.Services.GetRequiredService<CliParserService>();
        var help = host.Services.GetRequiredService<CliHelpPrinterService>();
        var banner = host.Services.GetRequiredService<BannerProcessor>();
        var engine = host.Services.GetRequiredService<Engine>();

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

        // Dynamically register enabled command sources
        var commandSources = host.Services.GetServices<ICommandSource>();
        foreach (var source in commandSources)
        {
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
        // ─── CLI ───────────────────────────────────────────────
        services.AddSingleton<CliParserService>();
        services.AddSingleton<CliHelpPrinterService>();
        services.AddSingleton<CliSchemaFactory>();
        services.AddSingleton<CliSchemaModel>(sp => sp.GetRequiredService<CliSchemaFactory>().Build());
        services.AddSingleton<BannerProcessor>();

        // ─── Engine ────────────────────────────────────────────
        services.AddSingleton<Engine>();
        services.AddSingleton<Func<Engine>>(sp => sp.GetRequiredService<Engine>);

        // ─── Command Source Settings ───────────────────────────
        var commandSourceSettings = configuration.GetSection("commandSources")
            .Get<CommandSourceSettings>() ?? new CommandSourceSettings();

        // Fail if no source is enabled
        if (!commandSourceSettings.HttpEnabled && !commandSourceSettings.TelegramEnabled)
        {
            throw new InvalidOperationException("At least one command source must be enabled (HTTP or Telegram).");
        }

        // ─── Dynamically register enabled command sources ───────
        var allSourceTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(ICommandSource).IsAssignableFrom(t));

        foreach (var type in allSourceTypes)
        {
            var enabled =
                type == typeof(HttpCommandSource) && commandSourceSettings.HttpEnabled ||
                type == typeof(TelegramCommandSource) && commandSourceSettings.TelegramEnabled;

            if (enabled)
            {
                // Register concrete type
                services.AddSingleton(type);

                // Register as ICommandSource (same instance)
                services.AddSingleton(typeof(ICommandSource), sp => sp.GetRequiredService(type));
            }
        }

        // ─── Command Pipeline ──────────────────────────────────
        services.AddSingleton<ICommandValidator, BasicCommandValidator>();
        services.AddSingleton<ICommandAuthorizer>(_ => new TokenCommandAuthorizer(GetSuiteToken()));
        services.AddSingleton<CommandRateLimiter>();

        // ─── Command Handlers ──────────────────────────────────
        services.AddSingleton<ShutdownCommandHandler>();
        services.AddSingleton<ICommandHandler>(sp => sp.GetRequiredService<ShutdownCommandHandler>());
        AddCommandHandlers(services);

        // ─── Resolver & Bus ────────────────────────────────────
        services.AddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        services.AddSingleton<CommandBus>();
    }
    
    private static void AddCommandHandlers(IServiceCollection services)
    {
        var handlerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract &&
                        typeof(ICommandHandler).IsAssignableFrom(t) &&
                        t != typeof(ShutdownCommandHandler));

        foreach (var handlerType in handlerTypes)
        {
            services.AddSingleton(typeof(ICommandHandler), handlerType);
            services.AddSingleton(handlerType);
        }
    }

    private static string GetSuiteToken()
    {
        return Environment.GetEnvironmentVariable("PLANKTON_ADMIN_TOKEN")
               ?? throw new InvalidOperationException("Environment variable BOT_ADMIN_TOKEN is not defined.");
    }
}