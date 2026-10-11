using ReWASD.Core.Models;
using ReWASD.Core.Skins;

namespace ReWASD.Tests;

public class SkinTests
{
    [Fact]
    public void Mirror_ReflectsEveryXAndKeepsEveryY()
    {
        Assert.Equal("M 524 90 C 528 62 510 44 484 44", ControllerSkins.Mirror("M 176 90 C 172 62 190 44 216 44"));
    }

    [Fact]
    public void Mirror_HandlesCommaSeparatedPairs_AndIsItsOwnInverse()
    {
        const string path = "M 250,120 L 100,80 C 10,20 30,40 70,90 Z";

        Assert.Equal("M 450 120 L 600 80 C 690 20 670 40 630 90 Z", ControllerSkins.Mirror(path));
        Assert.Equal(Normalise(path), Normalise(ControllerSkins.Mirror(ControllerSkins.Mirror(path))));
    }

    private static string Normalise(string d) => string.Join(' ', d.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    [Theory]
    [InlineData(SkinKind.Xbox)]
    [InlineData(SkinKind.PlayStation4)]
    [InlineData(SkinKind.PlayStation5)]
    [InlineData(SkinKind.SwitchPro)]
    public void EverySkin_PlacesEveryButton_InsideTheDrawing(SkinKind kind)
    {
        var skin = ControllerSkins.Get(kind);

        foreach (var button in Enum.GetValues<ControllerButton>().Where(b => b != ControllerButton.None
                     && b is not (ControllerButton.LeftStickUp or ControllerButton.LeftStickDown or ControllerButton.LeftStickLeft or ControllerButton.LeftStickRight
                                  or ControllerButton.RightStickUp or ControllerButton.RightStickDown or ControllerButton.RightStickLeft or ControllerButton.RightStickRight
                                  or ControllerButton.Paddle1 or ControllerButton.Paddle2 or ControllerButton.Paddle3 or ControllerButton.Paddle4)))
        {
            Assert.True(skin.Layout.TryGetValue(button, out var p), $"{kind} has no place for {button}");
            Assert.InRange(p.Cx - p.W / 2, 0, ControllerSkins.CanvasWidth);
            Assert.InRange(p.Cx + p.W / 2, 0, ControllerSkins.CanvasWidth);
            Assert.InRange(p.Cy - p.H / 2, 0, ControllerSkins.CanvasHeight);
            Assert.InRange(p.Cy + p.H / 2, 0, ControllerSkins.CanvasHeight);
        }
    }

    [Theory]
    [InlineData(SkinKind.Xbox)]
    [InlineData(SkinKind.PlayStation4)]
    [InlineData(SkinKind.PlayStation5)]
    [InlineData(SkinKind.SwitchPro)]
    public void EverySkin_HasNoOverlappingButtons(SkinKind kind)
    {
        var layout = ControllerSkins.Get(kind).Layout.ToList();

        for (int i = 0; i < layout.Count; i++)
        {
            for (int j = i + 1; j < layout.Count; j++)
            {
                var a = layout[i].Value;
                var b = layout[j].Value;
                bool overlap = Math.Abs(a.Cx - b.Cx) < (a.W + b.W) / 2 && Math.Abs(a.Cy - b.Cy) < (a.H + b.H) / 2;
                Assert.False(overlap, $"{kind}: {layout[i].Key} overlaps {layout[j].Key}");
            }
        }
    }

    [Theory]
    [InlineData(SkinKind.Xbox)]
    [InlineData(SkinKind.PlayStation4)]
    [InlineData(SkinKind.PlayStation5)]
    [InlineData(SkinKind.SwitchPro)]
    public void EverySkin_DrawsAGlowingOutline(SkinKind kind)
    {
        var decor = ControllerSkins.Get(kind).Decor;

        Assert.True(decor.Count > 20);
        Assert.All(decor, d => Assert.False(string.IsNullOrWhiteSpace(d.Data)));
        Assert.Contains(decor, d => d.StrokeThickness > 6); // the soft glow under the crisp line
    }

    [Fact]
    public void RightHandShoulderShapes_AreNotOnTheLeft()
    {
        // Regression: the mirrored trigger and bumper once landed on top of the left-hand ones
        var decor = ControllerSkins.Get(SkinKind.PlayStation5).Decor.Select(d => d.Data).ToList();

        Assert.Contains(decor, d => d.StartsWith("M 524 90"));
    }

    [Fact]
    public void EveryMouseButton_IsAShapeOfTheMouse_NotABox()
    {
        Assert.All(InputLayouts.Mouse, s => Assert.False(string.IsNullOrWhiteSpace(s.ShapeData), $"{s.Name} has no shape"));

        // The big buttons and the thumb buttons are regions of the one body outline; the wheel parts have their own shapes
        var body = InputLayouts.Mouse.First(s => s.Name == "Left Click").ShapeData;
        foreach (var name in new[] { "Left Click", "Right Click", "Mouse Button 4", "Mouse Button 5" })
        {
            var s = InputLayouts.Mouse.First(m => m.Name == name);
            Assert.Equal(body, s.ShapeData);
            Assert.False(string.IsNullOrWhiteSpace(s.ClipData), $"{name} should be cut down to its own region");
        }
    }

    [Fact]
    public void MouseDrawing_CoversEveryInputTheHookCanReport()
    {
        var drawn = InputLayouts.Mouse.Select(s => s.Name).OrderBy(n => n).ToList();
        var reported = ReWASD.Engine.Input.LowLevelInputHook.MouseNames.OrderBy(n => n).ToList();

        Assert.Equal(reported, drawn);
    }

    [Fact]
    public void MouseLegends_SitInsideTheBody_OnTheirOwnSide()
    {
        Assert.All(InputLayouts.Mouse, s => Assert.InRange(s.Place.Cx, 250, 450));
        Assert.True(InputLayouts.Mouse.First(s => s.Name == "Left Click").Place.Cx < 330);
        Assert.True(InputLayouts.Mouse.First(s => s.Name == "Right Click").Place.Cx > 370);
        Assert.True(InputLayouts.Mouse.First(s => s.Name == "Mouse Button 5").Place.Cy < InputLayouts.Mouse.First(s => s.Name == "Mouse Button 4").Place.Cy);
    }

    [Fact]
    public void KeyboardAndMouse_HaveNoOverlappingKeys()
    {
        foreach (var layout in new[] { InputLayouts.Keyboard, InputLayouts.Mouse })
        {
            var list = layout.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i].Place;
                    var b = list[j].Place;
                    bool overlap = Math.Abs(a.Cx - b.Cx) < (a.W + b.W) / 2 - 0.5 && Math.Abs(a.Cy - b.Cy) < (a.H + b.H) / 2 - 0.5;
                    Assert.False(overlap, $"{list[i].Name} overlaps {list[j].Name}");
                }
            }
        }
    }
}
