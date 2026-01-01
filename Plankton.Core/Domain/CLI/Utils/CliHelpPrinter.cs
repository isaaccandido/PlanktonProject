using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;

namespace Plankton.Core.Domain.CLI.Utils;

public static partial class CliHelpPrinter
{
    public static void Print(CliSchema schema, ILogger logger)
    {
        logger.LogAvailableCommandLineOptions();

        foreach (var (name, opt) in schema.Options)
        {
            logger.LogOptionHelp(name, opt.Help);
        }
    }

    [LoggerMessage(LogLevel.Information, "Available command line options:")]
    private static partial void LogAvailableCommandLineOptions(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "{Option,-20} {help}")]
    public static partial void LogOptionHelp(this ILogger logger, string option, string help);
}