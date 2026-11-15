using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppOogst.Models;
using AppOogst.Services;

namespace AppOogst.ViewModels;

public sealed record LogEntry(string Text, string Level);

public partial class MainViewModel : ViewModelBase
{
    public static readonly string[] PageNames = { "account", "search", "purchases", "download", "log" };
    public static readonly string[] PageLabels = { "Account", "Search", "Purchases", "Download", "Log" };

    private readonly ActionQueue _queue = new();
    private int _purchasePage = 1;
    private bool _purchasesLoaded;

    [ObservableProperty] private int _currentPageIndex = 1;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _statusText = "ready";
    [ObservableProperty] private string _authDisplay = "status unknown";
    [ObservableProperty] private string _sideEmail = "not signed in";
    [ObservableProperty] private bool _authOk;

    [ObservableProperty] private string _loginEmail = "";
    [ObservableProperty] private string _loginPassword = "";
    [ObservableProperty] private string _login2fa = "";
    [ObservableProperty] private int _themeIndex;

    [ObservableProperty] private string _searchTerm = "";
    [ObservableProperty] private string _searchPlatform = "alle (standaard)";
    [ObservableProperty] private string _searchLimitText = "10";
    public ObservableCollection<AppInfo> SearchResults { get; } = new();
    [ObservableProperty] private AppInfo? _selectedSearchApp;

    [ObservableProperty] private string _purchasePlatform = "alle";
    [ObservableProperty] private string _purchaseMaxText = "50";
    [ObservableProperty] private string _purchasePageLabel = "page 1";
    public ObservableCollection<AppInfo> Purchases { get; } = new();
    [ObservableProperty] private AppInfo? _selectedPurchaseApp;

    [ObservableProperty] private string _dlAppId = "";
    [ObservableProperty] private string _dlBundle = "";
    [ObservableProperty] private string _dlPlatform = "alle (standaard)";
    [ObservableProperty] private string _dlOutputDir =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads";
    [ObservableProperty] private bool _dlPurchase = true;

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public string[] PlatformChoices { get; } = IpatoolService.Platforms;
    public string[] SearchPlatformChoices { get; } =
        new[] { "alle (standaard)" }.Concat(IpatoolService.Platforms).ToArray();
    public string[] PurchasePlatformChoices { get; } =
        new[] { "alle" }.Concat(IpatoolService.Platforms).ToArray();
    public string[] ThemeChoices { get; } = ThemeManager.ThemeChoices;
    public string[] LimitChoices { get; } = { "5", "10", "20", "50" };
    public string[] MaxChoices { get; } = { "10", "25", "50", "100", "200" };

    public bool IsAccountPage => CurrentPageIndex == 0;
    public bool IsSearchPage => CurrentPageIndex == 1;
    public bool IsPurchasesPage => CurrentPageIndex == 2;
    public bool IsDownloadPage => CurrentPageIndex == 3;
    public bool IsLogPage => CurrentPageIndex == 4;

    partial void OnThemeIndexChanged(int value) => ThemeManager.Apply(value);

    public MainViewModel(bool autoAuth = true)
    {
        _themeIndex = ThemeManager.Load();
        _queue.BusyChanged += b => Dispatcher.UIThread.Post(() => Busy = b);
        _queue.StatusChanged += s => Dispatcher.UIThread.Post(() => StatusText = s);
        Log("AppOogst (C#/Avalonia) started");

        // Report the resolved helper up front. A blocked ipatool binary used to
        // look like a failed login, because every command just logged "exit 137".
        var tool = IpatoolService.Status;
        Log($"ipatool: {tool.Path}");
        foreach (var d in tool.Diagnostics)
            Log("ipatool: " + d);
        if (!tool.Runnable)
        {
            AuthDisplay = "ipatool unavailable — see Log";
            SetStatus("ipatool cannot run on this machine; open AppOogst --diagnose for details");
            return;
        }

        if (autoAuth)
            Enqueue(() => RefreshAuthCoreAsync(), "fetching account status ...");
    }

