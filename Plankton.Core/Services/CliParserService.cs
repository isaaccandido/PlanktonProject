using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Utils;
using Plankton.Core.Domain.Models;

namespace Plankton.Core.Services;

public sealed class CliParserService(ILogger<CliParserService> logger)
{
    public CliArgsResultModel Parse(string[] args, CliSchemaModel schemaModel)
    {
        return CliParser.Parse(args, schemaModel, logger);
    }
}