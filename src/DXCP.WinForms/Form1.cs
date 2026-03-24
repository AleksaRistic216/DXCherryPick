using System.Diagnostics;
using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using DevExpress.XtraSplashScreen;

namespace DXCP.WinForms;

public partial class Form1 : Form {
    private GitHubService? _gitHubService;
    private readonly RepositoryConfigManager _repoConfigManager = new();
    private IOverlaySplashScreenHandle? _overlayHandle;
    private readonly OverlayTextPainter _overlayPainter = new();

    public Form1() {
        InitializeComponent();
        SetupContextMenu();
    }

    private void SetupContextMenu() {
        gridView.MouseUp += GridView_MouseUp;
    }

    private void GridView_MouseUp(object? sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Right)
            return;

        var hitInfo = gridView.CalcHitInfo(e.Location);
        if (!hitInfo.InRow)
            return;

        gridView.FocusedRowHandle = hitInfo.RowHandle;
        popupMenuGrid.ShowPopup(gridControl.PointToScreen(e.Location));
    }

    private async void barButtonCherryPick_ItemClick(object sender, ItemClickEventArgs e) {
        var pr = gridView.GetFocusedRow() as PullRequest;
        if (pr == null)
            return;

        await PerformCherryPickAsync(pr);
    }

    private async void Form1_Load(object sender, EventArgs e) {
        if(await EnsureAuthenticatedAsync()) {
            await LoadPullRequestsAsync();
        }
    }

    private async void btnRefresh_Click(object sender, EventArgs e) {
        if(await EnsureAuthenticatedAsync()) {
            await LoadPullRequestsAsync();
        }
    }

    private async Task<bool> EnsureAuthenticatedAsync() {
        if(_gitHubService != null)
            return true;

        // Try to use stored token first
        var storedToken = CredentialManager.GetToken();
        if(!string.IsNullOrWhiteSpace(storedToken)) {
            if(await TryAuthenticateAsync(storedToken))
                return true;

            // Stored token is invalid, clear it
            CredentialManager.DeleteToken();
        }

        // Prompt user for token
        var token = DevExpress.XtraEditors.XtraInputBox.Show(
            "Enter your GitHub Personal Access Token.\n\n" +
            "For SSO-protected repos (like DevExpress), you must:\n" +
            "1. Create a PAT at: GitHub > Settings > Developer settings > Personal access tokens\n" +
            "2. Click 'Configure SSO' next to the token and authorize it for DevExpress\n\n" +
            "Your token will be stored securely in Windows Credential Manager.",
            "GitHub Authentication",
            string.Empty);

        if(string.IsNullOrWhiteSpace(token)) {
            return false;
        }

        if(await TryAuthenticateAsync(token)) {
            // Save valid token for future use
            CredentialManager.SaveToken(token);
            return true;
        }

        return false;
    }

    private async Task<bool> TryAuthenticateAsync(string token) {
        try {
            ShowOverlay(this, "Verifying GitHub credentials...");
            _gitHubService = new GitHubService(token);
            await _gitHubService.GetCurrentUsernameAsync();
            return true;
        }
        catch(Exception ex) {
            _gitHubService?.Dispose();
            _gitHubService = null;

            Debug.WriteLine("=== AUTHENTICATION ERROR ===");
            Debug.WriteLine($"Error: {ex.Message}");
            Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

            return false;
        }
        finally {
            CloseOverlay();
        }
    }

    private async Task LoadPullRequestsAsync() {
        if(_gitHubService == null)
            return;

        btnRefresh.Enabled = false;
        ShowOverlay(gridControl, "Fetching your pull requests from DevExpress...");

        try {
            var pullRequests = await _gitHubService.GetMyPullRequestsAsync();
            gridControl.DataSource = pullRequests;
            gridView.BestFitColumns();
            ApplyDefaultFilter();
        }
        catch(Exception ex) {
            Debug.WriteLine("=== LOAD PR ERROR ===");
            Debug.WriteLine($"Error: {ex.Message}");
            Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        finally {
            CloseOverlay();
            btnRefresh.Enabled = true;
        }
    }

    private void ApplyDefaultFilter() {
        var twoWeeksAgo = DateTime.Now.AddDays(-14).ToString("MM/dd/yyyy");
        gridView.ActiveFilterString = $"Contains([Repository], 'dxvcs') AND [CreatedAt] >= #{twoWeeksAgo}#";
    }

    private async Task PerformCherryPickAsync(PullRequest pr) {
        if (_gitHubService == null)
            return;

        try {
            // Check if Git is installed
            if (!await GitService.IsGitInstalledAsync()) {
                XtraMessageBox.Show(
                    "Git is not installed or not found in PATH.\n\nPlease install Git and ensure it's accessible from the command line.",
                    "Git Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Get or prompt for worktree parent folder
            var worktreeParent = await GetOrPromptForWorktreeParentAsync(pr.Repository);
            if (string.IsNullOrEmpty(worktreeParent))
                return;

            ShowOverlay(gridControl, "Loading commits and target branches...");

            // Get PR commits
            var commits = await _gitHubService.GetPullRequestCommitsAsync(pr.Repository, pr.Number);
            if (commits.Count == 0) {
                CloseOverlay();
                XtraMessageBox.Show(
                    "No commits found for this pull request.",
                    "No Commits",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Get versioned branches, excluding the PR's base branch
            var allVersionedBranches = await _gitHubService.GetVersionedBranchesAsync(pr.Repository);
            var targetBranches = allVersionedBranches
                .Where(b => !b.Equals(pr.BaseBranch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetBranches.Count == 0) {
                CloseOverlay();
                XtraMessageBox.Show(
                    "No target branches found for cherry-picking.\n\nThe repository needs versioned branches (e.g., 2025.2, 2025.1).",
                    "No Target Branches",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            CloseOverlay();

            // Show cherry-pick dialog
            using var dialog = new CherryPickDialog(pr, commits, targetBranches);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var selectedBranches = dialog.SelectedBranches;
            if (selectedBranches.Count == 0)
                return;

            // Perform cherry-pick for each selected branch using worktrees
            await ExecuteCherryPicksAsync(pr, commits, selectedBranches, worktreeParent, dialog.ForcePush);
        }
        catch (Exception ex) {
            CloseOverlay();
            Debug.WriteLine($"Cherry-pick error: {ex}");
            XtraMessageBox.Show(
                $"An error occurred during cherry-pick:\n\n{ex.Message}",
                "Cherry Pick Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task<string?> GetOrPromptForWorktreeParentAsync(string githubRepo) {
        var existingPath = _repoConfigManager.GetWorktreeParentFolder(githubRepo);
        if (!string.IsNullOrEmpty(existingPath) && Directory.Exists(existingPath)) {
            return existingPath;
        }

        using var folderDialog = new FolderBrowserDialog {
            Description = $"Select the parent folder containing worktrees for {githubRepo}\n(e.g., folder containing 2025.2, 2025.1 subfolders)",
            UseDescriptionForTitle = true
        };

        if (folderDialog.ShowDialog(this) != DialogResult.OK)
            return null;

        var selectedPath = folderDialog.SelectedPath;
        _repoConfigManager.SetWorktreeParentFolder(githubRepo, selectedPath);
        return selectedPath;
    }

    private async Task ExecuteCherryPicksAsync(PullRequest pr, List<CommitInfo> commits, List<string> targetBranches, string worktreeParent, bool forcePush) {
        var results = new List<(string Branch, bool Success, string? Error)>();

        foreach (var targetBranch in targetBranches) {
            ShowOverlay(gridControl, $"Applying commits to {targetBranch}...");

            // Get worktree path for this branch
            var worktreePath = Path.Combine(worktreeParent, targetBranch);

            if (!Directory.Exists(worktreePath)) {
                results.Add((targetBranch, false, $"Worktree folder not found: {worktreePath}"));
                continue;
            }

            var gitService = new GitService(worktreePath);

            // Validate it's a git repository
            if (!await gitService.IsValidGitRepositoryAsync()) {
                results.Add((targetBranch, false, $"Not a valid Git repository: {worktreePath}"));
                continue;
            }

            // Check for uncommitted changes in this worktree
            if (await gitService.HasUncommittedChangesAsync()) {
                CloseOverlay();
                var choice = XtraMessageBox.Show(
                    $"Worktree '{targetBranch}' has uncommitted changes.\n\nDiscard them and continue?",
                    "Uncommitted Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (choice == DialogResult.Yes) {
                    await gitService.ResetHardAsync();
                    ShowOverlay(gridControl, $"Applying commits to {targetBranch}...");
                }
                else {
                    results.Add((targetBranch, false, "Skipped — uncommitted changes in worktree."));
                    continue;
                }
            }

            var cherryPickBranch = $"cherry-pick/#{pr.Number}-to-{targetBranch}";

            try {
                // Fetch latest
                var fetchResult = await gitService.FetchAsync();
                if (!fetchResult.Success) {
                    results.Add((targetBranch, false, $"Failed to fetch: {fetchResult.Error}"));
                    continue;
                }

                // Reset to latest remote state
                await gitService.CheckoutAsync(targetBranch);
                await gitService.ResetHardToAsync($"origin/{targetBranch}");

                // Check if branch already exists
                if (await gitService.BranchExistsAsync(cherryPickBranch)) {
                    var overwrite = XtraMessageBox.Show(
                        $"Branch '{cherryPickBranch}' already exists.\n\nDo you want to delete it and create a new one?",
                        "Branch Exists",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (overwrite == DialogResult.Yes) {
                        await gitService.DeleteBranchAsync(cherryPickBranch, force: true);
                    }
                    else {
                        results.Add((targetBranch, false, "Branch already exists - skipped"));
                        continue;
                    }
                }

                // Create new branch from current HEAD (worktree is already on target branch)
                var createResult = await gitService.CreateBranchAsync(cherryPickBranch, "HEAD");
                if (!createResult.Success) {
                    results.Add((targetBranch, false, $"Failed to create branch: {createResult.Error}"));
                    continue;
                }

                // Cherry-pick all commits
                var allCommitsSucceeded = true;
                foreach (var commit in commits) {
                    var cpResult = await gitService.CherryPickAsync(commit.Sha);
                    if (!cpResult.Success) {
                        if (cpResult.HasConflict) {
                            CloseOverlay();
                            var conflictedFiles = await gitService.GetConflictedFilesAsync();
                            if (conflictedFiles.Count > 0) {
                                using var conflictDialog = new ConflictResolutionDialog(gitService, conflictedFiles, commit, targetBranch);
                                conflictDialog.ShowDialog(this);
                                if (conflictDialog.Resolved) {
                                    ShowOverlay(gridControl, $"Applying commits to {targetBranch}...");
                                    continue; // conflict resolved, move to next commit
                                }
                            }
                            else {
                                await gitService.AbortCherryPickAsync();
                            }
                            results.Add((targetBranch, false, $"Conflict on commit {commit.ShortSha}. Aborted by user."));
                        }
                        else {
                            results.Add((targetBranch, false, $"Cherry-pick failed: {cpResult.Error}"));
                        }
                        allCommitsSucceeded = false;
                        break;
                    }
                }

                if (!allCommitsSucceeded) {
                    // Switch back to target branch on failure
                    await gitService.CheckoutAsync(targetBranch);
                    continue;
                }

                // Push to origin
                var pushResult = await gitService.PushAsync(cherryPickBranch, force: forcePush);
                if (!pushResult.Success) {
                    results.Add((targetBranch, false, $"Failed to push: {pushResult.Error}"));
                    // Switch back to target branch
                    await gitService.CheckoutAsync(targetBranch);
                    continue;
                }

                // Create pull request
                ShowOverlay(gridControl, $"Creating PR for {targetBranch}...");
                try {
                    var prTitle = $"CP: {pr.Title}";
                    var prBody = $"Cherry pick #{pr.Number} to {targetBranch}";
                    var prUrl = await _gitHubService!.CreatePullRequestAsync(
                        pr.Repository, prTitle, prBody, cherryPickBranch, targetBranch);
                    results.Add((targetBranch, true, prUrl));
                }
                catch (Exception prEx) {
                    results.Add((targetBranch, true, $"Pushed but PR creation failed: {prEx.Message}"));
                }

                // Switch back to target branch after successful push
                await gitService.CheckoutAsync(targetBranch);
            }
            catch (Exception ex) {
                results.Add((targetBranch, false, ex.Message));
                // Try to switch back to target branch
                try { await gitService.CheckoutAsync(targetBranch); } catch { }
            }
        }

        CloseOverlay();

        // Show results
        ShowCherryPickResults(pr, results);
    }

    private void ShowCherryPickResults(PullRequest pr, List<(string Branch, bool Success, string? Error)> results) {
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        var message = $"Cherry-pick completed.\n\nSuccessful: {successCount}\nFailed: {failureCount}";

        if (failureCount > 0) {
            message += "\n\nFailures:";
            foreach (var (branch, success, error) in results.Where(r => !r.Success)) {
                message += $"\n• {branch}: {error}";
            }
        }

        if (successCount > 0) {
            message += "\n\nSuccessful:";
            foreach (var (branch, success, info) in results.Where(r => r.Success)) {
                message += $"\n• {branch}: {info}";
            }
        }

        var icon = failureCount == 0 ? MessageBoxIcon.Information :
                   successCount == 0 ? MessageBoxIcon.Error :
                   MessageBoxIcon.Warning;

        XtraMessageBox.Show(message, "Cherry Pick Results", MessageBoxButtons.OK, icon);
    }

    private void ShowOverlay(Control owner, string message) {
        CloseOverlay();
        _overlayPainter.Text = message;
        _overlayHandle = SplashScreenManager.ShowOverlayForm(owner, customPainter: _overlayPainter);
    }

    private void CloseOverlay() {
        if (_overlayHandle != null) {
            SplashScreenManager.CloseOverlayForm(_overlayHandle);
            _overlayHandle = null;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        CloseOverlay();
        _gitHubService?.Dispose();
        base.OnFormClosed(e);
    }
}
