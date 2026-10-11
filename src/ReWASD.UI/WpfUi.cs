using System.Collections;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace ReWASD.UI;

/// <summary>Hosts the shared view models in WPF: UI-thread work, a render-priority timer and grouped collection views.</summary>
internal sealed class WpfUi : IPlatformUi
{
    public void Post(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);

    public IDisposable StartTimer(TimeSpan interval, Action tick)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = interval };
        timer.Tick += (_, _) => tick();
        timer.Start();
        return new TimerStopper(timer);
    }

    public IEnumerable? CreateGroupedView(IEnumerable source, string groupProperty)
    {
        var view = new ListCollectionView((IList)source);
        view.GroupDescriptions.Add(new PropertyGroupDescription(groupProperty));
        return view;
    }

    private sealed class TimerStopper : IDisposable
    {
        private readonly DispatcherTimer _timer;
        public TimerStopper(DispatcherTimer timer) => _timer = timer;
        public void Dispose() => _timer.Stop();
    }
}
