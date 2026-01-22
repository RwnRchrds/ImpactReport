using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis.Models;

public sealed record MethodPreImpact(
    IMethodSymbol Method,
    string MethodDisplay,
    int TotalReferences,
    int ProjectsImpacted)
{
    public int RiskScore => TotalReferences * ProjectsImpacted;
}