using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ReWASD.Core.Models;
using ReWASD.Core.Skins.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Mac.Art;

/// <summary>
/// Shows a detailed controller drawing that reacts to the real controller: buttons sink, triggers pull, sticks lean, and
/// the part being edited glows. Clicking a part selects it. All motion comes from the same inputs the rest of the app
/// reads, smoothed by springs so it looks like hardware rather than a flicker.
/// </summary>
public sealed class ArtView : Decorator
{
    public static readonly StyledProperty<ArtScene?> SceneProperty = AvaloniaProperty.Register<ArtView, ArtScene?>(nameof(Scene));
    public static readonly StyledProperty<IEnumerable<ButtonViewModel>?> SourcesProperty = AvaloniaProperty.Register<ArtView, IEnumerable<ButtonViewModel>?>(nameof(Sources));

    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E9C46A"));
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#171A21"));

    /// <summary>One animated part and everything attached to it.</summary>
    private sealed class PartVisual
    {
        public required ArtPart Part { get; init; }
        public required ArtPartAnimator Animator { get; init; }
        public required Canvas Group { get; init; }
        public required Canvas Mover { get; init; }               // what the pose is applied to (the cap for sticks, else the whole group)
        public required MatrixTransform MoverTransform { get; init; }
        public List<Control> Glows { get; } = new();
        public List<Control> Halo { get; } = new();              // soft + crisp outlines of the hit shapes
        public required Control Marker { get; init; }             // the "this input is remapped" dot
        public ButtonViewModel? Source { get; set; }
        public PropertyChangedEventHandler? Handler { get; set; }
    }

