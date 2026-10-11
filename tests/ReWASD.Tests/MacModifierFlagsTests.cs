using ReWASD.Engine.Platform.MacOS;

namespace ReWASD.Tests;

public class MacModifierFlagsTests
{
    [Theory]
    [InlineData(0x20000UL, 0UL, 0UL, 0x20000UL)] // physical Shift + mapped key
    [InlineData(0x100000UL, 0UL, 0UL, 0x100000UL)] // physical Command + mapped click
    [InlineData(0x20000UL, 0x40000UL, 0UL, 0x60000UL)] // physical Shift + injected Control
    [InlineData(0x20000UL, 0UL, 0x20000UL, 0UL)] // mapped physical Shift stays swallowed
    [InlineData(0x20002UL, 0UL, 0x20006UL, 0UL)] // side-specific swallowed Shift bits also stay removed
    [InlineData(0x20000UL, 0x20000UL, 0x20000UL, 0x20000UL)] // injected Shift still works
    [InlineData(0UL, 0UL, 0UL, 0UL)] // released injected modifier does not latch
    public void MappedEvents_PreservePhysicalModifiersExceptSwallowedOnes(
        ulong physical, ulong injected, ulong swallowed, ulong expected) =>
        Assert.Equal(expected, MacModifierFlags.Merge(physical, injected, swallowed));
}
