using System.Reflection;

namespace ReWASD.Core.Skins.Art;

/// <summary>The drawings that ship with the app, read from the SVG files embedded in this assembly.</summary>
public static class ArtLibrary
{
    private static readonly Lazy<ArtScene> DualSenseScene = new(() => Load("dualsense.svg"));
    private static readonly Lazy<ArtScene> XboxOneScene = new(() => Load("xboxone.svg"));
    private static readonly Lazy<ArtScene> KeyboardScene = new(() => SvgArtParser.Parse(KeyboardArt.Svg()));

    /// <summary>A tenkeyless keyboard, drawn from <see cref="KeyboardArt"/>.</summary>
    public static ArtScene Keyboard => KeyboardScene.Value;

    /// <summary>The keyboard drawing, or null if it cannot be built (the keyboard tab then shows its plain key boxes).</summary>
    public static ArtScene? TryGetKeyboard()
    {
        try
        {
            return Keyboard;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The PlayStation 5 DualSense controller, front view.</summary>
    public static ArtScene DualSense => DualSenseScene.Value;

    /// <summary>The Xbox One controller, front view.</summary>
    public static ArtScene XboxOne => XboxOneScene.Value;

    /// <summary>The Xbox drawing, or null if it cannot be read (see <see cref="TryGetDualSense"/>).</summary>
    public static ArtScene? TryGetXboxOne()
    {
        try
        {
            return XboxOne;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The DualSense drawing, or null if it cannot be read. A skin falls back to its plain outline in that case, so a
    /// problem with a drawing can never stop a controller picture from showing.
    /// </summary>
    public static ArtScene? TryGetDualSense()
    {
        try
        {
            return DualSense;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ArtScene Load(string file)
    {
        var assembly = typeof(ArtLibrary).Assembly;
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + file, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return SvgArtParser.Parse(reader.ReadToEnd());
    }
}
