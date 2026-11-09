using TechnicalTest.Api.Models;

namespace TechnicalTest.Api.Clients;

public interface IPublicApiClient
{
    Task<List<Post>> GetPostsAsync();
    Task<Post?> GetPostAsync(int id);
    Task<Post> CreatePostAsync(Post newPost);
}
