using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using ReWASD.Engine.Platform;
using ReWASD.UI.Updates;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Tests;

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<string> Requests { get; } = new();

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.ToString());
        return Task.FromResult(_respond(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public class UpdateServiceTests
{
    private const string Remote = "1234567890abcdef1234567890abcdef12345678";

    private static string Release(string? commitLine = $"commit: {Remote}", bool windowsAsset = true) => $$"""
        {
          "name": "Latest build",
          "body": "{{commitLine}}",
          "assets": [
            {{(windowsAsset ? """{ "name": "ReWASD-win-x64.exe", "browser_download_url": "https://example.test/ReWASD-win-x64.exe", "size": 100 },""" : "")}}
            { "name": "ReWASD-osx-arm64.zip", "browser_download_url": "https://example.test/ReWASD-osx-arm64.zip", "size": 200 },
            { "name": "ReWASD-osx-x64.zip", "browser_download_url": "https://example.test/ReWASD-osx-x64.zip", "size": 300 }
          ]
        }
        """;

    private static UpdateService Service(Func<HttpRequestMessage, HttpResponseMessage> respond, out FakeHandler handler)
    {
        handler = new FakeHandler(respond);
        return new UpdateService(new HttpClient(handler));
    }

    private static HttpResponseMessage Api(HttpRequestMessage r, string compareStatus = "ahead") =>
        r.RequestUri!.AbsolutePath.Contains("/releases/tags/") ? FakeHandler.Json(Release())
        : r.RequestUri.AbsolutePath.Contains("/compare/") ? FakeHandler.Json($$"""{ "status": "{{compareStatus}}" }""")
        : new HttpResponseMessage(HttpStatusCode.NotFound);

    [Theory]
    [InlineData(true, false, Architecture.X64, "ReWASD-win-x64.exe")]
    [InlineData(false, true, Architecture.Arm64, "ReWASD-osx-arm64.zip")]
    [InlineData(false, true, Architecture.X64, "ReWASD-osx-x64.zip")]
    [InlineData(false, false, Architecture.X64, null)]
    public void TheRightFileIsChosenForTheComputer(bool windows, bool mac, Architecture arch, string? expected) =>
        Assert.Equal(expected, UpdateService.AssetNameFor(windows, mac, arch));

    [Fact]
    public async Task ACommitAheadOfOurs_IsAnUpdate_WithTheFileForThisComputer()
    {
        var service = Service(r => Api(r), out var handler);

        var result = await service.CheckAsync("abc1234");

        var available = Assert.IsType<UpdateCheck.Available>(result);
        Assert.Equal("1234567", available.Update.Revision);
        Assert.Equal(UpdateService.AssetNameForThisComputer(), available.Update.AssetName);
        Assert.StartsWith("https://example.test/", available.Update.DownloadUrl);
        Assert.Contains(handler.Requests, u => u.Contains("/compare/abc1234..." + Remote));
    }

    [Theory]
    [InlineData("identical")]
    [InlineData("behind")]
    [InlineData("diverged")]
    public async Task ACommitThatIsNotAhead_IsNotAnUpdate(string status)
    {
        var service = Service(r => Api(r, status), out _);

        Assert.IsType<UpdateCheck.UpToDate>(await service.CheckAsync("abc1234"));
    }

    [Fact]
    public async Task TheSameCommit_IsUpToDate_WithoutAskingGitHubToCompare()
    {
        var service = Service(r => Api(r), out var handler);

        Assert.IsType<UpdateCheck.UpToDate>(await service.CheckAsync("1234567"));
        Assert.DoesNotContain(handler.Requests, u => u.Contains("/compare/"));
    }

    [Fact]
    public async Task ABuildOfACommitGitHubHasNeverSeen_IsLeftAlone()
    {
        var service = Service(r => r.RequestUri!.AbsolutePath.Contains("/releases/tags/") ? FakeHandler.Json(Release()) : new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        Assert.IsType<UpdateCheck.Unknown>(await service.CheckAsync("deadbee"));
    }

    [Fact]
    public async Task NoPublishedBuild_NoCommitLine_OrNoFileForThisComputer_AreNotErrors()
    {
        Assert.IsType<UpdateCheck.Unknown>(await Service(_ => new HttpResponseMessage(HttpStatusCode.NotFound), out _).CheckAsync("abc1234"));
        Assert.IsType<UpdateCheck.Unknown>(await Service(_ => FakeHandler.Json(Release(commitLine: "no commit here")), out _).CheckAsync("abc1234"));
        Assert.IsType<UpdateCheck.Unknown>(await Service(_ => FakeHandler.Json("""{ "body": "commit: 1234567", "assets": [] }"""), out _).CheckAsync("abc1234"));
    }

    [Fact]
    public async Task BeingOffline_OrRateLimited_IsNotAnError()
    {
        Assert.IsType<UpdateCheck.Unknown>(await Service(_ => throw new HttpRequestException("no network"), out _).CheckAsync("abc1234"));
        Assert.IsType<UpdateCheck.Unknown>(await Service(_ => new HttpResponseMessage(HttpStatusCode.Forbidden), out _).CheckAsync("abc1234"));
        Assert.IsType<UpdateCheck.Unknown>(await Service(_ => FakeHandler.Json("not json"), out _).CheckAsync("abc1234"));
    }

    [Fact]
    public async Task ABuildWithoutAKnownCommit_NeverAsksTheNetwork()
    {
        var service = Service(r => Api(r), out var handler);

        Assert.IsType<UpdateCheck.Unknown>(await service.CheckAsync(""));
        Assert.Empty(handler.Requests);
    }
}

public class UpdateInstallerTests
{
    [Fact]
    public void OnlyABuiltApp_CanReplaceItself()
    {
        Assert.Null(UpdateInstaller.InstalledTarget(null));
        Assert.Null(UpdateInstaller.InstalledTarget("/usr/local/share/dotnet/dotnet"));

        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("/Applications/ReWASD.app", UpdateInstaller.InstalledTarget("/Applications/ReWASD.app/Contents/MacOS/ReWASD"));
            Assert.Equal("/Users/me/dist/ReWASD.app", UpdateInstaller.InstalledTarget("/Users/me/dist/ReWASD.app/Contents/MacOS/ReWASD"));
        }
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(@"C:\Tools\ReWASD.exe", UpdateInstaller.InstalledTarget(@"C:\Tools\ReWASD.exe"));
            Assert.Null(UpdateInstaller.InstalledTarget(@"C:\Program Files\dotnet\dotnet.exe"));
        }
    }

    [Fact]
    public void TheMacScript_WaitsForTheApp_ReplacesIt_AndReopensIt()
    {
        string script = UpdateInstaller.MacScript("/tmp/ReWASD-update/ReWASD-osx-arm64.zip", "/Users/me/dist/ReWASD.app", 4242);

        Assert.StartsWith("#!/bin/sh", script);
        Assert.Contains("PID=4242", script);
        Assert.Contains("kill -0", script);                                  // waits for the old app to close
        Assert.Contains("ditto -x -k \"$ZIP\" \"$STAGE\"", script);
        Assert.Contains("APP='/Users/me/dist/ReWASD.app'", script);
        Assert.Contains("rm -rf \"$APP\"", script);
        Assert.True(script.IndexOf("ditto -x", StringComparison.Ordinal) < script.IndexOf("rm -rf \"$APP\"", StringComparison.Ordinal), "unpack before removing the old app");
        Assert.Contains("open -n \"$APP\"", script);
    }

    [Fact]
    public void ThePathsInTheMacScript_AreQuotedSoSpacesAndQuotesCannotBreakIt()
    {
        string script = UpdateInstaller.MacScript("/tmp/it's here/x.zip", "/Users/me/My Apps/ReWASD.app", 1);

        Assert.Contains("ZIP='/tmp/it'\\''s here/x.zip'", script);
        Assert.Contains("APP='/Users/me/My Apps/ReWASD.app'", script);
    }

    [Fact]
    public void TheWindowsScript_WaitsForTheApp_ReplacesIt_AndStartsTheNewOne()
    {
        string script = UpdateInstaller.WindowsScript(@"C:\Temp\ReWASD-update\ReWASD-win-x64.exe", @"C:\Tools\ReWASD.exe", 777);

        Assert.Contains("tasklist /fi \"PID eq 777\"", script);               // waits for the old app to close
        Assert.Contains(@"set ""TARGET=C:\Tools\ReWASD.exe""", script);
        Assert.Contains("move /y \"%NEW%\" \"%TARGET%\"", script);
        Assert.Contains("start \"\" \"%TARGET%\"", script);
        Assert.Contains("\r\n", script);
        Assert.True(script.IndexOf("move /y", StringComparison.Ordinal) < script.IndexOf("start \"\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDownload_IsSavedWithProgress_AndAnEmptyOneIsRefused()
    {
        var bytes = new byte[300_000];
        new Random(1).NextBytes(bytes);
        var ok = new UpdateInstaller(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })));
        var reports = new List<double>();

        string file = await ok.DownloadAsync(new UpdateInfo("abc1234", "test-update.bin", "https://example.test/x", bytes.Length), new Progress<double>(reports.Add));
        await Task.Delay(50);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(file));
        Assert.Contains(1.0, reports);
        File.Delete(file);

        var empty = new UpdateInstaller(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) })));
        await Assert.ThrowsAsync<IOException>(() => empty.DownloadAsync(new UpdateInfo("abc1234", "test-empty.bin", "https://example.test/x", 0)));

        var failing = new UpdateInstaller(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));
        await Assert.ThrowsAsync<HttpRequestException>(() => failing.DownloadAsync(new UpdateInfo("abc1234", "test-missing.bin", "https://example.test/x", 0)));
    }
}

