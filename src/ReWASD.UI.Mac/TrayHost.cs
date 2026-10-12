using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac;

/// <summary>The menu bar (macOS) or notification area icon: show the window, start or stop remapping, lock, quit.</summary>
internal static class TrayHost
{
    public static void Attach(Application app, Window window, MainViewModel vm)
    {
        var show = new NativeMenuItem("Show ReWASD");
        show.Click += (_, _) => Show(window);

        var apply = new NativeMenuItem("Start or stop remapping");
        apply.Click += (_, _) => vm.ToggleApplyCommand.Execute(null);

        var lockItem = new NativeMenuItem("Lock or unlock the controller");
        lockItem.Click += (_, _) => vm.ToggleLockCommand.Execute(null);

        var overlay = new NativeMenuItem("Show or hide the overlay");
        overlay.Click += (_, _) => vm.ShowOverlay = !vm.ShowOverlay;

        var quit = new NativeMenuItem("Quit ReWASD");
        quit.Click += (_, _) => vm.RequestQuit();

        var menu = new NativeMenu();
        foreach (var item in new NativeMenuItemBase[] { show, new NativeMenuItemSeparator(), apply, lockItem, overlay, new NativeMenuItemSeparator(), quit })
            menu.Items.Add(item);

        using var stream = AssetLoader.Open(new Uri("avares://ReWASD/Assets/tray.png"));
        var icon = new TrayIcon { Icon = new WindowIcon(stream), ToolTipText = "ReWASD", Menu = menu };
        icon.Clicked += (_, _) => Show(window);
        TrayIcon.SetIcons(app, new TrayIcons { icon });
    }

    public static void Show(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
}
