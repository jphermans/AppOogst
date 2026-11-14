using Avalonia;
using Avalonia.Styling;

namespace AppOogst.Services;

/// <summary>Applies and persists the app theme choice (System / Dark / Light).</summary>
public static class ThemeManager
{
    public static readonly string[] ThemeChoices = { "System", "Dark", "Light" };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AppOogst", "theme.txt");

    public static int Load()
    {
        try
        {
            var v = int.Parse(File.ReadAllText(StorePath).Trim());
            return v is >= 0 and <= 2 ? v : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void Save(int index)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, index.ToString());
        }
        catch
        {
            // persistence is best-effort
        }
    }

    public static ThemeVariant ResolveVariant(int index) => index switch
    {
        1 => ThemeVariant.Dark,
        2 => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

    public static void Apply(int index)
    {
        if (Application.Current is not { } app)
            return; // headless (selftest): no application instance
        app.RequestedThemeVariant = ResolveVariant(index);
        Save(index);
    }
}
