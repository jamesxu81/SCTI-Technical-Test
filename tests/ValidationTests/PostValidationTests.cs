using TechnicalTest.Api.Models;
using TechnicalTest.Api.Validators;
using NUnit.Framework;

namespace TechnicalTest.Tests.ValidationTests;

// [Description] values are the test case IDs in specs/7-validation-tests.plan.md.
[TestFixture]
[Category("Validation")]
public class PostValidationTests
{
    public enum TextField { Title, Body }

    private const string ValidTitle = "Valid Title";
    private const string ValidBody = "Valid Body";

    // Exactly the characters char.IsWhiteSpace accepts (checked by WS-01).
    private static readonly (string Name, int CodePoint)[] WhitespaceCharacters =
    {
        ("tab U+0009", 0x0009), ("line feed U+000A", 0x000A), ("vertical tab U+000B", 0x000B),
        ("form feed U+000C", 0x000C), ("carriage return U+000D", 0x000D), ("space U+0020", 0x0020),
        ("next line U+0085", 0x0085), ("no-break space U+00A0", 0x00A0), ("Ogham space U+1680", 0x1680),
        ("en quad U+2000", 0x2000), ("em quad U+2001", 0x2001), ("en space U+2002", 0x2002),
        ("em space U+2003", 0x2003), ("three-per-em space U+2004", 0x2004), ("four-per-em space U+2005", 0x2005),
        ("six-per-em space U+2006", 0x2006), ("figure space U+2007", 0x2007), ("punctuation space U+2008", 0x2008),
        ("thin space U+2009", 0x2009), ("hair space U+200A", 0x200A), ("line separator U+2028", 0x2028),
        ("paragraph separator U+2029", 0x2029), ("narrow no-break space U+202F", 0x202F),
        ("medium mathematical space U+205F", 0x205F), ("ideographic space U+3000", 0x3000),
    };

    // Look empty but aren't whitespace, so the validator accepts them (plan V1).
    private static readonly (string Name, int CodePoint)[] InvisibleCharacters =
    {
        ("NUL U+0000", 0x0000), ("bell U+0007", 0x0007), ("unit separator U+001F", 0x001F),
        ("zero-width space U+200B", 0x200B), ("zero-width non-joiner U+200C", 0x200C),
        ("zero-width joiner U+200D", 0x200D), ("word joiner U+2060", 0x2060),
        ("zero-width no-break space U+FEFF", 0xFEFF), ("right-to-left mark U+200F", 0x200F),
        ("combining acute accent U+0301", 0x0301), ("Hangul filler U+3164", 0x3164),
        ("braille blank U+2800", 0x2800), ("unpaired surrogate U+D83D", 0xD83D),
    };

    private static readonly (string Name, string Value)[] ContentClasses =
    {
        ("letters", "Hello"), ("digits only", "123"), ("punctuation only", "..."), ("single emoji", "🎉"),
        ("mixed", "Post #1: ready? Yes!"),
    };

    private static readonly (string Name, string Value)[] SpecialCharacters =
    {
        ("ASCII punctuation", "!@#$%^&*()_+-={}[]|:;<>?,./"),
        ("quotes", "'single' \"double\" `back`"),
        ("backslash path", @"C:\temp\x"),
        ("HTML", "<script>alert(1)</script>"),
        ("SQL", "'; DROP TABLE posts; --"),
        ("JSON", "{\"a\":1}"),
        ("URL with query string", "https://example.com/search?q=a&b=c#top"),
        ("email address", "test.adult@example.com"),
    };

    private static readonly (string Name, string Value)[] UnicodeText =
    {
        ("Chinese", "你好"), ("Greek", "αβγ"), ("Māori macrons", "ā ē ī ō ū"), ("Arabic (right-to-left)", "مرحبا"),
        ("Hindi", "नमस्ते"), ("emoji", "🎉"), ("emoji with skin tone", "👍🏽"), ("family emoji with joiners", "👨‍👩‍👧"),
        ("letter plus combining mark", "e\u0301"),
    };

