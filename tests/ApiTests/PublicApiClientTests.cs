using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Tests.Support;
using NUnit.Framework;

namespace TechnicalTest.Tests.ApiTests;

// [Description] values are the test case IDs in specs/2-api-automation-tests.plan.md.
[TestFixture]
[Category("API")]
public class PublicApiClientTests
{
    private const int TotalPosts = 100;
    // JSONPlaceholder fakes creation: nothing is stored and the new id is always 101.
    private const int CreatedPostId = 101;

    private static readonly Lazy<JsonSchema> PostSchema = new(() =>
        JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "ApiTests", "Schemas", "post.schema.json")));

    private static readonly EvaluationOptions SchemaOptions = new() { OutputFormat = OutputFormat.List };

    private static readonly object NewPost = new { userId = 1, title = "Test automation post", body = "Created by PublicApiClientTests" };

    private HttpClient _httpClient = null!;
    private PublicApiClient _apiClient = null!;

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

    #region GET /posts

    [Test, Description("GET-01")]
    public async Task GetPosts_Returns200WithJsonContentType()
    {
        using var response = await _httpClient.GetAsync("posts");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test, Description("GET-02")]
    public async Task GetPosts_ReturnsExactly100Posts()
    {
        var posts = await GetJsonAsync("posts");

        Assert.That(posts.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(posts.GetArrayLength(), Is.EqualTo(TotalPosts));
    }

    [Test, Description("GET-03")]
    public async Task GetPosts_EveryPostMatchesSchema()
    {
        var posts = await GetJsonAsync("posts");

        Assert.Multiple(() =>
        {
            foreach (var post in posts.EnumerateArray())
                AssertMatchesPostSchema(post);
        });
    }

    [Test, Description("GET-04")]
    public async Task GetPosts_IdsAreUniqueAndOrderedFrom1To100()
    {
        var posts = await GetJsonAsync("posts");

        var ids = posts.EnumerateArray().Select(p => p.GetProperty("id").GetInt32());

        Assert.That(ids, Is.EqualTo(Enumerable.Range(1, TotalPosts)));
    }

    [Test, Description("GET-05")]
    public async Task GetPosts_EachOf10UsersHas10Posts()
    {
        var posts = await GetJsonAsync("posts");

        var postsPerUser = posts.EnumerateArray()
            .GroupBy(p => p.GetProperty("userId").GetInt32())
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.That(postsPerUser.Keys, Is.EquivalentTo(Enumerable.Range(1, 10)));
        Assert.That(postsPerUser.Values, Has.All.EqualTo(10));
    }

    [Test, Description("GET-06")]
    public async Task GetPosts_FilteredByUserId_ReturnsOnlyThatUsersPosts()
    {
        var posts = await GetJsonAsync("posts?userId=1");

        Assert.That(posts.GetArrayLength(), Is.EqualTo(10));
        Assert.That(posts.EnumerateArray().Select(p => p.GetProperty("userId").GetInt32()), Has.All.EqualTo(1));
    }

    [Test, Description("GET-07")]
    public async Task GetPosts_FilteredByUnknownUserId_ReturnsEmptyArray()
    {
        var posts = await GetJsonAsync("posts?userId=999");

        Assert.That(posts.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(posts.GetArrayLength(), Is.Zero);
    }

    #endregion

    #region GET /posts/{id}

    [Description("ID-01")]
    [TestCase(1)]
    [TestCase(50)]
    [TestCase(100)]
    public async Task GetPostById_WithValidId_Returns200AndMatchingPost(int id)
    {
        using var response = await _httpClient.GetAsync($"posts/{id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var post = await ReadJsonAsync(response);

        AssertMatchesPostSchema(post);
        Assert.That(post.GetProperty("id").GetInt32(), Is.EqualTo(id));
    }

    [Test, Description("ID-02")]
    public async Task GetPostById_Post1_HasExpectedContent()
    {
        var post = await GetJsonAsync("posts/1");

        Assert.That(post.GetProperty("userId").GetInt32(), Is.EqualTo(1));
        Assert.That(post.GetProperty("title").GetString(), Does.StartWith("sunt aut facere"));
    }

    [Test, Description("ID-03")]
    public async Task GetPostById_MatchesSamePostInList()
    {
        var single = await GetJsonAsync("posts/1");
        var all = await GetJsonAsync("posts");

        var first = all.EnumerateArray().First();

        Assert.That(JsonNode.DeepEquals(JsonNode.Parse(single.GetRawText()), JsonNode.Parse(first.GetRawText())), Is.True,
            $"/posts/1 {single.GetRawText()} differs from /posts[0] {first.GetRawText()}");
    }

    [Description("ID-04")]
    [TestCase(101)]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(9999)]
    public async Task GetPostById_WithOutOfRangeId_Returns404AndEmptyObject(int id)
    {
        using var response = await _httpClient.GetAsync($"posts/{id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var body = await ReadJsonAsync(response);
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(body.EnumerateObject(), Is.Empty);
    }

    [Test, Description("ID-05")]
    public async Task GetPostById_WithNonNumericId_Returns404()
    {
        using var response = await _httpClient.GetAsync("posts/abc");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region POST /posts

    [Test, Description("POST-01")]
    public async Task CreatePost_Returns201WithJsonContentType()
    {
        using var response = await _httpClient.PostAsJsonAsync("posts", NewPost);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test, Description("POST-02")]
    public async Task CreatePost_EchoesRequestWithNewId()
    {
        using var response = await _httpClient.PostAsJsonAsync("posts", NewPost);
        var created = await ReadJsonAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(created.GetProperty("userId").GetInt32(), Is.EqualTo(1));
            Assert.That(created.GetProperty("title").GetString(), Is.EqualTo("Test automation post"));
            Assert.That(created.GetProperty("body").GetString(), Is.EqualTo("Created by PublicApiClientTests"));
            Assert.That(created.GetProperty("id").GetInt32(), Is.EqualTo(CreatedPostId));
        });
    }

    [Test, Description("POST-03")]
    public async Task CreatePost_ResponseMatchesSchema()
    {
        using var response = await _httpClient.PostAsJsonAsync("posts", NewPost);

        AssertMatchesPostSchema(await ReadJsonAsync(response));
    }

    private static IEnumerable<TestCaseData> UnusualContent()
    {
        yield return new TestCaseData("Quote's \"special\" <chars> & symbols !@#$%^*()", "Line one\nLine two\ttabbed \\ backslash")
            .SetArgDisplayNames("special characters");
        yield return new TestCaseData("Kia ora – ñ 漢字 😀", "Māori macrons ā ē ī ō ū")
            .SetArgDisplayNames("unicode");
        yield return new TestCaseData("Long body", new string('x', 5000))
            .SetArgDisplayNames("5000-character body");
    }

    [Description("POST-04")]
    [TestCaseSource(nameof(UnusualContent))]
    public async Task CreatePost_WithUnusualContent_EchoesItUnchanged(string title, string body)
    {
        using var response = await _httpClient.PostAsJsonAsync("posts", new { userId = 1, title, body });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await ReadJsonAsync(response);

        Assert.That(created.GetProperty("title").GetString(), Is.EqualTo(title));
        Assert.That(created.GetProperty("body").GetString(), Is.EqualTo(body));
    }

    [Test, Description("POST-05")]
    public async Task CreatePost_WithEmptyObject_IsAcceptedWithoutValidation()
    {
        using var response = await _httpClient.PostAsJsonAsync("posts", new { });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await ReadJsonAsync(response);

        // Known API limitation: no server-side validation, so the response breaks the post schema.
        Assert.That(created.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "id" }));
        Assert.That(PostSchema.Value.Evaluate(created, SchemaOptions).IsValid, Is.False);
    }

    [Test, Description("POST-06")]
    public async Task CreatePost_WithMalformedJson_Returns500()
    {
        using var content = new StringContent("{\"title\": ", Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("posts", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test, Description("POST-07")]
    public async Task CreatePost_IsNotPersisted()
    {
        using var created = await _httpClient.PostAsJsonAsync("posts", NewPost);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var response = await _httpClient.GetAsync($"posts/{CreatedPostId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region PublicApiClient

    [Test, Description("CLI-01")]
    public async Task GetPostsAsync_Returns100FullyPopulatedPosts()
    {
        var posts = await _apiClient.GetPostsAsync();

        Assert.That(posts, Has.Count.EqualTo(TotalPosts));
        Assert.That(posts, Has.All.Matches<Post>(p => p.Id > 0 && p.UserId > 0 && p.Title != "" && p.Body != ""));
    }

    [Test, Description("CLI-02")]
    public async Task GetPostAsync_WithValidId_ReturnsPopulatedPost()
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

    [Test, Description("CLI-03")]
    public async Task GetPostAsync_WithUnknownId_ReturnsNull()
    {
        var post = await _apiClient.GetPostAsync(9999);

        Assert.That(post, Is.Null);
    }

    [Test, Description("CLI-04")]
    public async Task CreatePostAsync_ReturnsCreatedPost()
    {
        var newPost = new Post { UserId = 1, Title = "Client post", Body = "Created by PublicApiClient" };

        var created = await _apiClient.CreatePostAsync(newPost);

        Assert.Multiple(() =>
        {
            Assert.That(created.Id, Is.EqualTo(CreatedPostId));
            Assert.That(created.UserId, Is.EqualTo(newPost.UserId));
            Assert.That(created.Title, Is.EqualTo(newPost.Title));
            Assert.That(created.Body, Is.EqualTo(newPost.Body));
        });
    }

    [Test, Description("CLI-05")]
    public async Task Client_WhenServerReturns500_ThrowsOrReturnsNull()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError)) { BaseAddress = new Uri("https://stub.local/") };
        var client = new PublicApiClient(http);

        Assert.ThrowsAsync<HttpRequestException>(() => client.GetPostsAsync());
        Assert.ThrowsAsync<HttpRequestException>(() => client.CreatePostAsync(new Post { UserId = 1, Title = "t", Body = "b" }));
        Assert.That(await client.GetPostAsync(1), Is.Null);
    }

    #endregion

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await _httpClient.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"GET {path}");
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static void AssertMatchesPostSchema(JsonElement post)
    {
        var result = PostSchema.Value.Evaluate(post, SchemaOptions);

        Assert.That(result.IsValid, Is.True, () => $"{post.GetRawText()} breaks the post schema: {DescribeErrors(result)}");
    }

    private static string DescribeErrors(EvaluationResults result) =>
        string.Join("; ", new[] { result }.Concat(result.Details ?? [])
            .Where(r => r.Errors is { Count: > 0 })
            .SelectMany(r => r.Errors!.Select(e => $"{r.InstanceLocation} {e.Key}: {e.Value}")));

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }
}
