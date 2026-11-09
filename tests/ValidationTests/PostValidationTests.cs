using TechnicalTest.Api.Models;
using TechnicalTest.Api.Validators;
using NUnit.Framework;

namespace TechnicalTest.Tests.ValidationTests;

[TestFixture]
[Category("Validation")]
public class PostValidationTests
{
    [Test]
    [TestCase(1, "Title", "Body", ExpectedResult = true)]
    [TestCase(100, "Long Title", "Long Body", ExpectedResult = true)]
    [TestCase(int.MaxValue, "T", "B", ExpectedResult = true)]
    public bool IsValid_WithVariousValidInputs_ReturnsExpectedResult(int userId, string title, string body)
    {
        var post = new Post { UserId = userId, Title = title, Body = body };
        return PostValidator.IsValid(post);
    }

    [Test]
    [TestCase(0, "Title", "Body")]
    [TestCase(-1, "Title", "Body")]
    [TestCase(-100, "Title", "Body")]
    [TestCase(int.MinValue, "Title", "Body")]
    public void IsValid_WithInvalidUserId_ReturnsFalse(int userId, string title, string body)
    {
        var post = new Post { UserId = userId, Title = title, Body = body };
        Assert.That(PostValidator.IsValid(post), Is.False);
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    [TestCase("\t")]
    [TestCase("\n")]
    public void IsValid_WithInvalidTitle_ReturnsFalse(string? title)
    {
        var post = new Post { UserId = 1, Title = title!, Body = "Valid Body" };
        Assert.That(PostValidator.IsValid(post), Is.False);
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    [TestCase("\t")]
    [TestCase("\n")]
    public void IsValid_WithInvalidBody_ReturnsFalse(string? body)
    {
        var post = new Post { UserId = 1, Title = "Valid Title", Body = body! };
        Assert.That(PostValidator.IsValid(post), Is.False);
    }

    [Test]
    public void IsValid_WithMultipleInvalidFields_ReturnsFalse()
    {
        var testCases = new[]
        {
            new Post { UserId = 0, Title = "", Body = "" },
            new Post { UserId = -1, Title = null!, Body = null! },
            new Post { UserId = 0, Title = "  ", Body = "  " },
            new Post { UserId = -1, Title = "", Body = "Valid" }
        };

        foreach (var post in testCases)
        {
            Assert.That(PostValidator.IsValid(post), Is.False, 
                $"Post with UserId={post.UserId}, Title='{post.Title}', Body='{post.Body}' should be invalid");
        }
    }

    [Test]
    public void IsValid_BoundaryValueTesting_UserIdOne_ReturnsTrue()
    {
        var post = new Post { UserId = 1, Title = "Title", Body = "Body" };
        Assert.That(PostValidator.IsValid(post), Is.True);
    }

    [Test]
    public void IsValid_BoundaryValueTesting_SingleCharacterStrings_ReturnsTrue()
    {
        var post = new Post { UserId = 1, Title = "T", Body = "B" };
        Assert.That(PostValidator.IsValid(post), Is.True);
    }

    [Test]
    public void IsValid_WithVeryLongStrings_ReturnsTrue()
    {
        var longString = new string('A', 10000);
        var post = new Post { UserId = 1, Title = longString, Body = longString };
        Assert.That(PostValidator.IsValid(post), Is.True);
    }

    [Test]
    public void IsValid_WithSpecialCharacters_ReturnsTrue()
    {
        var post = new Post 
        { 
            UserId = 1, 
            Title = "!@#$%^&*()_+-={}[]|:;<>?,./", 
            Body = "Special chars: 你好 🎉 αβγ" 
        };
        Assert.That(PostValidator.IsValid(post), Is.True);
    }

    [Test]
    public void IsValid_RequiredFieldsValidation()
    {
        var validPost = new Post { UserId = 1, Title = "T", Body = "B" };
        Assert.That(PostValidator.IsValid(validPost), Is.True);

        validPost.UserId = 0;
        Assert.That(PostValidator.IsValid(validPost), Is.False, "UserId is required");

        validPost.UserId = 1;
        validPost.Title = "";
        Assert.That(PostValidator.IsValid(validPost), Is.False, "Title is required");

        validPost.Title = "T";
        validPost.Body = "";
        Assert.That(PostValidator.IsValid(validPost), Is.False, "Body is required");
    }
}