    partial void OnCurrentPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAccountPage));
        OnPropertyChanged(nameof(IsSearchPage));
        OnPropertyChanged(nameof(IsPurchasesPage));
        OnPropertyChanged(nameof(IsDownloadPage));
        OnPropertyChanged(nameof(IsLogPage));
        if (PageNames[value] == "purchases" && !_purchasesLoaded)
        {
            _purchasesLoaded = true;
            LoadPurchases();
        }
    }

    [RelayCommand]
    private void ShowPage(string? page)
    {
        var idx = Array.IndexOf(PageNames, page ?? "");
        if (idx >= 0)
            CurrentPageIndex = idx;
    }

    private static string? NormalizePlatform(string? combo)
        => string.IsNullOrEmpty(combo) || combo.StartsWith("alle", StringComparison.OrdinalIgnoreCase)
            ? null : combo;

    private void Enqueue(Func<Task> work, string busyStatus)
    {
        if (!_queue.Enqueue(work, busyStatus))
            Log("queue full — action ignored");
    }

    public void Log(string text)
    {
        var lower = text.ToLowerInvariant();
        var level = text.Contains("ERR") ? "ERR"
            : lower.Contains("warn") ? "WRN"
            : text.StartsWith("$") ? "CMD"
            : lower.Contains("successful") || lower.Contains("download ok") ? "OK"
            : "INF";
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(new LogEntry(text, level));
            while (LogEntries.Count > 2000)
                LogEntries.RemoveAt(0);
        });
    }

    private void SetStatus(string s) => Dispatcher.UIThread.Post(() => StatusText = s);

    // ---------------------------------------------------------------- account
    private async Task RefreshAuthCoreAsync()
    {
        Log("$ ipatool auth info");
        var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs("auth", "info"), Log);
        var auth = IpatoolService.ExtractAuth(IpatoolService.ParseJsonLines(res.Lines));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (auth is { Success: true } a)
            {
                AuthDisplay = $"Signed in as {a.Name} ({a.Email})";
                SideEmail = a.Email;
                AuthOk = true;
            }
            else
            {
                AuthDisplay = "Not signed in";
                SideEmail = "not signed in";
                AuthOk = false;
            }
        });
        SetStatus($"account status fetched (exit {res.ExitCode})");
    }

    [RelayCommand]
    private void RefreshAuth() => Enqueue(() => RefreshAuthCoreAsync(), "fetching account status ...");

    [RelayCommand]
    private void Login()
    {
        var email = LoginEmail.Trim();
        var pw = LoginPassword;
        var twofa = Login2fa.Trim();
        if (email.Length == 0 || pw.Length == 0)
        {
            SetStatus("enter email and password");
            return;
        }
        Enqueue(async () =>
        {
            Log("$ ipatool auth login -e " + email + " -p ***" + (twofa.Length > 0 ? " --auth-code ***" : ""));
            var args = new List<string> { "auth", "login", "-e", email, "-p", pw };
            if (twofa.Length > 0)
                args.AddRange(new[] { "--auth-code", twofa });
            var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs(args.ToArray()), Log);
            if (res.ExitCode != 0)
            {
                var reason = IpatoolService.ExtractLastError(IpatoolService.ParseJsonLines(res.Lines));
                if (reason.Contains("2FA code is required", StringComparison.OrdinalIgnoreCase))
                    reason = "2FA code is required — enter your 6-digit code from a trusted device and sign in again";
                Log($"sign-in reported failure (exit {res.ExitCode}): {reason}");
                SetStatus("login error — verifying stored session before reporting failure ...");
                // ipatool can store a valid session and still exit non-zero (known
                // Apple AMS dialog issue) — always verify before declaring defeat.
                await RefreshAuthCoreAsync();
                if (AuthOk)
                {
                    Log("session verified after non-zero exit — sign-in actually succeeded");
                    SetStatus("sign-in succeeded (session verified)");
                    return;
                }
                Log($"sign-in FAILED (exit {res.ExitCode}): {reason}");
                SetStatus($"sign-in failed: {reason}");
                return;
            }
            Log("sign-in successful");
            await RefreshAuthCoreAsync();
            SetStatus("sign-in finished successfully");
        }, "signing in ...");
    }

    [RelayCommand]
    private void Revoke()
    {
        Enqueue(async () =>
        {
            Log("$ ipatool auth revoke");
            var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs("auth", "revoke"), Log);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AuthDisplay = "Not signed in";
                SideEmail = "not signed in";
                AuthOk = false;
            });
            SetStatus($"signed out (exit {res.ExitCode})");
        }, "signing out ...");
    }

    // ---------------------------------------------------------------- search
    [RelayCommand]
    private void Search()
    {
        var term = SearchTerm.Trim();
        if (term.Length == 0)
        {
            SetStatus("enter a search term");
            return;
        }
        var limit = int.TryParse(SearchLimitText, out var l) ? Math.Clamp(l, 1, 50) : 10;
        var plat = NormalizePlatform(SearchPlatform);
        Enqueue(async () =>
        {
            var args = new List<string> { "search", term, "--limit", limit.ToString() };
            if (plat != null)
                args.AddRange(new[] { "--platform", plat });
            Log("$ ipatool " + string.Join(' ', args));
            var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs(args.ToArray()), Log);
            var apps = IpatoolService.ExtractApps(IpatoolService.ParseJsonLines(res.Lines));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SearchResults.Clear();
                foreach (var a in apps)
                    SearchResults.Add(a);
            });
            if (apps.Count == 0)
            {
                Log("no results");
                SetStatus($"no results for '{term}'");
            }
            else
                SetStatus($"search: {apps.Count} results (exit {res.ExitCode})");
        }, "searching ...");
    }

    [RelayCommand]
    private void DownloadSelectedSearch()
    {
        if (SelectedSearchApp is { } app)
            OpenDownloadFor(app);
    }

    [RelayCommand]
    private void BuySelectedSearch()
    {
        if (SelectedSearchApp is { } app)
            BuyLicense(app);
    }

    // ---------------------------------------------------------------- purchases
    [RelayCommand]
    private void LoadPurchases()
    {
        _purchasePage = 1;
        EnqueuePurchases();
    }

    [RelayCommand]
    private void NextPurchasePage()
    {
        _purchasePage++;
        EnqueuePurchases();
    }

    private void EnqueuePurchases()
        => Enqueue(() => PurchasesCoreAsync(_purchasePage), "loading purchases ... (this may take a while)");

    private async Task PurchasesCoreAsync(int page)
    {
        var max = int.TryParse(PurchaseMaxText, out var m) ? Math.Clamp(m, 5, 200) : 50;
        var args = new List<string>
            { "list-purchases", "--max-results", max.ToString(), "--page", page.ToString() };
        var plat = NormalizePlatform(PurchasePlatform);
        if (plat != null)
            args.AddRange(new[] { "--platform", plat });
        Log("$ ipatool " + string.Join(' ', args));
        var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs(args.ToArray()), Log);
        var objs = IpatoolService.ParseJsonLines(res.Lines);
        var apps = IpatoolService.ExtractApps(objs);
        var total = IpatoolService.ExtractTotalCount(objs);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Purchases.Clear();
            foreach (var a in apps)
                Purchases.Add(a);
            PurchasePageLabel = $"page {page}" + (total is { } t ? $" — {apps.Count} of {t} apps" : "");
        });
        SetStatus($"purchases page {page}: {apps.Count} apps (exit {res.ExitCode})");
    }

    [RelayCommand]
    private void DownloadSelectedPurchase()
    {
        if (SelectedPurchaseApp is { } app)
            OpenDownloadFor(app);
    }

    // ---------------------------------------------------------------- download
    public void OpenDownloadFor(AppInfo app)
    {
        PrefillDownloadFrom(app);
        ShowPage("download");
    }

    /// <summary>
    /// Fills the Download fields from a selected app without switching pages.
    /// Called on selection in Purchases; double-click uses OpenDownloadFor
    /// (prefill + jump straight to Download).
    /// </summary>
    public void PrefillDownloadFrom(AppInfo? app)
    {
        if (app is null)
            return;
        DlAppId = app.Id.ToString();
        DlBundle = app.BundleId;
        if (app.Platforms.Count > 0 && Array.IndexOf(IpatoolService.Platforms, app.Platforms[0]) >= 0)
            DlPlatform = app.Platforms[0];
        SetStatus($"copied to Download: {app.Name} ({app.BundleId})");
    }

    private void BuyLicense(AppInfo app)
    {
        var bundle = app.BundleId;
        Enqueue(async () =>
        {
            Log($"$ ipatool purchase -b {bundle}");
            var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs("purchase", "-b", bundle), Log);
            SetStatus($"purchase finished (exit {res.ExitCode})");
        }, "buying license ...");
    }

    [RelayCommand]
    private void StartDownload()
    {
        var appId = DlAppId.Trim();
        var bundle = DlBundle.Trim();
        if (!long.TryParse(appId, out _) && bundle.Length == 0)
        {
            SetStatus("enter a valid App ID or a Bundle ID");
            return;
        }
        var outDir = string.IsNullOrWhiteSpace(DlOutputDir) ? DlOutputDir : DlOutputDir.Trim();
        if (!Directory.Exists(outDir))
        {
            SetStatus($"destination folder does not exist: {outDir}");
            return;
        }
        var plat = NormalizePlatform(DlPlatform);
        var purchase = DlPurchase;
        Enqueue(async () =>
        {
            var args = new List<string> { "download", "-o", outDir };
            if (bundle.Length > 0)
                args.AddRange(new[] { "-b", bundle });
            else
                args.AddRange(new[] { "-i", appId });
            if (plat != null)
                args.AddRange(new[] { "--platform", plat });
            if (purchase)
                args.Add("--purchase");
            Log("$ ipatool " + string.Join(' ', args));
            var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs(args.ToArray()), Log);
            Log(res.ExitCode == 0 ? $"download OK -> {outDir}" : $"download FAILED (exit {res.ExitCode})");
            SetStatus($"download finished (exit {res.ExitCode})");
        }, "downloading (this may take a while) ...");
    }
}
