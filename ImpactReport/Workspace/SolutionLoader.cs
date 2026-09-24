using ImpactReport.Cli;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ImpactReport.Workspace;

public static class SolutionLoader
{
    public static async Task<MSBuildWorkspace> OpenAsync(
        string solutionPath,
        ConsoleProgress progress,
        CancellationToken cancellationToken = default)
    {
        var workspace = MSBuildWorkspace.Create();
        var failures = new List<string>();

        using (workspace.RegisterWorkspaceFailedHandler(e =>
               {
                   if (e.Diagnostic.Kind != WorkspaceDiagnosticKind.Failure)
                       return;

                   lock (failures)
                   {
                       failures.Add(e.Diagnostic.Message);
                   }
               }))
        {
            await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        var projectCount = workspace.CurrentSolution.Projects.Count();
        var documentCount = workspace.CurrentSolution.Projects.Sum(p => p.Documents.Count());

        progress.Report($"Loaded {projectCount} project(s), {documentCount} document(s).");

        ReportFailures(failures, projectCount);

        return workspace;
    }

    private static void ReportFailures(List<string> failures, int projectCount)
    {
        if (failures.Count == 0)
            return;

        Console.Error.WriteLine();
        Console.Error.WriteLine($"WARNING: {failures.Count} project(s) failed to load. Results may be incomplete.");

        foreach (var message in failures.Take(5))
            Console.Error.WriteLine($"  - {message}");

        if (failures.Count > 5)
            Console.Error.WriteLine($"  ... and {failures.Count - 5} more");

        if (projectCount == 0)
            Console.Error.WriteLine("  No projects loaded at all - check that the required SDKs are installed.");

        Console.Error.WriteLine();
    }
}
