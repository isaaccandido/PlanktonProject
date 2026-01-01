using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;
using Plankton.Core.Domain.CLI.Utils;

namespace Plankton.Core.Services;

public sealed class CliParserService(ILogger<CliParserService> logger)
{
    public CliParseResult Parse(string[] args, CliSchema schema)
    {
        return CliParser.Parse(args, schema, logger);
    }
}