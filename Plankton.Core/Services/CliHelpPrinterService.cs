using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Models;
using CliHelpPrinter = Plankton.Core.Domain.CLI.CliHelpPrinter;

namespace Plankton.Core.Services;

public sealed class CliHelpPrinterService(ILogger<CliHelpPrinterService> logger)
{
    public void Print(CliSchemaModel schemaModel)
    {
        CliHelpPrinter.Print(schemaModel, logger);
    }
}