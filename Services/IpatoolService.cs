using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AppOogst.Models;

namespace AppOogst.Services;

/// <summary>
/// Resolved state of the ipatool executable: where it is, whether it can
/// actually run, and what had to be repaired to get there (surfaced by
/// --diagnose and in the GUI log).
/// </summary>
public sealed record ToolStatus(
    string Path, bool Runnable, string Version, IReadOnlyList<string> Diagnostics)
{
    public static ToolStatus Unavailable(string path, IReadOnlyList<string> diagnostics)
        => new(path, false, "", diagnostics);
}

/// <summary>CLI wrapper around ipatool: argument list (never a shell), JSON output.</summary>
public sealed class IpatoolService
{
    public static readonly string[] Platforms =
        { "iphone", "ipad", "appletv", "watchos", "visionos", "macos" };

    /// <summary>Exit code macOS reports for a child killed by SIGKILL.</summary>
    public const int KillExitCode = 137;

    private static readonly Lazy<ToolStatus> LazyStatus =
        new(Prepare, LazyThreadSafetyMode.ExecutionAndPublication);

    public static ToolStatus Status => LazyStatus.Value;

    public static string BinaryPath => Status.Path;

    private static string ExeName => OperatingSystem.IsWindows() ? "ipatool.exe" : "ipatool";

    private static IEnumerable<string> Candidates()
    {
        // 1) bundled binary shipped next to the executable (preferred, all OSes)
        yield return Path.Combine(AppContext.BaseDirectory, ExeName);

        // 2) well-known system locations
        if (!OperatingSystem.IsWindows())
        {
            yield return "/opt/homebrew/bin/ipatool";
            yield return "/usr/local/bin/ipatool";
        }

        // 3) PATH (Linux distro packages, scoop/choco on Windows, custom installs)
        foreach (var fromPath in PathCandidates())
            yield return fromPath;
    }

