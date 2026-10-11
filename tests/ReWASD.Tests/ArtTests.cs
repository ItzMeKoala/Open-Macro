using ReWASD.Core.Models;
using ReWASD.Core.Skins;
using ReWASD.Core.Skins.Art;

namespace ReWASD.Tests;

public class ArtTests
{
    private const string Sample = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">
          <defs>
            <linearGradient id="lg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF"/><stop offset="1" stop-color="#000000" stop-opacity="0.5"/></linearGradient>
            <radialGradient id="rg" cx="0.3" cy="0.4" r="0.6"><stop offset="0" stop-color="#112233"/><stop offset="100%" stop-color="#445566"/></radialGradient>
            <filter id="blur"><feGaussianBlur stdDeviation="4"/></filter>
            <clipPath id="clip"><circle cx="10" cy="10" r="5"/></clipPath>
          </defs>
          <g id="part" data-button="A" data-anim="press" data-origin="50 40" data-press-scale="0.9" data-press-dy="2" data-name="Cross">
            <circle cx="50" cy="40" r="20" fill="url(#lg)" stroke="#FF0000" stroke-width="3" stroke-dasharray="2 4"/>
            <path d="M 0 0 L 10 0" class="hit" fill="transparent"/>
            <circle cx="50" cy="40" r="22" class="hit" fill="transparent"/>
            <path d="M 0 0 L 5 5" data-fx="glow" data-max="0.4" opacity="0" filter="url(#blur)"/>
          </g>
          <g clip-path="url(#clip)" transform="translate(5 6) scale(2)">
            <rect x="1" y="2" width="10" height="4" rx="1" fill="url(#rg)" fill-opacity="0.5"/>
            <polygon points="0,0 10,0 10,10" fill="none" stroke="#00F"/>
            <line x1="0" y1="0" x2="4" y2="4" stroke="#0F0"/>
            <ellipse cx="1" cy="1" rx="2" ry="1"/>
          </g>
          <g id="trig" data-button="LeftTrigger" data-anim="trigger" data-travel="12">
            <path class="hit" d="M 100 10 C 120 0 140 0 160 10 L 160 30 L 100 30 Z"/>
          </g>
        </svg>
        """;

    [Fact]
    public void Parser_ReadsPaintsShapesAndEffects()
    {
        var scene = SvgArtParser.Parse(Sample);

        Assert.Equal(200, scene.Width);
        Assert.Equal(100, scene.Height);
        Assert.Equal(0, scene.OriginX);

        var part = scene.Root.Children.OfType<ArtGroup>().First(g => g.Id == "part");
        var circle = Assert.IsType<ArtPath>(part.Children[0]);
        var gradient = Assert.IsType<ArtLinearGradient>(circle.Fill);
        Assert.Equal(2, gradient.Stops.Count);
        Assert.Equal(0.5, gradient.Stops[1].Opacity);
        Assert.False(gradient.UserSpace);
        Assert.Equal(3, circle.StrokeWidth);
        Assert.Equal(new[] { 2.0, 4.0 }, circle.DashArray);
        Assert.StartsWith("M 70 40 C", circle.D); // a circle becomes four cubic curves

        var glow = part.Children.OfType<ArtPath>().Single(p => p.IsGlow);
        Assert.Equal(4, glow.Blur);
        Assert.Equal(0, glow.Opacity);
        Assert.Equal("0.4", glow.Get("max"));
    }

    [Fact]
    public void Parser_ResolvesRadialGradientsClipsAndTransforms()
    {
        var scene = SvgArtParser.Parse(Sample);
        var group = scene.Root.Children.OfType<ArtGroup>().First(g => g.ClipPath != null);

        Assert.NotNull(group.ClipPath);
        Assert.Equal(ArtMatrix.Translate(5, 6) * ArtMatrix.Scale(2, 2), group.Transform);
        Assert.Equal((5 + 2 * 3, 6 + 2 * 4), group.Transform.Apply(3, 4));

        var rect = Assert.IsType<ArtPath>(group.Children[0]);
        var radial = Assert.IsType<ArtRadialGradient>(rect.Fill);
        Assert.Equal(0.3, radial.Cx);
        Assert.Equal(1.0, radial.Stops[1].Offset); // "100%"
        Assert.Equal(0.5, rect.FillOpacity);
        Assert.Contains("C", rect.D); // rounded corners

        var poly = Assert.IsType<ArtPath>(group.Children[1]);
        Assert.Null(poly.Fill);
        Assert.EndsWith("Z", poly.D);
        Assert.Equal("M 0 0 L 4 4", ((ArtPath)group.Children[2]).D);
    }

    [Fact]
    public void Parser_FindsPartsWithTheirMotionAndHitBox()
    {
        var scene = SvgArtParser.Parse(Sample);

        Assert.Equal(2, scene.Parts.Count);

        var cross = scene.PartFor(ControllerButton.A)!;
        Assert.Equal(ArtMotion.Press, cross.Motion);
        Assert.Equal("Cross", cross.Name);
        Assert.Equal((50.0, 40.0), cross.Origin);
        Assert.Equal(0.9, cross.PressScale);
        Assert.Equal(2, cross.PressDy);
        // The box wraps every hit shape: the big circle (28..72 x 18..62) and the tiny line at the corner
        Assert.Equal(0, cross.HitBounds.X, 3);
        Assert.Equal(72, cross.HitBounds.Right, 3);

        var trigger = scene.PartFor(ControllerButton.LeftTrigger)!;
        Assert.Equal(ArtMotion.Trigger, trigger.Motion);
        Assert.Equal(12, trigger.Travel);
        Assert.Equal(100, trigger.HitBounds.X, 3);
        Assert.Equal(30, trigger.HitBounds.Bottom, 3);
    }

    [Fact]
    public void Parser_ReadsTheViewBoxOrigin()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="30 90 960 640"></svg>""";

