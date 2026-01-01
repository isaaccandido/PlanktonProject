using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;
using Plankton.Core.Domain.CLI.Utils;
using Plankton.Core.Domain.Startup;
using Plankton.Core.Services;
using Serilog;

namespace Plankton.Core;

public sealed class Engine
{
    private static readonly Lazy<Engine> Instance = new(() => new Engine());

    public static Engine GetInstance()
    {
        return Instance.Value;
    }

    private Engine()
    {
    }

    public void Start(string[] args)
    {
        ConfigureApp(args);
    }

    private static void ConfigureApp(string[] args)
    {
        using var host = CreateHost(args);

        var logger = host.Services.GetRequiredService<ILogger<Engine>>();

        logger.LogInformation("Initializing application...");

        var schema = host.Services.GetRequiredService<CliSchema>();
        var parser = host.Services.GetRequiredService<CliParserService>();
        var help = host.Services.GetRequiredService<CliHelpPrinterService>();

        var banner = host.Services.GetRequiredService<BannerProcessor>();
        banner.PrintBanner();

        var result = parser.Parse(args, schema);

        if (result.HasHelp)
        {
            help.Print(schema);
            return;
        }

        logger.LogInformation("Startup complete.");
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((ctx, services, cfg) =>
            {
                cfg.ReadFrom.Configuration(ctx.Configuration).ReadFrom.Services(services);
            })
            .ConfigureServices((ctx, services) =>
            {
                AddCliToolsToContainer(services);
            }).Build();
    }

    private static void AddCliToolsToContainer(IServiceCollection services)
    {
        services.AddSingleton<CliParserService>();
        services.AddSingleton<CliHelpPrinterService>();
        services.AddSingleton<CliSchemaFactory>();
        services.AddSingleton<CliSchema>(sp => sp.GetRequiredService<CliSchemaFactory>().Build());
        services.AddSingleton<BannerProcessor>();
    }
}