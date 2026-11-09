using System.Globalization;

namespace TechnicalTest.Ui.Helpers;

public static class UiSettings
{
    public static string BaseUrl => Required("BaseUrl");

    public static float TimeoutMs => float.Parse(Required("Timeout"), CultureInfo.InvariantCulture);

    public static bool Headless => bool.Parse(ConfigurationHelper.Configuration["Headless"] ?? "true");

    // Playwright engine: chromium, firefox or webkit.
    public static string Browser => (ConfigurationHelper.Configuration["Browser"] ?? "chromium").ToLowerInvariant();

    private static string Required(string key) =>
        ConfigurationHelper.Configuration[key]
        ?? throw new InvalidOperationException($"'{key}' is missing from the appsettings files.");
}
