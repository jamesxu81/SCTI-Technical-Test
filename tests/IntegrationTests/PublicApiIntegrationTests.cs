using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Api.Processors;
using TechnicalTest.Api.Validators;
using TechnicalTest.Tests.Support;
using TechnicalTest.Ui.Helpers;
using NUnit.Framework;

namespace TechnicalTest.Tests.IntegrationTests;

// [Description] values are the test case IDs in specs/6-integration-tests.plan.md.
[TestFixture]
[Category("Integration")]
public class PublicApiIntegrationTests
{
    private const int TotalPosts = 100;
    // JSONPlaceholder fakes creation: nothing is stored and the new id is always 101.
    private const int CreatedPostId = 101;

    // Range ends, user boundaries (10/11, 91) and the middle (50/51).
    private static readonly int[] BoundaryIds = { 1, 10, 11, 25, 50, 51, 75, 91, 100 };

    private HttpClient _httpClient = null!;
    private IPublicApiClient _apiClient = null!;
    private PostProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        _httpClient = ApiSettings.CreateHttpClient();
        _apiClient = new PublicApiClient(_httpClient);
        _processor = new PostProcessor(_apiClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    #region Real HTTP client usage

    [Test, Description("HTTP-01")]
    public async Task ConfiguredClient_ReachesTheConfiguredApi()
    {
        var configured = ConfigurationHelper.Configuration["ApiBaseUrl"]!.TrimEnd('/') + "/";

        var posts = await _apiClient.GetPostsAsync();

        Assert.That(_httpClient.BaseAddress, Is.EqualTo(new Uri(configured)));
        Assert.That(posts, Is.Not.Empty);
    }

    [Test, Description("HTTP-02")]
    public async Task OneClient_ServesSeveralCalls()
    {
        var posts = await _apiClient.GetPostsAsync();
        var post = await _apiClient.GetPostAsync(1);
        var created = await _apiClient.CreatePostAsync(NewPost("Shared client", "Created after two reads"));

        Assert.That(posts, Has.Count.EqualTo(TotalPosts));
        Assert.That(post?.Id, Is.EqualTo(1));
        Assert.That(created.Id, Is.EqualTo(CreatedPostId));
    }

    #endregion

    #region Real API calls

    [Test, Description("API-01")]
    public async Task GetPostsAsync_ReturnsLivePostsWithEveryFieldFilledIn()
    {
        var posts = await _apiClient.GetPostsAsync();

        Assert.That(posts, Has.Count.EqualTo(TotalPosts));
        Assert.That(posts, Has.All.Matches<Post>(p => p.UserId > 0 && p.Id > 0 && p.Title != "" && p.Body != ""));
    }

    [Test, Description("API-02")]
    public async Task GetPostAsync_ReturnsLivePost()
    {
        var post = await _apiClient.GetPostAsync(1);

        Assert.That(post, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(post!.Id, Is.EqualTo(1));
            Assert.That(post.UserId, Is.EqualTo(1));
            Assert.That(post.Title, Does.StartWith("sunt aut facere"));
            Assert.That(post.Body, Is.Not.Empty);
        });
    }

    [Test, Description("API-03")]
    public async Task CreatePostAsync_ReturnsTheCreatedPost()
    {
        var newPost = NewPost("Integration post", "Created by PublicApiIntegrationTests");

        var created = await _apiClient.CreatePostAsync(newPost);

        AssertSamePost(created, new Post { UserId = newPost.UserId, Id = CreatedPostId, Title = newPost.Title, Body = newPost.Body });
    }

    #endregion

    #region End-to-end flow validation

    [Test, Description("E2E-01")]
    public async Task Processor_WithLiveData_ReturnsAllPostsInOrder()
    {
        var valid = await _processor.GetValidPostsAsync();

        Assert.That(valid.Select(p => p.Id), Is.EqualTo(Enumerable.Range(1, TotalPosts)));
    }

    [Test, Description("E2E-02")]
    public async Task Processor_KeepsExactlyWhatTheValidatorAccepts()
    {
        var posts = await _apiClient.GetPostsAsync();
        var valid = await _processor.GetValidPostsAsync();

        Assert.That(Fields(valid), Is.EqualTo(Fields(posts.Where(PostValidator.IsValid))));
    }

    [Test, Description("E2E-03")]
    public async Task CreatedPost_PassesTheValidator()
    {
        var created = await _apiClient.CreatePostAsync(NewPost("Valid title", "Valid body"));

        Assert.That(PostValidator.IsValid(created), Is.True);
    }

    [Test, Description("E2E-04")]
    public async Task CreatedPost_IsNotStored()
    {
        await _apiClient.CreatePostAsync(NewPost("Not stored", "JSONPlaceholder fakes writes"));

        Assert.That(await _apiClient.GetPostAsync(CreatedPostId), Is.Null);
    }

    [Test, Description("E2E-05")]
    public async Task GetPostAsync_WithUnknownId_ReturnsNull()
    {
        Assert.That(await _apiClient.GetPostAsync(9999), Is.Null);
    }

