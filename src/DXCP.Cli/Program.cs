using DXCP.Core;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    var parsed = ParseArgs(args);
    if (parsed == null)
    {
        PrintUsage();
        return 1;
    }

    var (prNumber, targetBranchesArg, options) = parsed.Value;

    // Resolve GitHub token
    var token = options.Token
        ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
        ?? CredentialManager.GetToken();

    if (string.IsNullOrWhiteSpace(token))
    {
        Console.Error.WriteLine("error: no GitHub token found.");
        Console.Error.WriteLine("  Set GITHUB_TOKEN env var, pass --token, or authenticate via the DXCherryPick UI first.");
        return 1;
    }

    // Detect repo from CWD git remote (or --repo override)
    string repo;
    if (!string.IsNullOrEmpty(options.Repo))
    {
        repo = options.Repo;
    }
    else
    {
        var cwd = Directory.GetCurrentDirectory();
        var gitService = new GitService(cwd);

        if (!await gitService.IsValidGitRepositoryAsync())
        {
            Console.Error.WriteLine("error: not inside a git repository. Run from within a worktree or pass --repo owner/repo.");
            return 1;
        }

        var remoteUrl = await gitService.GetRemoteUrlAsync();
        if (remoteUrl == null)
        {
            Console.Error.WriteLine("error: could not read origin remote URL. Pass --repo owner/repo.");
            return 1;
        }

        var parsed2 = GitService.ParseRepoFromRemoteUrl(remoteUrl);
        if (parsed2 == null)
        {
            Console.Error.WriteLine($"error: could not parse GitHub repo from remote URL: {remoteUrl}");
            Console.Error.WriteLine("  Pass --repo owner/repo explicitly.");
            return 1;
        }

        repo = parsed2;
    }

    // Determine worktree parent
    string worktreeParent;
    if (!string.IsNullOrEmpty(options.WorktreeParent))
    {
        worktreeParent = options.WorktreeParent;
    }
    else
    {
        var repoConfig = new RepositoryConfigManager();
        var configuredParent = repoConfig.GetWorktreeParentFolder(repo);
        if (!string.IsNullOrEmpty(configuredParent) && Directory.Exists(configuredParent))
        {
            worktreeParent = configuredParent;
        }
        else
        {
            // Default: parent of CWD (assumes CWD is one of the branch worktrees)
            worktreeParent = Path.GetDirectoryName(Directory.GetCurrentDirectory())
                ?? Directory.GetCurrentDirectory();
        }
    }

    using var github = new GitHubService(token);

    // Fetch PR
    Console.WriteLine($"PR #{prNumber} in {repo} — fetching details...");
    var pr = await github.GetPullRequestAsync(repo, prNumber);
    if (pr == null)
    {
        Console.Error.WriteLine($"error: PR #{prNumber} not found in {repo}.");
        return 1;
    }

    Console.WriteLine($"  {pr.Title}");
    Console.WriteLine($"  base: {pr.BaseBranch}  state: {pr.State}");

    // Fetch commits
    var commits = await github.GetPullRequestCommitsAsync(repo, prNumber);
    if (commits.Count == 0)
    {
        Console.Error.WriteLine("error: no commits found for this PR.");
        return 1;
    }
    Console.WriteLine($"  {commits.Count} commit(s):");
    foreach (var c in commits)
        Console.WriteLine($"    {c.ShortSha}  {FirstLine(c.Message)}");

    // Resolve target branches
    List<string> targetBranches;
    if (targetBranchesArg.Count > 0)
    {
        targetBranches = targetBranchesArg;
    }
    else
    {
        Console.WriteLine("  detecting versioned branches...");
        var versioned = await github.GetVersionedBranchesAsync(repo);
        targetBranches = versioned
            .Where(b => !b.Equals(pr.BaseBranch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetBranches.Count == 0)
        {
            Console.Error.WriteLine("error: no versioned target branches found (e.g. 2025.2, 2025.1).");
            Console.Error.WriteLine("  Specify branches explicitly: cp <pr> <branch1> <branch2>");
            return 1;
        }
    }

    Console.WriteLine($"  target branches: {string.Join(", ", targetBranches)}");
    Console.WriteLine();

    var results = new List<(string Branch, bool Success, string Info)>();

    foreach (var targetBranch in targetBranches)
    {
        var result = await ProcessBranchAsync(
            pr, commits, targetBranch, worktreeParent, github, options.ForcePush);
        results.Add((targetBranch, result.Success, result.Info));
        Console.WriteLine();
    }

    // Summary
    Console.WriteLine("─────────────────────────────");
    Console.WriteLine("Summary:");
    foreach (var (branch, success, info) in results)
    {
        var mark = success ? "OK" : "FAIL";
        Console.WriteLine($"  [{mark}] {branch}: {info}");
    }

    return results.All(r => r.Success) ? 0 : 1;
}

