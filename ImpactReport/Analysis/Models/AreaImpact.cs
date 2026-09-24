namespace ImpactReport.Analysis.Models;

public sealed record AreaImpact(
    string Area,
    int ReferenceCount,
    int ProjectCount,
    int NearestDepth);