    #endregion

    #region Data consistency checks

    [Description("CON-01")]
    [TestCaseSource(nameof(BoundaryIds))]
    public async Task ListAndSinglePost_Agree(int id)
    {
        var posts = await _apiClient.GetPostsAsync();
        var post = await _apiClient.GetPostAsync(id);

        Assert.That(post, Is.Not.Null);
        AssertSamePost(post!, posts.Single(p => p.Id == id));
    }

    [Test, Description("CON-02")]
    public async Task RepeatedCalls_ReturnTheSameData()
    {
        var first = await _apiClient.GetPostsAsync();
        var second = await _apiClient.GetPostsAsync();

        Assert.That(Fields(second), Is.EqualTo(Fields(first)));
    }

    [Test, Description("CON-03")]
    public async Task ProcessedPosts_MatchTheRawList()
    {
        var posts = (await _apiClient.GetPostsAsync()).ToDictionary(p => p.Id);
        var valid = await _processor.GetValidPostsAsync();

        Assert.Multiple(() =>
        {
            foreach (var post in valid)
            {
                AssertSamePost(post, posts[post.Id]);
            }
        });
    }

    [Test, Description("CON-04")]
    public async Task LiveData_HasAConsistentShape()
    {
        var posts = await _apiClient.GetPostsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(posts.Select(p => p.Id), Is.EqualTo(Enumerable.Range(1, TotalPosts)));
            Assert.That(posts.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Count()),
                Is.EquivalentTo(Enumerable.Range(1, 10).ToDictionary(userId => userId, _ => 10)));
            Assert.That(posts.Select(p => p.Title), Is.Unique);
            Assert.That(posts, Has.All.Matches<Post>(p => p.Body.Contains('\n')),
                "every live body has line breaks; they should survive deserialisation");
        });
    }

    [Test, Description("CON-05")]
    public async Task CreatedPost_EchoesUnicodeAndLineBreaksExactly()
    {
        var newPost = NewPost("Kia ora – ñ 漢字 😀", "Line one\nLine two – Māori macrons ā ē ī ō ū");

        var created = await _apiClient.CreatePostAsync(newPost);

        Assert.That(created.Title, Is.EqualTo(newPost.Title));
        Assert.That(created.Body, Is.EqualTo(newPost.Body));
    }

    #endregion

    #region Multiple valid IDs testing

    [Description("MID-01")]
    [TestCaseSource(nameof(BoundaryIds))]
    public async Task GetPostAsync_WithValidId_ReturnsThatPost(int id)
    {
        var post = await _apiClient.GetPostAsync(id);

        Assert.That(post, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(post!.Id, Is.EqualTo(id));
            Assert.That(post.UserId, Is.EqualTo((id - 1) / 10 + 1), "the API gives each user 10 posts in id order");
            Assert.That(post.Title, Is.Not.Empty);
            Assert.That(post.Body, Is.Not.Empty);
            Assert.That(PostValidator.IsValid(post), Is.True);
        });
    }

    [Test, Description("MID-02")]
    public async Task EveryId_AgreesWithTheList()
    {
        var posts = await _apiClient.GetPostsAsync();
        // At most 10 requests at a time, to be gentle with the public API.
        using var throttle = new SemaphoreSlim(10);

        var singles = await Task.WhenAll(Enumerable.Range(1, TotalPosts).Select(async id =>
        {
            await throttle.WaitAsync();
            try
            {
                return await _apiClient.GetPostAsync(id);
            }
            finally
            {
                throttle.Release();
            }
        }));

        Assert.That(singles, Has.None.Null);
        Assert.That(Fields(singles.Select(p => p!)), Is.EqualTo(Fields(posts)));
    }

    #endregion

    #region Negative integration

    [Test, Description("NEG-01")]
    public void Processor_WhenHostCannotBeReached_ThrowsHttpRequestException()
    {
        // The .invalid domain is reserved and never resolves (RFC 2606).
        using var http = new HttpClient(new HttpLoggingHandler()) { BaseAddress = new Uri("https://api.invalid/") };
        var processor = new PostProcessor(new PublicApiClient(http));

        Assert.ThrowsAsync<HttpRequestException>(() => processor.GetValidPostsAsync());
    }

    #endregion

    private static Post NewPost(string title, string body) => new() { UserId = 1, Title = title, Body = body };

    private static IEnumerable<(int UserId, int Id, string Title, string Body)> Fields(IEnumerable<Post> posts) =>
        posts.Select(p => (p.UserId, p.Id, p.Title, p.Body)).ToList();

    private static void AssertSamePost(Post actual, Post expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.UserId, Is.EqualTo(expected.UserId), $"UserId of post {expected.Id}");
            Assert.That(actual.Id, Is.EqualTo(expected.Id), "Id");
            Assert.That(actual.Title, Is.EqualTo(expected.Title), $"Title of post {expected.Id}");
            Assert.That(actual.Body, Is.EqualTo(expected.Body), $"Body of post {expected.Id}");
        });
    }
}
