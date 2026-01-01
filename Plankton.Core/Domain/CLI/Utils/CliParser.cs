using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;

namespace Plankton.Core.Domain.CLI.Utils;

public static partial class CliParser
{
    private static readonly string[] HelpOptions = ["-help", "--help", "-h"];

    public static CliParseResult Parse(string[] args, CliSchema schema, ILogger logger)
    {
        var raw = ParseRaw(args, logger, out var helpRequested);

        var result = new CliParseResult { HasHelp = helpRequested };

        if (schema.Options == null) return result;

        foreach (var (name, opt) in schema.Options)
        {
            if (!raw.TryGetValue(name, out var values))
            {
                if (opt.Required)
                {
                    logger.LogMissingRequiredOption(name);
                    Environment.Exit(1);
                }

                result.Values[name] = opt.Default;
                continue;
            }

            result.Values[name] = CliTypeProcessor.ConvertValue(name, opt, values, logger);
        }

        return result;
    }

    private static Dictionary<string, List<string>> ParseRaw(
        string[] args,
        ILogger logger,
        out bool helpRequested)
    {
        var map = new Dictionary<string, List<string>>();
        helpRequested = false;

        string? current = null;

        foreach (var arg in args)
        {
            if (IsHelp(arg))
            {
                helpRequested = true;
                continue;
            }

            if (arg.StartsWith('-'))
            {
                if (map.ContainsKey(arg))
                    logger.LogDuplicateOption(arg);

                current = arg;
                map[current] = [];
            }
            else if (current != null)
            {
                map[current].Add(arg);
            }
            else
            {
                logger.LogIgnoringOrphanArgument(arg);
            }
        }

        return map;
    }
    
    private static bool IsHelp(string arg)
    {
        return HelpOptions.Contains(arg, StringComparer.OrdinalIgnoreCase);
    }

    [LoggerMessage(LogLevel.Warning, "Missing required option '{name}'. Application cannot continue. Exitting.")]
    static partial void LogMissingRequiredOption(this ILogger logger, string name);

    [LoggerMessage(LogLevel.Warning, "Ignoring orphan argument '{arg}'.")]
    static partial void LogIgnoringOrphanArgument(this ILogger logger, string arg);

    [LoggerMessage(LogLevel.Warning, "Option '{name}' specified more than once. Using last occurrence.")]
    static partial void LogDuplicateOption(this ILogger logger, string name);
}