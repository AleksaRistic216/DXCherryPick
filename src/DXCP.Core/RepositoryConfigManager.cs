using System.Text.Json;

namespace DXCP.Core;

public class RepositoryConfigManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DXCherryPick");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "repos.json");

    private Dictionary<string, string> _repoMappings = new(StringComparer.OrdinalIgnoreCase);

    public RepositoryConfigManager()
    {
        Load();
    }

    public string? GetWorktreeParentFolder(string githubRepo)
    {
        return _repoMappings.TryGetValue(githubRepo, out var path) ? path : null;
    }

    public void SetWorktreeParentFolder(string githubRepo, string parentFolder)
    {
        _repoMappings[githubRepo] = parentFolder;
        Save();
    }

    public string? GetWorktreePath(string githubRepo, string branch)
    {
        var parentFolder = GetWorktreeParentFolder(githubRepo);
        if (string.IsNullOrEmpty(parentFolder))
            return null;

        return Path.Combine(parentFolder, branch);
    }

    public bool HasWorktreeParentFolder(string githubRepo)
    {
        return _repoMappings.ContainsKey(githubRepo);
    }

    public void RemoveWorktreeParentFolder(string githubRepo)
    {
        if (_repoMappings.Remove(githubRepo))
            Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (mappings != null)
                    _repoMappings = new Dictionary<string, string>(mappings, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _repoMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);

            var json = JsonSerializer.Serialize(_repoMappings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch
        {
            // Config persistence is not critical
        }
    }
}
