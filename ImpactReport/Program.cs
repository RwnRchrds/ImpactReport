using ImpactReport.Analysis;
using ImpactReport.Cli;
using ImpactReport.Git;
using ImpactReport.Reporting;
using ImpactReport.Utils;
using ImpactReport.Workspace;

namespace ImpactReport;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ImpactReportOptions.Parse(args);

            MsBuildBootstrapper.Register();

            using var workspace = await SolutionLoader.OpenAsync(options.SolutionPath);
            var solution = workspace.CurrentSolution;

            if (!options.ChangedMode)
            {
                var method = await SymbolResolver.FindMethodAsync(
                    solution,
                    options.TypeName!,
                    options.MethodName!);

                var result = await ReferenceAnalyzer.AnalyzeAsync(
                    solution,
                    method,
                    options.AreaMap,
                    options.MaxCallSitesPerProject);

                MarkdownReportWriter.WriteToFile(result, options.OutputPath);
                Console.WriteLine($"Wrote: {options.OutputPath}");
                return 0;
            }

            // Changed mode
            var repoRoot = GitRunner.GetRepoRoot();
            var changedFiles = ChangedFilesProvider.GetChangedCsFiles(options.BaseRef, repoRoot);

            Console.WriteLine($"Changed .cs files: {changedFiles.Count}");

            var changedMethods = await ChangedMethodDetector.FindChangedMethodsAsync(
                solution,
                changedFiles,
                repoRoot,
                options.IncludeTests);

            Console.WriteLine($"Changed methods detected: {changedMethods.Count}");

            var ranked = await MultiMethodReferenceAnalyzer.PreAnalyzeManyAsync(solution, changedMethods);

            var filtered = MultiMethodReferenceAnalyzer.ApplyFiltersAndTakeTop(ranked, options);

            Console.WriteLine($"Changed methods included in report: {filtered.Count}");

            var multi = await MultiMethodReferenceAnalyzer.AnalyzeManyAsync(
                solution,
                filtered.Select(x => x.Method),
                options.AreaMap,
                options.MaxCallSitesPerProject,
                options);

            MarkdownReportWriter.WriteToFile(multi, options.OutputPath);
            Console.WriteLine($"Wrote: {options.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}