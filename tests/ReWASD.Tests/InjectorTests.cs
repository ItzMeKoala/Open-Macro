using ReWASD.Engine.Input;
using ReWASD.Engine.Remapper;

namespace ReWASD.Tests;

public class InjectorTests
{
    private sealed class FakeBackend : IInputBackend
    {
        public List<string> Events { get; } = new();
        public void KeyDown(ushort virtualKey) => Events.Add($"kd:{virtualKey:X}");
        public void KeyUp(ushort virtualKey) => Events.Add($"ku:{virtualKey:X}");
        public void MouseDown(MouseButtonKind button) => Events.Add($"md:{button}");
        public void MouseUp(MouseButtonKind button) => Events.Add($"mu:{button}");
        public void MouseWheel(int delta) => Events.Add($"wheel:{delta}");
        public void MouseMove(int dx, int dy) => Events.Add($"move:{dx},{dy}");
    }

    [Fact]
    public void KeyPress_FiresOncePerHold_AndReleasesOnDrop()
    {
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend);
        var state = new RemappedState();
        state.ActiveKeyboardKeys.Add("W");

        injector.Apply(state);
        injector.Apply(state);
        injector.Apply(new RemappedState());

        Assert.Equal(new[] { "kd:57", "ku:57" }, backend.Events);
    }

    [Fact]
    public void WheelAndMouse_ResolveFromProfileNames()
    {
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend);
        var state = new RemappedState();
        state.ActiveMouseButtons.Add("Left Click");
        state.ActiveMouseButtons.Add("Wheel Down");

        injector.Apply(state);
        injector.Apply(state);

        Assert.Contains("md:Left", backend.Events);
        Assert.Single(backend.Events, "wheel:-120");
    }

    [Fact]
    public void ReleaseAll_ReleasesHeldInputs()
    {
        var backend = new FakeBackend();
        var injector = new KeyboardMouseInjector(backend);
        var state = new RemappedState();
        state.ActiveKeyboardKeys.Add("Space");
        state.ActiveMouseButtons.Add("Right Click");
        injector.Apply(state);

        injector.ReleaseAll();

        Assert.Contains("ku:20", backend.Events);
        Assert.Contains("mu:Right", backend.Events);
    }
}
