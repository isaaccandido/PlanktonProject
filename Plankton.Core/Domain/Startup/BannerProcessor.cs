using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Plankton.Core.Domain.Startup;

public sealed partial class BannerProcessor(IConfiguration configuration, ILogger<BannerProcessor> logger)
{
    private const string BannerFileName = "banner.txt";
    private static readonly Regex PlaceholderRegex = MyRegex();

    public void PrintBanner()
    {
        try
        {
            if (!File.Exists(BannerFileName)) return;

            var banner = File.ReadAllText(BannerFileName);
            
            banner = ReplacePlaceholders(banner);

            Console.WriteLine(banner);
        }
        catch (Exception _)
        {
            // ignored
        }
    }

    private string ReplacePlaceholders(string input)
    {
        return PlaceholderRegex.Replace(input, match =>
        {
            var key = match.Groups[1].Value;
            return configuration[key] ?? string.Empty;
        });
    }

    [GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}