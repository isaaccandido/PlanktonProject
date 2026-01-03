using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Utils;
using Plankton.Core.Domain.Models;

namespace Plankton.Core.Services;

public sealed class CliHelpPrinterService(ILogger<CliHelpPrinterService> logger)
{
    public void Print(CliSchemaModel schemaModel)
    {
        CliHelpPrinter.Print(schemaModel, logger);
    }
}