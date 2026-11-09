using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Tests.Support;
using NUnit.Framework;

namespace TechnicalTest.Tests.ApiTests;

[TestFixture]
[Category("API")]
public class PublicApiClientTests
{
    private HttpClient _httpClient = null!;
    private PublicApiClient _apiClient = null!;

    [SetUp]
    public void Setup()
    {
        _httpClient = new HttpClient { BaseAddress = ApiSettings.BaseAddress };
        _apiClient = new PublicApiClient(_httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    //Sample test- add more tests as needed
    [Test]
    public async Task GetPostsAsync_ReturnsListOfPosts()
    {
        var posts = await _apiClient.GetPostsAsync();

        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.GreaterThan(0));
    }

}
