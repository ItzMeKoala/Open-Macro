using Avalonia;
using Avalonia.Headless;
using ReWASD.UI.Mac;

[assembly: AvaloniaTestApplication(typeof(ReWASD.UI.Mac.Tests.TestAppBuilder))]

namespace ReWASD.UI.Mac.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
