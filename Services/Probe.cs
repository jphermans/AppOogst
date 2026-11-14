using AppOogst.Services;

namespace AppOogst.Services;

/// <summary>Real CLI integration check without GUI (--probe).</summary>
public static class Probe
{
    public static async Task<int> RunAsync()
    {
        Console.WriteLine($"[0/3] using ipatool: {IpatoolService.BinaryPath}");
        Console.WriteLine("[1/3] auth info ...");
        var r1 = await IpatoolService.RunAsync(IpatoolService.BuildArgs("auth", "info"));
        var auth = IpatoolService.ExtractAuth(IpatoolService.ParseJsonLines(r1.Lines));
        Console.WriteLine(auth is { Success: true } a
            ? $"PASS auth: {a.Name} <{a.Email}>"
            : "FAIL auth");

        Console.WriteLine("[2/3] search instagram ...");
        var r2 = await IpatoolService.RunAsync(
            IpatoolService.BuildArgs("search", "instagram", "--limit", "2"));
        var apps = IpatoolService.ExtractApps(IpatoolService.ParseJsonLines(r2.Lines));
        Console.WriteLine(apps.Count > 0
            ? $"PASS search: {apps.Count} results (exit {r2.ExitCode})"
            : "FAIL search");

        Console.WriteLine("[3/3] list-purchases page 1 ...");
        var r3 = await IpatoolService.RunAsync(
            IpatoolService.BuildArgs("list-purchases", "--max-results", "20", "--page", "1"));
        var p3 = IpatoolService.ExtractApps(IpatoolService.ParseJsonLines(r3.Lines));
        var total = IpatoolService.ExtractTotalCount(IpatoolService.ParseJsonLines(r3.Lines));
        Console.WriteLine(p3.Count > 0
            ? $"PASS purchases: {p3.Count} apps, totaal {total}"
            : "FAIL purchases");

        bool ok = auth is { Success: true } && apps.Count > 0 && p3.Count > 0;
        Console.WriteLine(ok ? "PROBE OK" : "PROBE FAILED");
        return ok ? 0 : 1;
    }
}
