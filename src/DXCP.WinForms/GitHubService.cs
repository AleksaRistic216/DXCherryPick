using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DXCP.WinForms;

public class GitHubService : IDisposable
{
    private readonly HttpClient _httpClient;
    private string? _currentUsername;

    public GitHubService(string token)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DXCP-WinForms", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<string> GetCurrentUsernameAsync()
    {
        if (_currentUsername != null)
            return _currentUsername;

        var response = await _httpClient.GetAsync("user");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<GitHubUser>(json, JsonOptions);

        _currentUsername = user?.Login ?? throw new Exception("Could not determine current user");
        return _currentUsername;
    }

    public async Task<List<PullRequest>> GetMyPullRequestsAsync(int limit = 30)
    {
        var username = await GetCurrentUsernameAsync();

        var query = Uri.EscapeDataString($"type:pr author:{username} org:DevExpress sort:created-desc");
        var requestUrl = $"search/issues?q={query}&per_page={limit}";

        var response = await _httpClient.GetAsync(requestUrl);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"GitHub API error ({response.StatusCode}): {json}");
        }

        var searchResult = JsonSerializer.Deserialize<GitHubSearchResult>(json, JsonOptions);
        var pullRequests = new List<PullRequest>();

        if (searchResult?.Items != null)
        {
            foreach (var item in searchResult.Items)
            {
                var repoFullName = ExtractRepoFromUrl(item.RepositoryUrl);

                pullRequests.Add(new PullRequest
                {
                    Number = item.Number,
                    Title = item.Title ?? string.Empty,
                    State = item.State ?? string.Empty,
                    Repository = repoFullName,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    Url = item.HtmlUrl ?? string.Empty,
                    BaseBranch = string.Empty,
                    HeadBranch = string.Empty,
                    IsDraft = item.Draft,
                    Additions = 0,
                    Deletions = 0
                });
            }
        }

        return pullRequests;
    }

    private static string ExtractRepoFromUrl(string? repositoryUrl)
    {
        if (string.IsNullOrEmpty(repositoryUrl))
            return string.Empty;

        var parts = repositoryUrl.Split('/');
        if (parts.Length >= 2)
            return $"{parts[^2]}/{parts[^1]}";

        return repositoryUrl;
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private class GitHubUser
    {
        public string? Login { get; set; }
    }

    private class GitHubSearchResult
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
        public List<GitHubIssueItem>? Items { get; set; }
    }

    private class GitHubIssueItem
    {
        public int Number { get; set; }
        public string? Title { get; set; }
        public string? State { get; set; }
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        [JsonPropertyName("repository_url")]
        public string? RepositoryUrl { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        public bool Draft { get; set; }
    }
}
