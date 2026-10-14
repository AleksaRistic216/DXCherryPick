// DXAssistant plugin for DXCherryPick: installs the app by building it from source, and launches it.
// DXAssistant runs InstallAsync again whenever this plugin is updated, which rebuilds the app from the latest source.
using System.Diagnostics;
using System.Threading;

public class DXCherryPickApp : IStandaloneAppPlugin {
    const string RepositoryUrl = "https://github.com/AleksaRistic216/DXCherryPick.git";
    const string ProjectPath = "src/DXCP.WinForms/DXCP.WinForms.csproj";
    const string ExecutableName = "DXCP.WinForms.exe";

    // Layout of context.AppDirectory:
    //   source\   git clone of the repository
    //   app\      published application that Launch starts
    static string SourceDirectory(IStandaloneAppContext context) => Path.Combine(context.AppDirectory, "source");
    static string InstallDirectory(IStandaloneAppContext context) => Path.Combine(context.AppDirectory, "app");

    public Task<bool> IsInstalledAsync(IStandaloneAppContext context, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(Path.Combine(InstallDirectory(context), ExecutableName)));

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
                throw new InvalidOperationException("DXCherryPick is running. Close it and try again.", ex);
            }
        }
        Directory.Move(staging, target);
        DeleteDirectory(previous);
        context.Log($"DXCherryPick is installed in {target}.");
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
