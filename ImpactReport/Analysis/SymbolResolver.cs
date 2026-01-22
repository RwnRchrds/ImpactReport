using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public static class SymbolResolver
{
    public static async Task<IMethodSymbol> FindMethodAsync(
        Solution solution,
        string typeFullName,
        string methodName)
    {
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
                continue;

            var type = compilation.GetTypeByMetadataName(typeFullName);
            if (type is null)
                continue;

            var methods = type.GetMembers(methodName).OfType<IMethodSymbol>().ToList();
            if (methods.Count == 0)
                continue;

            var candidate =
                methods.FirstOrDefault(m => m.MethodKind == MethodKind.Ordinary)
                ?? methods.First();

            return candidate;
        }

        throw new InvalidOperationException($"Could not find method '{methodName}' on type '{typeFullName}'.");
    }
}