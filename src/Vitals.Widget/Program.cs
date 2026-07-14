using Avalonia;
using System;

namespace Vitals.Widget;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Diagnostic mode: probe every sensor provider, print results, exit.
        // No UI. Output only shows up when stdout is redirected (e.g. "app --probe | more"
        // or from a script), because this is a WinExe on Windows.
        if (Array.Exists(args, a => string.Equals(a, "--probe", StringComparison.OrdinalIgnoreCase)))
        {
            ProviderProbe.Run();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
