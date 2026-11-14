using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AppOogst.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() { }

    public AboutWindow(Window owner) : this()
    {
        Owner = owner;
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = v is null ? "Version 0.1" : $"Version {v.Major}.{v.Minor}.{v.Build}";
    }

    private void OpenProject_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/jphermans/AppOogst") { UseShellExecute = true });
        }
        catch
        {
            // ignore: opening a browser is best-effort
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
