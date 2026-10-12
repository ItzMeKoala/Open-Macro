using System.Collections;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace ReWASD.UI.Mac;

/// <summary>Hosts the shared view models in Avalonia: UI-thread work and a render-priority timer.</summary>
public sealed class AvaloniaUi : IPlatformUi
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public IDisposable StartTimer(TimeSpan interval, Action tick)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = interval };
        timer.Tick += (_, _) => tick();
        timer.Start();
        return new TimerStopper(timer);
    }

    public void RequestExit() =>
        Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());

    public bool SetDockIconHidden(bool hidden) => OperatingSystem.IsMacOS() && ReWASD.Engine.Platform.MacOS.MacDock.SetHidden(hidden);

    private static Avalonia.Controls.Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static readonly Avalonia.Platform.Storage.FilePickerFileType JsonFiles = new("ReWASD profile") { Patterns = new[] { "*.json" } };

    public async Task<string?> PickOpenFileAsync(string title)
    {
        var storage = MainWindow?.StorageProvider;
        if (storage == null) return null;

        var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonFiles }
        });
        return files.Count == 0 ? null : Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(files[0]);
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string extension = "json")
    {
        var storage = MainWindow?.StorageProvider;
        if (storage == null) return null;

        var type = extension == "json" ? JsonFiles : new Avalonia.Platform.Storage.FilePickerFileType($"{extension.ToUpperInvariant()} file") { Patterns = new[] { "*." + extension } };
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { type }
        });
        return file == null ? null : Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(file);
    }

    public async Task<bool> SetClipboardTextAsync(string text)
    {
        var clipboard = MainWindow?.Clipboard;
        if (clipboard == null) return false;
        try
        {
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = MainWindow;
        if (owner == null) return false;
        return await ConfirmDialog.ShowAsync(owner, title, message);
    }

    // Avalonia has no grouped collection views; the view model also offers RemapGroups, which the menu uses
    public IEnumerable? CreateGroupedView(IEnumerable source, string groupProperty) => null;

    private sealed class TimerStopper : IDisposable
    {
        private readonly DispatcherTimer _timer;
        public TimerStopper(DispatcherTimer timer) => _timer = timer;
        public void Dispose() => _timer.Stop();
    }
}
