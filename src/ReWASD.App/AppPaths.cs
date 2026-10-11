namespace ReWASD.UI;

/// <summary>Where the app keeps its files, in the place each operating system expects.</summary>
public static class AppPaths
{
    private const string AppFolder = "ReWASD-OpenSource";

    /// <summary>Saved configs. Windows: %APPDATA%\ReWASD-OpenSource\profiles; macOS: ~/Library/Application Support/ReWASD-OpenSource/profiles.</summary>
    public static string ProfilesDirectory => Path.Combine(DataRoot, AppFolder, "profiles");

    private static string DataRoot =>
        OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
