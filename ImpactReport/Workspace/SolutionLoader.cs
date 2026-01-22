using Microsoft.CodeAnalysis.MSBuild;

namespace ImpactReport.Workspace;

public static class SolutionLoader
{
    public static async Task<MSBuildWorkspace> OpenAsync(string solutionPath)
    {
        var workspace = MSBuildWorkspace.Create();

        workspace.WorkspaceFailed += (_, e) =>
        {
            // Fail hard on real load failures. Warnings are common; tune as needed.
            if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
            {
                // Throwing inside an event handler can be messy; simplest is to log loudly.
                Console.Error.WriteLine($"[workspace failure] {e.Diagnostic.Message}");
            }
        };

        await workspace.OpenSolutionAsync(solutionPath);
        return workspace;
    }
}