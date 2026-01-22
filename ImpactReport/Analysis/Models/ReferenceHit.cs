namespace ImpactReport.Analysis.Models;

public sealed record ReferenceHit(
    string ProjectName,
    string DocumentPath,
    int LineNumber,
    string LineText,
    string? ContainingNamespace,
    string? ImpactArea);