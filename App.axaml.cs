using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AppOogst.Services;
using AppOogst.ViewModels;
using AppOogst.Views;

namespace AppOogst;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

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
