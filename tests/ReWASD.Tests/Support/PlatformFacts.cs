namespace ReWASD.Tests;

/// <summary>A test that needs Windows (its drivers, hooks or the ViGEm client library); skipped elsewhere.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows";
    }
}

/// <summary>A theory that needs Windows; skipped elsewhere.</summary>
public sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows";
    }
}

/// <summary>A test that needs macOS; skipped elsewhere.</summary>
public sealed class MacFactAttribute : FactAttribute
{
    public MacFactAttribute()
    {
        if (!OperatingSystem.IsMacOS()) Skip = "Requires macOS";
    }
}
