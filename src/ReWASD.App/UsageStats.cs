using System.Text.Json;
using ReWASD.Core.Models;

namespace ReWASD.UI;

/// <summary>
/// Counts what the controller is used for, on this computer only: how often each button was pressed, how many macros ran and
/// how long remapping has been on. Nothing here is ever sent anywhere.
/// </summary>
public sealed class UsageStats
{
    public Dictionary<ControllerButton, long> Presses { get; set; } = new();
    public long MacrosRun { get; set; }
    public double SecondsRemapping { get; set; }
    public DateTime Since { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore]
    public long TotalPresses => Presses.Values.Sum();

    private readonly HashSet<ControllerButton> _held = new();

    public void Observe(IReadOnlyCollection<ControllerButton> pressed)
    {
        foreach (var b in pressed)
        {
            if (_held.Add(b)) Presses[b] = Presses.GetValueOrDefault(b) + 1;
        }
        _held.RemoveWhere(b => !pressed.Contains(b));
    }

    /// <summary>The buttons pressed most, most first (ties by name so the list never jumps around).</summary>
    public IReadOnlyList<(ControllerButton Button, long Count)> Top(int count) =>
        Presses.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ThenBy(p => p.Key.ToString(), StringComparer.Ordinal)
            .Take(count).Select(p => (p.Key, p.Value)).ToList();

    public string Summary
    {
        get
        {
            if (TotalPresses == 0 && MacrosRun == 0) return "Nothing counted yet. Press Apply and use your controller.";
            var span = TimeSpan.FromSeconds(SecondsRemapping);
            string time = span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min" : $"{span.Minutes} min {span.Seconds} s";
            return $"{TotalPresses:N0} button presses, {MacrosRun:N0} macros run, {time} of remapping since {Since:d}";
        }
    }

    public void Reset()
    {
        Presses.Clear();
        _held.Clear();
        MacrosRun = 0;
        SecondsRemapping = 0;
        Since = DateTime.Now;
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static UsageStats Load(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<UsageStats>(File.ReadAllText(path), Options) ?? new UsageStats();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Damaged counts start over; they are only for fun
        }
        return new UsageStats();
    }

    public bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
