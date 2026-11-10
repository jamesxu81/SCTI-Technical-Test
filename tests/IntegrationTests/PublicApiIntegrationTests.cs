using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Tests.Support;
using NUnit.Framework;

namespace TechnicalTest.Tests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class PublicApiIntegrationTests
{
    private HttpClient _httpClient = null!;
    private IPublicApiClient _apiClient = null!;

    [SetUp]
    public void Setup()
    {
        _httpClient = ApiSettings.CreateHttpClient();
        _apiClient = new PublicApiClient(_httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    
}
