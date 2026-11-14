using Avalonia.Styling;
using System.Text.Json.Nodes;
using AppOogst.Models;
using AppOogst.ViewModels;

namespace AppOogst.Services;

/// <summary>Same checks as the Python version; runs without a window.</summary>
public static class SelfTest
{
    public static int Run()
    {
        var results = new List<(string Name, bool Ok)>();
        void Check(string name, Func<bool> fn)
        {
            bool ok;
            try { ok = fn(); }
            catch (Exception ex) { Console.WriteLine($"FAIL {name} (exception: {ex.Message})"); return; }
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
            results.Add((name, ok));
        }

        Check("BuildArgs basics", () =>
            IpatoolService.BuildArgs("search", "x").SequenceEqual(
                new[] { "search", "x", "--non-interactive", "--format", "json" }));

        var sample = new[]
        {
            "{\"level\":\"info\",\"count\":2,\"apps\":[{\"id\":389801252,\"bundleID\":\"com.burbn.instagram\",\"name\":\"Instagram\",\"version\":\"455.0.0\",\"price\":0,\"platforms\":[\"iphone\",\"ipad\"]}]}",
            "8:07PM ERR error=demo success=false",
        };
        var objs = IpatoolService.ParseJsonLines(sample);
        Check("ParseJsonLines", () => objs.Count == 1);

        var apps = IpatoolService.ExtractApps(objs);
        Check("ExtractApps", () => apps.Count == 1 && apps[0].Id == 389801252
            && apps[0].BundleId == "com.burbn.instagram" && apps[0].PriceText == "free"
            && apps[0].PlatformsText == "iphone, ipad");

        Check("ExtractApps ignores non-JSON lines", () =>
            IpatoolService.ExtractApps(IpatoolService.ParseJsonLines(new[] { "geen json" })).Count == 0);

        var auth = IpatoolService.ExtractAuth(IpatoolService.ParseJsonLines(new[]
        {
            "{\"level\":\"info\",\"name\":\"Jean-Pierre Hermans\",\"email\":\"test@example.com\",\"success\":true}",
        }));
        Check("ExtractAuth", () => auth is { Success: true } && auth.Value.Email == "test@example.com");

        var total = IpatoolService.ExtractTotalCount(IpatoolService.ParseJsonLines(new[]
        {
            "{\"level\":\"info\",\"count\":1,\"totalCount\":2704,\"page\":1,\"apps\":[]}",
        }));
        Check("ExtractTotalCount", () => total == 2704);

        Check("Platforms list", () => IpatoolService.Platforms.SequenceEqual(
            new[] { "iphone", "ipad", "appletv", "watchos", "visionos", "macos" }));

        Check("AppInfo.FromJson platforms", () => AppInfo.FromJson(
            (JsonNode.Parse("{\"id\":1,\"bundleID\":\"b\",\"name\":\"n\",\"version\":\"v\",\"price\":0,\"platforms\":[\"iphone\",\"ipad\"]}"))!.AsObject())
            .PlatformsText == "iphone, ipad");

        var vm = new MainViewModel(autoAuth: false);
        vm.PrefillDownloadFrom(new AppInfo
        {
            Id = 389801252,
            BundleId = "com.burbn.instagram",
            Name = "Instagram",
            Version = "455.0.0",
            Price = "0",
            Platforms = new[] { "iphone", "ipad" },
        });
        Check("PrefillDownloadFrom", () => vm.DlAppId == "389801252"
            && vm.DlBundle == "com.burbn.instagram" && vm.DlPlatform == "iphone");

        Check("Locate returns a candidate", () =>
            !string.IsNullOrWhiteSpace(IpatoolService.BinaryPath));

        Check("ThemeManager variants", () =>
            ThemeManager.ResolveVariant(0) == ThemeVariant.Default
            && ThemeManager.ResolveVariant(1) == ThemeVariant.Dark
            && ThemeManager.ResolveVariant(2) == ThemeVariant.Light
            && ThemeManager.ThemeChoices.SequenceEqual(new[] { "System", "Dark", "Light" }));

        var errs = IpatoolService.ExtractLastError(IpatoolService.ParseJsonLines(new[]
        {
            "{\"level\":\"info\",\"time\":\"t\",\"message\":\"preparing authentication\"}",
            "{\"level\":\"info\",\"message\":\"2FA code is required; run the command again and supply a code using the --auth-code flag\"}",
        }));
        Check("ExtractLastError", () => errs.Contains("2FA code is required"));

        if (results.All(r => r.Ok) && results.Count == 12)
        {
            Console.WriteLine($"SELFTEST OK ({results.Count} checks)");
            return 0;
        }
        Console.WriteLine("SELFTEST FAILED");
        return 1;
    }
}
