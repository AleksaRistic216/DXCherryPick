// DXAssistant plugin for DXCherryPick: installs the app by building it from source, and launches it.
// Install and update are the same steps: get the latest source, publish it, swap it in.
using System.Diagnostics;
using System.Threading;

public class DXCherryPickApp : IStandaloneAppPlugin {
    const string RepositoryUrl = "https://github.com/AleksaRistic216/DXCherryPick.git";
    const string ProjectPath = "src/DXCP.WinForms/DXCP.WinForms.csproj";
    const string ExecutableName = "DXCP.WinForms.exe";

    // Layout of context.AppDirectory:
    //   source\       git clone of the repository
    //   app\          published application that Launch starts
    //   version.txt   commit the published application was built from
    static string SourceDirectory(IStandaloneAppContext context) => Path.Combine(context.AppDirectory, "source");
    static string InstallDirectory(IStandaloneAppContext context) => Path.Combine(context.AppDirectory, "app");
    static string VersionFile(IStandaloneAppContext context) => Path.Combine(context.AppDirectory, "version.txt");

    public async Task<StandaloneAppStatus> GetStatusAsync(IStandaloneAppContext context, CancellationToken cancellationToken) {
        string? available = null;
        try {
            string remoteHead = await context.CaptureAsync("git", new[] { "ls-remote", RepositoryUrl, "HEAD" }, null, cancellationToken);
            available = ShortCommit(remoteHead.Split('\t')[0]);
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {
            context.Log($"Could not check for updates: {ex.Message}");
        }

        bool installed = File.Exists(Path.Combine(InstallDirectory(context), ExecutableName)) && File.Exists(VersionFile(context));
        return installed
            ? StandaloneAppStatus.Installed(File.ReadAllText(VersionFile(context)).Trim(), available)
            : StandaloneAppStatus.NotInstalled(available);
    }

    public async Task InstallAsync(IStandaloneAppContext context, CancellationToken cancellationToken) {
        await EnsureDotNetSdkAsync(context, cancellationToken);

        string source = SourceDirectory(context);
        if(Directory.Exists(Path.Combine(source, ".git"))) {
            await context.RunAsync("git", new[] { "fetch", "--depth", "1", "origin", "HEAD" }, source, cancellationToken);
            await context.RunAsync("git", new[] { "reset", "--hard", "FETCH_HEAD" }, source, cancellationToken);
        }
        else {
            DeleteDirectory(source);
            await context.RunAsync("git", new[] { "clone", "--depth", "1", RepositoryUrl, source }, null, cancellationToken);
        }
        string commit = ShortCommit(await context.CaptureAsync("git", new[] { "rev-parse", "HEAD" }, source, cancellationToken));

        // Publish next to the installed copy first, so a failed build leaves the working installation alone.
        string staging = InstallDirectory(context) + ".new";
        DeleteDirectory(staging);
        await context.RunAsync("dotnet", new[] {
            "publish", ProjectPath, "-c", "Release", "-r", "win-x64", "--self-contained", "false", "-o", staging, "--nologo",
        }, source, cancellationToken);

        // Move the old copy aside rather than deleting it: a running DXCherryPick makes the move fail
        // before anything is removed, instead of leaving a half-deleted installation.
        string target = InstallDirectory(context);
        string previous = target + ".old";
        DeleteDirectory(previous);
        if(Directory.Exists(target)) {
            try {
                Directory.Move(target, previous);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
                throw new InvalidOperationException("DXCherryPick is running. Close it and click Update again.", ex);
            }
        }
        Directory.Move(staging, target);
        File.WriteAllText(VersionFile(context), commit);
        DeleteDirectory(previous);
        context.Log($"DXCherryPick {commit} is installed in {target}.");
    }

    public ProcessStartInfo GetLaunchInfo(IStandaloneAppContext context) =>
        new ProcessStartInfo(Path.Combine(InstallDirectory(context), ExecutableName)) {
            WorkingDirectory = InstallDirectory(context),
            UseShellExecute = true,
        };

    static async Task EnsureDotNetSdkAsync(IStandaloneAppContext context, CancellationToken cancellationToken) {
        string sdks;
        try {
            sdks = await context.CaptureAsync("dotnet", new[] { "--list-sdks" }, null, cancellationToken);
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {
            sdks = string.Empty;
        }
        if(!sdks.Split('\n').Any(line => line.TrimStart().StartsWith("10.")))
            throw new InvalidOperationException("Building DXCherryPick needs the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0");
    }

    static string ShortCommit(string commit) => commit.Trim().Length > 7 ? commit.Trim().Substring(0, 7) : commit.Trim();

    // git marks object files read-only, which Directory.Delete refuses.
    static void DeleteDirectory(string directory) {
        if(!Directory.Exists(directory))
            return;
        foreach(string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(directory, true);
    }
}

return new DXCherryPickApp();
