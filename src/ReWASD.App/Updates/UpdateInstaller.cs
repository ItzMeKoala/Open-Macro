using System.Diagnostics;

namespace ReWASD.UI.Updates;

/// <summary>
/// Downloads an update and swaps it in for the running app: a small script waits for the app to close, replaces it,
/// and starts the new one. Only a built app can be replaced; a copy run from source is told to rebuild instead.
/// </summary>
public sealed class UpdateInstaller
{
    private readonly HttpClient _http;

    public UpdateInstaller(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0) _http.DefaultRequestHeaders.UserAgent.ParseAdd("ReWASD-updater");
    }

    /// <summary>The file or app bundle that would be replaced, or null when this copy is not a built app (for example run from source).</summary>
    public static string? InstalledTarget(string? processPath)
    {
        if (string.IsNullOrEmpty(processPath)) return null;

        if (OperatingSystem.IsWindows())
        {
            return string.Equals(Path.GetFileName(processPath), "ReWASD.exe", StringComparison.OrdinalIgnoreCase) ? processPath : null;
        }

        if (OperatingSystem.IsMacOS())
        {
            const string Marker = ".app/Contents/MacOS/";
            int i = processPath.IndexOf(Marker, StringComparison.Ordinal);
            return i > 0 ? processPath[..(i + 4)] : null;
        }

        return null;
    }

    public bool CanSelfUpdate => InstalledTarget(Environment.ProcessPath) != null;

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        string folder = Path.Combine(Path.GetTempPath(), "ReWASD-update");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, update.AssetName);

        using var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? update.Size;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellation))
        await using (var target = File.Create(file))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                done += read;
                if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
            }
        }

        if (new FileInfo(file).Length == 0) throw new IOException("The downloaded update is empty");
        return file;
    }

    /// <summary>Writes and starts the script that replaces the running app with the download. The caller must then close the app.</summary>
    public void Install(string downloadedFile)
    {
        string? target = InstalledTarget(Environment.ProcessPath) ?? throw new InvalidOperationException("This copy of ReWASD is not a built app, so it cannot replace itself");
        int pid = Environment.ProcessId;
        string folder = Path.GetDirectoryName(downloadedFile)!;

        if (OperatingSystem.IsWindows())
        {
            string script = Path.Combine(folder, "update.cmd");
            File.WriteAllText(script, WindowsScript(downloadedFile, target, pid));
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { UseShellExecute = false, CreateNoWindow = true });
        }
        else
        {
            string script = Path.Combine(folder, "update.sh");
            File.WriteAllText(script, MacScript(downloadedFile, target, pid));
            Process.Start(new ProcessStartInfo("/bin/sh", $"\"{script}\"") { UseShellExecute = false, CreateNoWindow = true });
        }
    }

    // ---- the scripts (plain text, so they can be checked without running anything) --------------------------------------

    public static string WindowsScript(string newExe, string targetExe, int processId) => string.Join("\r\n", new[]
    {
        "@echo off",
        $"set \"NEW={newExe}\"",
        $"set \"TARGET={targetExe}\"",
        "set /a tries=0",
        ":wait",
        $"tasklist /fi \"PID eq {processId}\" 2>nul | find \"{processId}\" >nul",
        "if not errorlevel 1 (",
        "  ping 127.0.0.1 -n 2 >nul",
        "  goto wait",
        ")",
        ":replace",
        "move /y \"%NEW%\" \"%TARGET%\" >nul 2>&1",
        "if not errorlevel 1 goto start",
        "set /a tries+=1",
        "if %tries% geq 40 exit /b 1",
        "ping 127.0.0.1 -n 2 >nul",
        "goto replace",
        ":start",
        "start \"\" \"%TARGET%\"",
        "(goto) 2>nul & del \"%~f0\""
    }) + "\r\n";

    public static string MacScript(string zipFile, string appBundle, int processId) => string.Join("\n", new[]
    {
        "#!/bin/sh",
        $"PID={processId}",
        $"ZIP='{Quote(zipFile)}'",
        $"APP='{Quote(appBundle)}'",
        "while kill -0 \"$PID\" 2>/dev/null; do sleep 0.3; done",
        "STAGE=$(mktemp -d)",
        "ditto -x -k \"$ZIP\" \"$STAGE\" || exit 1",
        "[ -d \"$STAGE/ReWASD.app\" ] || exit 1",
        "rm -rf \"$APP\"",
        "mv \"$STAGE/ReWASD.app\" \"$APP\" || exit 1",
        "xattr -cr \"$APP\" 2>/dev/null",
        "open -n \"$APP\"",
        "rm -rf \"$STAGE\" \"$ZIP\" \"$0\""
    }) + "\n";

    private static string Quote(string path) => path.Replace("'", "'\\''");
}
