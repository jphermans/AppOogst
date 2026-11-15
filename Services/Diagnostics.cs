using System.Runtime.InteropServices;
using AppOogst.Services;

namespace AppOogst.Services;

/// <summary>
/// Support mode (<c>--diagnose</c>): says which ipatool binary the app will use,
/// whether it can actually execute, and what was repaired on the way. This is
/// the first thing to ask for when a packaged build "cannot log in", because a
/// blocked helper binary looks like an authentication failure in the GUI.
/// </summary>
public static class Diagnostics
{
    public static async Task<int> RunAsync()
    {
        var status = IpatoolService.Status;
        var version = typeof(Diagnostics).Assembly.GetName().Version;

        Console.WriteLine($"app        : AppOogst {version}");
        Console.WriteLine($"platform   : {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})");
        Console.WriteLine($"base dir   : {AppContext.BaseDirectory}");
        Console.WriteLine($"ipatool    : {status.Path}");
        Console.WriteLine($"exists     : {(File.Exists(status.Path) ? "yes" : "no")}");
        Console.WriteLine($"runnable   : {(status.Runnable ? "yes" : "NO")}");
        Console.WriteLine($"version    : {(status.Version.Length > 0 ? status.Version : "-")}");
        foreach (var d in status.Diagnostics)
            Console.WriteLine($"  - {d}");

        if (!status.Runnable)
        {
            Console.WriteLine("DIAGNOSE: ipatool is not executable — see the lines above");
            return 1;
        }

        Console.WriteLine("auth info  :");
        var res = await IpatoolService.RunAsync(IpatoolService.BuildArgs("auth", "info"));
        foreach (var line in res.Lines)
            Console.WriteLine($"  {line}");
        Console.WriteLine($"auth info exit: {res.ExitCode}");

        var auth = IpatoolService.ExtractAuth(IpatoolService.ParseJsonLines(res.Lines));
        var signedIn = auth is { Success: true };
        Console.WriteLine($"signed in  : {(signedIn ? "yes" : "no")}");
        Console.WriteLine(signedIn ? "DIAGNOSE OK" : "DIAGNOSE: not signed in");
        return 0;
    }
}
