using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Utils;
using Plankton.Core.Domain.Commands.Handlers;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.Commands.Sources;
using Plankton.Core.Domain.Commands.Validation;
using Plankton.Core.Domain.Models;
using Plankton.Core.Domain.Startup;
using Plankton.Core.Interfaces;
using Plankton.Core.Services;
using Serilog;

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
        var engine = host.Services.GetRequiredService<Engine>();
        var httpCommandSource = host.Services.GetRequiredService<HttpCommandSource>();

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
        engine.RegisterCommandSource(httpCommandSource);

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
            .ConfigureServices((ctx, services) => { AddServices(services); })
            .Build();
    }

    private static void AddServices(IServiceCollection services)
    {
        // ─── CLI ───────────────────────────────────────────────
        services.AddSingleton<CliParserService>();
        services.AddSingleton<CliHelpPrinterService>();
        services.AddSingleton<CliSchemaFactory>();
        services.AddSingleton<CliSchemaModel>(sp => sp.GetRequiredService<CliSchemaFactory>().Build());
        services.AddSingleton<BannerProcessor>();

        // ─── Engine ────────────────────────────────────────────
        services.AddSingleton<Engine>();

        // ─── Command Sources ───────────────────────────────────
        services.AddSingleton<HttpCommandSource>();

        // ─── Command Pipeline ──────────────────────────────────
        services.AddSingleton<ICommandValidator, BasicCommandValidator>();
        services.AddSingleton<ICommandAuthorizer>(_ => new TokenCommandAuthorizer(GetSuiteToken()));
        services.AddSingleton<CommandRateLimiter>();

        // ─── Command Handlers ──────────────────────────────────
        // Use factory injection to break circular dependency for ShutdownCommandHandler
        services.AddSingleton<ShutdownCommandHandler>(sp =>
        {
            // Provide a factory to retrieve Engine lazily
            var engineFactory = new Func<Engine>(sp.GetRequiredService<Engine>);
            return new ShutdownCommandHandler(engineFactory);
        });

        services.AddSingleton<ICommandHandler>(sp => sp.GetRequiredService<ShutdownCommandHandler>());

        // Other handlers that don’t depend on Engine directly
        services.AddSingleton<ICommandHandler, StartBotCommandHandler>();
        services.AddSingleton<ICommandHandler, ListBotsCommandHandler>();

        // ─── Resolver & Bus ────────────────────────────────────
        services.AddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        services.AddSingleton<CommandBus>();
    }

    private static string GetSuiteToken()
    {
        return Environment.GetEnvironmentVariable("PLANKTON_ADMIN_TOKEN")
               ?? throw new InvalidOperationException("Environment variable BOT_ADMIN_TOKEN is not defined.");
    }
}