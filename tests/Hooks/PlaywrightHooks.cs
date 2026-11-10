using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;
using Reqnroll.BoDi;
using TechnicalTest.Tests.Support;
using TechnicalTest.Ui.Helpers;
using TechnicalTest.Ui.Pages;

namespace TechnicalTest.Tests.Hooks;

[Binding]
public sealed class PlaywrightHooks
{
    private readonly IObjectContainer _container;
    private readonly ScenarioContext _scenarioContext;
    private readonly IReqnrollOutputHelper _output;
    private readonly string _artifactId = Guid.NewGuid().ToString("N")[..8];
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private NetworkRecorder? _network;

    public PlaywrightHooks(IObjectContainer container, ScenarioContext scenarioContext, IReqnrollOutputHelper output)
    {
        _container = container;
        _scenarioContext = scenarioContext;
        _output = output;
    }

    // Expect() assertions otherwise default to 5 s; use the same Timeout as actions and navigation.
    [BeforeTestRun]
    public static void ConfigureAssertionTimeout() => Assertions.SetDefaultExpectTimeout(UiSettings.TimeoutMs);

    [BeforeScenario("UI")]
    public async Task StartBrowserAsync()
    {
        _playwright = await Playwright.CreateAsync();
        var browserType = UiSettings.Browser switch
        {
            "chromium" => _playwright.Chromium,
            "firefox" => _playwright.Firefox,
            "webkit" => _playwright.Webkit,
            var other => throw new InvalidOperationException(
                $"Unknown Browser '{other}' in settings; use chromium, firefox or webkit."),
        };
        _browser = await browserType.LaunchAsync(new() { Headless = UiSettings.Headless });

        // The site works out quote dates and traveller ages from the browser clock.
        var contextOptions = new BrowserNewContextOptions
        {
            Locale = "en-NZ",
            TimezoneId = "Pacific/Auckland",
            RecordVideoDir = Path.Combine(ArtifactDirectory(), "video-tmp"),
        };
        if (!string.IsNullOrWhiteSpace(UiSettings.RecordHarDir))
        {
            contextOptions.RecordHarPath = Path.Combine(
                Directory.CreateDirectory(UiSettings.RecordHarDir).FullName, $"{ArtifactName()}.har");
            contextOptions.RecordHarContent = HarContentPolicy.Embed;
            contextOptions.RecordHarMode = HarMode.Full;
        }

        _context = await _browser.NewContextAsync(contextOptions);
        _context.SetDefaultTimeout(UiSettings.TimeoutMs);
        _context.SetDefaultNavigationTimeout(UiSettings.TimeoutMs);
        await _context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });

        var page = await _context.NewPageAsync();
        var network = new NetworkRecorder(page);
        _page = page;
        _network = network;

        _container.RegisterInstanceAs(page);
        _container.RegisterInstanceAs(network);
        _container.RegisterInstanceAs(new QuoteFormPage(page));
        _container.RegisterInstanceAs(new PlanOptionsPage(page));
        _container.RegisterInstanceAs(new TravellerDetailsPage(page));
    }

    [AfterScenario("UI")]
    public async Task StopBrowserAsync()
    {
        var videoPath = _page?.Video is { } video ? await video.PathAsync() : null;
        try
        {
            await SaveScreenshotIfFailedAsync();
            await SaveTraceIfFailedAsync();
            await SaveNetworkLogAsync();
        }
        finally
        {
            await CloseBrowserAsync();
            // The video file is only complete once the browser has closed.
            KeepVideoIfFailed(videoPath);
        }
    }

    // Chromium sometimes crashes while closing a page that has an open modal. The scenario result
    // is already known by then, so a crash during cleanup is logged rather than failing the test.
    private async Task CloseBrowserAsync()
    {
        try
        {
            // The HAR file is only written when the context closes.
            if (_context is not null)
            {
                await _context.CloseAsync();
            }

            // Closing the browser also closes its context and page.
            if (_browser is not null)
            {
                await _browser.CloseAsync();
            }
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            _output.WriteLine($"Browser closed unexpectedly during cleanup: {exception.Message.Split('\n')[0]}");
        }
        finally
        {
            _playwright?.Dispose();
        }
    }

    private async Task SaveScreenshotIfFailedAsync()
    {
        if (_scenarioContext.TestError is null || _page is null || _page.IsClosed)
        {
            return;
        }

        try
        {
            var path = ArtifactPath("png");
            await _page.ScreenshotAsync(new() { Path = path, FullPage = true });
            _output.AddAttachment(path);
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            _output.WriteLine($"Could not take a failure screenshot: {exception.Message}");
        }
    }

    // Traces are recorded for every scenario but only kept when it fails.
    private async Task SaveTraceIfFailedAsync()
    {
        if (_context is null)
        {
            return;
        }

        try
        {
            if (_scenarioContext.TestError is null)
            {
                await _context.Tracing.StopAsync();
                return;
            }

            var path = ArtifactPath("trace.zip");
            await _context.Tracing.StopAsync(new() { Path = path });
            _output.AddAttachment(path);
            _output.WriteLine($"Playwright trace saved to {path}; open it at https://trace.playwright.dev or with 'playwright.ps1 show-trace'.");
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            _output.WriteLine($"Could not save the Playwright trace: {exception.Message.Split('\n')[0]}");
        }
    }

    private void KeepVideoIfFailed(string? videoPath)
    {
        if (videoPath is null || !File.Exists(videoPath))
        {
            return;
        }

        try
        {
            if (_scenarioContext.TestError is null)
            {
                File.Delete(videoPath);
                return;
            }

            var path = ArtifactPath("webm");
            File.Move(videoPath, path, overwrite: true);
            _output.AddAttachment(path);
        }
        catch (IOException exception)
        {
            _output.WriteLine($"Could not save the video: {exception.Message}");
        }
    }

    private async Task SaveNetworkLogAsync()
    {
        if (_network is null)
        {
            return;
        }

        await _network.WaitForResponseBodiesAsync();

        var path = ArtifactPath("network.log");
        // Bodies only for the site's own API; third-party analytics payloads would bury them.
        await File.WriteAllLinesAsync(path, _network.Entries.Select(entry =>
            _network.IsFirstPartyApiCall(entry) ? entry.ToLogText() : entry.ToString()));
        _output.AddAttachment(path);

        foreach (var call in _network.FirstPartyApiCalls())
        {
            _output.WriteLine(call.ToLogText());
        }
    }

    private string ArtifactPath(string extension) =>
        Path.Combine(ArtifactDirectory(), $"{ArtifactName()}.{extension}");

    private string ArtifactName() =>
        $"{Regex.Replace(_scenarioContext.ScenarioInfo.Title, @"[^\w-]+", "-")}-{_artifactId}";

    private static string ArtifactDirectory() =>
        Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", "ui")).FullName;
}