static async Task<(bool Success, string Info)> ProcessBranchAsync(
    PullRequest pr,
    List<CommitInfo> commits,
    string targetBranch,
    string worktreeParent,
    GitHubService github,
    bool forcePush)
{
    var prefix = $"[{targetBranch}]";
    var worktreePath = Path.Combine(worktreeParent, targetBranch);

    if (!Directory.Exists(worktreePath))
        return Fail($"worktree not found: {worktreePath}");

    var git = new GitService(worktreePath);

    if (!await git.IsValidGitRepositoryAsync())
        return Fail($"not a git repository: {worktreePath}");

    if (await git.HasUncommittedChangesAsync())
        return Fail($"worktree has uncommitted changes — clean up first or use the UI");

    var cherryPickBranch = $"cherry-pick/#{pr.Number}-to-{targetBranch}";

    Console.WriteLine($"{prefix} fetching...");
    var fetchResult = await git.FetchAsync();
    if (!fetchResult.Success)
        return Fail($"fetch failed: {fetchResult.Error.Trim()}");

    await git.FetchPrAsync(pr.Number);

    Console.WriteLine($"{prefix} checking out {targetBranch}...");
    await git.CheckoutAsync(targetBranch);
    await git.ResetHardToAsync($"origin/{targetBranch}");

    if (await git.BranchExistsAsync(cherryPickBranch))
    {
        Console.WriteLine($"{prefix} branch {cherryPickBranch} already exists — recreating...");
        await git.DeleteBranchAsync(cherryPickBranch, force: true);
    }

    var createResult = await git.CreateBranchAsync(cherryPickBranch, "HEAD");
    if (!createResult.Success)
        return Fail($"failed to create branch: {createResult.Error.Trim()}");

    foreach (var commit in commits)
    {
        Console.WriteLine($"{prefix} cherry-picking {commit.ShortSha} {FirstLine(commit.Message)}...");
        var cpResult = await git.CherryPickAsync(commit.Sha);
        if (!cpResult.Success)
        {
            await git.AbortCherryPickAsync();
            await git.CheckoutAsync(targetBranch);
            var reason = cpResult.HasConflict
                ? $"conflict on {commit.ShortSha} — resolve manually and re-run, or use the UI"
                : $"cherry-pick failed: {cpResult.Error.Trim()}";
            return Fail(reason);
        }
    }

    Console.WriteLine($"{prefix} pushing {cherryPickBranch}...");
    var pushResult = await git.PushAsync(cherryPickBranch, force: forcePush);
    if (!pushResult.Success)
    {
        await git.CheckoutAsync(targetBranch);
        return Fail($"push failed: {pushResult.Error.Trim()}");
    }

    Console.WriteLine($"{prefix} creating PR...");
    string prInfo;
    try
    {
        var prTitle = $"CP: {pr.Title}";
        var prBody = $"Cherry pick #{pr.Number} to {targetBranch}";
        var prUrl = await github.CreatePullRequestAsync(pr.Repository, prTitle, prBody, cherryPickBranch, targetBranch);
        prInfo = prUrl ?? "pushed (PR URL unavailable)";
        Console.WriteLine($"{prefix} PR created: {prInfo}");
    }
    catch (Exception ex)
    {
        prInfo = $"pushed, but PR creation failed: {ex.Message}";
        Console.WriteLine($"{prefix} warning: {prInfo}");
    }

    await git.CheckoutAsync(targetBranch);
    return (true, prInfo);

    static (bool Success, string Info) Fail(string msg)
    {
        Console.Error.WriteLine($"  error: {msg}");
        return (false, msg);
    }
}

static string FirstLine(string s)
{
    var nl = s.IndexOfAny(['\r', '\n']);
    return nl >= 0 ? s[..nl] : s;
}

static (int PrNumber, List<string> Branches, CliOptions Options)? ParseArgs(string[] args)
{
    if (args.Length == 0)
        return null;

    var options = new CliOptions();
    var positional = new List<string>();
    int i = 0;

    while (i < args.Length)
    {
        switch (args[i])
        {
            case "--repo" when i + 1 < args.Length:
                options.Repo = args[++i];
                break;
            case "--worktree-parent" when i + 1 < args.Length:
                options.WorktreeParent = args[++i];
                break;
            case "--token" when i + 1 < args.Length:
                options.Token = args[++i];
                break;
            case "--force-push":
                options.ForcePush = true;
                break;
            default:
                if (!args[i].StartsWith("--"))
                    positional.Add(args[i]);
                break;
        }
        i++;
    }

    if (positional.Count == 0 || !int.TryParse(positional[0], out var prNumber))
        return null;

    var branches = positional.Skip(1).ToList();
    return (prNumber, branches, options);
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: cp <pr-number> [<branch>...] [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  <pr-number>              GitHub PR number to cherry-pick");
    Console.Error.WriteLine("  [<branch>...]            Target branches (auto-detected if omitted)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  --repo <owner/repo>      Override repo (default: inferred from git remote)");
    Console.Error.WriteLine("  --worktree-parent <path> Parent folder containing branch worktrees");
    Console.Error.WriteLine("                           (default: saved config or parent of CWD)");
    Console.Error.WriteLine("  --token <pat>            GitHub PAT (overrides GITHUB_TOKEN / Credential Manager)");
    Console.Error.WriteLine("  --force-push             Force-push cherry-pick branches");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  cp 12345");
    Console.Error.WriteLine("  cp 12345 2025.2 2025.1");
    Console.Error.WriteLine("  cp 12345 --repo DevExpress/DevExtreme --worktree-parent C:\\repos\\dxe");
}

class CliOptions
{
    public string? Repo { get; set; }
    public string? WorktreeParent { get; set; }
    public string? Token { get; set; }
    public bool ForcePush { get; set; }
}
