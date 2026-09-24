using ImpactReport.Utils;
using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public static class SymbolResolver
{
    public static async Task<IMethodSymbol> FindMethodAsync(
        Solution solution,
        string typeFullName,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        var typeFound = false;
        IMethodSymbol? fromElsewhere = null;

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
                continue;

            var type = compilation.GetTypeByMetadataName(typeFullName);
            if (type is null)
                continue;

            typeFound = true;

            var method = SelectOverload(type, methodName);
            if (method is null)
                continue;

            if (!SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, compilation.Assembly))
            {
                fromElsewhere ??= method;
                continue;
            }

            Warn(typeFullName, methodName, type);
            return method;
        }

        if (fromElsewhere is not null)
        {
            Console.Error.WriteLine(
                $"Warning: '{typeFullName}' was only reachable through a project that references it, not through " +
                "the project that declares it. That project most likely failed to load, so the report will be " +
                "empty. Restore packages and try again.");

            return fromElsewhere;
        }

        throw new CommandLineException(typeFound
            ? $"Type '{typeFullName}' was found, but it has no method named '{methodName}'."
            : $"Could not find type '{typeFullName}' in the solution. Use the fully qualified name, " +
              "e.g. MyApp.Services.OrderService.");
    }

    private static IMethodSymbol? SelectOverload(INamedTypeSymbol type, string methodName)
    {
        var methods = type.GetMembers(methodName).OfType<IMethodSymbol>().ToList();

        if (methods.Count == 0)
            return null;

        return methods.FirstOrDefault(m => m.MethodKind == MethodKind.Ordinary) ?? methods[0];
    }

    private static void Warn(string typeFullName, string methodName, INamedTypeSymbol type)
    {
        var count = type.GetMembers(methodName).OfType<IMethodSymbol>().Count();

        if (count > 1)
        {
            Console.Error.WriteLine(
                $"Note: '{typeFullName}.{methodName}' has {count} overloads; analysing the first ordinary one.");
        }
    }
}
