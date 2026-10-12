using CommunityToolkit.Mvvm.ComponentModel;
using ReWASD.Core.Models;

namespace ReWASD.UI.ViewModels;

/// <summary>The settings of one stick as the window shows them. Changes are written straight into the profile's <see cref="StickSettings"/>.</summary>
public partial class StickViewModel : ObservableObject
{
    public static IReadOnlyList<string> ModeNames { get; } = new[] { "Controller stick", "Mouse cursor", "Scroll wheel", "Keys (WASD / arrows)" };
    public static IReadOnlyList<string> KeyPresetNames { get; } = new[] { "W A S D", "Arrow keys" };

    /// <summary>Pad buttons that can slow the stick down, in the order the list shows them; None first.</summary>
    public static IReadOnlyList<ControllerButton> PrecisionButtons { get; } = new[]
    {
        ControllerButton.None, ControllerButton.LeftTrigger, ControllerButton.RightTrigger,
        ControllerButton.LeftBumper, ControllerButton.RightBumper,
        ControllerButton.LeftStickClick, ControllerButton.RightStickClick
    };

    public static IReadOnlyList<string> PrecisionNames { get; } =
        PrecisionButtons.Select(b => b == ControllerButton.None ? "Off" : b.ToString()).ToList();

    private StickSettings _settings = new();
    private bool _loading;

    public string Title { get; }

    public StickViewModel(string title)
    {
        Title = title;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMouse), nameof(IsScroll), nameof(IsKeys), nameof(UsesSpeed), nameof(CanBePrecise))]
    private int _modeIndex;

    [ObservableProperty]
    private double _mouseSpeed = 1200;

    [ObservableProperty]
    private double _scrollSpeed = 14;

    [ObservableProperty]
    private int _keyPresetIndex;

    [ObservableProperty]
    private double _keyThreshold = 0.5;

    [ObservableProperty]
    private int _precisionIndex;

    [ObservableProperty]
    private double _precisionFactor = 0.3;

    public bool IsMouse => ModeIndex == (int)StickMode.Mouse;
    public bool IsScroll => ModeIndex == (int)StickMode.Scroll;
    public bool IsKeys => ModeIndex == (int)StickMode.Keys;
    public bool UsesSpeed => IsMouse || IsScroll;
    public bool CanBePrecise => IsMouse || IsScroll;

    /// <summary>Shows a profile's settings; the view model then edits those very settings.</summary>
    public void Load(StickSettings settings)
    {
        _loading = true;
        try
        {
            _settings = settings;
            ModeIndex = (int)settings.Mode;
            MouseSpeed = settings.MouseSpeed;
            ScrollSpeed = settings.ScrollSpeed;
            KeyPresetIndex = settings.KeyUp.Equals("Up", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            KeyThreshold = settings.KeyThreshold;
            PrecisionIndex = Math.Max(0, PrecisionButtons.ToList().IndexOf(settings.PrecisionButton));
            PrecisionFactor = settings.PrecisionFactor;
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnModeIndexChanged(int value)
    {
        if (!_loading) _settings.Mode = Enum.IsDefined(typeof(StickMode), value) ? (StickMode)value : StickMode.Gamepad;
    }

    partial void OnMouseSpeedChanged(double value)
    {
        if (!_loading) _settings.MouseSpeed = value;
    }

    partial void OnScrollSpeedChanged(double value)
    {
        if (!_loading) _settings.ScrollSpeed = value;
    }

    partial void OnKeyPresetIndexChanged(int value)
    {
        if (!_loading) _settings.UseKeyPreset(arrows: value == 1);
    }

    partial void OnKeyThresholdChanged(double value)
    {
        if (!_loading) _settings.KeyThreshold = value;
    }

    partial void OnPrecisionIndexChanged(int value)
    {
        if (!_loading && value >= 0 && value < PrecisionButtons.Count) _settings.PrecisionButton = PrecisionButtons[value];
    }

    partial void OnPrecisionFactorChanged(double value)
    {
        if (!_loading) _settings.PrecisionFactor = value;
    }
}
