using TechnicalTest.Api.Models;

namespace TechnicalTest.Api.Validators;

public static class PostValidator
{
    public static bool IsValid(Post? post)
    {
        if (post == null) return false;
        if (post.UserId <= 0) return false;
        if (string.IsNullOrWhiteSpace(post.Title)) return false;
        if (string.IsNullOrWhiteSpace(post.Body)) return false;
        return true;
    }
}
