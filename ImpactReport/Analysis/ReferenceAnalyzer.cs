using ImpactReport.Analysis.Models;
using ImpactReport.Areas;
using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public static class ReferenceAnalyzer
{
    public static async Task<ImpactResult> AnalyzeAsync(
        IMethodSymbol methodSymbol,
        ReferenceFinder finder,
        AreaMap areaMap,
        CallGraphOptions options,
        CancellationToken cancellationToken = default)
    {
        var display = Display(methodSymbol);

        var walker = new CallGraphWalker(finder, areaMap, options);
        var walk = await walker.WalkAsync(methodSymbol, cancellationToken).ConfigureAwait(false);

        return new ImpactResult(
            ChangedMethodDisplay: display,
            Projects: BuildProjects(walk.Hits, options.MaxCallSitesPerProject),
            Areas: BuildAreas(walk.Hits),
            Members: walk.Members,
            TotalReferences: walk.Hits.Count,
            MaxDepthReached: walk.MaxDepthReached,
            Truncated: walk.Truncated);
    }

    public static string Display(IMethodSymbol method) =>
        method.ToDisplayString(DisplayFormat);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
                       | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static IReadOnlyList<ProjectImpact> BuildProjects(
        IReadOnlyList<ReferenceHit> hits,
        int maxCallSitesPerProject) =>
        hits
            .GroupBy(h => h.ProjectName, StringComparer.Ordinal)
            .Select(g => new ProjectImpact(
                ProjectName: g.Key,
                TotalReferences: g.Count(),
                TopNamespaces: g
                    .GroupBy(h => h.ContainingNamespace ?? "(unknown)", StringComparer.Ordinal)
                    .OrderByDescending(ns => ns.Count())
                    .ThenBy(ns => ns.Key, StringComparer.Ordinal)
                    .Take(10)
                    .Select(ns => (Namespace: ns.Key, Count: ns.Count()))
                    .ToList(),
                SampleHits: g
                    .OrderBy(h => h.Depth)
                    .ThenBy(h => h.DocumentPath, StringComparer.Ordinal)
                    .ThenBy(h => h.LineNumber)
                    .Take(maxCallSitesPerProject)
                    .ToList()))
            .OrderByDescending(p => p.TotalReferences)
            .ThenBy(p => p.ProjectName, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<AreaImpact> BuildAreas(IReadOnlyList<ReferenceHit> hits) =>
        hits
            .SelectMany(h => h.Areas.Select(area => (Area: area, Hit: h)))
            .GroupBy(x => x.Area, StringComparer.Ordinal)
            .Select(g => new AreaImpact(
                Area: g.Key,
                ReferenceCount: g.Count(),
                ProjectCount: g.Select(x => x.Hit.ProjectName).Distinct(StringComparer.Ordinal).Count(),
                NearestDepth: g.Min(x => x.Hit.Depth)))
            .OrderBy(a => a.NearestDepth)
            .ThenByDescending(a => a.ReferenceCount)
            .ThenBy(a => a.Area, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<AffectedProject> BuildAffectedProjects(IEnumerable<AffectedMember> members) =>
        members
            .GroupBy(m => m.ProjectName, StringComparer.Ordinal)
            .Select(g => new AffectedProject(
                ProjectName: g.Key,
                CallSites: g.Sum(m => m.CallSites),
                MemberCount: g.Count(),
                NearestDepth: g.Min(m => m.NearestDepth),
                Areas: g.SelectMany(m => m.Areas)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(a => a, StringComparer.Ordinal)
                        .ToList()))
            .OrderBy(p => p.NearestDepth)
            .ThenByDescending(p => p.CallSites)
            .ThenBy(p => p.ProjectName, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<AffectedMember> MergeMembers(IEnumerable<AffectedMember> members) =>
        members
            .GroupBy(m => m.Key, StringComparer.Ordinal)
            .Select(g => Merge(g.ToList()))
            .OrderBy(m => m.NearestDepth)
            .ThenByDescending(m => m.CallSites)
            .ThenBy(m => m.TypeName, StringComparer.Ordinal)
            .ThenBy(m => m.MemberName, StringComparer.Ordinal)
            .ToList();

    private static AffectedMember Merge(IReadOnlyList<AffectedMember> occurrences)
    {
        var searched = occurrences.Where(m => !m.BeyondDepthLimit).ToList();
        var hasCallers = searched.Any(m => !m.IsEntryPoint);

        return occurrences[0] with
        {
            CallSites = occurrences.Sum(m => m.CallSites),
            NearestDepth = occurrences.Min(m => m.NearestDepth),
            BeyondDepthLimit = searched.Count == 0,
            IsEntryPoint = searched.Count > 0 && !hasCallers,
            Areas = occurrences
                .SelectMany(m => m.Areas)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToList()
        };
    }
}
