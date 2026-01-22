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
        int maxCallSitesPerProject)
    {
        var changedMethodDisplay = $"{methodSymbol.ContainingType.ToDisplayString()}.{methodSymbol.Name}(...)";

        var references = await SymbolFinder.FindReferencesAsync(methodSymbol, solution);

        var hits = new List<ReferenceHit>();

        foreach (var reference in references)
        {
            foreach (var loc in reference.Locations)
            {
                var doc = solution.GetDocument(loc.Document.Id);
                if (doc is null) continue;

                var projectName = doc.Project.Name;

                var sourceText = await doc.GetTextAsync();
                var span = loc.Location.SourceSpan;

                var linePos = sourceText.Lines.GetLinePosition(span.Start);
                var lineNumber = linePos.Line + 1;
                var lineText = sourceText.Lines[linePos.Line].ToString().Trim();

                var ns = await NamespaceResolver.GetContainingNamespaceAsync(doc, span.Start);
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