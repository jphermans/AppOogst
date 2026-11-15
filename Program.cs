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
        if (args.Contains("--diagnose"))
            return Diagnostics.RunAsync().GetAwaiter().GetResult();
        if (args.Contains("--shot"))
            return Shot(ValueAfter(args, "--shot") ?? "/tmp/appoogst.png",
                        ValueAfter(args, "--page") ?? "search");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static string? ValueAfter(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>
    /// Renders the live window to a PNG and exits (no Screen Recording permission
    /// needed — the app paints itself). <c>--page</c> picks the sidebar page,
    /// e.g. <c>--shot /tmp/p.png --page purchases</c>.
    /// </summary>
    private static int Shot(string path, string page)
    {
        var lifetime = default(Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime);
        BuildAvaloniaApp().AfterSetup(_ =>
        {
            lifetime = (Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                Avalonia.Application.Current!.ApplicationLifetime!;

            Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                if (lifetime.MainWindow?.DataContext is AppOogst.ViewModels.MainViewModel vm)
                    vm.ShowPageCommand.Execute(page);
            }, TimeSpan.FromSeconds(1));

            // Purchases takes ~12 s on Apple's side; give every page room.
            Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                var win = lifetime.MainWindow;
                if (win is null)
                {
                    Console.WriteLine("[shot] no window");
                    lifetime.Shutdown();
                    return;
                }

                try
                {
                    var width = (int)Math.Ceiling(win.ClientSize.Width);
                    var height = (int)Math.Ceiling(win.ClientSize.Height);
                    using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                        new Avalonia.PixelSize(Math.Max(width, 1), Math.Max(height, 1)),
                        new Avalonia.Vector(96, 96));
                    bitmap.Render(win);
                    // PngBitmapEncoderOptions: the int-quality overload is obsolete in Avalonia 12.
                    bitmap.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    Console.WriteLine($"[shot] {page} -> {path} ({width}x{height})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[shot] failed: {ex}");
                }

                lifetime.Shutdown();
            }, TimeSpan.FromSeconds(22));
        }).StartWithClassicDesktopLifetime(Array.Empty<string>());
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
