using System.Text.Json;
using System.Text.Json.Serialization;
using ReWASD.Core.Models;

namespace ReWASD.Core.Serialization;

public static class ProfileSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(Profile profile)
    {
        return JsonSerializer.Serialize(profile, Options);
    }

    public static Profile FromJson(string json)
    {
        return JsonSerializer.Deserialize<Profile>(json, Options) ?? new Profile();
    }

    public static void SaveToFile(Profile profile, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, ToJson(profile));
    }

    public static Profile LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return new Profile();
        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }
}
