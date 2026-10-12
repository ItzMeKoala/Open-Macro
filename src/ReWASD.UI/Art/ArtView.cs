using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ReWASD.Core.Models;
using ReWASD.Core.Skins.Art;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Art;

/// <summary>
/// Shows a detailed controller drawing that reacts to the real controller: buttons sink, triggers pull, sticks lean, and
/// the part being edited glows. Clicking a part selects it. All motion comes from the same inputs the rest of the app
/// reads, smoothed by springs. If anything goes wrong while building or animating it, <see cref="RenderFailed"/> is raised
/// and the app falls back to the plain outline drawing instead of breaking.
/// </summary>
public sealed class ArtView : Decorator
{
    public static readonly DependencyProperty SceneProperty =
        DependencyProperty.Register(nameof(Scene), typeof(ArtScene), typeof(ArtView), new PropertyMetadata(null, OnInputsChanged));

    public static readonly DependencyProperty SourcesProperty =
        DependencyProperty.Register(nameof(Sources), typeof(IEnumerable<ButtonViewModel>), typeof(ArtView), new PropertyMetadata(null, OnInputsChanged));

    private static readonly Brush Gold = Freeze(new SolidColorBrush(Color.FromRgb(0xE9, 0xC4, 0x6A)));
    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(0x17, 0x1A, 0x21)));

    private const double StickReach = 12.0; // the view model reports a stick as a pixel offset of up to this much

    private sealed class PartVisual
    {
        public required ArtPart Part { get; init; }
        public required ArtPartAnimator Animator { get; init; }
        public required Canvas Group { get; init; }
        public required Canvas Mover { get; init; }               // what the pose is applied to (the cap for sticks, else the whole group)
        public required MatrixTransform MoverTransform { get; init; }
        public List<UIElement> Glows { get; } = new();
        public List<Path> Halo { get; } = new();                  // soft + crisp outlines of the hit shapes
        public required Ellipse Marker { get; init; }             // the "this input is remapped" dot
        public ButtonViewModel? Source { get; set; }
        public PropertyChangedEventHandler? Handler { get; set; }
    }

    private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform };
    private readonly List<PartVisual> _parts = new();
    private PartVisual? _hover;
    private bool _rendering;
    private TimeSpan? _lastFrame;

    public ArtView()
    {
        Child = _viewbox;
        Unloaded += (_, _) => StopFrames();
    }

    /// <summary>
    /// Whether the part being edited glows and remapped inputs carry a dot. On in the editor, off in the overlay, which only
    /// shows what the controller is doing.
    /// </summary>
    public bool ShowEditing { get; set; } = true;

    public ArtScene? Scene { get => (ArtScene?)GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public IEnumerable<ButtonViewModel>? Sources { get => (IEnumerable<ButtonViewModel>?)GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <summary>Raised if the drawing could not be built or animated; the owner should show the plain outline instead.</summary>
    public event EventHandler? RenderFailed;

    public bool HasFailed { get; private set; }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static void OnInputsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ArtView)d).Rebuild();

    // ---- building -------------------------------------------------------------------------------------------------

    private void Fail()
    {
        if (HasFailed) return;
        HasFailed = true;
        StopFrames();
        RenderFailed?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        try
        {
            BuildNow();
        }
        catch (Exception)
        {
            Fail();
        }
    }

    private void BuildNow()
    {
        foreach (var p in _parts) Detach(p);
        _parts.Clear();
        _hover = null;
        _viewbox.Child = null;

        var scene = Scene;
        if (scene == null) return;

        var builder = new ArtBuilder(scene);
        var root = builder.BuildRoot();
        var sources = Sources?.ToList() ?? new List<ButtonViewModel>();
        var built = new List<PartVisual>();

        foreach (var part in scene.Parts)
        {
            var group = builder.Groups[part.Group];
            var mover = part.Motion == ArtMotion.Stick
                ? part.Group.Children.OfType<ArtGroup>().Where(g => g.Get("role") == "move").Select(g => builder.Groups[g]).FirstOrDefault() ?? group
                : group;

            var transform = new MatrixTransform();
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

            visual.Source = sources.FirstOrDefault(s => s.Kind == SourceKind.Pad && s.Button == part.Button);
            built.Add(visual);
        }

        _parts.AddRange(built);
        foreach (var v in _parts) Attach(v);

        // Start where the inputs already are; only changes after this animate
        foreach (var v in _parts)
        {
            v.Animator.Snap();
            Render(v);
        }

        _viewbox.Child = root;
    }

    private void AttachDecorations(PartVisual v, ArtBuilder builder)
    {
        void Walk(ArtGroup g)
        {
            foreach (var child in g.Children)
            {
                if (child is ArtPath p)
                {
                    var control = builder.Paths[p];
                    if (p.IsGlow) { control.Visibility = Visibility.Collapsed; v.Glows.Add(control); }
                    if (p.IsHit)
                    {
                        control.Cursor = Cursors.Hand;
                        control.ToolTip = v.Part.Name;
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
                var geometry = Geometry.Parse(hit.D);
                geometry.Freeze();
                var halo = new Path
                {
                    Data = geometry,
                    Stroke = Gold,
                    StrokeThickness = width * HaloScale(v.Part),
                    StrokeLineJoin = PenLineJoin.Round,
                    IsHitTestVisible = false,
                    Opacity = 0,
                    Visibility = Visibility.Collapsed,
                    Tag = opacity
                };
                if (blur > 0) halo.Effect = new BlurEffect { Radius = blur * HaloScale(v.Part) * 2.2, KernelType = KernelType.Gaussian };
                if (!hit.Transform.IsIdentity) halo.RenderTransform = new MatrixTransform(ArtBuilder.ToMatrix(hit.Transform));
                v.Group.Children.Add(halo);
                v.Halo.Add(halo);
            }
        }
    }

    // The drawings differ in size (a thin outline suits one drawing and is lost on another), so scale the halo to the part
    private static double HaloScale(ArtPart part) => Math.Clamp(Math.Min(part.HitBounds.Width, part.HitBounds.Height) / 60.0, 0.7, 2.2);

    private static IEnumerable<ArtPath> HitPaths(ArtGroup g)
    {
        foreach (var child in g.Children)
        {
            if (child is ArtPath p && p.IsHit) yield return p;
            else if (child is ArtGroup sub) foreach (var h in HitPaths(sub)) yield return h;
        }
    }

    private static Ellipse MakeMarker(ArtPart part)
    {
        var b = part.HitBounds;
        double size = Math.Clamp(Math.Min(b.Width, b.Height) * 0.2, 9, 30);
        var dot = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = Gold,
            Stroke = Ink,
            StrokeThickness = Math.Max(2, size / 7),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
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
            try
            {
                if (e.PropertyName is nameof(ButtonViewModel.IsPressed) or nameof(ButtonViewModel.AnalogValue)
                    or nameof(ButtonViewModel.StickOffsetX) or nameof(ButtonViewModel.StickOffsetY)
                    or nameof(ButtonViewModel.IsSelected) or nameof(ButtonViewModel.IsRemapped) or null)
                {
                    Pull(v);
                    StartFrames();
                }
            }
            catch (Exception)
            {
                Fail();
            }
        };
        v.Source.PropertyChanged += v.Handler;
        Pull(v);
    }

    private static void Detach(PartVisual v)
    {
        if (v.Source != null && v.Handler != null) v.Source.PropertyChanged -= v.Handler;
    }

    private void Pull(PartVisual v)
    {
        var s = v.Source;
        if (s == null) return;

        v.Animator.SetPressed(s.IsPressed);
        v.Animator.SetAnalog(s.AnalogValue);
        v.Animator.SetStick(s.StickOffsetX / StickReach, s.StickOffsetY / StickReach);
        v.Animator.SetSelected(ShowEditing && s.IsSelected);
        v.Marker.Visibility = ShowEditing && s.IsRemapped ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- animation --------------------------------------------------------------------------------------------------

    private void StartFrames()
    {
        if (_rendering || HasFailed) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopFrames()
    {
        if (!_rendering) return;
        _rendering = false;
        _lastFrame = null;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        try
        {
            var now = e is RenderingEventArgs r ? r.RenderingTime : TimeSpan.Zero;
            double dt = _lastFrame is { } last ? Math.Clamp((now - last).TotalSeconds, 0, 0.1) : 1.0 / 60;
            if (now == _lastFrame) return; // WPF can raise the event twice for one frame
            _lastFrame = now;

            Advance(dt);
            if (_parts.All(p => p.Animator.IsSettled)) StopFrames();
        }
        catch (Exception)
        {
            Fail();
        }
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

    private static void Render(PartVisual p)
    {
        var pose = p.Animator.Pose;
        p.MoverTransform.Matrix = ArtBuilder.ToMatrix(pose.Matrix);

        foreach (var g in p.Glows)
        {
            g.Opacity = pose.Glow * (g is FrameworkElement { Tag: double max } ? max : 1.0);
            g.Visibility = g.Opacity > 0.02 ? Visibility.Visible : Visibility.Collapsed;
        }

        double halo = Math.Max(p.Animator.Selection, p.Animator.Hover * 0.55);
        foreach (var h in p.Halo)
        {
            h.Opacity = halo * (double)h.Tag!;
            h.Visibility = h.Opacity > 0.02 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>What a part looks like right now, for tests.</summary>
    internal readonly record struct PartSnapshot(Matrix Transform, double Glow, double Halo, bool MarkerVisible);

    internal PartSnapshot? Inspect(ControllerButton button)
    {
        var p = _parts.FirstOrDefault(x => x.Part.Button == button);
        if (p == null) return null;
        return new PartSnapshot(
            p.MoverTransform.Matrix,
            p.Glows.Where(g => g.Visibility == Visibility.Visible).Select(g => g.Opacity).DefaultIfEmpty(0).Max(),
            p.Halo.Where(h => h.Visibility == Visibility.Visible).Select(h => h.Opacity).DefaultIfEmpty(0).Max(),
            p.Marker.Visibility == Visibility.Visible);
    }

    // ---- pointer -----------------------------------------------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        try
        {
            var part = PartAt(e.GetPosition(this));
            if (part?.Source == null) return;

            part.Source.SelectCommand.Execute(null);
            e.Handled = true;
        }
        catch (Exception)
        {
            Fail();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        try
        {
            SetHover(PartAt(e.GetPosition(this)));
        }
        catch (Exception)
        {
            Fail();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(null);
    }

    private void SetHover(PartVisual? part)
    {
        if (ReferenceEquals(part, _hover)) return;
        _hover?.Animator.SetHover(false);
        _hover = part;
        part?.Animator.SetHover(true);
        StartFrames();
    }

    /// <summary>The ButtonViewModel-backed part under a point (in this control's coordinates), or null for the body and empty space.</summary>
    internal ControllerButton? ButtonAt(Point point) => PartAt(point)?.Part.Button;

    private PartVisual? PartAt(Point point)
    {
        PartVisual? found = null;
        VisualTreeHelper.HitTest(this,
            // Only skip elements that opted out (glows and shadows); the Viewbox's own internal visuals are not UIElements and must be searched
            target => target is UIElement { IsHitTestVisible: false } ? HitTestFilterBehavior.ContinueSkipSelfAndChildren : HitTestFilterBehavior.Continue,
            result =>
            {
                for (DependencyObject? v = result.VisualHit; v != null && !ReferenceEquals(v, this); v = VisualTreeHelper.GetParent(v))
                {
                    if (v is FrameworkElement { Tag: PartVisual pv }) { found = pv; return HitTestResultBehavior.Stop; }
                }
                return HitTestResultBehavior.Stop; // the topmost thing hit is not part of a button (the body), so stop there
            },
            new PointHitTestParameters(point));
        return found;
    }
}
