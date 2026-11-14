using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AppOogst.Services;
using AppOogst.ViewModels;
using AppOogst.Views;

namespace AppOogst;

public partial class App : Application
{
    public App()
    {
        // 1) macOS menu bar app-menu TITLE: AvaloniaNativePlatform.
        //    SetupApplicationName() pushes Application.Name to the native
        //    SetApplicationTitle (-> NSProcessInfo process name) during
        //    platform initialization. The framework default is the literal
        //    "Avalonia Application" (Application.cs ctor). This App ctor runs
        //    at AppBuilder.Configure() time, long before platform init.
        Name = "AppOogst";

        // 2) macOS app-menu CONTENT: the AvaloniaNativeMenuExporter
        //    (Application target) runs DoLayoutReset() synchronously during
        //    platform init and reads NativeMenu.GetMenu(Application.Current);
        //    when it is null it exports its DEFAULT menu ("About Avalonia" +
        //    standard items) — that is why the menu bar used to show
        //    Avalonia's own About. Setting the attached property HERE means
        //    the exporter finds OUR menu instead of creating the default, and
        //    Avalonia then appends the standard items (Services, Hide
        //    AppOogst, Quit with Cmd+Q) below our About item automatically.
        //    Note: Application is neither a TopLevel nor an
        //    INativeMenuExporterProvider, so this property set is pure
        //    storage — that is exactly what we want at ctor time; the export
        //    happens later, when the exporter reads it.
        var root = new NativeMenu();
        var about = new NativeMenuItem("About AppOogst");
        about.Click += (_, _) => ShowAbout();
        root.Add(about);
        NativeMenu.SetMenu(this, root);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    internal static void ShowAbout()
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
            new AboutWindow().Show();
        else
            new AboutWindow(owner).Show();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeManager.Apply(ThemeManager.Load());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
