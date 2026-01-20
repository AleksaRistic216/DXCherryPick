using System.Diagnostics;

namespace DXCP.WinForms;

public partial class Form1 : Form {
    private GitHubService? _gitHubService;

    public Form1() {
        InitializeComponent();
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
            labelStatus.Text = "Authentication required";
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
            labelStatus.Text = "Authenticating...";
            _gitHubService = new GitHubService(token);
            var username = await _gitHubService.GetCurrentUsernameAsync();
            labelStatus.Text = $"Authenticated as {username}";
            return true;
        }
        catch(Exception ex) {
            _gitHubService?.Dispose();
            _gitHubService = null;

            Debug.WriteLine("=== AUTHENTICATION ERROR ===");
            Debug.WriteLine($"Error: {ex.Message}");
            Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

            labelStatus.Text = "Authentication failed - see Output window";
            return false;
        }
    }

    private async Task LoadPullRequestsAsync() {
        if(_gitHubService == null)
            return;

        btnRefresh.Enabled = false;
        labelStatus.Text = "Loading pull requests...";

        try {
            var username = await _gitHubService.GetCurrentUsernameAsync();
            var pullRequests = await _gitHubService.GetMyPullRequestsAsync();
            gridControl.DataSource = pullRequests;
            gridView.BestFitColumns();
            labelStatus.Text = $"Loaded {pullRequests.Count} pull requests for {username} in DevExpress";
        }
        catch(Exception ex) {
            Debug.WriteLine("=== LOAD PR ERROR ===");
            Debug.WriteLine($"Error: {ex.Message}");
            Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

            labelStatus.Text = "Error loading pull requests - see Output window";
        }
        finally {
            btnRefresh.Enabled = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        _gitHubService?.Dispose();
        base.OnFormClosed(e);
    }
}