    #region Required fields validation

    [Test, Description("REQ-01")]
    public void IsValid_WithAllRequiredFields_ReturnsTrue()
    {
        Assert.That(PostValidator.IsValid(ValidPost(userId: 1, title: "T", body: "B")), Is.True);
    }

    [Description("REQ-02")]
    [TestCase(0, "T", "B", TestName = "IsValid_WithOneRequiredFieldMissing_ReturnsFalse(UserId 0)")]
    [TestCase(1, null, "B", TestName = "IsValid_WithOneRequiredFieldMissing_ReturnsFalse(Title null)")]
    [TestCase(1, "", "B", TestName = "IsValid_WithOneRequiredFieldMissing_ReturnsFalse(Title empty)")]
    [TestCase(1, "T", null, TestName = "IsValid_WithOneRequiredFieldMissing_ReturnsFalse(Body null)")]
    [TestCase(1, "T", "", TestName = "IsValid_WithOneRequiredFieldMissing_ReturnsFalse(Body empty)")]
    public void IsValid_WithOneRequiredFieldMissing_ReturnsFalse(int userId, string? title, string? body)
    {
        Assert.That(PostValidator.IsValid(ValidPost(userId, title, body)), Is.False);
    }

    [Test, Description("REQ-03")]
    public void IsValid_WithEveryRequiredFieldMissing_ReturnsFalse()
    {
        Assert.That(PostValidator.IsValid(new Post()), Is.False);
    }

    #endregion

    #region Title/Body constraints

    [Test, Description("TB-01")]
    public void TitleAndBody_GiveTheSameVerdictForEveryInput()
    {
        var samples = ContentClasses.Concat(SpecialCharacters).Concat(UnicodeText)
            .Select(row => (row.Name, Text: (string?)row.Value))
            .Concat(WhitespaceCharacters.Concat(InvisibleCharacters).Select(row => (row.Name, Text: (string?)Char(row.CodePoint))))
            .Append(("null", null))
            .Append(("empty", ""))
            .Append(("10,000 characters", new string('a', 10_000)));

        Assert.Multiple(() =>
        {
            foreach (var (name, text) in samples)
            {
                Assert.That(IsValidWith(TextField.Body, text), Is.EqualTo(IsValidWith(TextField.Title, text)), name);
            }
        });
    }

    [Description("TB-02")]
    [TestCaseSource(nameof(ContentClassRows))]
    public void IsValid_WithTextContentClasses_ReturnsTrue(TextField field, string text)
    {
        Assert.That(IsValidWith(field, text), Is.True);
    }

    #endregion

    #region UserId constraints

