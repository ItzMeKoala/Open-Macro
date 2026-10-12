namespace ReWASD.UI;

/// <summary>Where the app keeps its files, in the place each operating system expects.</summary>
public static class AppPaths
{
    private const string AppFolder = "ReWASD-OpenSource";

    /// <summary>Saved configs. Windows: %APPDATA%\ReWASD-OpenSource\profiles; macOS: ~/Library/Application Support/ReWASD-OpenSource/profiles.</summary>
    public static string ProfilesDirectory => Path.Combine(DataRoot, AppFolder, "profiles");

    /// <summary>Where unexpected errors are written, so a crash can be understood afterwards.</summary>
    public static string CrashLogPath => Path.Combine(DataRoot, AppFolder, "crash.log");

    /// <summary>Appends an error to the crash log. Never throws: reporting a problem must not cause another.</summary>
    public static void WriteCrashLog(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source} ({BuildInfo.Text})\n{exception}\n\n");
        }
        catch (Exception)
        {
            // Nowhere to write: nothing more can be done
        }
    }

    private static string DataRoot =>
        OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
