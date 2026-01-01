using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;
using Plankton.Core.Domain.CLI.Utils;
using Plankton.Core.Domain.Startup;
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

    public void Boot(string[] args)
    {
        using var host = CreateHost(args);

        var logger = host.Services.GetRequiredService<ILogger<Startup>>();
        var schema = host.Services.GetRequiredService<CliSchema>();
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
        engine.Run();
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((ctx, services, cfg) =>
            {
                cfg.ReadFrom.Configuration(ctx.Configuration)
                    .ReadFrom.Services(services);
            })
            .ConfigureServices((ctx, services) =>
            {
                AddCliToolsToContainer(services);
                services.AddSingleton<Engine>();
            })
            .Build();
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