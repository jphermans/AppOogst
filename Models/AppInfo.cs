using System.Text.Json.Nodes;

namespace AppOogst.Models;

public sealed class AppInfo
{
    public long Id { get; init; }
    public string BundleId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Price { get; init; } = "";
    public IReadOnlyList<string> Platforms { get; init; } = Array.Empty<string>();

    public string PlatformsText => string.Join(", ", Platforms);
    public string PriceText => Price is "0" or "free" or "0.0" ? "free" : Price;

    public static AppInfo FromJson(JsonObject o)
    {
        var plats = new List<string>();
        if (o["platforms"] is JsonArray arr)
            foreach (var n in arr)
                if (n?.ToString() is { } s)
                    plats.Add(s);

        return new AppInfo
        {
            Id = o["id"] is JsonNode id && long.TryParse(id.ToString(), out var l) ? l : 0,
            BundleId = o["bundleID"]?.ToString() ?? "",
            Name = o["name"]?.ToString() ?? "",
            Version = o["version"]?.ToString() ?? "",
            Price = o["price"]?.ToString() ?? "",
            Platforms = plats,
        };
    }
}
