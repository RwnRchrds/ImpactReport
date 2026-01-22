namespace ImpactReport.Analysis.Models;

public sealed record ImpactResult(
    string ChangedMethodDisplay,
    IReadOnlyList<ProjectImpact> Projects,
    IReadOnlyList<AreaImpact> Areas);