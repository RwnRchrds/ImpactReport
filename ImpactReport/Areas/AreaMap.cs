using System.Text.Json;

namespace ImpactReport.Areas;

public sealed class AreaMap
{
    private readonly Dictionary<string, string> _prefixToArea;

    private AreaMap(Dictionary<string, string> prefixToArea)
    {
        _prefixToArea = prefixToArea;
    }

    public static AreaMap LoadOrEmpty(string? jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            return new AreaMap(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var json = File.ReadAllText(jsonPath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();

        return new AreaMap(new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase));
    }

    public bool HasMappings => _prefixToArea.Count > 0;

    public string? GuessArea(string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            return null;

        string? best = null;
        var bestLen = -1;

        foreach (var (prefix, area) in _prefixToArea)
        {
            if (@namespace.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > bestLen)
            {
                bestLen = prefix.Length;
                best = area;
            }
        }

        return best;
    }
}