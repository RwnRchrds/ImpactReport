using System.Text.Json;
using System.Text.RegularExpressions;
using ImpactReport.Utils;

namespace ImpactReport.Areas;

public sealed class AreaMap
{
    private readonly IReadOnlyList<CompiledRule> _rules;

    private AreaMap(IReadOnlyList<CompiledRule> rules) => _rules = rules;

    public static AreaMap Empty { get; } = new([]);

    public bool HasMappings => _rules.Count > 0;

    public IEnumerable<string> AreaNames => _rules.Select(r => r.Name);

    public static AreaMap LoadOrEmpty(string? jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            return Empty;

        if (!File.Exists(jsonPath))
            throw new CommandLineException($"Areas file not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);

        List<AreaRule> rules;
        try
        {
            rules = Parse(json);
        }
        catch (JsonException ex)
        {
            throw new CommandLineException($"Could not parse areas file '{jsonPath}': {ex.Message}");
        }

        return new AreaMap(rules.Select(CompiledRule.From).ToList());
    }

    private static List<AreaRule> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("areas", out var areas) &&
            areas.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<AreaRule>>(areas.GetRawText(), JsonOptions) ?? [];
        }

        var legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];

        return legacy
            .GroupBy(kvp => kvp.Value, StringComparer.Ordinal)
            .Select(g => new AreaRule { Name = g.Key, Namespaces = g.Select(kvp => kvp.Key).ToList() })
            .ToList();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public IReadOnlyList<string> MatchAreas(string? @namespace, string? filePath, string? projectName, string? typeName)
    {
        if (_rules.Count == 0)
            return [];

        var path = filePath is null ? null : PathNormalizer.ToForwardSlashes(filePath);

        return _rules
            .Where(r => r.Matches(@namespace, path, projectName, typeName))
            .Select(r => r.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private sealed record CompiledRule(
        string Name,
        IReadOnlyList<string> Namespaces,
        IReadOnlyList<Regex> Paths,
        IReadOnlyList<string> Projects,
        IReadOnlyList<string> Types)
    {
        public static CompiledRule From(AreaRule rule) => new(
            Name: rule.Name,
            Namespaces: rule.Namespaces,
            Paths: rule.Paths.Select(GlobToRegex).ToList(),
            Projects: rule.Projects,
            Types: rule.Types);

        public bool Matches(string? @namespace, string? path, string? project, string? type)
        {
            if (@namespace is not null && Namespaces.Any(p => IsNamespacePrefix(@namespace, p)))
                return true;

            if (path is not null && Paths.Any(r => r.IsMatch(path)))
                return true;

            if (project is not null && Projects.Any(p => p.Equals(project, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (type is not null && Types.Any(t => type.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        private static bool IsNamespacePrefix(string @namespace, string prefix)
        {
            if (!@namespace.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            return @namespace.Length == prefix.Length || @namespace[prefix.Length] == '.';
        }
    }

    internal static Regex GlobToRegex(string glob)
    {
        var normalised = glob.Replace('\\', '/');
        var pattern = new System.Text.StringBuilder("^");

        for (var i = 0; i < normalised.Length; i++)
        {
            var c = normalised[i];
            switch (c)
            {
                case '*' when i + 1 < normalised.Length && normalised[i + 1] == '*':
                    if (i + 2 < normalised.Length && normalised[i + 2] == '/')
                    {
                        pattern.Append("(?:.*/)?");
                        i += 2;
                    }
                    else
                    {
                        pattern.Append(".*");
                        i++;
                    }
                    break;

                case '*':
                    pattern.Append("[^/]*");
                    break;

                case '?':
                    pattern.Append("[^/]");
                    break;

                default:
                    pattern.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        pattern.Append('$');

        return new Regex(pattern.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
