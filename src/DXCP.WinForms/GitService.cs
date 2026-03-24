using System.Diagnostics;
using System.Text;

namespace DXCP.WinForms;

public class GitService
{
    private readonly string _workingDirectory;

    public string WorkingDirectory => _workingDirectory;

    public GitService(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public async Task<GitResult> FetchAsync(string remote = "origin")
    {
        return await RunGitAsync($"fetch {remote}");
    }

    public async Task<GitResult> CheckoutAsync(string branch)
    {
        return await RunGitAsync($"checkout {branch}");
    }

    public async Task<GitResult> CreateBranchAsync(string branchName, string startPoint)
    {
        return await RunGitAsync($"checkout -b {branchName} {startPoint}");
    }

    public async Task<CherryPickResult> CherryPickAsync(string commitSha)
    {
        var result = await RunGitAsync($"cherry-pick {commitSha}");

        if (result.Success)
        {
            return new CherryPickResult { Success = true };
        }

        // Check if it's a conflict
        if (result.Output.Contains("CONFLICT") || result.Error.Contains("CONFLICT") ||
            result.Output.Contains("could not apply") || result.Error.Contains("could not apply"))
        {
            return new CherryPickResult { Success = false, HasConflict = true, Error = result.Error };
        }

        return new CherryPickResult { Success = false, HasConflict = false, Error = result.Error };
    }

    public async Task<GitResult> PushAsync(string branchName, bool force = false, string remote = "origin")
    {
        var forceFlag = force ? " --force" : "";
        return await RunGitAsync($"push -u{forceFlag} {remote} {branchName}");
    }

    public async Task<GitResult> AbortCherryPickAsync()
    {
        return await RunGitAsync("cherry-pick --abort");
    }

    public async Task<GitResult> ResetHardAsync()
    {
        return await RunGitAsync("reset --hard HEAD");
    }

    public async Task<GitResult> ResetHardToAsync(string target)
    {
        return await RunGitAsync($"reset --hard {target}");
    }

    public async Task<List<string>> GetConflictedFilesAsync()
    {
        var result = await RunGitAsync("diff --name-only --diff-filter=U");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            return new List<string>();

        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToList();
    }

    public async Task<GitResult> StageFileAsync(string filePath)
    {
        return await RunGitAsync($"add \"{filePath}\"");
    }

    public async Task<GitResult> ContinueCherryPickAsync()
    {
        return await RunGitAsync("cherry-pick --continue --no-edit");
    }

    public async Task<bool> HasUncommittedChangesAsync()
    {
        var result = await RunGitAsync("status --porcelain");
        return result.Success && !string.IsNullOrWhiteSpace(result.Output);
    }

    public async Task<bool> BranchExistsAsync(string branchName)
    {
        var result = await RunGitAsync($"rev-parse --verify {branchName}");
        return result.Success;
    }

    public async Task<GitResult> DeleteBranchAsync(string branchName, bool force = false)
    {
        var forceFlag = force ? "-D" : "-d";
        return await RunGitAsync($"branch {forceFlag} {branchName}");
    }

    public async Task<string?> GetCurrentBranchAsync()
    {
        var result = await RunGitAsync("rev-parse --abbrev-ref HEAD");
        return result.Success ? result.Output.Trim() : null;
    }

    public async Task<bool> IsValidGitRepositoryAsync()
    {
        var result = await RunGitAsync("rev-parse --is-inside-work-tree");
        return result.Success && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> IsGitInstalledAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<GitResult> RunGitAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new GitResult
                {
                    Success = false,
                    Error = "Failed to start git process"
                };
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            return new GitResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new GitResult
            {
                Success = false,
                Error = $"Failed to execute git command: {ex.Message}"
            };
        }
    }
}

public class GitResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
}

public class CherryPickResult
{
    public bool Success { get; set; }
    public bool HasConflict { get; set; }
    public string Error { get; set; } = string.Empty;
}