public class UpdateViewModelTests
{
    private sealed class FakeUpdates : IUpdateService
    {
        public UpdateCheck Result = new UpdateCheck.UpToDate();
        public int Calls;
        public Task<UpdateCheck> CheckAsync(string localRevision, CancellationToken cancellation = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private static MainViewModel New(IUpdateService updates, string? dir = null) =>
        new(Fakes.Platform(PlatformServices.MacOSInfo), new Fakes.Ui(), dir ?? Fakes.TempProfiles(), updates);

    [Fact]
    public async Task ANewerBuild_ShowsTheUpdateButton_AndSaysSo()
    {
        var vm = New(new FakeUpdates { Result = new UpdateCheck.Available(new UpdateInfo("abc1234", "x.zip", "https://example.test/x.zip", 1)) });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(vm.IsUpdateAvailable);
        Assert.True(vm.ShowUpdateButton);
        Assert.Contains("abc1234", vm.UpdateStatusText);
    }

    [Fact]
    public async Task TheLatestBuild_AndAnOfflineCheck_AreTold_WithoutAnyButton()
    {
        var updates = new FakeUpdates();
        var vm = New(updates);

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);
        Assert.False(vm.IsUpdateAvailable);
        Assert.Equal("You have the latest build.", vm.UpdateStatusText);

        updates.Result = new UpdateCheck.Unknown("no network");
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);
        Assert.False(vm.IsUpdateAvailable);
        Assert.Contains("no network", vm.UpdateStatusText);
    }

