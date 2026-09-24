namespace ImpactReport.Analysis.Models;

public sealed record ImpactResult(
    string ChangedMethodDisplay,
    IReadOnlyList<ProjectImpact> Projects,
    IReadOnlyList<AreaImpact> Areas,
    IReadOnlyList<AffectedMember> Members,
    int TotalReferences,
    int MaxDepthReached,
    bool Truncated)
{
    public static ImpactResult Empty(string display) =>
        new(display, [], [], [], 0, 0, false);
}
