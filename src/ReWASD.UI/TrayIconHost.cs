using System.Windows;
using ReWASD.UI.ViewModels;
using Forms = System.Windows.Forms;

namespace ReWASD.UI;

/// <summary>The notification-area icon: show the window, start or stop remapping, lock, quit.</summary>
internal sealed class TrayIconHost : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Window _window;

    public TrayIconHost(Window window, MainViewModel vm)
    {
        _window = window;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show Open-Macro", null, (_, _) => Show());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Start or stop remapping", null, (_, _) => Run(() => vm.ToggleApplyCommand.Execute(null)));
        menu.Items.Add("Lock or unlock the controller", null, (_, _) => Run(() => vm.ToggleLockCommand.Execute(null)));
        menu.Items.Add("Show or hide the overlay", null, (_, _) => Run(() => vm.ShowOverlay = !vm.ShowOverlay));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit Open-Macro", null, (_, _) => Run(vm.RequestQuit));

        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ReWASD.ico"))?.Stream;
        _icon = new Forms.NotifyIcon
        {
            Text = "Open-Macro",
            ContextMenuStrip = menu,
            Icon = stream != null ? new System.Drawing.Icon(stream) : System.Drawing.SystemIcons.Application,
            Visible = true
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) Show();
        };
    }

    // The menu raises its events on the UI thread, but go through the dispatcher so a handler can never run mid-layout
    private void Run(Action action) => _window.Dispatcher.BeginInvoke(action);

    public void Show()
    {
        _window.Dispatcher.BeginInvoke(() =>
        {
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
