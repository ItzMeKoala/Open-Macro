using System.Reflection;

namespace ReWASD.UI;

/// <summary>Which build is running: the version and the git commit it was built from, so an old copy is easy to spot.</summary>
public static class BuildInfo
{
    public const string Version = "0.1.0-alpha";

    /// <summary>The short git commit this build was made from, or empty if it was built without git.</summary>
    public static string Revision { get; } = ReadRevision();

    /// <summary>For example "v0.1.0-alpha · 80cf0d3".</summary>
    public static string Text => Revision.Length > 0 ? $"v{Version} · {Revision}" : $"v{Version}";

    private static string ReadRevision()
    {
        var info = typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        int plus = info?.IndexOf('+') ?? -1;
        if (info == null || plus < 0) return string.Empty;

        string hash = info[(plus + 1)..].Trim();
        return hash.Length >= 7 && hash.All(Uri.IsHexDigit) ? hash[..7] : string.Empty;
    }
}
