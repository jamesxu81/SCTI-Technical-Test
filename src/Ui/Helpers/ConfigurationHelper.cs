using Microsoft.Extensions.Configuration;

namespace TechnicalTest.Ui.Helpers;

public static class ConfigurationHelper
{
    // Lazy keeps the build to one thread when parallel tests read settings at the same time.
    private static readonly Lazy<IConfiguration> LazyConfiguration = new(Build);

    public static IConfiguration Configuration => LazyConfiguration.Value;

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

        // e.g. TEST_Headless=false overrides "Headless" without editing the JSON files.
        builder.AddEnvironmentVariables("TEST_");

        return builder.Build();
    }
}
