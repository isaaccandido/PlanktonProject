using Plankton.Core.Domain.CLI.Models;

namespace Plankton.Core.Domain.CLI.Utils;

using Microsoft.Extensions.Configuration;

public sealed class CliSchemaFactory(IConfiguration config)
{
    private const string CliSectionName = "cli-options";

    public CliSchema Build()
    {
        var section = config.GetSection(CliSectionName);

        var options = section.Get<Dictionary<string, CliOption>>();

        var schema = new CliSchema { Options = options };

        CliSchemaValidator.Validate(schema);

        return schema;
    }
}