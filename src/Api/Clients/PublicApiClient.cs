using System.Text;
using System.Text.Json;
using TechnicalTest.Api.Models;

namespace TechnicalTest.Api.Clients;

public class PublicApiClient : IPublicApiClient
{
    private readonly HttpClient _http;

    // The HttpClient's BaseAddress decides which environment is called.
    public PublicApiClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<List<Post>> GetPostsAsync()
    {
        var response = await _http.GetAsync("posts");
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Post>>(content)!;
    }

    public async Task<Post?> GetPostAsync(int id)
    {
        var response = await _http.GetAsync($"posts/{id}");
        if (!response.IsSuccessStatusCode) return null;

        string content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Post>(content);
    }

    public async Task<Post> CreatePostAsync(Post newPost)
    {
        var body = new StringContent(JsonSerializer.Serialize(newPost), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("posts", body);
        response.EnsureSuccessStatusCode();

        string resp = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Post>(resp)!;
    }
}
