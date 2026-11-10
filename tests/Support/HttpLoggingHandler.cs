using NUnit.Framework;

namespace TechnicalTest.Tests.Support;

// Writes every API call, with its request and response bodies, to the current test's output.
public sealed class HttpLoggingHandler : DelegatingHandler
{
    public HttpLoggingHandler() : base(new HttpClientHandler())
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method.Method;
        var url = request.RequestUri?.ToString() ?? "";
        var requestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            TestContext.Out.WriteLine(new NetworkEntry(method, url, null, requestBody, exception.Message).ToLogText());
            throw;
        }

        // Reading buffers the content, so the test can still read it afterwards.
        var entry = new NetworkEntry(method, url, (int)response.StatusCode, requestBody, null)
        {
            ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken)
        };
        TestContext.Out.WriteLine(entry.ToLogText());
        return response;
    }
}
