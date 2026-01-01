using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;

namespace Plankton.Core;

public class Engine(ILogger<Engine> logger)
{
    public required CliArgsResult CliArgs { get; set; }

    public void Run()
    {
        logger.LogInformation(System.Text.Json.JsonSerializer.Serialize(CliArgs));
    }
}