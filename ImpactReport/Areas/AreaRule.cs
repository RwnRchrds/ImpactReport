using System.Text.Json.Serialization;

namespace ImpactReport.Areas;

public sealed class AreaRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespaces")]
    public List<string> Namespaces { get; set; } = [];

    [JsonPropertyName("paths")]
    public List<string> Paths { get; set; } = [];

    [JsonPropertyName("projects")]
    public List<string> Projects { get; set; } = [];

    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = [];
}

public sealed class AreaMapFile
{
    [JsonPropertyName("areas")]
    public List<AreaRule> Areas { get; set; } = [];
}
