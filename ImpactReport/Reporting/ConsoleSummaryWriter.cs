using ImpactReport.Analysis;
using ImpactReport.Analysis.Models;
using ImpactReport.Frontend;

namespace ImpactReport.Reporting;

public static class ConsoleSummaryWriter
{
    private const int MaxEntryPointsShown = 8;

    public static void Write(MultiImpactResult multi, string outputPath, bool hasAreaMap)
        => Write(multi, FrontendImpact.Empty, outputPath, hasAreaMap);

    public static void Write(MultiImpactResult multi, FrontendImpact frontend, string outputPath, bool hasAreaMap)
    {
        Console.WriteLine();
        WriteAreas(multi.Areas, frontend, hasAreaMap);
        WriteProjects(multi.Projects);
        WriteEntryPoints(multi.EntryPoints);
        WriteFrontend(frontend);

        Console.WriteLine(
            $"Methods analysed: {multi.Methods.Count} of {multi.MethodsDetected} changed  |  " +
            $"Affected members: {multi.Members.Count}  |  Call sites: {multi.Members.Sum(m => m.CallSites)}");

        WriteRiskiest(multi);

        if (multi.Truncated)
            Console.WriteLine("Note: the call graph hit --max-nodes; results are partial. Raise it for full coverage.");

        Console.WriteLine();
        Console.WriteLine($"Full report: {outputPath}");
    }

    public static void Write(ImpactResult result, string outputPath, bool hasAreaMap)
        => Write(result, FrontendImpact.Empty, outputPath, hasAreaMap);

    public static void Write(ImpactResult result, FrontendImpact frontend, string outputPath, bool hasAreaMap)
    {
        Console.WriteLine();
        WriteAreas(result.Areas, frontend, hasAreaMap);
        WriteProjects(ReferenceAnalyzer.BuildAffectedProjects(result.Members));
        WriteEntryPoints(result.Members.Where(m => m.IsEntryPoint).ToList());
        WriteFrontend(frontend);

        Console.WriteLine($"Affected members: {result.Members.Count}  |  Call sites: {result.TotalReferences}");
        Console.WriteLine();
        Console.WriteLine($"Full report: {outputPath}");
    }

    private static void WriteAreas(IReadOnlyList<AreaImpact> areas, FrontendImpact frontend, bool hasAreaMap)
    {
        var combined = areas.Select(a => a.Area)
            .Concat(frontend.Areas)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (combined.Count > 0)
        {
            Console.WriteLine($"Impact Areas: {string.Join(", ", combined)}");
            Console.WriteLine();
            return;
        }

        Console.WriteLine(hasAreaMap
            ? "Impact Areas: none matched (no call site fell inside a mapped area)"
            : "Impact Areas: not configured - pass --areas <file> to name the areas of your codebase");
        Console.WriteLine();
    }

    private static void WriteProjects(IReadOnlyList<AffectedProject> projects)
    {
        if (projects.Count == 0)
        {
            Console.WriteLine("Affected Projects: none");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("Affected Projects:");

        foreach (var project in projects)
        {
            Console.WriteLine(
                $"  {project.ProjectName,-40} {project.MemberCount,4} member(s)  " +
                $"{project.CallSites,4} call site(s)  nearest hop {project.NearestDepth}");
        }

        Console.WriteLine();
    }

    private static void WriteEntryPoints(IReadOnlyList<AffectedMember> entryPoints)
    {
        if (entryPoints.Count == 0)
            return;

        Console.WriteLine("Affected entry points (nothing else calls these):");

        foreach (var member in entryPoints.Take(MaxEntryPointsShown))
        {
            var areas = member.Areas.Count > 0 ? $"  [{string.Join(", ", member.Areas)}]" : string.Empty;
            Console.WriteLine($"  {member.Display,-50} hop {member.NearestDepth}  {member.ProjectName}{areas}");
        }

        if (entryPoints.Count > MaxEntryPointsShown)
            Console.WriteLine($"  ... and {entryPoints.Count - MaxEntryPointsShown} more (see the report)");

        Console.WriteLine();
    }

    private static void WriteFrontend(FrontendImpact frontend)
    {
        if (!frontend.HasAnything)
            return;

        if (frontend.Screens.Count > 0)
        {
            Console.WriteLine("Frontend screens calling affected endpoints:");

            foreach (var screen in frontend.Screens.Take(MaxEntryPointsShown))
            {
                var areas = screen.Areas.Count > 0 ? $"  [{string.Join(", ", screen.Areas)}]" : string.Empty;

                Console.WriteLine(
                    $"  {screen.Feature,-40} {screen.FileCount,3} file(s)  " +
                    $"via {string.Join(", ", screen.ViaEndpoints)}{areas}");
            }

            if (frontend.Screens.Count > MaxEntryPointsShown)
                Console.WriteLine($"  ... and {frontend.Screens.Count - MaxEntryPointsShown} more (see the report)");

            Console.WriteLine();
        }

        if (frontend.Changes.Count > 0)
        {
            var features = frontend.Changes
                .Select(c => c.Feature ?? "(outside a feature folder)")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            Console.WriteLine(
                $"Frontend files changed: {frontend.Changes.Count} in {string.Join(", ", features)}");
            Console.WriteLine();
        }
    }

    private static void WriteRiskiest(MultiImpactResult multi)
    {
        var riskiest = multi.Methods.FirstOrDefault();
        if (riskiest is null || riskiest.Result.TotalReferences == 0)
            return;

        Console.WriteLine(
            $"Highest fan-out: {riskiest.MethodDisplay} ({riskiest.Result.TotalReferences} call sites " +
            $"across {riskiest.Result.Projects.Count} project(s))");
    }
}