        var scene = SvgArtParser.Parse(svg);

        Assert.Equal((30.0, 90.0, 960.0, 640.0), (scene.OriginX, scene.OriginY, scene.Width, scene.Height));
    }

    [Fact]
    public void Parser_RejectsAPartWithNothingToClick()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><g data-button="A"><path d="M 0 0 L 1 1"/></g></svg>""";

        Assert.Throws<FormatException>(() => SvgArtParser.Parse(svg));
    }

    [Fact]
    public void Parser_RejectsAnUnknownButton()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><g data-button="Nope"><path class="hit" d="M 0 0 L 1 1"/></g></svg>""";

        Assert.Throws<FormatException>(() => SvgArtParser.Parse(svg));
    }

    [Theory]
    [InlineData("M 0 0 L 10 5 L 4 20 Z", 0, 0, 10, 20)]
    [InlineData("m 5 5 l 10 0 l 0 10 z", 5, 5, 10, 10)]
    [InlineData("M 0 0 H 10 V 8 H 0 Z", 0, 0, 10, 8)]
    [InlineData("M 0 0 C 0 10 10 10 10 0", 0, 0, 10, 7.5)] // the curve's own extreme, not its control points
    public void PathBounds_AreTheShapesBox(string d, double x, double y, double w, double h)
    {
        var r = PathBounds.Of(d);

        Assert.Equal(x, r.X, 1);
        Assert.Equal(y, r.Y, 1);
        Assert.Equal(w, r.Width, 1);
        Assert.Equal(h, r.Height, 1);
    }

    // ---- the shipped DualSense drawing ---------------------------------------------------------------------------------

    private static readonly ControllerButton[] DualSenseButtons =
    {
        ControllerButton.LeftTrigger, ControllerButton.RightTrigger, ControllerButton.LeftBumper, ControllerButton.RightBumper,
        ControllerButton.View, ControllerButton.Menu, ControllerButton.Guide,
        ControllerButton.DpadUp, ControllerButton.DpadDown, ControllerButton.DpadLeft, ControllerButton.DpadRight,
        ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y,
        ControllerButton.LeftStickClick, ControllerButton.RightStickClick
    };

    [Fact]
    public void DualSense_HasEveryButtonOnce_InsideTheDrawing()
    {
        var scene = ArtLibrary.DualSense;

        Assert.Equal(DualSenseButtons.Length, scene.Parts.Count);
        foreach (var button in DualSenseButtons)
        {
            var part = scene.PartFor(button);
            Assert.True(part != null, $"{button} is missing");
            Assert.InRange(part!.HitBounds.X, scene.OriginX, scene.OriginX + scene.Width);
            Assert.InRange(part.HitBounds.Right, scene.OriginX, scene.OriginX + scene.Width);
            Assert.InRange(part.HitBounds.Y, scene.OriginY, scene.OriginY + scene.Height);
            Assert.InRange(part.HitBounds.Bottom, scene.OriginY, scene.OriginY + scene.Height);
            Assert.True(part.HitBounds.Width > 15 && part.HitBounds.Height > 15, $"{button} is too small to click");
        }
        Assert.Equal(scene.Parts.Count, scene.Parts.Select(p => p.Button).Distinct().Count());
    }

    [Fact]
    public void DualSense_PlacesButtonsWhereTheRealOnesAre()
    {
        var s = ArtLibrary.DualSense;
        ArtRect B(ControllerButton b) => s.PartFor(b)!.HitBounds;

        // Face buttons: triangle on top, cross at the bottom, square left, circle right (Y, A, X, B)
        Assert.True(B(ControllerButton.Y).CenterY < B(ControllerButton.X).CenterY);
        Assert.True(B(ControllerButton.X).CenterY < B(ControllerButton.A).CenterY);
        Assert.True(B(ControllerButton.X).CenterX < B(ControllerButton.Y).CenterX);
        Assert.True(B(ControllerButton.Y).CenterX < B(ControllerButton.B).CenterX);
        Assert.Equal(B(ControllerButton.Y).CenterX, B(ControllerButton.A).CenterX, 1);
        Assert.Equal(B(ControllerButton.X).CenterY, B(ControllerButton.B).CenterY, 1);

        // D-pad on the left, face buttons on the right, sticks side by side under the touchpad, PS button between
        Assert.True(B(ControllerButton.DpadUp).CenterX < 400 && B(ControllerButton.Y).CenterX > 600);
        Assert.True(B(ControllerButton.DpadUp).CenterY < B(ControllerButton.DpadLeft).CenterY);
        Assert.True(B(ControllerButton.DpadLeft).CenterX < B(ControllerButton.DpadRight).CenterX);
        Assert.True(B(ControllerButton.LeftStickClick).CenterX < B(ControllerButton.Guide).CenterX);
        Assert.True(B(ControllerButton.Guide).CenterX < B(ControllerButton.RightStickClick).CenterX);
        Assert.True(B(ControllerButton.Guide).CenterY < B(ControllerButton.LeftStickClick).Bottom);

        // Triggers above the bumpers, create/options beside the touchpad
        Assert.True(B(ControllerButton.LeftTrigger).Y < B(ControllerButton.LeftBumper).Y);
        Assert.True(B(ControllerButton.View).CenterX < B(ControllerButton.Menu).CenterX);
    }

    [Fact]
    public void DualSense_LeftAndRightPartsMirrorEachOther()
    {
        var s = ArtLibrary.DualSense;
        const double axis = 513.5;

        foreach (var (left, right) in new[]
                 {
                     (ControllerButton.LeftTrigger, ControllerButton.RightTrigger), (ControllerButton.LeftBumper, ControllerButton.RightBumper),
                     (ControllerButton.View, ControllerButton.Menu), (ControllerButton.LeftStickClick, ControllerButton.RightStickClick)
                 })
        {
            var l = s.PartFor(left)!.HitBounds;
            var r = s.PartFor(right)!.HitBounds;
            Assert.Equal(2 * axis - l.CenterX, r.CenterX, 1.5);
            Assert.Equal(l.CenterY, r.CenterY, 1.5);
            Assert.Equal(l.Width, r.Width, 1.5);
        }

        Assert.Equal(axis, s.PartFor(ControllerButton.Guide)!.HitBounds.CenterX, 1.5);
    }

    [Fact]
    public void DualSense_HasNoStrayCurves()
    {
        // A curve whose control points fly far from its ends draws a thin spike across the picture (a fitting glitch once did)
        var paths = new List<ArtPath>();
        void Walk(ArtGroup g) { foreach (var c in g.Children) { if (c is ArtPath p) paths.Add(p); else if (c is ArtGroup sub) Walk(sub); } }
        Walk(ArtLibrary.DualSense.Root);
        Assert.True(paths.Count > 100);

        foreach (var p in paths)
        {
            var t = System.Text.RegularExpressions.Regex.Matches(p.D, @"[MLCZ]|-?\d+(?:\.\d+)?").Select(m => m.Value).ToList();
            (double X, double Y) cur = (0, 0);
            for (int i = 0; i < t.Count;)
            {
                double N(int k) => double.Parse(t[i + k], System.Globalization.CultureInfo.InvariantCulture);
                switch (t[i])
                {
                    case "M" or "L": cur = (N(1), N(2)); i += 3; break;
                    case "C":
                    {
                        var c1 = (X: N(1), Y: N(2)); var c2 = (X: N(3), Y: N(4)); var end = (X: N(5), Y: N(6));
                        double chord = Math.Sqrt(Math.Pow(end.X - cur.X, 2) + Math.Pow(end.Y - cur.Y, 2));
                        double reach = Math.Max(Math.Sqrt(Math.Pow(c1.X - cur.X, 2) + Math.Pow(c1.Y - cur.Y, 2)), Math.Sqrt(Math.Pow(c2.X - end.X, 2) + Math.Pow(c2.Y - end.Y, 2)));
                        Assert.True(reach <= chord * 1.6 + 4, $"a curve in {p.Id ?? p.D[..Math.Min(40, p.D.Length)]} reaches {reach:0} on a {chord:0} chord");
                        cur = end; i += 7; break;
                    }
                    default: i++; break;
                }
            }
        }
    }

    [Fact]
    public void DualSense_MotionsMatchTheHardware()
    {
        var s = ArtLibrary.DualSense;

        Assert.Equal(ArtMotion.Trigger, s.PartFor(ControllerButton.LeftTrigger)!.Motion);
        Assert.Equal(ArtMotion.Trigger, s.PartFor(ControllerButton.RightTrigger)!.Motion);
        Assert.Equal(ArtMotion.Stick, s.PartFor(ControllerButton.LeftStickClick)!.Motion);
        Assert.Equal(ArtMotion.Stick, s.PartFor(ControllerButton.RightStickClick)!.Motion);
        Assert.All(s.Parts.Where(p => p.Motion == ArtMotion.Stick || p.Motion == ArtMotion.Trigger), p => Assert.True(p.Travel > 5));

        // A stick's moving cap is its own group, so the well around it stays still
        foreach (var stick in s.Parts.Where(p => p.Motion == ArtMotion.Stick))
            Assert.Contains(stick.Group.Children.OfType<ArtGroup>(), g => g.Get("role") == "move");
    }

    [Fact]
    public void DualSense_EveryPartCanGlow()
    {
        foreach (var part in ArtLibrary.DualSense.Parts)
        {
            Assert.True(HasGlow(part.Group), $"{part.Name} has nothing that lights up");
        }

        static bool HasGlow(ArtGroup g) => g.Children.Any(c => c is ArtPath { IsGlow: true } || (c is ArtGroup sub && HasGlow(sub)));
    }

    [Fact]
    public void OnlyThePlayStation5Skin_HasTheDetailedDrawing()
    {
        Assert.NotNull(ControllerSkins.Get(SkinKind.PlayStation5).Art);
        Assert.Null(ControllerSkins.Get(SkinKind.PlayStation4).Art);
        Assert.Null(ControllerSkins.Get(SkinKind.Xbox).Art);
        Assert.Null(ControllerSkins.Get(SkinKind.SwitchPro).Art);
    }

    [Fact]
    public void ThePlainOutline_StillBacksUpThePlayStation5Skin()
    {
        // Front ends that cannot show the drawing (and any failure to read it) fall back to this
        var skin = ControllerSkins.Get(SkinKind.PlayStation5);

        Assert.True(skin.Decor.Count > 20);
        Assert.True(skin.Layout.ContainsKey(ControllerButton.A));
    }
}
