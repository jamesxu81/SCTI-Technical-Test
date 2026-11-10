using System.Net;
using System.Text.Json;
using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Api.Processors;
using Moq;
using NUnit.Framework;

namespace TechnicalTest.Tests.MockTests;

// [Description] values are the test case IDs in specs/4-mock-tests.plan.md.
[TestFixture]
[Category("Mock")]
public class PostProcessorMockTests
{
    private Mock<IPublicApiClient> _client = null!;
    private PostProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        _client = new Mock<IPublicApiClient>();
        _processor = new PostProcessor(_client.Object);
    }

    #region Simulating API success

    [Test, Description("SUC-01")]
    public async Task GetValidPostsAsync_WithValidPosts_ReturnsThemInOrder()
    {
        ClientReturns(ValidPost(1), ValidPost(2));

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test, Description("SUC-02")]
    public async Task GetValidPostsAsync_ReturnsTheSamePostsUnchanged()
    {
        var first = ValidPost(1);
        var second = ValidPost(2);
        ClientReturns(first, second);

        var result = await _processor.GetValidPostsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.SameAs(first));
            Assert.That(result[1], Is.SameAs(second));
            Assert.That(first.UserId, Is.EqualTo(1));
            Assert.That(first.Title, Is.EqualTo("Title 1"));
            Assert.That(first.Body, Is.EqualTo("Body 1"));
        });
    }

    [Test, Description("SUC-03")]
    public async Task GetValidPostsAsync_DoesNotChangeTheClientsList()
    {
        var posts = new List<Post> { ValidPost(1), Broken(2, p => p.UserId = 0), ValidPost(3) };
        _client.Setup(c => c.GetPostsAsync()).ReturnsAsync(posts);

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result, Is.Not.SameAs(posts));
        Assert.That(posts.Select(p => p.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test, Description("SUC-04")]
    public async Task GetValidPostsAsync_WithAFullPageOf100Posts_ReturnsAll()
    {
        ClientReturns(Enumerable.Range(1, 100).Select(ValidPost).ToArray());

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result, Has.Count.EqualTo(100));
    }

    #endregion

    #region Simulating API failures

    [Test, Description("FAIL-01")]
    public void GetValidPostsAsync_WhenApiFails_PropagatesTheSameException()
    {
        var failure = new HttpRequestException("Network error");
        _client.Setup(c => c.GetPostsAsync()).ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<HttpRequestException>(() => _processor.GetValidPostsAsync());

        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(thrown!.Message, Is.EqualTo("Network error"));
    }

    // The real client throws HttpRequestException with the status code for any non-success response.
    [Description("FAIL-02")]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public void GetValidPostsAsync_WhenApiFails_KeepsTheStatusCode(HttpStatusCode status)
    {
        _client.Setup(c => c.GetPostsAsync())
            .ThrowsAsync(new HttpRequestException($"Response status code: {(int)status}", null, status));

        var thrown = Assert.ThrowsAsync<HttpRequestException>(() => _processor.GetValidPostsAsync());

        Assert.That(thrown!.StatusCode, Is.EqualTo(status));
    }

    [Test, Description("FAIL-03")]
    public async Task GetValidPostsAsync_AfterAFailure_ReturnsPostsOnTheNextCall()
    {
        _client.SetupSequence(c => c.GetPostsAsync())
            .ThrowsAsync(new HttpRequestException("Temporary failure"))
            .ReturnsAsync(new List<Post> { ValidPost(1) });

        Assert.ThrowsAsync<HttpRequestException>(() => _processor.GetValidPostsAsync());
        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1 }));
    }

    #endregion

    #region Simulating empty lists

    [Test, Description("EMP-01")]
    public async Task GetValidPostsAsync_WithEmptyList_ReturnsEmptyList()
    {
        ClientReturns();

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result, Is.Not.Null.And.Empty);
    }

    [Test, Description("EMP-02")]
    public async Task GetValidPostsAsync_WhenEveryPostIsInvalid_ReturnsEmptyList()
    {
        ClientReturns(
            Broken(1, p => p.UserId = 0),
            Broken(2, p => p.Title = ""),
            Broken(3, p => p.Body = "   "));

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result, Is.Empty);
    }

    // Known gap (plan M1): a null list isn't treated as empty. An unconfigured loose mock also returns null.
    [Description("EMP-03")]
    [TestCase(true, TestName = "GetValidPostsAsync_WhenClientReturnsNull_ThrowsArgumentNullException(explicit null)")]
    [TestCase(false, TestName = "GetValidPostsAsync_WhenClientReturnsNull_ThrowsArgumentNullException(unconfigured mock)")]
    public void GetValidPostsAsync_WhenClientReturnsNull_ThrowsArgumentNullException(bool setUpNull)
    {
        if (setUpNull)
        {
            _client.Setup(c => c.GetPostsAsync()).ReturnsAsync((List<Post>)null!);
        }

        var thrown = Assert.ThrowsAsync<ArgumentNullException>(() => _processor.GetValidPostsAsync());

        Assert.That(thrown!.ParamName, Is.EqualTo("source"));
    }

    #endregion

    #region Simulating thrown exceptions

    private static IEnumerable<TestCaseData> ClientExceptions()
    {
        yield return new TestCaseData(new TaskCanceledException("Request timed out")).SetArgDisplayNames("timeout");
        yield return new TestCaseData(new OperationCanceledException("Cancelled")).SetArgDisplayNames("cancelled");
        yield return new TestCaseData(new JsonException("Bad response body")).SetArgDisplayNames("bad JSON");
        yield return new TestCaseData(new InvalidOperationException("Unexpected")).SetArgDisplayNames("unexpected error");
    }

    [Description("EXC-01")]
    [TestCaseSource(nameof(ClientExceptions))]
    public void GetValidPostsAsync_WhenClientThrows_PropagatesTheSameException(Exception exception)
    {
        _client.Setup(c => c.GetPostsAsync()).ThrowsAsync(exception);

        var thrown = Assert.CatchAsync(() => _processor.GetValidPostsAsync());

        Assert.That(thrown, Is.SameAs(exception));
    }

    [Test, Description("EXC-02")]
    public void GetValidPostsAsync_WhenClientThrowsSynchronously_ReturnsAFaultedTask()
    {
        var failure = new InvalidOperationException("Thrown before a task was returned");
        _client.Setup(c => c.GetPostsAsync()).Throws(failure);

        var task = _processor.GetValidPostsAsync();

        Assert.That(task.IsFaulted, Is.True);
        Assert.That(Assert.CatchAsync(() => task), Is.SameAs(failure));
    }

    #endregion

    #region Simulating invalid posts

    [Test, Description("INV-01")]
    public async Task GetValidPostsAsync_WithMixedPosts_ReturnsOnlyTheValidOne()
    {
        ClientReturns(
            ValidPost(1),
            Broken(2, p => p.UserId = 0),
            Broken(3, p => p.Title = ""),
            Broken(4, p => p.Body = ""));

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1 }));
    }

    private static IEnumerable<TestCaseData> InvalidPosts()
    {
        yield return new TestCaseData(Broken(2, p => p.UserId = 0)).SetArgDisplayNames("UserId 0");
        yield return new TestCaseData(Broken(2, p => p.UserId = -1)).SetArgDisplayNames("UserId -1");
        yield return new TestCaseData(Broken(2, p => p.Title = null!)).SetArgDisplayNames("Title null");
        yield return new TestCaseData(Broken(2, p => p.Title = "")).SetArgDisplayNames("Title empty");
        yield return new TestCaseData(Broken(2, p => p.Title = "   ")).SetArgDisplayNames("Title whitespace");
        yield return new TestCaseData(Broken(2, p => p.Body = null!)).SetArgDisplayNames("Body null");
        yield return new TestCaseData(Broken(2, p => p.Body = "")).SetArgDisplayNames("Body empty");
        yield return new TestCaseData(Broken(2, p => p.Body = "   ")).SetArgDisplayNames("Body whitespace");
    }

    [Description("INV-02")]
    [TestCaseSource(nameof(InvalidPosts))]
    public async Task GetValidPostsAsync_DropsEachKindOfInvalidPost(Post invalid)
    {
        ClientReturns(ValidPost(1), invalid);

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1 }));
    }

    [Test, Description("INV-03")]
    public async Task GetValidPostsAsync_WithNullItem_DropsIt()
    {
        ClientReturns(null!, ValidPost(1));

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1 }));
    }

    [Test, Description("INV-04")]
    public async Task GetValidPostsAsync_KeepsTheOrderOfValidPosts()
    {
        ClientReturns(
            ValidPost(1),
            Broken(2, p => p.UserId = 0),
            ValidPost(3),
            Broken(4, p => p.Title = ""),
            ValidPost(5));

        var result = await _processor.GetValidPostsAsync();

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1, 3, 5 }));
    }

    #endregion

    #region Verifying mock interactions

    [Test, Description("VER-01")]
    public async Task GetValidPostsAsync_CallsTheClientOnce()
    {
        ClientReturns();

        await _processor.GetValidPostsAsync();

        _client.Verify(c => c.GetPostsAsync(), Times.Once);
    }

    [Test, Description("VER-02")]
    public async Task GetValidPostsAsync_UsesNoOtherClientMethod()
    {
        ClientReturns(ValidPost(1));

        await _processor.GetValidPostsAsync();

        _client.Verify(c => c.GetPostsAsync(), Times.Once);
        _client.Verify(c => c.GetPostAsync(It.IsAny<int>()), Times.Never);
        _client.Verify(c => c.CreatePostAsync(It.IsAny<Post>()), Times.Never);
        _client.VerifyNoOtherCalls();
    }

    // A strict mock throws on any call that wasn't set up.
    [Test, Description("VER-03")]
    public async Task GetValidPostsAsync_WithStrictMock_MakesOnlyTheExpectedCall()
    {
        var strictClient = new Mock<IPublicApiClient>(MockBehavior.Strict);
        strictClient.Setup(c => c.GetPostsAsync()).ReturnsAsync(new List<Post> { ValidPost(1) });

        var result = await new PostProcessor(strictClient.Object).GetValidPostsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        strictClient.VerifyAll();
    }

    [Test, Description("VER-04")]
    public async Task GetValidPostsAsync_CalledTwice_CallsTheClientTwice()
    {
        ClientReturns(ValidPost(1));

        await _processor.GetValidPostsAsync();
        await _processor.GetValidPostsAsync();

        _client.Verify(c => c.GetPostsAsync(), Times.Exactly(2));
    }

    [Test, Description("VER-05")]
    public void GetValidPostsAsync_WhenApiFails_DoesNotRetry()
    {
        _client.Setup(c => c.GetPostsAsync()).ThrowsAsync(new HttpRequestException("Network error"));

        Assert.ThrowsAsync<HttpRequestException>(() => _processor.GetValidPostsAsync());

        _client.Verify(c => c.GetPostsAsync(), Times.Once);
    }

    [Test, Description("VER-06")]
    public void Constructor_DoesNotCallTheClient()
    {
        _ = new PostProcessor(_client.Object);

        _client.VerifyNoOtherCalls();
    }

    #endregion

    private void ClientReturns(params Post[] posts) =>
        _client.Setup(c => c.GetPostsAsync()).ReturnsAsync(posts.ToList());

    private static Post ValidPost(int id) => new() { UserId = 1, Id = id, Title = $"Title {id}", Body = $"Body {id}" };

    private static Post Broken(int id, Action<Post> breakField)
    {
        var post = ValidPost(id);
        breakField(post);
        return post;
    }
}
