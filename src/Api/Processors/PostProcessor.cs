using TechnicalTest.Api.Clients;
using TechnicalTest.Api.Models;
using TechnicalTest.Api.Validators;

namespace TechnicalTest.Api.Processors;

public class PostProcessor
{
    private readonly IPublicApiClient _client;

    public PostProcessor(IPublicApiClient client)
    {
        _client = client;
    }

    public async Task<List<Post>> GetValidPostsAsync()
    {
        var posts = await _client.GetPostsAsync();
        return posts.Where(PostValidator.IsValid).ToList();
    }
}
