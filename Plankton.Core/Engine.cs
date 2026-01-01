using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;

namespace Plankton.Core;

public partial class Engine(ILogger<Engine> logger)
{
    public required CliArgsResult CliArgs { get; set; }

    public void Run()
    {
        LogObject(logger, System.Text.Json.JsonSerializer.Serialize(CliArgs));
    }

    [LoggerMessage(LogLevel.Information, "{object}")]
    static partial void LogObject(ILogger<Engine> logger, string @object);
}