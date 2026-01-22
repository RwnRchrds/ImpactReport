using ImpactReport.Analysis;
using ImpactReport.Cli;
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

            var method = await SymbolResolver.FindMethodAsync(
                workspace.CurrentSolution,
                options.TypeName,
                options.MethodName);

            var result = await ReferenceAnalyzer.AnalyzeAsync(
                workspace.CurrentSolution,
                method,
                options.AreaMap,
                options.MaxCallSitesPerProject);

            MarkdownReportWriter.WriteToFile(result, options.OutputPath);

            Console.WriteLine($"Wrote: {options.OutputPath}");
            Console.WriteLine($"Projects impacted: {result.Projects.Count}");
            foreach (var p in result.Projects.OrderBy(p => p.ProjectName))
                Console.WriteLine($"- {p.ProjectName}: {p.TotalReferences} reference(s)");

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}