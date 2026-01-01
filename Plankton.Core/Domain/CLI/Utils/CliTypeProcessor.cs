using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;

namespace Plankton.Core.Domain.CLI.Utils;

public static partial class CliTypeProcessor
{
    public static object? ConvertValue(string name, CliOption option, List<string> values, ILogger logger)
    {
        try
        {
            return option.Type switch
            {
                "flag" => true,
                "bool" => ParseBool(name, values, option, logger),
                "int" => ParseInt(name, values, option, logger),
                "string" => ParseString(name, values, option, logger),
                "enum" => ParseEnum(name, values, option, logger),
                _ => option.Default
            };
        }
        catch (Exception ex)
        {
            logger.LogInvalidValueUsingDefault(name, ex.Message);
            return option.Default;
        }
    }

    private static bool ParseBool(string name, List<string> values, CliOption opt, ILogger logger)
    {
        if (values.Count == 1 && bool.TryParse(values[0], out var b))
            return b;

        logger.LogExpectsBoolean(name);
        return opt.Default is true;
    }

    private static int ParseInt(string name, List<string> values, CliOption opt, ILogger logger)
    {
        if (values.Count == 1 && int.TryParse(values[0], out var i))
            return i;

        logger.LogExpectsInteger(name);
        return opt.Default is int d ? d : 0;
    }

    private static string[] ParseString(string name, List<string> values, CliOption opt, ILogger logger)
    {
        if (opt.MinArgs.HasValue && values.Count < opt.MinArgs.Value)
            logger.LogExpectsAtLeast(name, opt.MinArgs.Value);

        if (opt.MaxArgs.HasValue && values.Count > opt.MaxArgs.Value)
            logger.LogExpectsAtMost(name, opt.MaxArgs.Value);

        return values.ToArray();
    }

    private static string ParseEnum(string name, List<string> values, CliOption opt, ILogger logger)
    {
        if (values.Count != 1)
        {
            logger.LogInvalidEnum(name, values);
            return opt.Default?.ToString() ?? opt.Values![0];
        }

        var value = values[0];

        if (opt.Values!.Contains(value, StringComparer.OrdinalIgnoreCase))
            return value;

        logger.LogInvalidEnum(name, values);
        return opt.Default?.ToString() ?? opt.Values![0];
    }
    
    [LoggerMessage(LogLevel.Warning, "Invalid value for '{name}': {reason}. Using default.")]
    static partial void LogInvalidValueUsingDefault(this ILogger logger, string name, string reason);
    
    [LoggerMessage(LogLevel.Warning, "'{name}' expects true or false.")]
    static partial void LogExpectsBoolean(this ILogger logger, string name);

    [LoggerMessage(LogLevel.Warning, "'{name}' expects an integer.")]
    static partial void LogExpectsInteger(this ILogger logger, string name);

    [LoggerMessage(LogLevel.Warning, "'{name}' expects at least {min} values.")]
    static partial void LogExpectsAtLeast(this ILogger logger, string name, int min);

    [LoggerMessage(LogLevel.Warning, "'{name}' expects at most {max} values.")]
    static partial void LogExpectsAtMost(this ILogger logger, string name, int max);

    [LoggerMessage(LogLevel.Warning, "Invalid value for enum '{name}': {values}.")]
    static partial void LogInvalidEnum(this ILogger logger, string name, IEnumerable<string> values);
}