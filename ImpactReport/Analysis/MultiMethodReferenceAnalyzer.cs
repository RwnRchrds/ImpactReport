using ImpactReport.Analysis.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace ImpactReport.Analysis;

public sealed record MethodImpact(string MethodDisplay, ImpactResult Result);

public sealed record MultiImpactResult(IReadOnlyList<MethodImpact> Methods);

public static class MultiMethodReferenceAnalyzer
{
    // ---------------------------
    // Pass A: Cheap pre-analysis
    // ---------------------------
    public static async Task<IReadOnlyList<MethodPreImpact>> PreAnalyzeManyAsync(
        Solution solution,
        IEnumerable<IMethodSymbol> methods)
    {
        var list = new List<MethodPreImpact>();

        foreach (IMethodSymbol method in methods.Distinct(SymbolEqualityComparer.Default))
        {
            // FindReferencesAsync returns IEnumerable<ReferencedSymbol>
            var refs = await SymbolFinder.FindReferencesAsync(method, solution).ConfigureAwait(false);

            var methodDef = method.OriginalDefinition;

            var exactLocations =
                refs.SelectMany(r =>
                        r.Definition is IMethodSymbol def &&
                        SymbolEqualityComparer.Default.Equals(def.OriginalDefinition, methodDef)
                            ? r.Locations
                            : Enumerable.Empty<ReferenceLocation>())
                    .ToList();

            var totalRefs = exactLocations.Count;

            var projectsImpacted = exactLocations
                .Select(l => l.Document?.Project?.Id)
                .Where(pid => pid is not null)
                .Distinct()
                .Count();

            var display = $"{method.ContainingType.ToDisplayString()}.{method.Name}(...)";

            list.Add(new MethodPreImpact(
                Method: method,
                MethodDisplay: display,
                TotalReferences: totalRefs,
                ProjectsImpacted: projectsImpacted));
        }

        return list
            .OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.TotalReferences)
            .ToList();
    }

    // ----------------------------------------
    // Apply filtering / top selection in one go
    // ----------------------------------------
    public static IReadOnlyList<MethodPreImpact> ApplyFiltersAndTakeTop(
        IReadOnlyList<MethodPreImpact> ranked,
        Cli.ImpactReportOptions options)
    {
        IEnumerable<MethodPreImpact> q = ranked;

        // Default: exclude zero-ref unless explicitly asked
        if (!options.IncludeZero)
            q = q.Where(x => x.TotalReferences > 0);

        if (options.MinRefs > 0)
            q = q.Where(x => x.TotalReferences >= options.MinRefs);

        if (options.MinProjects > 0)
            q = q.Where(x => x.ProjectsImpacted >= options.MinProjects);

        q = q.OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.TotalReferences);

        if (options is { All: false, Top: { } top })
            q = q.Take(top);

        return q.ToList();
    }

    // --------------------------
    // Pass B: Full deep analysis
    // --------------------------
    public static async Task<MultiImpactResult> AnalyzeManyAsync(
        Solution solution,
        IEnumerable<IMethodSymbol> methods,
        Areas.AreaMap areaMap,
        int maxCallSitesPerProject,
        Cli.ImpactReportOptions options)
    {
        var list = new List<MethodImpact>();

        foreach (var m in methods)
        {
            var r = await ReferenceAnalyzer.AnalyzeAsync(solution, m, areaMap, maxCallSitesPerProject);
            list.Add(new MethodImpact(r.ChangedMethodDisplay, r));
        }

        return new MultiImpactResult(list
            .OrderByDescending(x => x.Result.Projects.Sum(p => p.TotalReferences))
            .ToList());
    }
}