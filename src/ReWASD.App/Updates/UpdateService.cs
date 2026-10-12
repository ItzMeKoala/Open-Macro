using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReWASD.UI.Updates;

/// <summary>A newer build that can be installed.</summary>
public sealed record UpdateInfo(string Revision, string AssetName, string DownloadUrl, long Size);

/// <summary>What looking for an update found.</summary>
public abstract record UpdateCheck
{
    public sealed record UpToDate : UpdateCheck;
    public sealed record Available(UpdateInfo Update) : UpdateCheck;

    /// <summary>The check could not be completed (offline, rate limited, no published build...). Never an error for the person using the app.</summary>
    public sealed record Unknown(string Reason) : UpdateCheck;
}

public interface IUpdateService
{
    Task<UpdateCheck> CheckAsync(string localRevision, CancellationToken cancellation = default);
}

/// <summary>An update source that never finds anything (tests, and builds made without git).</summary>
public sealed class NoUpdates : IUpdateService
{
    public Task<UpdateCheck> CheckAsync(string localRevision, CancellationToken cancellation = default) =>
        Task.FromResult<UpdateCheck>(new UpdateCheck.Unknown("Updates are switched off in this build"));
}

/// <summary>
/// Looks for a newer build on GitHub. Every push to main publishes the "latest" release with the Windows .exe and the Mac
/// apps; a build is newer when GitHub says the published commit is ahead of the one this app was built from.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public const string Owner = "ItzMeKoala";
    public const string Repository = "ReWASD-Open-Source";
    public const string ReleaseTag = "latest";

    private readonly HttpClient _http;

    public UpdateService(HttpClient? http = null)
    {
        _http = http ?? CreateClient();
    }

    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ReWASD-updater");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>The release file made for this computer: the Windows .exe, or the Mac app for this CPU.</summary>
    public static string? AssetNameFor(bool windows, bool mac, Architecture architecture) =>
        windows ? "ReWASD-win-x64.exe"
        : mac ? (architecture == Architecture.Arm64 ? "ReWASD-osx-arm64.zip" : "ReWASD-osx-x64.zip")
        : null;

    public static string? AssetNameForThisComputer() =>
        AssetNameFor(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture);

    public async Task<UpdateCheck> CheckAsync(string localRevision, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(localRevision)) return new UpdateCheck.Unknown("This build does not know which commit it came from");

        string? wanted = AssetNameForThisComputer();
        if (wanted == null) return new UpdateCheck.Unknown("Updates are not available on this system");

        try
        {
            using var release = await GetJsonAsync($"https://api.github.com/repos/{Owner}/{Repository}/releases/tags/{ReleaseTag}", cancellation);
            if (release == null) return new UpdateCheck.Unknown("No published build yet");

            var root = release.RootElement;
            string? remote = CommitOf(root);
            if (remote == null) return new UpdateCheck.Unknown("The published build does not say which commit it is");

            var asset = FindAsset(root, wanted);
            if (asset == null) return new UpdateCheck.Unknown($"The published build has no {wanted}");

            if (remote.StartsWith(localRevision, StringComparison.OrdinalIgnoreCase) || localRevision.StartsWith(remote, StringComparison.OrdinalIgnoreCase))
                return new UpdateCheck.UpToDate();

            // Ask GitHub whether the published commit is ahead of ours; a build of a commit it has never seen is left alone
            using var compare = await GetJsonAsync($"https://api.github.com/repos/{Owner}/{Repository}/compare/{localRevision}...{remote}", cancellation);
            if (compare == null) return new UpdateCheck.Unknown("This build is not on GitHub, so there is nothing to compare it with");

            string? status = compare.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            return status == "ahead"
                ? new UpdateCheck.Available(new UpdateInfo(remote[..Math.Min(7, remote.Length)], asset.Value.Name, asset.Value.Url, asset.Value.Size))
                : new UpdateCheck.UpToDate();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return new UpdateCheck.Unknown("Could not reach GitHub: " + ex.Message);
        }
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellation)
    {
        using var response = await _http.GetAsync(url, cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellation), cancellationToken: cancellation);
    }

    // The release notes carry a line "commit: <sha>" written by the build that published it
    internal static string? CommitOf(JsonElement release)
    {
        string text = (release.TryGetProperty("body", out var b) ? b.GetString() : null) + " " + (release.TryGetProperty("name", out var n) ? n.GetString() : null);
        var match = Regex.Match(text, @"commit:\s*([0-9a-fA-F]{7,40})");
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static (string Name, string Url, long Size)? FindAsset(JsonElement release, string name)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;

        foreach (var a in assets.EnumerateArray())
        {
            if (a.TryGetProperty("name", out var n) && string.Equals(n.GetString(), name, StringComparison.OrdinalIgnoreCase)
                && a.TryGetProperty("browser_download_url", out var u) && u.GetString() is { Length: > 0 } url)
            {
                return (n.GetString()!, url, a.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var size) ? size : 0);
            }
        }
        return null;
    }
}