    [Description("UID-01")]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(10)]
    [TestCase(100)]
    [TestCase(1_000_000)]
    [TestCase(int.MaxValue)]
    public void IsValid_WithUserIdInValidRange_ReturnsTrue(int userId)
    {
        Assert.That(PostValidator.IsValid(ValidPost(userId: userId)), Is.True);
    }

    [Description("UID-02")]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    [TestCase(int.MinValue)]
    public void IsValid_WithUserIdInInvalidRange_ReturnsFalse(int userId)
    {
        Assert.That(PostValidator.IsValid(ValidPost(userId: userId)), Is.False);
    }

    #endregion

    #region Boundary values

    [Description("BND-01")]
    [TestCase(0, ExpectedResult = false)]
    [TestCase(1, ExpectedResult = true)]
    public bool IsValid_UserIdEitherSideOfTheLimit(int userId) => PostValidator.IsValid(ValidPost(userId: userId));

    private static IEnumerable<TestCaseData> LengthBoundaryRows() =>
        from field in BothFields
        from row in new[] { (Name: "0 characters", Text: "", Valid: false), (Name: "1 character", Text: "x", Valid: true) }
        select new TestCaseData(field, row.Text).Returns(row.Valid).SetArgDisplayNames(field.ToString(), row.Name);

    [Description("BND-02")]
    [TestCaseSource(nameof(LengthBoundaryRows))]
    public bool IsValid_TextLengthEitherSideOfTheLimit(TextField field, string text) => IsValidWith(field, text);

    private static IEnumerable<TestCaseData> OneRealCharacterRows() =>
        from field in BothFields
        from row in new[] { (Before: 1, After: 0), (Before: 1_000, After: 0), (Before: 0, After: 1_000) }
        select new TestCaseData(field, row.Before, row.After)
            .SetArgDisplayNames(field.ToString(), $"{row.Before} spaces + x + {row.After} spaces");

    [Description("BND-03")]
    [TestCaseSource(nameof(OneRealCharacterRows))]
    public void IsValid_WithOneRealCharacterAmongSpaces_ReturnsTrue(TextField field, int spacesBefore, int spacesAfter)
    {
        var text = new string(' ', spacesBefore) + "x" + new string(' ', spacesAfter);

        Assert.That(IsValidWith(field, text), Is.True);
    }

    [Description("BND-04")]
    [TestCaseSource(nameof(SpaceCountRows))]
    public void IsValid_WithOnlySpacesOfAnyLength_ReturnsFalse(TextField field, int spaces)
    {
        Assert.That(IsValidWith(field, new string(' ', spaces)), Is.False);
    }

    #endregion

    #region Special characters handling

    // The validator checks presence, not safety: markup and SQL-like text are valid (plan V3).
    [Description("SPC-01")]
    [TestCaseSource(nameof(SpecialCharacterRows))]
    public void IsValid_WithSpecialCharacters_ReturnsTrue(TextField field, string text)
    {
        Assert.That(IsValidWith(field, text), Is.True);
    }

    [Description("SPC-02")]
    [TestCaseSource(nameof(UnicodeTextRows))]
    public void IsValid_WithUnicodeText_ReturnsTrue(TextField field, string text)
    {
        Assert.That(IsValidWith(field, text), Is.True);
    }

    // Known gap (plan V1): text made only of invisible or control characters passes.
    [Description("SPC-03")]
    [TestCaseSource(nameof(InvisibleCharacterRows))]
    public void IsValid_WithOnlyAnInvisibleCharacter_ReturnsTrue(TextField field, int codePoint)
    {
        Assert.That(IsValidWith(field, Char(codePoint)), Is.True);
    }

    #endregion

    #region Very long strings handling

    // There is no maximum length (plan V2).
    [Description("LEN-01")]
    [TestCaseSource(nameof(LongLengthRows))]
    public void IsValid_WithLongText_ReturnsTrue(TextField field, int length)
    {
        Assert.That(IsValidWith(field, new string('a', length)), Is.True);
    }

    [Description("LEN-02")]
    [TestCaseSource(nameof(BothFieldRows))]
    public void IsValid_WithThousandLinesOfText_ReturnsTrue(TextField field)
    {
        var text = string.Join('\n', Enumerable.Range(1, 1_000).Select(line => $"Line {line}"));

        Assert.That(IsValidWith(field, text), Is.True);
    }

    [Description("LEN-03")]
    [TestCaseSource(nameof(LongWhitespaceRows))]
    public void IsValid_WithLongWhitespaceOnlyText_ReturnsFalse(TextField field, string kind)
    {
        var text = kind == "1,000,000 spaces"
            ? new string(' ', 1_000_000)
            : string.Concat(Enumerable.Range(0, 10_000).Select(i => Char(WhitespaceCharacters[i % WhitespaceCharacters.Length].CodePoint)));

        Assert.That(IsValidWith(field, text), Is.False);
    }

    #endregion

    #region Whitespace validation

    [Test, Description("WS-01")]
    public void WhitespaceList_MatchesTheRuntime()
    {
        var runtimeWhitespace = Enumerable.Range(0, 0x10000).Where(codePoint => char.IsWhiteSpace((char)codePoint));

        Assert.That(runtimeWhitespace, Is.EqualTo(WhitespaceCharacters.Select(row => row.CodePoint)));
    }

    [Description("WS-01")]
    [TestCaseSource(nameof(WhitespaceCharacterRows))]
    public void IsValid_WithOnlyOneWhitespaceCharacter_ReturnsFalse(TextField field, int codePoint)
    {
        Assert.That(IsValidWith(field, Char(codePoint)), Is.False);
    }

    [Description("WS-02")]
    [TestCaseSource(nameof(BothFieldRows))]
    public void IsValid_WithMixedWhitespaceOnly_ReturnsFalse(TextField field)
    {
        Assert.That(IsValidWith(field, " \t\r\n\u00A0\u3000"), Is.False);
    }

    [Description("WS-03")]
    [TestCaseSource(nameof(TextWithWhitespaceRows))]
    public void IsValid_WithTextAroundOrBetweenWhitespace_ReturnsTrue(TextField field, string text)
    {
        Assert.That(IsValidWith(field, text), Is.True);
    }

    [Description("WS-04")]
    [TestCaseSource(nameof(RepeatedWhitespaceRows))]
    public void IsValid_WithRepeatedWhitespace_ReturnsFalse(TextField field, string text)
    {
        Assert.That(IsValidWith(field, text), Is.False);
    }

    #endregion

    #region Test data

    private static readonly TextField[] BothFields = { TextField.Title, TextField.Body };

    private static IEnumerable<TestCaseData> OnBothFields<T>(IEnumerable<(string Name, T Value)> rows) =>
        from field in BothFields
        from row in rows
        select new TestCaseData(field, row.Value).SetArgDisplayNames(field.ToString(), row.Name);

    private static IEnumerable<TestCaseData> BothFieldRows() =>
        BothFields.Select(field => new TestCaseData(field).SetArgDisplayNames(field.ToString()));

    private static IEnumerable<TestCaseData> ContentClassRows() => OnBothFields(ContentClasses);

    private static IEnumerable<TestCaseData> SpecialCharacterRows() => OnBothFields(SpecialCharacters);

    private static IEnumerable<TestCaseData> UnicodeTextRows() => OnBothFields(UnicodeText);

    // Code points rather than strings, so control characters never reach the test runner's output.
    private static IEnumerable<TestCaseData> WhitespaceCharacterRows() => OnBothFields(WhitespaceCharacters);

    private static IEnumerable<TestCaseData> InvisibleCharacterRows() => OnBothFields(InvisibleCharacters);

    private static IEnumerable<TestCaseData> SpaceCountRows() =>
        OnBothFields(new[] { 1, 2, 100, 10_000 }.Select(count => ($"{count:N0} spaces", count)));

    private static IEnumerable<TestCaseData> LongLengthRows() =>
        OnBothFields(new[] { 255, 1_000, 10_000, 100_000, 1_000_000 }.Select(length => ($"{length:N0} characters", length)));

    private static IEnumerable<TestCaseData> LongWhitespaceRows() =>
        OnBothFields(new[] { ("1,000,000 spaces", "1,000,000 spaces"), ("10,000 mixed whitespace", "10,000 mixed whitespace") });

    private static IEnumerable<TestCaseData> TextWithWhitespaceRows() => OnBothFields(new[]
    {
        ("surrounding spaces", "  Title  "), ("tab and line feed around", "\tTitle\n"),
        ("two spaces between words", "Two  words"), ("no-break spaces around", "\u00A0Title\u00A0"),
    });

    private static IEnumerable<TestCaseData> RepeatedWhitespaceRows() => OnBothFields(new[]
    {
        ("3 spaces", "   "), ("3 line feeds", "\n\n\n"), ("2 tabs", "\t\t"),
    });

    #endregion

    private static Post ValidPost(int userId = 1, string? title = ValidTitle, string? body = ValidBody) =>
        new() { UserId = userId, Id = 1, Title = title!, Body = body! };

    private static bool IsValidWith(TextField field, string? text) =>
        PostValidator.IsValid(field == TextField.Title ? ValidPost(title: text) : ValidPost(body: text));

    private static string Char(int codePoint) => ((char)codePoint).ToString();
}
