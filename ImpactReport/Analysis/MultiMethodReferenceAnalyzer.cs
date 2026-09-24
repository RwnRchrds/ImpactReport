using ImpactReport.Analysis.Models;
using ImpactReport.Areas;
using ImpactReport.Cli;
using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public sealed record MethodImpact(string MethodDisplay, ImpactResult Result);

public sealed record MultiImpactResult(
    IReadOnlyList<MethodImpact> Methods,
    IReadOnlyList<AreaImpact> Areas,
    IReadOnlyList<AffectedProject> Projects,
    IReadOnlyList<AffectedMember> Members,
    int MethodsDetected,
    bool Truncated)
{
    public IReadOnlyList<AffectedMember> EntryPoints =>
        Members.Where(m => m.IsEntryPoint).ToList();
}

public static class MultiMethodReferenceAnalyzer
{
    public static async Task<IReadOnlyList<MethodPreImpact>> PreAnalyzeManyAsync(
        IEnumerable<IMethodSymbol> methods,
        ReferenceFinder finder,
        bool includeTests,
        CancellationToken cancellationToken = default)
    {
        var list = new List<MethodPreImpact>();

        foreach (var method in Distinct(methods))
        {
            var locations = await finder.FindAsync(method, cancellationToken).ConfigureAwait(false);

            var relevant = includeTests
                ? locations
                : locations.Where(l => !CallGraphWalker.IsTestProject(l.Document.Project)).ToList();

            list.Add(new MethodPreImpact(
                Method: method,
                MethodDisplay: ReferenceAnalyzer.Display(method),
                TotalReferences: relevant.Count,
                ProjectsImpacted: relevant.Select(l => l.Document.Project.Id).Distinct().Count()));
        }

        return Rank(list).ToList();
    }

    public static IReadOnlyList<MethodPreImpact> ApplyFiltersAndTakeTop(
        IReadOnlyList<MethodPreImpact> ranked,
        ImpactReportOptions options)
    {
        IEnumerable<MethodPreImpact> query = ranked;

        if (!options.IncludeZero)
            query = query.Where(x => x.TotalReferences > 0);

        if (options.MinRefs > 0)
            query = query.Where(x => x.TotalReferences >= options.MinRefs);

        if (options.MinProjects > 0)
            query = query.Where(x => x.ProjectsImpacted >= options.MinProjects);

        query = Rank(query);

        if (options is { All: false, Top: { } top })
            query = query.Take(top);

        return query.ToList();
    }

    public static async Task<MultiImpactResult> AnalyzeManyAsync(
        IEnumerable<IMethodSymbol> methods,
        ReferenceFinder finder,
        AreaMap areaMap,
        CallGraphOptions options,
        int methodsDetected,
        CancellationToken cancellationToken = default)
    {
        var list = new List<MethodImpact>();

        foreach (var method in methods)
        {
            var result = await ReferenceAnalyzer
                .AnalyzeAsync(method, finder, areaMap, options, cancellationToken)
                .ConfigureAwait(false);

            list.Add(new MethodImpact(result.ChangedMethodDisplay, result));
        }

        var ordered = list
            .OrderByDescending(x => x.Result.TotalReferences)
            .ThenBy(x => x.MethodDisplay, StringComparer.Ordinal)
            .ToList();

        var members = ReferenceAnalyzer.MergeMembers(ordered.SelectMany(m => m.Result.Members));

        return new MultiImpactResult(
            Methods: ordered,
            Areas: MergeAreas(ordered),
            Projects: ReferenceAnalyzer.BuildAffectedProjects(members),
            Members: members,
            MethodsDetected: methodsDetected,
            Truncated: ordered.Any(x => x.Result.Truncated));
    }

    public static IReadOnlyList<AreaImpact> MergeAreas(IEnumerable<MethodImpact> methods) =>
        methods
            .SelectMany(m => m.Result.Areas)
            .GroupBy(a => a.Area, StringComparer.Ordinal)
            .Select(g => new AreaImpact(
                Area: g.Key,
                ReferenceCount: g.Sum(a => a.ReferenceCount),
                ProjectCount: g.Max(a => a.ProjectCount),
                NearestDepth: g.Min(a => a.NearestDepth)))
            .OrderBy(a => a.NearestDepth)
            .ThenByDescending(a => a.ReferenceCount)
            .ThenBy(a => a.Area, StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<IMethodSymbol> Distinct(IEnumerable<IMethodSymbol> methods) =>
        methods
            .GroupBy(DispatchSet.KeyOf, StringComparer.Ordinal)
            .Select(g => g.First());

    private static IOrderedEnumerable<MethodPreImpact> Rank(IEnumerable<MethodPreImpact> methods) =>
        methods
            .OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.TotalReferences)
            .ThenBy(x => x.MethodDisplay, StringComparer.Ordinal);
}
