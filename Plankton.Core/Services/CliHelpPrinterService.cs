using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.CLI.Models;
using Plankton.Core.Domain.CLI.Utils;

namespace Plankton.Core.Services;

public sealed class CliHelpPrinterService(ILogger<CliHelpPrinterService> logger)
{
    public void Print(CliSchema schema) => CliHelpPrinter.Print(schema, logger);
}