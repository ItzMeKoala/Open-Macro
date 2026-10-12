using System.Collections;

namespace ReWASD.UI;

/// <summary>
/// What the view models need from whichever UI toolkit hosts them (WPF on Windows, Avalonia on macOS):
/// running work on the UI thread, a UI-thread timer, and a grouped view of a list for toolkits that have one.
/// </summary>
public interface IPlatformUi
{
    /// <summary>Runs <paramref name="action"/> on the UI thread.</summary>
    void Post(Action action);

    /// <summary>Calls <paramref name="tick"/> on the UI thread every <paramref name="interval"/> until the result is disposed.</summary>
    IDisposable StartTimer(TimeSpan interval, Action tick);

    /// <summary>Closes the application (after an update has been staged).</summary>
    void RequestExit();

    /// <summary>Hides or shows the app's Dock icon (macOS only; elsewhere nothing happens). Returns whether it took effect.</summary>
    bool SetDockIconHidden(bool hidden);

    /// <summary>Asks for a profile file to open. Null when the person cancels.</summary>
    Task<string?> PickOpenFileAsync(string title);

    /// <summary>Asks where to save a profile file. Null when the person cancels.</summary>
    Task<string?> PickSaveFileAsync(string title, string suggestedName);

    /// <summary>Asks a yes/no question. False when the person says no or closes the question.</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>A collection view of <paramref name="source"/> grouped by a property, or null if the toolkit has no grouped views.</summary>
    IEnumerable? CreateGroupedView(IEnumerable source, string groupProperty);
}

/// <summary>Set by the application at startup so a view model created from XAML finds its toolkit.</summary>
public static class UiHost
{
    public static IPlatformUi? Current { get; set; }
}

/// <summary>
/// A toolkit-neutral host that marshals onto the <see cref="SynchronizationContext"/> that was current when it was
/// created. Used when no toolkit registered one, and by tests.
/// </summary>
public sealed class SynchronizationContextUi : IPlatformUi
{
    private readonly SynchronizationContext _context = SynchronizationContext.Current ?? new SynchronizationContext();

    public void Post(Action action) => _context.Post(_ => action(), null);

    public IDisposable StartTimer(TimeSpan interval, Action tick)
    {
        int pending = 0; // never queue a tick while the previous one is still waiting
        var timer = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref pending, 1) == 1) return;
            _context.Post(_ =>
            {
                try { tick(); }
                finally { Interlocked.Exchange(ref pending, 0); }
            }, null);
        }, null, interval, interval);
        return timer;
    }

    /// <summary>True once <see cref="RequestExit"/> was called (nothing else can close a headless host).</summary>
    public bool ExitRequested { get; private set; }

    public void RequestExit() => ExitRequested = true;

    public bool SetDockIconHidden(bool hidden) => false;
    public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveFileAsync(string title, string suggestedName) => Task.FromResult<string?>(null);
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);

    public IEnumerable? CreateGroupedView(IEnumerable source, string groupProperty) => null;
}
