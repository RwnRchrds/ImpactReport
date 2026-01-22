namespace ImpactReport.Analysis.Models;

public sealed record ProjectImpact(
    string ProjectName,
    int TotalReferences,
    IReadOnlyList<(string Namespace, int Count)> TopNamespaces,
    IReadOnlyList<ReferenceHit> SampleHits);