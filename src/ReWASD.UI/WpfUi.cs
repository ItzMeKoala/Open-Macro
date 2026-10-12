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

    public void RequestExit() => Application.Current?.Dispatcher.BeginInvoke(() => Application.Current?.Shutdown());

    private const string Filter = "ReWASD profile (*.json)|*.json|All files (*.*)|*.*";

    public Task<string?> PickOpenFileAsync(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = Filter, CheckFileExists = true };
        return Task.FromResult(dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FileName : null);
    }

    public Task<string?> PickSaveFileAsync(string title, string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = title, Filter = Filter, FileName = suggestedName, DefaultExt = ".json", AddExtension = true };
        return Task.FromResult(dialog.ShowDialog(Application.Current?.MainWindow) == true ? dialog.FileName : null);
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        var answer = owner != null
            ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return Task.FromResult(answer == MessageBoxResult.Yes);
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
