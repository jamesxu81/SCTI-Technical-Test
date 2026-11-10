using TechnicalTest.Api.Models;
using TechnicalTest.Api.Validators;
using NUnit.Framework;

namespace TechnicalTest.Tests.UnitTests;

// [Description] values are the test case IDs in specs/3-unit-tests.plan.md.
[TestFixture]
[Category("Unit")]
public class PostValidatorTests
{
    private const string ValidTitle = "Valid Title";
    private const string ValidBody = "Valid Body";

    #region Null checks

    [Test, Description("NUL-01")]
    public void IsValid_WithNullPost_ReturnsFalse()
    {
        Assert.That(PostValidator.IsValid(null), Is.False);
    }

    [Test, Description("NUL-02")]
    public void IsValid_WithNullTitle_ReturnsFalse()
    {
        Assert.That(PostValidator.IsValid(ValidPost(title: null)), Is.False);
    }

    [Test, Description("NUL-03")]
    public void IsValid_WithNullBody_ReturnsFalse()
    {
        Assert.That(PostValidator.IsValid(ValidPost(body: null)), Is.False);
    }

    [Test, Description("NUL-04")]
    public void IsValid_WithDefaultPost_ReturnsFalse()
    {
        Assert.That(PostValidator.IsValid(new Post()), Is.False);
    }

    #endregion

    #region Field validations

    [Test, Description("FLD-01")]
    public void IsValid_WithValidPost_ReturnsTrue()
    {
        Assert.That(PostValidator.IsValid(ValidPost()), Is.True);
    }

    [Description("FLD-02")]
    [TestCase(0)]
    [TestCase(-1)]
    public void IsValid_WithInvalidUserId_ReturnsFalse(int userId)
    {
        Assert.That(PostValidator.IsValid(ValidPost(userId: userId)), Is.False);
    }

    [Description("FLD-03")]
    [TestCase("")]
    [TestCase("   ")]
    public void IsValid_WithEmptyOrWhitespaceTitle_ReturnsFalse(string title)
    {
        Assert.That(PostValidator.IsValid(ValidPost(title: title)), Is.False);
    }

    [Description("FLD-04")]
    [TestCase("")]
    [TestCase("   ")]
    public void IsValid_WithEmptyOrWhitespaceBody_ReturnsFalse(string body)
    {
        Assert.That(PostValidator.IsValid(ValidPost(body: body)), Is.False);
    }

    // Id isn't validated: a new post has Id 0 until the API assigns one.
    [Description("FLD-05")]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void IsValid_IgnoresId(int id)
    {
        Assert.That(PostValidator.IsValid(ValidPost(id: id)), Is.True);
    }

    #endregion

    #region Boundary values

    [Description("BND-01")]
    [TestCase(int.MinValue, ExpectedResult = false)]
    [TestCase(-1, ExpectedResult = false)]
    [TestCase(0, ExpectedResult = false)]
    [TestCase(1, ExpectedResult = true)]
    [TestCase(2, ExpectedResult = true)]
    [TestCase(int.MaxValue, ExpectedResult = true)]
    public bool IsValid_UserIdBoundaries(int userId) => PostValidator.IsValid(ValidPost(userId: userId));

    [Description("BND-02")]
    [TestCase("", ExpectedResult = false)]
    [TestCase("T", ExpectedResult = true)]
    public bool IsValid_TitleLengthBoundaries(string title) => PostValidator.IsValid(ValidPost(title: title));

    [Description("BND-03")]
    [TestCase("", ExpectedResult = false)]
    [TestCase("B", ExpectedResult = true)]
    public bool IsValid_BodyLengthBoundaries(string body) => PostValidator.IsValid(ValidPost(body: body));

    #endregion

    #region Edge cases

    private static IEnumerable<TestCaseData> WhitespaceOnly()
    {
        yield return new TestCaseData(" ").SetArgDisplayNames("space");
        yield return new TestCaseData("   ").SetArgDisplayNames("spaces");
        yield return new TestCaseData("\t").SetArgDisplayNames("tab");
        yield return new TestCaseData("\n").SetArgDisplayNames("line feed");
        yield return new TestCaseData("\r\n").SetArgDisplayNames("CRLF");
        yield return new TestCaseData("\u00A0").SetArgDisplayNames("no-break space U+00A0");
        yield return new TestCaseData("\u2003").SetArgDisplayNames("em space U+2003");
        yield return new TestCaseData(" \t\n ").SetArgDisplayNames("mixed");
    }

