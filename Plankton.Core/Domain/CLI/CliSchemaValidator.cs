using Plankton.Core.Domain.Models;

namespace Plankton.Core.Domain.CLI;

public static class CliSchemaValidator
{
    public static void Validate(CliSchemaModel schemaModel)
    {
        if (schemaModel.Options == null || schemaModel.Options.Count == 0) return;

        foreach (var (name, opt) in schemaModel.Options)
        {
            if (!name.StartsWith('-')) throw new InvalidOperationException($"Invalid option '{name}'.");

            if (string.IsNullOrWhiteSpace(opt.Help))
                throw new InvalidOperationException($"Option '{name}' must define help.");

            switch (opt.Type)
            {
                case "flag":
                    if (opt.Default is not false && opt.Default is not null)
                        throw new InvalidOperationException(
                            $"Flag '{name}' default must be false or null.");
                    break;
                case "bool":
                case "int":
                case "string":
                    break;
                case "enum":
                    if (opt.Values is null || opt.Values.Length == 0)
                        throw new InvalidOperationException($"Enum '{name}' must define values.");
                    break;
                default:
                    throw new InvalidOperationException($"Unknown type '{opt.Type}'.");
            }
        }
    }
}