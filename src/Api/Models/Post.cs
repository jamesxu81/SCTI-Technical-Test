namespace TechnicalTest.Api.Models;

public class Post
{
    public int UserId { get; set; }
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";

    // Shown in assertion failures, e.g. when Has.All.Matches<Post> lists the posts that failed.
    public override string ToString() =>
        $"Post {{ Id = {Id}, UserId = {UserId}, Title = {Quote(Title)}, Body = {Quote(Body)} }}";

    private static string Quote(string? text) =>
        text is null ? "null" : $"\"{(text.Length > 40 ? text[..40] + "..." : text)}\"";
}
