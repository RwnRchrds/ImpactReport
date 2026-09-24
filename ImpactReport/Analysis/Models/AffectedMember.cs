namespace ImpactReport.Analysis.Models;

public sealed record AffectedMember(
    string Key,
    string TypeName,
    string MemberName,
    string ProjectName,
    string? Namespace,
    int NearestDepth,
    int CallSites,
    IReadOnlyList<string> Areas,
    bool IsEntryPoint,
    bool BeyondDepthLimit)
{
    public string Display => $"{ShortType}.{MemberName}";

    public string ShortType
    {
        get
        {
            var lastDot = TypeName.LastIndexOf('.');
            return lastDot < 0 ? TypeName : TypeName[(lastDot + 1)..];
        }
    }
}
