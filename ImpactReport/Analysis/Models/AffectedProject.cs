namespace ImpactReport.Analysis.Models;

public sealed record AffectedProject(
    string ProjectName,
    int CallSites,
    int MemberCount,
    int NearestDepth,
    IReadOnlyList<string> Areas);
