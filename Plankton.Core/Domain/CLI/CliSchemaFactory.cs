using Microsoft.Extensions.Configuration;
using Plankton.Core.Domain.Models;

namespace Plankton.Core.Domain.CLI;

public sealed class CliSchemaFactory(IConfiguration config)
{
    private const string CliSectionName = "cli-options";

    public CliSchemaModel Build()
    {
        var section = config.GetSection(CliSectionName);

        var options = section.Get<Dictionary<string, CliOptionModel>>();

        var schema = new CliSchemaModel { Options = options };

        CliSchemaValidator.Validate(schema);

        return schema;
    }
}