    /// <summary>
    /// Resolves the exe name against PATH. A bare "ipatool" string would only
    /// ever match the current directory: every Filesystem check here needs an
    /// absolute path, so a PATH-installed ipatool (apt, scoop, choco) used to be
    /// rejected as missing.
    /// </summary>
    private static IEnumerable<string> PathCandidates()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length == 0)
                continue;

            string full;
            try
            {
                full = Path.Combine(trimmed, ExeName);
            }
            catch
            {
                continue;
            }

            if (File.Exists(full))
                yield return full;
        }
    }

    /// <summary>
    /// Picks the first candidate that both exists and provably runs.
    ///
    /// Existing is not enough: a downloaded macOS bundle carries
    /// com.apple.quarantine on every file, and macOS kills an ad-hoc signed
    /// third-party helper (SIGKILL, exit 137) the moment it is executed. So the
    /// flag is cleared here — in place when the bundle is writable, otherwise on
    /// a private copy in Application Support — and the binary is accepted only
    /// after `--version` actually succeeds.
    /// </summary>
    private static ToolStatus Prepare()
    {
        var diag = new List<string>();
        var exists = false;

        foreach (var candidate in Candidates())
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                continue;

            exists = true;
            var usable = TryMakeRunnable(candidate, diag);
            if (usable is null)
                continue;

            var version = ProbeVersion(usable, out var exit);
            if (version is not null)
            {
                diag.Add($"{usable}: {version}");
                return new ToolStatus(usable, true, version, diag);
            }

            diag.Add(exit == KillExitCode
                ? $"{usable}: killed by the OS (exit 137) — wrong platform build, or blocked by macOS Gatekeeper/AMFI"
                : $"{usable}: '--version' failed (exit {exit})");
        }

        if (!exists)
            diag.Add("no ipatool binary found next to the app, in Homebrew or on PATH");
        else
            diag.Add("no usable ipatool binary — see the lines above");

        return ToolStatus.Unavailable(Candidates().First(), diag);
    }

    /// <summary>Clears the OS exec blockers on a candidate; returns the path to use.</summary>
    private static string? TryMakeRunnable(string path, List<string> diag)
    {
        if (OperatingSystem.IsWindows())
        {
            // A downloaded zip stamps Mark-of-the-Web on the extracted helper —
            // the Windows equivalent of macOS quarantine. Remove it so
            // SmartScreen cannot refuse the bundled ipatool.exe.
            if (TryClearMarkOfTheWeb(path))
                diag.Add($"{path}: removed the Mark-of-the-Web (Zone.Identifier)");
            return path;
        }

        TrySetExecutable(path);

        if (!OperatingSystem.IsMacOS())
            return path;

        if (!HasQuarantine(path))
            return path;

        // An immutable (uchg) binary rejects xattr writes; clear that first so the
        // in-place repair can succeed and no copy is needed.
        RunQuiet("/usr/bin/chflags", "nouchg", path);

        if (RunQuiet("/usr/bin/xattr", "-d", "com.apple.quarantine", path) == 0)
        {
            diag.Add($"{path}: removed com.apple.quarantine");
            return path;
        }

        diag.Add($"{path}: com.apple.quarantine not removable (read-only bundle?) — using a private copy");
        return CopyToSupportDirectory(path, diag);
    }

    private static string? CopyToSupportDirectory(string source, List<string> diag)
    {
        var dir = SupportDirectory();
        var target = Path.Combine(dir, Path.GetFileName(source));
        // Unique temp name: two app instances must not fight over one file, and a
        // crashed previous run must not leave a poisoned ".tmp" behind.
        var temp = Path.Combine(dir,
            $"{Path.GetFileNameWithoutExtension(source)}.{Environment.ProcessId}.tmp");

        try
        {
            Directory.CreateDirectory(dir);
            File.Copy(source, temp, overwrite: true);

            if (OperatingSystem.IsMacOS())
            {
                // A read-only/immutable source propagates uchg to the copy, which
                // would defeat both the xattr strip and the chmod below.
                RunQuiet("/usr/bin/chflags", "nouchg", temp);
                RunQuiet("/usr/bin/xattr", "-c", temp);
                if (HasQuarantine(temp))
                {
                    diag.Add($"{target}: private copy is still quarantined — not usable");
                    TryDelete(temp);
                    return null;
                }
            }

            TrySetExecutable(temp);
            File.Move(temp, target, overwrite: true);

            diag.Add($"copied to {target}");
            return target;
        }
        catch (Exception ex)
        {
            diag.Add($"could not create a private copy ({ex.GetType().Name}: {ex.Message})");
            TryDelete(temp);
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // nothing else we can do; a later run overwrites a unique temp name
        }
    }

    private static string SupportDirectory()
    {
        // Native macOS location; XDG-style ApplicationData elsewhere.
        var root = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "AppOogst");
    }

    private static bool HasQuarantine(string path)
        => RunQuiet("/usr/bin/xattr", "-p", "com.apple.quarantine", path) == 0;

    /// <summary>
    /// Deletes the "Zone.Identifier" alternate data stream Windows attaches to
    /// files that arrived from the internet. No-op elsewhere; returns false when
    /// the stream does not exist (the normal case for a locally built app).
    /// </summary>
    private static bool TryClearMarkOfTheWeb(string path)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            // "file:Zone.Identifier" is a stream name rather than a path, so the
            // Win32 call is the documented way to drop it.
            return DeleteFileW($"{path}:Zone.Identifier");
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteFileW(string lpFileName);

    private static void TrySetExecutable(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return;
            File.SetUnixFileMode(path, File.GetUnixFileMode(path)
                | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // best effort — a read-only bundle keeps its current mode
        }
    }

    /// <summary>Runs `ipatool --version`; returns the version line, or null plus the exit code.</summary>
    private static string? ProbeVersion(string path, out int exit)
    {
        exit = -1;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var stdout = proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(15_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
                exit = -2;
                return null;
            }

            exit = proc.ExitCode;
            var line = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(l => l.Contains("ipatool", StringComparison.OrdinalIgnoreCase));
            return exit == 0 ? line ?? "" : null;
        }
        catch (Exception ex)
        {
            exit = -3;
            Console.WriteLine($"ipatool probe failed: {ex.Message}");
            return null;
        }
    }

    private static int RunQuiet(string fileName, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc is null)
                return -1;
            proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(10_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return -2;
            }
            return proc.ExitCode;
        }
        catch
        {
            return -1;
        }
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
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            // The binary vanished or the OS refused to exec it: report it instead
            // of throwing into the action queue, where the reason would be lost.
            var msg = $"ipatool could not be started ({ex.GetType().Name}: {ex.Message})";
            onLine?.Invoke(msg);
            return new RunResult(-1, new[] { msg });
        }

        var lines = new List<string>();
        var outTask = PumpAsync(proc.StandardOutput, lines, onLine);
        var errTask = PumpAsync(proc.StandardError, lines, onLine);
        await Task.WhenAll(outTask, errTask);
        await proc.WaitForExitAsync();

        if (proc.ExitCode == KillExitCode && lines.Count == 0)
        {
            var where = OperatingSystem.IsMacOS()
                ? "macOS blocked the bundled binary (wrong platform build or an ad-hoc signature killed by Gatekeeper/AMFI)"
                : "the OS killed the ipatool binary — it is most likely a build for a different platform";
            var hint = $"ipatool was killed (exit 137): {where} — run `AppOogst --diagnose`";
            lines.Add(hint);
            onLine?.Invoke(hint);
        }

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

    /// <summary>Extracts the last human-readable error/message line from ipatool JSON output (login failures etc.).</summary>
    public static string ExtractLastError(List<JsonObject> objs)
    {
        string? msg = null;
        foreach (var o in objs)
        {
            var m = o["message"] ?? o["error"];
            if (m is JsonValue v && v.TryGetValue(out string? sv) && !string.IsNullOrWhiteSpace(sv))
                msg = sv;
        }
        return msg ?? "unknown error";
    }

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
