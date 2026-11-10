using TechnicalTest.Ui.Helpers;

namespace TechnicalTest.Tests.Support;

public static class ApiSettings
{
    // A trailing slash keeps relative paths like "posts" under any path in the base URL.
    public static Uri BaseAddress
    {
        get
        {
            var url = ConfigurationHelper.Configuration["ApiBaseUrl"]
                ?? throw new InvalidOperationException("'ApiBaseUrl' is missing from the appsettings files.");
            return new Uri(url.EndsWith('/') ? url : url + "/");
        }
    }

    public static HttpClient CreateHttpClient() => new(new HttpLoggingHandler()) { BaseAddress = BaseAddress };
}
