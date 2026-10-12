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

    // Avalonia has no grouped collection views; the view model also offers RemapGroups, which the menu uses
    public IEnumerable? CreateGroupedView(IEnumerable source, string groupProperty) => null;

    private sealed class TimerStopper : IDisposable
    {
        private readonly DispatcherTimer _timer;
        public TimerStopper(DispatcherTimer timer) => _timer = timer;
        public void Dispose() => _timer.Stop();
    }
}