    [Description("EDG-01")]
    [TestCaseSource(nameof(WhitespaceOnly))]
    public void IsValid_WithWhitespaceOnlyTitle_ReturnsFalse(string title)
    {
        Assert.That(PostValidator.IsValid(ValidPost(title: title)), Is.False);
    }

    [Description("EDG-02")]
    [TestCaseSource(nameof(WhitespaceOnly))]
    public void IsValid_WithWhitespaceOnlyBody_ReturnsFalse(string body)
    {
        Assert.That(PostValidator.IsValid(ValidPost(body: body)), Is.False);
    }

    [Test, Description("EDG-03")]
    public void IsValid_WithTextSurroundedByWhitespace_ReturnsTrue()
    {
        Assert.That(PostValidator.IsValid(ValidPost(title: "  Title  ", body: "\tBody\n")), Is.True);
    }

    // Known gap (plan O1): zero-width characters aren't whitespace, so an invisible title passes.
    [Description("EDG-04")]
    [TestCase("\u200B", TestName = "IsValid_WithZeroWidthOnlyTitle_ReturnsTrue(zero-width space U+200B)")]
    [TestCase("\uFEFF", TestName = "IsValid_WithZeroWidthOnlyTitle_ReturnsTrue(zero-width no-break space U+FEFF)")]
    public void IsValid_WithZeroWidthOnlyTitle_ReturnsTrue(string title)
    {
        Assert.That(PostValidator.IsValid(ValidPost(title: title)), Is.True);
    }

    [Description("EDG-05")]
    [TestCase(1, ValidTitle, ValidBody)]
    [TestCase(0, "  ", null)]
    public void IsValid_DoesNotChangeThePost(int userId, string? title, string? body)
    {
        var post = ValidPost(userId: userId, title: title, body: body, id: 7);

        PostValidator.IsValid(post);

        Assert.Multiple(() =>
        {
            Assert.That(post.UserId, Is.EqualTo(userId));
            Assert.That(post.Id, Is.EqualTo(7));
            Assert.That(post.Title, Is.EqualTo(title));
            Assert.That(post.Body, Is.EqualTo(body));
        });
    }

    #endregion

    #region Multiple invalid fields

    [Description("MUL-01")]
    [TestCase(0, "", ValidBody, TestName = "IsValid_WithSeveralInvalidFields_ReturnsFalse(UserId and Title)")]
    [TestCase(-1, ValidTitle, null, TestName = "IsValid_WithSeveralInvalidFields_ReturnsFalse(UserId and Body)")]
    [TestCase(1, "  ", "\t", TestName = "IsValid_WithSeveralInvalidFields_ReturnsFalse(Title and Body)")]
    [TestCase(0, null, "", TestName = "IsValid_WithSeveralInvalidFields_ReturnsFalse(all three)")]
    public void IsValid_WithSeveralInvalidFields_ReturnsFalse(int userId, string? title, string? body)
    {
        Assert.That(PostValidator.IsValid(ValidPost(userId: userId, title: title, body: body)), Is.False);
    }

    // Fixing fields one at a time shows each rule is enforced on its own.
    [Description("MUL-02")]
    [TestCase("UserId")]
    [TestCase("Title")]
    [TestCase("Body")]
    public void IsValid_FixingInvalidFieldsOneByOne_IsTrueOnlyAfterTheLastFix(string fixedLast)
    {
        var post = new Post { UserId = 0, Title = "", Body = "" };
        var fixes = new Dictionary<string, Action>
        {
            ["UserId"] = () => post.UserId = 1,
            ["Title"] = () => post.Title = ValidTitle,
            ["Body"] = () => post.Body = ValidBody,
        };

        foreach (var (field, fix) in fixes.Where(f => f.Key != fixedLast))
        {
            fix();
            Assert.That(PostValidator.IsValid(post), Is.False, $"{field} fixed, but {fixedLast} is still invalid");
        }

        fixes[fixedLast]();
        Assert.That(PostValidator.IsValid(post), Is.True);
    }

    #endregion

    private static Post ValidPost(int userId = 1, string? title = ValidTitle, string? body = ValidBody, int id = 1) =>
        new() { UserId = userId, Id = id, Title = title!, Body = body! };
}
