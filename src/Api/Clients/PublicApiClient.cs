using System.Text;
using System.Text.Json;
using TechnicalTest.Api.Models;

namespace TechnicalTest.Api.Clients;

public class PublicApiClient : IPublicApiClient
{
    // The API uses camelCase names; Web defaults match them case-insensitively.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    // The HttpClient's BaseAddress decides which environment is called.
    public PublicApiClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<List<Post>> GetPostsAsync()
    {
        using var response = await _http.GetAsync("posts");
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Post>>(content, JsonOptions)!;
    }

    public async Task<Post?> GetPostAsync(int id)
    {
        using var response = await _http.GetAsync($"posts/{id}");
        if (!response.IsSuccessStatusCode) return null;

        string content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Post>(content, JsonOptions);
    }

    public async Task<Post> CreatePostAsync(Post newPost)
    {
        using var body = new StringContent(JsonSerializer.Serialize(newPost, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("posts", body);
        response.EnsureSuccessStatusCode();

        string resp = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Post>(resp, JsonOptions)!;
    }
}
