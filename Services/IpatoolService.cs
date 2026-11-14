using System.Diagnostics;
using System.Text.Json.Nodes;
using AppOogst.Models;

namespace AppOogst.Services;

/// <summary>CLI wrapper around ipatool: argument list (never a shell), JSON output.</summary>
public sealed class IpatoolService
{
    public static readonly string[] Platforms =
        { "iphone", "ipad", "appletv", "watchos", "visionos", "macos" };

    public static string BinaryPath { get; } = Locate();

    private static string Locate()
    {
        // 1) bundled binary shipped next to the executable (preferred)
        var bundled = Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "ipatool.exe" : "ipatool");
        if (File.Exists(bundled))
            return bundled;

        // 2) well-known system locations
        foreach (var p in new[] { "/opt/homebrew/bin/ipatool", "/usr/local/bin/ipatool" })
            if (File.Exists(p))
                return p;

        // 3) fall back to PATH
        return "ipatool";
    }

    /// <summary>Mirrors the Python build_cmd: args + --non-interactive + --format json.</summary>
    public static List<string> BuildArgs(params string[] args)
    {
        var l = new List<string>(args) { "--non-interactive", "--format", "json" };
        return l;
    }

    public static async Task<RunResult> RunAsync(
        IReadOnlyList<string> args, Action<string>? onLine = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = BinaryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var lines = new List<string>();
        var outTask = PumpAsync(proc.StandardOutput, lines, onLine);
        var errTask = PumpAsync(proc.StandardError, lines, onLine);
        await Task.WhenAll(outTask, errTask);
        await proc.WaitForExitAsync();
        return new RunResult(proc.ExitCode, lines);
    }

    private static async Task PumpAsync(StreamReader r, List<string> lines, Action<string>? onLine)
    {
        while (await r.ReadLineAsync() is { } line)
        {
            lines.Add(line);
            onLine?.Invoke(line);
        }
    }

    // ------------------------------ defensive JSON parsing (line by line) ------------------------------

    public static List<JsonObject> ParseJsonLines(IEnumerable<string> lines)
    {
        var objs = new List<JsonObject>();
        foreach (var raw in lines)
        {
            var s = raw.TrimStart();
            if (!s.StartsWith("{"))
                continue;
            try
            {
                if (JsonNode.Parse(s) is JsonObject o)
                    objs.Add(o);
            }
            catch (System.Text.Json.JsonException)
            {
                // ignore non-JSON lines
            }
        }
        return objs;
    }

    public static List<AppInfo> ExtractApps(IEnumerable<JsonObject> objs)
    {
        foreach (var o in objs.Reverse())
            if (o["apps"] is JsonArray arr)
            {
                var list = new List<AppInfo>();
                foreach (var n in arr)
                    if (n is JsonObject ao)
                        list.Add(AppInfo.FromJson(ao));
                return list;
            }
        return new List<AppInfo>();
    }

    public static (string Name, string Email, bool Success)? ExtractAuth(IEnumerable<JsonObject> objs)
    {
        foreach (var o in objs.Reverse())
            if (o.ContainsKey("email") || o.ContainsKey("success"))
                return (
                    o["name"]?.ToString() ?? "?",
                    o["email"]?.ToString() ?? "?",
                    o["success"]?.ToString() != "False");
        return null;
    }

    public static long? ExtractTotalCount(IEnumerable<JsonObject> objs)
    {
        foreach (var o in objs.Reverse())
            if (o["totalCount"] is JsonNode n && long.TryParse(n.ToString(), out var t))
                return t;
        return null;
    }
}

public sealed record RunResult(int ExitCode, IReadOnlyList<string> Lines);
