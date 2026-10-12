using System.Text.Json;
using ReWASD.Core.Models;
using ReWASD.Core.Serialization;

namespace ReWASD.UI.Profiles;

/// <summary>One saved copy of a profile, kept before something replaced or removed it.</summary>
public sealed record HistoryEntry(string FilePath, DateTime SavedAt, string Reason, string ProfileName)
{
    public string Description => $"{SavedAt:g} — {Reason}: {ProfileName}";
}

/// <summary>
/// Quietly keeps copies of profiles before they are deleted, replaced by a save, or restored over, so any of those can be undone.
/// Copies live in a "history" folder next to the profiles; only the newest few are kept.
/// </summary>
public sealed class ProfileHistory
{
    public const int MaxEntries = 20;

    private sealed class Stored
    {
        public string Reason { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
        public Profile Profile { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private readonly string _folder;
    private readonly Func<DateTime> _now;
    private int _counter;

    public ProfileHistory(string profilesDirectory, Func<DateTime>? now = null)
    {
        _folder = Path.Combine(profilesDirectory, "history");
        _now = now ?? (() => DateTime.Now);
    }

    /// <summary>Saves a copy of the profile as it is now. Never throws: failing to keep history must not stop what the person asked for.</summary>
    public bool Snapshot(Profile profile, string reason)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            var when = _now();
            string name = $"{when:yyyyMMdd-HHmmss-fff}-{Interlocked.Increment(ref _counter):D3}.json";
            var stored = new Stored { Reason = reason, SavedAt = when, Profile = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile)) };
            File.WriteAllText(Path.Combine(_folder, name), JsonSerializer.Serialize(stored, Options));
            Trim();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    /// <summary>The kept copies, newest first.</summary>
    public IReadOnlyList<HistoryEntry> List()
    {
        var entries = new List<HistoryEntry>();
        if (!Directory.Exists(_folder)) return entries;

        foreach (var file in Directory.EnumerateFiles(_folder, "*.json").OrderByDescending(f => f, StringComparer.Ordinal))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(file), Options);
                if (stored != null) entries.Add(new HistoryEntry(file, stored.SavedAt, stored.Reason, stored.Profile.Name));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // A damaged copy is skipped, the others stay usable
            }
        }
        return entries;
    }

    /// <summary>The profile as it was in a kept copy, or null if the copy is gone or damaged.</summary>
    public Profile? Load(HistoryEntry entry)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(entry.FilePath), Options);
            if (stored == null) return null;
            foreach (var layer in stored.Profile.ShiftLayers.Values) layer.RestoreComparers();
            return stored.Profile;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Trim()
    {
        var files = Directory.EnumerateFiles(_folder, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
        foreach (var old in files.Take(Math.Max(0, files.Count - MaxEntries)))
        {
            try { File.Delete(old); } catch (IOException) { }
        }
    }
}
