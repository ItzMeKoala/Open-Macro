using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering;

namespace ReWASD.UI.Mac.Views;

/// <summary>A shaped source only accepts clicks inside its painted region.</summary>
public sealed class SourceButton : Button, ICustomHitTest
{
    protected override Type StyleKeyOverride => typeof(Button);

    public static readonly StyledProperty<Geometry?> ShapeGeometryProperty =
        AvaloniaProperty.Register<SourceButton, Geometry?>(nameof(ShapeGeometry));

    public Geometry? ShapeGeometry
    {
        get => GetValue(ShapeGeometryProperty);
        set => SetValue(ShapeGeometryProperty, value);
    }

    public bool HitTest(Point point)
    {
        // The composition renderer supplies coordinates local to this visual.
        return ShapeGeometry == null
            ? new Rect(Bounds.Size).Contains(point)
            : ShapeGeometry.FillContains(point) && (Clip == null || Clip.FillContains(point));
    }
}