    private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform };
    private readonly List<PartVisual> _parts = new();
    private PartVisual? _hover;
    private bool _frameRequested;
    private TimeSpan? _lastFrame;

    public ArtView()
    {
        Child = _viewbox;
        Cursor = Cursor.Default;
    }

    /// <summary>
    /// Whether the part being edited glows and remapped inputs carry a dot. On in the editor, off in the overlay, which only
    /// shows what the controller is doing.
    /// </summary>
    public bool ShowEditing { get; set; } = true;

    public ArtScene? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public IEnumerable<ButtonViewModel>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty || change.Property == SourcesProperty) Rebuild();
    }

    // ---- building -------------------------------------------------------------------------------------------------

    private void Rebuild()
    {
        foreach (var p in _parts) Detach(p);
        _parts.Clear();
        _hover = null;
        _viewbox.Child = null;

        var scene = Scene;
        if (scene == null) return;

        var builder = new ArtBuilder(scene);
        var root = builder.BuildRoot();
        _viewbox.Child = root;

        var sources = Sources?.ToList() ?? new List<ButtonViewModel>();

        foreach (var part in scene.Parts)
        {
            var group = builder.Groups[part.Group];
            var mover = part.Motion == ArtMotion.Stick
                ? part.Group.Children.OfType<ArtGroup>().Where(g => g.Get("role") == "move").Select(g => builder.Groups[g]).FirstOrDefault() ?? group
                : group;

            var transform = new MatrixTransform();
            mover.RenderTransformOrigin = RelativePoint.TopLeft;
            mover.RenderTransform = transform;

            var visual = new PartVisual
            {
                Part = part,
                Animator = new ArtPartAnimator(part),
                Group = group,
                Mover = mover,
                MoverTransform = transform,
                Marker = MakeMarker(part)
            };

            group.Tag = visual;
            AttachDecorations(visual, builder);
            group.Children.Add(visual.Marker);

            visual.Source = part.Key != null
                ? sources.FirstOrDefault(s => s.Kind == SourceKind.Key && string.Equals(s.SourceName, part.Key, StringComparison.OrdinalIgnoreCase))
                : sources.FirstOrDefault(s => s.Kind == SourceKind.Pad && s.Button == part.Button);
            Attach(visual);
            _parts.Add(visual);
        }

        // Start where the inputs already are; only changes after this animate
        foreach (var p in _parts)
        {
            p.Animator.Snap();
            Render(p);
        }
    }

    /// <summary>Finds the glows and hit shapes of a part, and adds the selection halo drawn from the hit shapes.</summary>
    private void AttachDecorations(PartVisual v, ArtBuilder builder)
    {
        void Walk(ArtGroup g)
        {
            foreach (var child in g.Children)
            {
                if (child is ArtPath p)
                {
                    var control = builder.Paths[p];
                    if (p.IsGlow) { control.IsVisible = false; v.Glows.Add(control); }
                    if (p.IsHit)
                    {
                        control.Cursor = new Cursor(StandardCursorType.Hand);
                        ToolTip.SetTip(control, v.Part.Name);
                    }
                }
                else if (child is ArtGroup sub) Walk(sub);
            }
        }
        Walk(v.Part.Group);

        // The halo hugs the real shapes, so a round button gets a ring and a trigger gets its own outline
        foreach (var hit in HitPaths(v.Part.Group))
        {
            foreach (var (width, blur, opacity) in new[] { (9.0, 5.0, 0.85), (2.6, 0.0, 1.0) })
            {
                var halo = new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse(hit.D),
                    Stroke = Gold,
                    StrokeThickness = width,
                    StrokeJoin = PenLineJoin.Round,
                    IsHitTestVisible = false,
                    Opacity = 0,
                    IsVisible = false,
                    Tag = opacity
                };
                if (blur > 0) halo.Effect = new BlurEffect { Radius = blur };
                if (!hit.Transform.IsIdentity)
                {
                    halo.RenderTransformOrigin = RelativePoint.TopLeft;
                    halo.RenderTransform = new MatrixTransform(ArtBuilder.ToMatrix(hit.Transform));
                }
                v.Group.Children.Add(halo);
                v.Halo.Add(halo);
            }
        }
    }

    private static IEnumerable<ArtPath> HitPaths(ArtGroup g)
    {
        foreach (var child in g.Children)
        {
            if (child is ArtPath p && p.IsHit) yield return p;
            else if (child is ArtGroup sub) foreach (var h in HitPaths(sub)) yield return h;
        }
    }

    private static Control MakeMarker(ArtPart part)
    {
        var b = part.HitBounds;
        double size = Math.Clamp(Math.Min(b.Width, b.Height) * 0.2, 9, 15);
        var dot = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = Gold,
            Stroke = Ink,
            StrokeThickness = 2,
            IsHitTestVisible = false,
            IsVisible = false
        };
        Canvas.SetLeft(dot, b.Right - size * 1.15);
        Canvas.SetTop(dot, b.Y + size * 0.15);
        return dot;
    }

    // ---- wiring to the view models --------------------------------------------------------------------------------

    private void Attach(PartVisual v)
    {
        if (v.Source == null) return;

        v.Handler = (_, e) =>
        {
            if (e.PropertyName is nameof(ButtonViewModel.IsPressed) or nameof(ButtonViewModel.AnalogValue)
                or nameof(ButtonViewModel.StickOffsetX) or nameof(ButtonViewModel.StickOffsetY)
                or nameof(ButtonViewModel.IsSelected) or nameof(ButtonViewModel.IsRemapped) or null)
            {
                Pull(v);
                RequestFrame();
            }
        };
        v.Source.PropertyChanged += v.Handler;
        Pull(v);
    }

    private static void Detach(PartVisual v)
    {
        if (v.Source != null && v.Handler != null) v.Source.PropertyChanged -= v.Handler;
    }

    /// <summary>Copies the current input state into the animator's targets.</summary>
    private void Pull(PartVisual v)
    {
        var s = v.Source;
        if (s == null) return;

        v.Animator.SetPressed(s.IsPressed);
        v.Animator.SetAnalog(s.AnalogValue);
        // The view model reports the stick as a pixel offset (up to 12); the drawing wants -1..1
        v.Animator.SetStick(s.StickOffsetX / StickReach, s.StickOffsetY / StickReach);
        v.Animator.SetSelected(ShowEditing && s.IsSelected);
        v.Marker.IsVisible = ShowEditing && s.IsRemapped;
    }

    private const double StickReach = 12.0;

    // ---- animation --------------------------------------------------------------------------------------------------

    private void RequestFrame()
    {
        if (_frameRequested) return;
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        _frameRequested = false;
        double dt = _lastFrame is { } last ? Math.Clamp((now - last).TotalSeconds, 0, 0.1) : 1.0 / 60;
        _lastFrame = now;

        Advance(dt);

        if (_parts.Any(p => !p.Animator.IsSettled)) RequestFrame();
        else _lastFrame = null;
    }

    /// <summary>Moves every part forward in time and redraws it. The frame loop calls this; tests call it to step time themselves.</summary>
    public void Advance(double seconds)
    {
        foreach (var p in _parts)
        {
            p.Animator.Step(seconds);
            Render(p);
        }
    }

    /// <summary>Jumps every part to where its input says it should be, without easing.</summary>
    public void Settle()
    {
        foreach (var p in _parts)
        {
            Pull(p);
            p.Animator.Snap();
            Render(p);
        }
    }

    private void Render(PartVisual p)
    {
        var pose = p.Animator.Pose;
        p.MoverTransform.Matrix = ArtBuilder.ToMatrix(pose.Matrix);

        foreach (var g in p.Glows)
        {
            g.Opacity = pose.Glow * (g.Tag is double max ? max : 1.0);
            g.IsVisible = g.Opacity > 0.02;
        }

        double halo = Math.Max(p.Animator.Selection, p.Animator.Hover * 0.55);
        foreach (var h in p.Halo)
        {
            h.Opacity = halo * (double)h.Tag!;
            h.IsVisible = h.Opacity > 0.02;
        }
    }

    /// <summary>What a part looks like right now, for tests.</summary>
    internal readonly record struct PartSnapshot(Matrix Transform, double Glow, double Halo, bool MarkerVisible);

    internal PartSnapshot? Inspect(ControllerButton button) => Inspect(_parts.FirstOrDefault(x => x.Part.Key == null && x.Part.Button == button));

    internal PartSnapshot? Inspect(string key) =>
        Inspect(_parts.FirstOrDefault(x => string.Equals(x.Part.Key, key, StringComparison.OrdinalIgnoreCase)));

    private PartSnapshot? Inspect(PartVisual? p)
    {
        if (p == null) return null;
        return new PartSnapshot(
            p.MoverTransform.Matrix,
            p.Glows.Where(g => g.IsVisible).Select(g => g.Opacity).DefaultIfEmpty(0).Max(),
            p.Halo.Where(h => h.IsVisible).Select(h => h.Opacity).DefaultIfEmpty(0).Max(),
            p.Marker.IsVisible);
    }

    // ---- pointer -----------------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var part = PartAt(e.GetPosition(this));
        if (part?.Source == null) return;

        part.Source.SelectCommand.Execute(null);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetHover(PartAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    private void SetHover(PartVisual? part)
    {
        if (ReferenceEquals(part, _hover)) return;
        _hover?.Animator.SetHover(false);
        _hover = part;
        part?.Animator.SetHover(true);
        RequestFrame();
    }

    /// <summary>The part under a point (in this control's coordinates), or null for the body and empty space.</summary>
    private PartVisual? PartAt(Point point)
    {
        if (this.InputHitTest(point) is not Visual hit) return null;

        for (Visual? v = hit; v != null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (v is Control { Tag: PartVisual pv }) return pv;
        }
        return null;
    }
}
