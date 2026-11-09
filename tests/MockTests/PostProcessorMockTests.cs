using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Api.Processors;
using Moq;
using NUnit.Framework;

namespace TechnicalTest.Tests.MockTests;

[TestFixture]
[Category("Mock")]
public class PostProcessorMockTests
{
    private Mock<IPublicApiClient> _mockApiClient = null!;
    private PostProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        _mockApiClient = new Mock<IPublicApiClient>();
        _processor = new PostProcessor(_mockApiClient.Object);
    }

    [Test]
    public async Task GetValidPostsAsync_WithSuccessfulApiCall_ReturnsValidPosts()
    {
        var mockPosts = new List<Post>
        {
            new() { UserId = 1, Id = 1, Title = "Valid Post", Body = "Valid Body" },
            new() { UserId = 2, Id = 2, Title = "Another Valid Post", Body = "Another Valid Body" }
        };

        _mockApiClient.Setup(x => x.GetPostsAsync()).ReturnsAsync(mockPosts);

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result, Is.EquivalentTo(mockPosts));
    }

    [Test]
    public async Task GetValidPostsAsync_WithEmptyList_ReturnsEmptyList()
    {
        _mockApiClient.Setup(x => x.GetPostsAsync()).ReturnsAsync(new List<Post>());

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetValidPostsAsync_WithInvalidPosts_FiltersThemOut()
    {
        var mockPosts = new List<Post>
        {
            new() { UserId = 1, Id = 1, Title = "Valid Post", Body = "Valid Body" },
            new() { UserId = 0, Id = 2, Title = "Invalid UserId", Body = "Body" },
            new() { UserId = 3, Id = 3, Title = "", Body = "Invalid Title" },
            new() { UserId = 4, Id = 4, Title = "Valid Title", Body = "" }
        };

        _mockApiClient.Setup(x => x.GetPostsAsync()).ReturnsAsync(mockPosts);

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(1));
    }

    [Test]
    public void GetValidPostsAsync_WhenApiThrowsException_PropagatesException()
    {
        _mockApiClient.Setup(x => x.GetPostsAsync()).ThrowsAsync(new HttpRequestException("Network error"));

        Assert.ThrowsAsync<HttpRequestException>(async () => await _processor.GetValidPostsAsync());
    }

    [Test]
    public async Task GetValidPostsAsync_WithNullTitlePost_FiltersItOut()
    {
        var mockPosts = new List<Post>
        {
            new() { UserId = 1, Id = 1, Title = "Valid Post", Body = "Valid Body" },
            new() { UserId = 2, Id = 2, Title = null!, Body = "Null Title" }
        };

        _mockApiClient.Setup(x => x.GetPostsAsync()).ReturnsAsync(mockPosts);

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.Not.Null);
    }

    [Test]
    public async Task GetValidPostsAsync_WithWhitespaceFields_FiltersThemOut()
    {
        var mockPosts = new List<Post>
        {
            new() { UserId = 1, Id = 1, Title = "Valid Post", Body = "Valid Body" },
            new() { UserId = 2, Id = 2, Title = "   ", Body = "Whitespace Title" },
            new() { UserId = 3, Id = 3, Title = "Valid Title", Body = "   " }
        };

        _mockApiClient.Setup(x => x.GetPostsAsync()).ReturnsAsync(mockPosts);

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetValidPostsAsync_VerifiesApiClientWasCalled()
    {
        _mockApiClient.Setup(x => x.GetPostsAsync()).ReturnsAsync(new List<Post>());

        await _processor.GetValidPostsAsync();

        _mockApiClient.Verify(x => x.GetPostsAsync(), Times.Once);
    }
}
