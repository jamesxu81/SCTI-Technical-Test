using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;
using TechnicalTest.Ui.Helpers;

namespace TechnicalTest.Tests.Support;

public sealed record NetworkEntry(string Method, string Url, int? Status, string? RequestBody, string? Failure)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;

    public string Path => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : string.Empty;

    // Only read for the site's own API calls, after the response arrives.
    public string? ResponseBody { get; internal set; }

    public override string ToString() =>
        $"{Method} {Url} => {(Status is null ? $"failed: {Failure}" : Status.ToString())}";

    public string ToLogText()
    {
        var lines = new List<string> { ToString() };
        AddBody(lines, "Request body", RequestBody);
        AddBody(lines, "Response body", ResponseBody);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddBody(List<string> lines, string title, string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        lines.Add($"  {title}:");
        lines.AddRange(Pretty(body).Split('\n').Select(line => "    " + line.TrimEnd('\r')));
    }

    private static string Pretty(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(json.RootElement, Indented);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}

public sealed class NetworkRecorder
{
    private readonly ConcurrentQueue<NetworkEntry> _entries = new();
    private readonly ConcurrentQueue<Task> _pendingResponseBodies = new();
    private readonly string _siteHost = new Uri(UiSettings.BaseUrl).Host;

    public NetworkRecorder(IPage page)
    {
        page.Response += (_, response) =>
        {
            var entry = new NetworkEntry(
                response.Request.Method, response.Url, response.Status, response.Request.PostData, null);
            _entries.Enqueue(entry);
            if (IsFirstPartyApiCall(entry))
            {
                _pendingResponseBodies.Enqueue(ReadResponseBodyAsync(entry, response));
            }
        };
        page.RequestFailed += (_, request) => _entries.Enqueue(new NetworkEntry(
            request.Method, request.Url, null, request.PostData, request.Failure));
    }

    public IReadOnlyList<NetworkEntry> Entries => _entries.ToArray();

    // Response bodies are read in the background; await this before using ResponseBody.
    public Task WaitForResponseBodiesAsync() => Task.WhenAll(_pendingResponseBodies);

    public IReadOnlyList<NetworkEntry> FirstPartyApiCalls() =>
        Entries.Where(IsFirstPartyApiCall).ToList();

    public IReadOnlyList<NetworkEntry> ApiCalls(string method, string path) =>
        FirstPartyApiCalls().Where(entry => Matches(entry, method, path)).ToList();

    public IReadOnlyList<NetworkEntry> FirstPartyApiCallsAfter(string method, string path)
    {
        var calls = FirstPartyApiCalls().ToList();
        var lastMatch = calls.FindLastIndex(entry => Matches(entry, method, path));
        return calls.Skip(lastMatch + 1).ToList();
    }

    private static bool Matches(NetworkEntry entry, string method, string path) =>
        entry.Method == method && entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase);

    public bool IsFirstPartyApiCall(NetworkEntry entry) =>
        entry.Host == _siteHost && entry.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

    private static async Task ReadResponseBodyAsync(NetworkEntry entry, IResponse response)
    {
        try
        {
            entry.ResponseBody = await response.TextAsync();
        }
        catch (PlaywrightException exception)
        {
            entry.ResponseBody = $"(response body unavailable: {exception.Message.Split('\n')[0]})";
        }
    }
}
