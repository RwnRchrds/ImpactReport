using ImpactReport.Analysis.Models;
using ImpactReport.Areas;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace ImpactReport.Analysis;

public static class ReferenceAnalyzer
{
    public static async Task<ImpactResult> AnalyzeAsync(
        Solution solution,
        IMethodSymbol methodSymbol,
        AreaMap areaMap,
        int maxCallSitesPerProject,
        CancellationToken cancellationToken = default)
    {
        var changedMethodDisplay =
            $"{methodSymbol.ContainingType.ToDisplayString()}.{methodSymbol.Name}(...)";

        // IMPORTANT:
        // FindReferencesAsync can return results for related symbols (interfaces/base/overrides).
        // We only want references where the definition matches *this* method.
        var target = methodSymbol.OriginalDefinition;

        var references = await SymbolFinder
            .FindReferencesAsync(methodSymbol, solution, cancellationToken)
            .ConfigureAwait(false);

        var hits = new List<ReferenceHit>();

        foreach (var reference in references)
        {
            // Filter to exact method symbol only
            if (reference.Definition is not IMethodSymbol def ||
                !SymbolEqualityComparer.Default.Equals(def.OriginalDefinition, target))
            {
                continue;
            }

            foreach (var loc in reference.Locations)
            {
                if (loc.Location is null || !loc.Location.IsInSource)
                    continue;

                var doc = solution.GetDocument(loc.Document.Id);
                if (doc is null) continue;

                var projectName = doc.Project.Name;

                var sourceText = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var span = loc.Location.SourceSpan;

                if (span.Start < 0 || span.Start >= sourceText.Length)
                    continue;

                var linePos = sourceText.Lines.GetLinePosition(span.Start);
                var lineNumber = linePos.Line + 1;

                // Guard for safety
                if (linePos.Line < 0 || linePos.Line >= sourceText.Lines.Count)
                    continue;

                var lineText = sourceText.Lines[linePos.Line].ToString().Trim();

                var ns = await NamespaceResolver
                    .GetContainingNamespaceAsync(doc, span.Start)
                    .ConfigureAwait(false);

                var impactArea = areaMap.GuessArea(ns);

                hits.Add(new ReferenceHit(
                    ProjectName: projectName,
                    DocumentPath: doc.FilePath ?? doc.Name,
                    LineNumber: lineNumber,
                    LineText: lineText,
                    ContainingNamespace: ns,
                    ImpactArea: impactArea));
            }
        }

        var projects = hits
            .GroupBy(h => h.ProjectName)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var topNamespaces = g
                    .GroupBy(h => h.ContainingNamespace ?? "(unknown)")
                    .OrderByDescending(gg => gg.Count())
                    .Take(10)
                    .Select(gg => (Namespace: gg.Key, Count: gg.Count()))
                    .ToList();

                var samples = g
                    .Take(maxCallSitesPerProject)
                    .ToList();

                return new ProjectImpact(
                    ProjectName: g.Key,
                    TotalReferences: g.Count(),
                    TopNamespaces: topNamespaces,
                    SampleHits: samples);
            })
            .ToList();

        var areas = areaMap.HasMappings
            ? hits
                .Where(h => !string.IsNullOrWhiteSpace(h.ImpactArea))
                .GroupBy(h => h.ImpactArea!)
                .OrderByDescending(g => g.Count())
                .Select(g => new AreaImpact(g.Key, g.Count()))
                .ToList()
            : new List<AreaImpact>();

        return new ImpactResult(
            ChangedMethodDisplay: changedMethodDisplay,
            Projects: projects,
            Areas: areas);
    }
}
