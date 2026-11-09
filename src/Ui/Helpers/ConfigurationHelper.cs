using Microsoft.Extensions.Configuration;

namespace TechnicalTest.Ui.Helpers;

public static class ConfigurationHelper
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??= Build();

    private static IConfiguration Build()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);

        var environment = Environment.GetEnvironmentVariable("TEST_ENV");
        if (!string.IsNullOrWhiteSpace(environment))
        {
            // Fail fast rather than silently running against the default settings.
            builder.AddJsonFile($"appsettings.{environment}.json", optional: false);
        }

        return builder.Build();
    }
}
