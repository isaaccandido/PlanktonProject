using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Models;

namespace Plankton.Core.Domain.CLI;

public static partial class CliHelpPrinter
{
    public static void Print(CliSchemaModel schemaModel, ILogger logger)
    {
        logger.LogAvailableCommandLineOptions();

        if (schemaModel.Options == null)
        {
            logger.LogInformation("No options specified.");
            return;
        }

        foreach (var (name, opt) in schemaModel.Options) logger.LogOptionHelp(name, opt.Help);
    }

    [LoggerMessage(LogLevel.Information, "Available command line options:")]
    private static partial void LogAvailableCommandLineOptions(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "{Option,-20} {help}")]
    public static partial void LogOptionHelp(this ILogger logger, string option, string help);
}