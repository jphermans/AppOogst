using System.Linq;
using Avalonia;
using AppOogst.Services;

namespace AppOogst;

sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--selftest"))
            return SelfTest.Run();
        if (args.Contains("--probe"))
            return Probe.RunAsync().GetAwaiter().GetResult();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
#if DEBUG
        .WithDeveloperTools()
#endif
        .WithInterFont()
        .LogToTrace();
}
