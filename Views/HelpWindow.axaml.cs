using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AppOogst.Views;

public partial class HelpWindow : Window
{
    public HelpWindow() { }

    public HelpWindow(Window owner) : this()
    {
        Owner = owner;
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
