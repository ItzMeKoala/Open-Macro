using CommunityToolkit.Mvvm.ComponentModel;
using ReWASD.Core.Models;

namespace ReWASD.UI.ViewModels;

/// <summary>The settings of one stick as the window shows them. Changes are written straight into the profile's <see cref="StickSettings"/>.</summary>
public partial class StickViewModel : ObservableObject
{
    public static IReadOnlyList<string> ModeNames { get; } = new[] { "Controller stick", "Mouse cursor", "Scroll wheel", "Keys (WASD / arrows)" };
    public static IReadOnlyList<string> CurveNames { get; } = new[] { "Linear", "Smooth (gentle start)", "Aggressive (fast start)", "Instant (on / off)" };

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

    [ObservableProperty]
    private double _deadzoneMin = 0.05;

    [ObservableProperty]
    private double _deadzoneMax = 0.95;

    [ObservableProperty]
    private int _curveIndex;

    [ObservableProperty]
    private double _sensitivity = 1.0;

    [ObservableProperty]
    private bool _invertX;

    [ObservableProperty]
    private bool _invertY;

    /// <summary>What the stick is doing right now, shown while Preferences is open.</summary>
    [ObservableProperty]
    private string _liveText = string.Empty;

    /// <summary>Progress of a calibration: what to do, then what was set.</summary>
    [ObservableProperty]
    private string _calibrationText = string.Empty;

    [ObservableProperty]
    private double _calibrationProgress;

    [ObservableProperty]
    private bool _isCalibrating;

    /// <summary>Set by the window's view model: starts measuring this stick.</summary>
    public Action<CalibrationKind>? CalibrationRequested { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CalibrateRest() => CalibrationRequested?.Invoke(CalibrationKind.Rest);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CalibrateRange() => CalibrationRequested?.Invoke(CalibrationKind.Range);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetTuning()
    {
        var fresh = new StickSettings();
        DeadzoneMin = fresh.DeadzoneMin;
        DeadzoneMax = fresh.DeadzoneMax;
        CurveIndex = (int)fresh.CurveType;
        Sensitivity = fresh.Sensitivity;
        InvertX = fresh.InvertX;
        InvertY = fresh.InvertY;
        CalibrationText = "Back to the defaults";
    }

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
            DeadzoneMin = settings.DeadzoneMin;
            DeadzoneMax = settings.DeadzoneMax;
            CurveIndex = Math.Clamp((int)settings.CurveType, 0, CurveNames.Count - 1);
            Sensitivity = settings.Sensitivity;
            InvertX = settings.InvertX;
            InvertY = settings.InvertY;
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

    partial void OnDeadzoneMinChanged(double value)
    {
        if (_loading) return;
        _settings.DeadzoneMin = value;
        if (DeadzoneMax < value + 0.05) DeadzoneMax = Math.Min(1.0, value + 0.05);   // the outer edge never sits inside the inner one
    }

    partial void OnDeadzoneMaxChanged(double value)
    {
        if (_loading) return;
        _settings.DeadzoneMax = value;
    }

    partial void OnCurveIndexChanged(int value)
    {
        if (!_loading) _settings.CurveType = value is >= 0 and < 4 ? (ResponseCurveType)value : ResponseCurveType.Linear;
    }

    partial void OnSensitivityChanged(double value)
    {
        if (!_loading) _settings.Sensitivity = value;
    }

    partial void OnInvertXChanged(bool value)
    {
        if (!_loading) _settings.InvertX = value;
    }

    partial void OnInvertYChanged(bool value)
    {
        if (!_loading) _settings.InvertY = value;
    }
}
