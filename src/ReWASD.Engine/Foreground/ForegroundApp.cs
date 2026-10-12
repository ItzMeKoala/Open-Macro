using ReWASD.Core.Models;

namespace ReWASD.Engine.Foreground;

/// <summary>The application the person is working in right now.</summary>
/// <param name="Name">Its process name on Windows (chrome.exe) or its display name on macOS (Safari).</param>
/// <param name="Id">Windows: the full path of the program. macOS: the bundle identifier (com.apple.Safari). May be null.</param>
/// <param name="ProcessId">The operating system's number for the process.</param>
public sealed record ForegroundApp(string Name, string? Id, int ProcessId);

public static class AppMatcher
{
    /// <summary>
    /// Whether a profile's "target process" text matches an app. The text can name the program (game.exe, game, Safari),
    /// its path or bundle id (com.apple.Safari), and can list several separated by commas or semicolons.
    /// Case never matters and a trailing .exe is optional.
    /// </summary>
    public static bool Matches(string? pattern, ForegroundApp? app)
    {
        if (app == null || string.IsNullOrWhiteSpace(pattern)) return false;

        var candidates = new List<string> { Normalize(app.Name) };
        if (!string.IsNullOrWhiteSpace(app.Id))
        {
            candidates.Add(Normalize(app.Id));
            candidates.Add(Normalize(Path.GetFileName(app.Id.Replace('\\', '/'))));
        }

        foreach (var wanted in pattern.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string w = Normalize(wanted);
            if (w.Length > 0 && candidates.Contains(w)) return true;
        }
        return false;
    }

    private static string Normalize(string text)
    {
        text = text.Trim().Trim('"').ToLowerInvariant();
        if (text.EndsWith(".exe")) text = text[..^4];
        if (text.EndsWith(".app")) text = text[..^4];
        return text;
    }

    /// <summary>
    /// The profile to use for the current app: the first one linked to it, or the profile the person picked by hand when
    /// none is. Our own window never counts as an app (the person is only editing), so it keeps whatever is active.
    /// </summary>
    public static Profile? Choose(IEnumerable<Profile> profiles, ForegroundApp? app, int ownProcessId, Profile manual, Profile? current)
    {
        if (app != null && app.ProcessId == ownProcessId) return current ?? manual;
        if (app == null) return manual;

        return profiles.FirstOrDefault(p => Matches(p.ProcessExecutable, app)) ?? manual;
    }
}
