namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop.Infrastructure;

internal sealed class ClaudeDesktopDataDirectory
{
    private const string ConfigFileName = "config.json";
    private const string LocalStateFileName = "Local State";

    private ClaudeDesktopDataDirectory(string rootDirectoryPath, string configFilePath, string localStateFilePath)
    {
        RootDirectoryPath = rootDirectoryPath;
        ConfigFilePath = configFilePath;
        LocalStateFilePath = localStateFilePath;
    }

    public string RootDirectoryPath { get; }

    public string ConfigFilePath { get; }

    public string LocalStateFilePath { get; }

    public static IReadOnlyList<ClaudeDesktopDataDirectory> Discover()
    {
        var dataDirectoryPaths = new List<string>();
        var localApplicationDataDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddDataDirectoryPathsFromClaudeDataDirectory(dataDirectoryPaths, Path.Combine(localApplicationDataDirectoryPath, "Claude-Data"));
        AddDataDirectoryPathsFromPackageDirectory(dataDirectoryPaths, Path.Combine(localApplicationDataDirectoryPath, "Packages"));
        dataDirectoryPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude"));

        return dataDirectoryPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(TryCreate)
            .Where(dataDirectory => dataDirectory is not null)
            .Select(dataDirectory => dataDirectory!)
            .ToArray();
    }

    private static void AddDataDirectoryPathsFromClaudeDataDirectory(List<string> dataDirectoryPaths, string claudeDataDirectoryPath)
    {
        try
        {
            if (!Directory.Exists(claudeDataDirectoryPath)) return;
            dataDirectoryPaths.AddRange(Directory.EnumerateDirectories(claudeDataDirectoryPath, "claude_*").OrderByDescending(directoryPath => directoryPath, StringComparer.OrdinalIgnoreCase));
        }
        catch { }
    }

    private static void AddDataDirectoryPathsFromPackageDirectory(List<string> dataDirectoryPaths, string packagesDirectoryPath)
    {
        try
        {
            if (!Directory.Exists(packagesDirectoryPath)) return;
            foreach (var packageDirectoryPath in Directory.EnumerateDirectories(packagesDirectoryPath, "Claude_*")) dataDirectoryPaths.Add(Path.Combine(packageDirectoryPath, "LocalCache", "Roaming", "Claude"));
        }
        catch { }
    }

    private static ClaudeDesktopDataDirectory? TryCreate(string rootDirectoryPath)
    {
        try
        {
            var configFilePath = Path.Combine(rootDirectoryPath, ConfigFileName);
            var localStateFilePath = Path.Combine(rootDirectoryPath, LocalStateFileName);
            if (!File.Exists(configFilePath) || !File.Exists(localStateFilePath)) return null;
            return new ClaudeDesktopDataDirectory(rootDirectoryPath, configFilePath, localStateFilePath);
        }
        catch { return null; }
    }
}
