using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ImpactReport.Tests;

public sealed class TestSolution : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private readonly Dictionary<string, ProjectId> _projects = new(StringComparer.Ordinal);

    private static readonly IReadOnlyList<MetadataReference> FrameworkReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
        .ToList();

    public Solution Solution => _workspace.CurrentSolution;

    public TestSolution AddProject(string name, params string[] references)
    {
        var projectId = ProjectId.CreateNewId(name);

        var info = ProjectInfo
            .Create(projectId, VersionStamp.Create(), name, name, LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(FrameworkReferences)
            .WithProjectReferences(references.Select(r => new ProjectReference(_projects[r])));

        _workspace.TryApplyChanges(_workspace.CurrentSolution.AddProject(info));
        _projects[name] = projectId;

        return this;
    }

    public TestSolution AddDocument(string projectName, string fileName, string source)
    {
        var projectId = _projects[projectName];

        var filePath = Path.Combine(Path.GetTempPath(), "impactreport-tests", projectName, fileName);

        var updated = _workspace.CurrentSolution.AddDocument(
            DocumentId.CreateNewId(projectId),
            Path.GetFileName(fileName),
            Microsoft.CodeAnalysis.Text.SourceText.From(source),
            filePath: filePath);

        _workspace.TryApplyChanges(updated);

        return this;
    }

    public async Task<IMethodSymbol> GetMethodAsync(string projectName, string typeName, string methodName)
    {
        var project = Solution.GetProject(_projects[projectName])!;
        var compilation = await project.GetCompilationAsync();

        var type = compilation!.GetTypeByMetadataName(typeName)
                   ?? throw new InvalidOperationException($"Type '{typeName}' not found in '{projectName}'.");

        return type.GetMembers(methodName).OfType<IMethodSymbol>().First();
    }

    public async Task AssertCompilesAsync()
    {
        foreach (var project in Solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();

            var errors = compilation!.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{project.Name}: {d}")
                .ToList();

            if (errors.Count > 0)
                throw new InvalidOperationException("Test sources did not compile:\n" + string.Join("\n", errors));
        }
    }

    public void Dispose() => _workspace.Dispose();
}
