using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AppOogst.Models;
using AppOogst.ViewModels;

namespace AppOogst.Views;

public partial class MainWindow : Window
{
    private bool _hooked;

    public MainWindow()
    {
        InitializeComponent();
        HookVm();
        KeyDown += (_, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.F1)
                HelpMenu_Click(this, e);
        };
        DataContextChanged += (_, _) => HookVm();
    }

    private void HookVm()
    {
        if (_hooked || DataContext is not MainViewModel vm)
            return;
        _hooked = true;
        vm.LogEntries.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (vm.LogEntries.Count > 0)
                LogList?.ScrollIntoView(vm.LogEntries[^1]);
        });
    }

    private void OpenSelected(DataGrid grid)
    {
        if (DataContext is MainViewModel vm && grid.SelectedItem is AppInfo app)
            vm.OpenDownloadFor(app);
    }

    private void SearchGrid_DoubleTapped(object? sender, RoutedEventArgs e) => OpenSelected(SearchGrid);
    private void PurchasesGrid_DoubleTapped(object? sender, RoutedEventArgs e) => OpenSelected(PurchasesGrid);

    private void PurchasesGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.PrefillDownloadFrom(PurchasesGrid.SelectedItem as AppInfo);
    }

    private async void PickFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } sp)
            return;
        var folders = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose destination folder",
            AllowMultiple = false,
        });
        if (folders.Count > 0 && DataContext is MainViewModel vm)
            vm.DlOutputDir = folders[0].Path.LocalPath;
    }

    private void HelpMenu_Click(object? sender, EventArgs e)
        => new HelpWindow(this).Show();
}