    [Fact]
    public async Task ACopyRunFromSource_ExplainsItCannotReplaceItself_AndIsNotClosed()
    {
        var ui = new Fakes.Ui();
        var vm = new MainViewModel(Fakes.Platform(PlatformServices.MacOSInfo), ui, Fakes.TempProfiles(),
            new FakeUpdates { Result = new UpdateCheck.Available(new UpdateInfo("abc1234", "x.zip", "https://example.test/x.zip", 1)) });
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        await vm.InstallUpdateCommand.ExecuteAsync(null);   // the tests run from dotnet, not from a built ReWASD.app

        Assert.Contains("build script", vm.UpdateStatusText);
        Assert.False(ui.ExitRequested);
        Assert.False(vm.IsUpdating);
    }

    [Fact]
    public void TheCheckOnOrOffChoice_IsRemembered()
    {
        string dir = Fakes.TempProfiles();
        var first = New(new FakeUpdates(), dir);
        Assert.True(first.AutoCheckUpdates);

        first.AutoCheckUpdates = false;
        var second = New(new FakeUpdates(), dir);

        Assert.False(second.AutoCheckUpdates);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task NoUpdateSource_MeansNoCheckAtAll()
    {
        var updates = new FakeUpdates();
        var vm = New(updates);
        await Task.Delay(100);

        Assert.Equal(0, updates.Calls);        // a check on start-up waits a few seconds, and a test never waits that long
        Assert.False(vm.IsUpdateAvailable);
    }
}
