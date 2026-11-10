using System.Net;
using System.Text;
using System.Text.Json;
using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Api.Processors;
using TechnicalTest.Api.Validators;
using Moq;
using NUnit.Framework;

namespace TechnicalTest.Tests.ComponentTests;

// [Description] values are the test case IDs in specs/5-component-tests.plan.md.
[TestFixture]
[Category("Component")]
public class PostProcessorComponentTests
{
    private static readonly (string Name, Action<Post> Break)[] InvalidKinds =
    {
        ("UserId 0", p => p.UserId = 0),
        ("UserId -1", p => p.UserId = -1),
        ("Title null", p => p.Title = null!),
        ("Title empty", p => p.Title = ""),
        ("Title whitespace", p => p.Title = "   "),
        ("Body null", p => p.Body = null!),
        ("Body empty", p => p.Body = ""),
        ("Body whitespace", p => p.Body = "   "),
    };

    private Mock<IPublicApiClient> _mockApiClient = null!;
    private PostProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        _mockApiClient = new Mock<IPublicApiClient>();
        _processor = new PostProcessor(_mockApiClient.Object);
    }

    #region PostProcessor filtering logic

    [Test, Description("FLT-01")]
    public async Task Processor_KeepsValidPostsWithoutAddingOrChangingAny()
    {
        var input = new List<Post>
        {
            ValidPost(1), Broken(2, p => p.UserId = 0), ValidPost(3),
            Broken(4, p => p.Title = ""), ValidPost(5), Broken(6, p => p.Body = null!),
        };

        var result = await ProcessAsync(input);

        Assert.That(result, Has.All.Matches<Post>(kept => input.Any(item => ReferenceEquals(item, kept))));
        Assert.That(result.Count + input.Count(item => !result.Contains(item)), Is.EqualTo(input.Count));
        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1, 3, 5 }));
        Assert.That(result.Select(p => p.Title), Is.EqualTo(new[] { "Title 1", "Title 3", "Title 5" }));
    }

    [Test, Description("FLT-02")]
    public async Task Processor_KeepsOrderAroundInvalidPostsAtStartEndAndInARun()
    {
        var input = new List<Post>
        {
            Broken(1, p => p.UserId = 0), ValidPost(2), Broken(3, p => p.Title = ""), Broken(4, p => p.Body = ""),
            Broken(5, p => p.UserId = -1), ValidPost(6), ValidPost(7), Broken(8, p => p.Title = null!),
        };

        var result = await ProcessAsync(input);

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 2, 6, 7 }));
    }

    // Known behaviour (plan C3): duplicates aren't removed.
    [Description("FLT-03")]
    [TestCase(true, TestName = "Processor_KeepsDuplicates(same instance twice)")]
    [TestCase(false, TestName = "Processor_KeepsDuplicates(two posts with the same Id)")]
    public async Task Processor_KeepsDuplicates(bool sameInstance)
    {
        var first = ValidPost(1);
        var second = sameInstance ? first : ValidPost(1);

        var result = await ProcessAsync(new List<Post> { first, second });

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.SameAs(first));
        Assert.That(result[1], Is.SameAs(second));
    }

    [Test, Description("FLT-04")]
    public async Task Processor_WithLargeDataSet_KeepsEveryValidPost()
    {
        var result = await ProcessAsync(MixedSet(total: 10_000, invalidEvery: 3));

        Assert.That(result, Has.Count.EqualTo(6_667));
        Assert.That(result.Select(p => p.Id), Is.EqualTo(Enumerable.Range(1, 10_000).Where(id => id % 3 != 0)));
    }

    #endregion

    #region Business rules validation

    // Every combination of UserId {valid, 0, -1} x Title {valid, null, empty, whitespace} x Body {same}.
    private static IEnumerable<(string Name, Post Post, bool Kept)> DecisionTable()
    {
        var userIds = new (string Name, int Value)[] { ("valid", 1), ("0", 0), ("-1", -1) };
        var texts = new (string Name, string? Value)[] { ("valid", "Text"), ("null", null), ("empty", ""), ("whitespace", "  ") };
        var id = 0;
        foreach (var userId in userIds)
        foreach (var title in texts)
        foreach (var body in texts)
        {
            yield return (
                $"UserId {userId.Name}, Title {title.Name}, Body {body.Name}",
                new Post { UserId = userId.Value, Id = ++id, Title = title.Value!, Body = body.Value! },
                userId.Name == "valid" && title.Name == "valid" && body.Name == "valid");
        }
    }

    private static IEnumerable<TestCaseData> DecisionTableRows() =>
        DecisionTable().Select(row => new TestCaseData(row.Post, row.Kept).SetArgDisplayNames(row.Name));

    [Description("BR-01")]
    [TestCaseSource(nameof(DecisionTableRows))]
    public async Task Processor_AppliesEveryRuleCombination(Post post, bool expectedKept)
    {
        var result = await ProcessAsync(new List<Post> { post });

        Assert.That(result, Has.Count.EqualTo(expectedKept ? 1 : 0));
    }

    [Test, Description("BR-02")]
    public async Task Processor_WithWholeDecisionTable_KeepsOnlyTheAllValidPost()
    {
        var table = DecisionTable().ToList();

        var result = await ProcessAsync(table.Select(row => row.Post).ToList());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.SameAs(table.Single(row => row.Kept).Post));
    }

    // Known behaviour (plan C2): Id isn't a business rule.
    [Description("BR-03")]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public async Task Processor_IgnoresId(int id)
    {
        var post = ValidPost(1);
        post.Id = id;

        var result = await ProcessAsync(new List<Post> { post });

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test, Description("BR-04")]
    public async Task Processor_KeepsValuesAtTheEdgesOfTheRules()
    {
        var input = new List<Post>
        {
            Broken(1, p => p.UserId = int.MaxValue),
            Broken(2, p => p.Title = "  Title  "),
            Broken(3, p => p.Body = new string('x', 10_000)),
        };

        var result = await ProcessAsync(input);

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    #endregion

    #region Partial data handling (JSON through the real client)

    private static IEnumerable<TestCaseData> RecordsMissingRequiredData()
    {
        yield return new TestCaseData("""{ "userId": 2, "id": 2, "body": "b" }""").SetArgDisplayNames("no title");
        yield return new TestCaseData("""{ "userId": 2, "id": 2, "title": "t" }""").SetArgDisplayNames("no body");
        yield return new TestCaseData("""{ "id": 2, "title": "t", "body": "b" }""").SetArgDisplayNames("no userId");
        yield return new TestCaseData("{}").SetArgDisplayNames("empty object");
        yield return new TestCaseData("""{ "userId": 2, "id": 2, "title": null, "body": "b" }""").SetArgDisplayNames("title null");
        yield return new TestCaseData("null").SetArgDisplayNames("null item");
    }

    [Description("PD-01")]
    [TestCaseSource(nameof(RecordsMissingRequiredData))]
    public async Task Processor_DropsRecordsMissingRequiredData(string record)
    {
        var result = await ProcessJsonAsync($"[{Record(1, 1)}, {record}]");

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1 }));
    }

    // Known behaviour (plan C2): a record without an id is kept with Id 0.
    [Test, Description("PD-02")]
    public async Task Processor_KeepsRecordWithoutId()
    {
        var result = await ProcessJsonAsync($$"""[{{Record(1, 1)}}, { "userId": 2, "title": "t", "body": "b" }]""");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[1].UserId, Is.EqualTo(2));
        Assert.That(result[1].Id, Is.Zero);
    }

    [Description("PD-03")]
    [TestCase("""{ "userId": 2, "id": 2, "title": "t", "body": "b", "tags": ["x"] }""", TestName = "Processor_KeepsRecordsWithExtraOrDifferentlyCasedProperties(extra property)")]
    [TestCase("""{ "UserId": 2, "ID": 2, "TITLE": "t", "Body": "b" }""", TestName = "Processor_KeepsRecordsWithExtraOrDifferentlyCasedProperties(different casing)")]
    public async Task Processor_KeepsRecordsWithExtraOrDifferentlyCasedProperties(string record)
    {
        var result = await ProcessJsonAsync($"[{Record(1, 1)}, {record}]");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[1].Id, Is.EqualTo(2));
        Assert.That(result[1].Title, Is.EqualTo("t"));
    }

    [Test, Description("PD-04")]
    public async Task Processor_ReadsNumberSentAsString()
    {
        var result = await ProcessJsonAsync("""[{ "userId": "2", "id": 2, "title": "t", "body": "b" }]""");

        Assert.That(result.Single().UserId, Is.EqualTo(2));
    }

    private static IEnumerable<TestCaseData> MalformedRecords()
    {
        yield return new TestCaseData("""{ "userId": null, "id": 2, "title": "t", "body": "b" }""").SetArgDisplayNames("userId null");
        yield return new TestCaseData("""{ "userId": "abc", "id": 2, "title": "t", "body": "b" }""").SetArgDisplayNames("userId text");
        yield return new TestCaseData("""{ "userId": 1.5, "id": 2, "title": "t", "body": "b" }""").SetArgDisplayNames("userId decimal");
        yield return new TestCaseData("""{ "userId": 2, "id": 2, "title": 123, "body": "b" }""").SetArgDisplayNames("title number");
    }

    // Known gap (plan C1): one record the client can't read loses the whole list, including the valid posts.
    [Description("PD-05")]
    [TestCaseSource(nameof(MalformedRecords))]
    public void Processor_WithOneMalformedRecord_FailsTheWholeList(string record)
    {
        Assert.ThrowsAsync<JsonException>(() => ProcessJsonAsync($"[{Record(1, 1)}, {record}, {Record(3, 3)}]"));
    }

    [Test, Description("PD-06")]
    public async Task Processor_WithMostlyCompleteData_KeepsTheCompleteRecords()
    {
        // 100 records in the API's shape; every 20th has no title.
        var records = Enumerable.Range(1, 100).Select(id => id % 20 == 0
            ? $$"""{ "userId": {{UserIdFor(id)}}, "id": {{id}}, "body": "Body {{id}}" }"""
            : Record(UserIdFor(id), id));

        var result = await ProcessJsonAsync($"[{string.Join(", ", records)}]");

        Assert.That(result, Has.Count.EqualTo(95));
        Assert.That(result.Select(p => p.Id), Is.EqualTo(Enumerable.Range(1, 100).Where(id => id % 20 != 0)));
    }

    #endregion

    #region Mixed valid/invalid data scenarios

    [Test, Description("MIX-01")]
    public async Task Processor_WithApiLikeDataSet_DropsEveryBrokenPost()
    {
        var result = await ProcessAsync(MixedSet(total: 100, invalidEvery: 10));

        Assert.That(result, Has.Count.EqualTo(90));
        Assert.That(result.Select(p => p.Id), Has.None.Matches<int>(id => id % 10 == 0));
    }

    [Description("MIX-02")]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(9)]
    [TestCase(10)]
    public async Task Processor_KeepsTheValidShareOfTenPosts(int invalidCount)
    {
        var input = Enumerable.Range(1, 10)
            .Select(id => id <= invalidCount ? Broken(id, InvalidKinds[id % InvalidKinds.Length].Break) : ValidPost(id))
            .ToList();

        var result = await ProcessAsync(input);

        Assert.That(result, Has.Count.EqualTo(10 - invalidCount));
    }

    [Test, Description("MIX-03")]
    public async Task Processor_WithEveryInvalidKindInterleaved_KeepsTheValidPostsInOrder()
    {
        var result = await ProcessAsync(EveryInvalidKindInterleaved());

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1, 3, 5, 7, 9, 11, 13, 15 }));
    }

    #endregion

    #region Integration of validator with processor

    private static IEnumerable<TestCaseData> OracleDataSets()
    {
        yield return new TestCaseData(DecisionTable().Select(row => row.Post).ToList()).SetArgDisplayNames("decision table");
        yield return new TestCaseData(MixedSet(total: 100, invalidEvery: 10)).SetArgDisplayNames("API-like mixed set");
    }

    [Description("INT-01")]
    [TestCaseSource(nameof(OracleDataSets))]
    public async Task Processor_ResultEqualsTheValidatorsVerdict(List<Post> input)
    {
        var expected = input.Where(PostValidator.IsValid).ToList();

        var result = await ProcessAsync(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test, Description("INT-02")]
    public async Task Processor_KeepsOnlyPostsTheValidatorAccepts()
    {
        var input = EveryInvalidKindInterleaved();

        var result = await ProcessAsync(input);
        var dropped = input.Where(post => !result.Contains(post)).ToList();

        Assert.That(result, Has.All.Matches<Post>(PostValidator.IsValid));
        Assert.That(dropped, Has.None.Matches<Post>(PostValidator.IsValid));
        Assert.That(dropped, Has.Count.EqualTo(InvalidKinds.Length));
    }

    [Test, Description("INT-03")]
    public async Task ClientProcessorAndValidator_TogetherWithoutTheNetwork_ReturnTheExpectedPosts()
    {
        var json = $$"""
            [
              {{Record(1, 1)}},
              { "userId": 1, "id": 2, "body": "No title" },
              {{Record(1, 3)}},
              { "userId": 0, "id": 4, "title": "Bad user", "body": "Body 4" },
              { "userId": 5, "title": "No id", "body": "Body" },
              { "userId": "6", "id": 6, "title": "User id as text", "body": "Body 6" }
            ]
            """;

        var result = await ProcessJsonAsync(json);

        Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { 1, 3, 0, 6 }));
        Assert.That(result.Select(p => p.UserId), Is.EqualTo(new[] { 1, 1, 5, 6 }));
        Assert.That(result, Has.All.Matches<Post>(p => p.Title.Length > 0 && p.Body.Length > 0));
    }

    #endregion

    private async Task<List<Post>> ProcessAsync(List<Post> input)
    {
        _mockApiClient.Setup(c => c.GetPostsAsync()).ReturnsAsync(input);
        return await _processor.GetValidPostsAsync();
    }

    // The JSON goes through the real PublicApiClient, the same way an API response would.
    private static async Task<List<Post>> ProcessJsonAsync(string json)
    {
        using var http = new HttpClient(new JsonStub(json)) { BaseAddress = new Uri("https://stub.local/") };
        return await new PostProcessor(new PublicApiClient(http)).GetValidPostsAsync();
    }

    // Like the API: ten posts per user.
    private static int UserIdFor(int id) => (id - 1) / 10 + 1;

    private static Post ValidPost(int id) =>
        new() { UserId = UserIdFor(id), Id = id, Title = $"Title {id}", Body = $"Body {id}" };

    private static Post Broken(int id, Action<Post> change)
    {
        var post = ValidPost(id);
        change(post);
        return post;
    }

    // Every `invalidEvery`-th post is broken, cycling through the invalid kinds.
    private static List<Post> MixedSet(int total, int invalidEvery)
    {
        var posts = new List<Post>(total);
        var kind = 0;
        for (var id = 1; id <= total; id++)
        {
            posts.Add(id % invalidEvery == 0
                ? Broken(id, InvalidKinds[kind++ % InvalidKinds.Length].Break)
                : ValidPost(id));
        }

        return posts;
    }

    // Odd ids are valid; each even id is broken in a different way.
    private static List<Post> EveryInvalidKindInterleaved() =>
        Enumerable.Range(1, InvalidKinds.Length * 2)
            .Select(id => id % 2 == 1 ? ValidPost(id) : Broken(id, InvalidKinds[id / 2 - 1].Break))
            .ToList();

    private static string Record(int userId, int id) =>
        $$"""{ "userId": {{userId}}, "id": {{id}}, "title": "Title {{id}}", "body": "Body {{id}}" }""";

    private sealed class JsonStub(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}
