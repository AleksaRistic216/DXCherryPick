using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
            var detailsTasks = searchResult.Items.Select(async item =>
            {
                var repoFullName = ExtractRepoFromUrl(item.RepositoryUrl);
                var details = await GetPullRequestDetailsAsync(repoFullName, item.Number);

                return new PullRequest
                {
                    Number = item.Number,
                    Title = item.Title ?? string.Empty,
                    State = item.State ?? string.Empty,
                    Repository = repoFullName,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    Url = item.HtmlUrl ?? string.Empty,
                    BaseBranch = details?.Base?.Ref ?? string.Empty,
                    HeadBranch = details?.Head?.Ref ?? string.Empty,
                    IsDraft = item.Draft,
                    Additions = details?.Additions ?? 0,
                    Deletions = details?.Deletions ?? 0
                };
            });

            pullRequests.AddRange(await Task.WhenAll(detailsTasks));
        }

        return pullRequests;
    }

    public async Task<List<CommitInfo>> GetPullRequestCommitsAsync(string repoFullName, int prNumber)
    {
        var commits = new List<CommitInfo>();
        var response = await _httpClient.GetAsync($"repos/{repoFullName}/pulls/{prNumber}/commits?per_page=100");

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API error ({response.StatusCode}): {errorJson}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var commitItems = JsonSerializer.Deserialize<List<GitHubCommitItem>>(json, JsonOptions);

        if (commitItems != null)
        {
            foreach (var item in commitItems)
            {
                // Skip merge commits (commits with more than one parent)
                if (item.Parents != null && item.Parents.Count > 1)
                    continue;

                commits.Add(new CommitInfo
                {
                    Sha = item.Sha ?? string.Empty,
                    Message = item.Commit?.Message ?? string.Empty,
                    Author = item.Commit?.Author?.Name ?? item.Author?.Login ?? string.Empty,
                    Date = item.Commit?.Author?.Date ?? DateTime.MinValue
                });
            }
        }

        return commits;
    }

    public async Task<List<string>> GetVersionedBranchesAsync(string repoFullName, int limit = 3)
    {
        var versionedBranches = new List<string>();
        var versionPattern = new Regex(@"^\d{4}\.\d+$");

        // GitHub API paginates branches, we need to fetch enough to find versioned ones
        var page = 1;
        const int perPage = 100;

        while (versionedBranches.Count < limit && page <= 5) // Limit to 5 pages to avoid too many requests
        {
            var response = await _httpClient.GetAsync($"repos/{repoFullName}/branches?per_page={perPage}&page={page}");

            if (!response.IsSuccessStatusCode)
                break;

            var json = await response.Content.ReadAsStringAsync();
            var branches = JsonSerializer.Deserialize<List<GitHubBranch>>(json, JsonOptions);

            if (branches == null || branches.Count == 0)
                break;

            foreach (var branch in branches)
            {
                if (branch.Name != null && versionPattern.IsMatch(branch.Name))
                {
                    versionedBranches.Add(branch.Name);
                }
            }

            page++;
        }

        // Sort descending by version (e.g., 2026.1, 2025.2, 2025.1)
        return versionedBranches
            .OrderByDescending(b => b)
            .Take(limit)
            .ToList();
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

    private async Task<GitHubPullRequestDetails?> GetPullRequestDetailsAsync(string repoFullName, int prNumber)
    {
        var response = await _httpClient.GetAsync($"repos/{repoFullName}/pulls/{prNumber}");
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubPullRequestDetails>(json, JsonOptions);
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
        [JsonPropertyName("pull_request")]
        public GitHubPullRequestRef? PullRequest { get; set; }
    }

    private class GitHubPullRequestRef
    {
        public string? Url { get; set; }
    }

    private class GitHubPullRequestDetails
    {
        public GitHubBranchRef? Head { get; set; }
        public GitHubBranchRef? Base { get; set; }
        public int Additions { get; set; }
        public int Deletions { get; set; }
    }

    private class GitHubBranchRef
    {
        public string? Ref { get; set; }
    }

    private class GitHubCommitItem
    {
        public string? Sha { get; set; }
        public GitHubCommitDetails? Commit { get; set; }
        public GitHubUser? Author { get; set; }
        public List<GitHubCommitParent>? Parents { get; set; }
    }

    private class GitHubCommitParent
    {
        public string? Sha { get; set; }
    }

    private class GitHubCommitDetails
    {
        public string? Message { get; set; }
        public GitHubCommitAuthor? Author { get; set; }
    }

    private class GitHubCommitAuthor
    {
        public string? Name { get; set; }
        public DateTime Date { get; set; }
    }

    private class GitHubBranch
    {
        public string? Name { get; set; }
    }
}
