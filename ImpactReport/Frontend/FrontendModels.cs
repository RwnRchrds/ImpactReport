namespace ImpactReport.Frontend;

public sealed record FrontendFile(
    string AbsolutePath,
    string RelativePath,
    string? Feature,
    IReadOnlyList<string> ApiSegments);

public sealed record FrontendChange(
    string RelativePath,
    string? Feature,
    IReadOnlyList<string> Areas,
    IReadOnlyList<string> ApiSegments);

public sealed record FrontendScreen(
    string Feature,
    IReadOnlyList<string> Areas,
    IReadOnlyList<string> ViaEndpoints,
    int FileCount);

public sealed record FrontendImpact(
    IReadOnlyList<FrontendChange> Changes,
    IReadOnlyList<FrontendScreen> Screens,
    IReadOnlyList<string> UnmatchedEndpoints,
    int FilesScanned)
{
    public IReadOnlyList<string> Areas =>
        Changes.SelectMany(c => c.Areas)
            .Concat(Screens.SelectMany(s => s.Areas))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();

    public bool HasAnything => Changes.Count > 0 || Screens.Count > 0;

    public static FrontendImpact Empty { get; } = new([], [], [], 0);
}
