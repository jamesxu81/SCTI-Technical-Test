using TechnicalTest.Api.Models;
using TechnicalTest.Api.Validators;
using NUnit.Framework;

namespace TechnicalTest.Tests.UnitTests;

[TestFixture]
[Category("Unit")]
public class PostValidatorTests
{
    [Test]
    public void IsValid_WithValidPost_ReturnsTrue()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "Valid Title",
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsValid_WithNullPost_ReturnsFalse()
    {
        var result = PostValidator.IsValid(null);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithZeroUserId_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 0,
            Id = 1,
            Title = "Valid Title",
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithNegativeUserId_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = -1,
            Id = 1,
            Title = "Valid Title",
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithNullTitle_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = null!,
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithEmptyTitle_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "",
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithWhitespaceTitle_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "   ",
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithNullBody_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "Valid Title",
            Body = null!
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithEmptyBody_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "Valid Title",
            Body = ""
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithWhitespaceBody_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "Valid Title",
            Body = "   "
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithMultipleInvalidFields_ReturnsFalse()
    {
        var post = new Post
        {
            UserId = 0,
            Id = 1,
            Title = "",
            Body = ""
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithBoundaryUserId_ReturnsTrue()
    {
        var post = new Post
        {
            UserId = 1,
            Id = 1,
            Title = "Valid Title",
            Body = "Valid Body"
        };

        var result = PostValidator.IsValid(post);

        Assert.That(result, Is.True);
    }
}
