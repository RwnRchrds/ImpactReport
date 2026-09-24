using Microsoft.CodeAnalysis;

namespace ImpactReport.Analysis;

public sealed record DispatchKeys(
    ISet<string> Direct,
    ISet<string> Interface,
    ISet<string> InterfaceOriginal)
{
    public bool NeedsCallSiteCheck(IMethodSymbol definition) =>
        InterfaceOriginal.Contains(DispatchSet.KeyOf(definition));

    public bool MatchesDirectly(IMethodSymbol definition) =>
        Direct.Contains(DispatchSet.KeyOf(definition)) ||
        Interface.Contains(DispatchSet.ConstructedKeyOf(definition));

    public bool MatchesCallSite(IMethodSymbol invoked) =>
        Interface.Contains(DispatchSet.ConstructedKeyOf(invoked)) ||
        Direct.Contains(DispatchSet.KeyOf(invoked));
}

public static class DispatchSet
{
    public static DispatchKeys For(IMethodSymbol method)
    {
        var direct = new HashSet<string>(StringComparer.Ordinal);
        var interfaceKeys = new HashSet<string>(StringComparer.Ordinal);
        var interfaceOriginal = new HashSet<string>(StringComparer.Ordinal);

        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<IMethodSymbol>();

        queue.Enqueue(method.OriginalDefinition);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!seen.Add(current))
                continue;

            direct.Add(KeyOf(current));

            if (current.OverriddenMethod is { } overridden)
                queue.Enqueue(overridden.OriginalDefinition);

            foreach (var member in InterfaceMembers(current))
            {
                interfaceKeys.Add(ConstructedKeyOf(member));
                interfaceOriginal.Add(KeyOf(member));
            }
        }

        return new DispatchKeys(direct, interfaceKeys, interfaceOriginal);
    }

    private static IEnumerable<IMethodSymbol> InterfaceMembers(IMethodSymbol method)
    {
        var type = method.ContainingType;
        if (type is null)
            yield break;

        foreach (var explicitImpl in method.ExplicitInterfaceImplementations)
            yield return explicitImpl;

        foreach (var @interface in type.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                var implementation = type.FindImplementationForInterfaceMember(member);

                if (implementation is not null &&
                    SymbolEqualityComparer.Default.Equals(implementation.OriginalDefinition, method.OriginalDefinition))
                {
                    yield return member;
                }
            }
        }
    }

    public static string KeyOf(IMethodSymbol method) => Render(method.OriginalDefinition, includeTypeArguments: false);

    public static string ConstructedKeyOf(IMethodSymbol method) =>
        Render(method.ConstructedFrom ?? method, includeTypeArguments: true);

    private static string Render(IMethodSymbol method, bool includeTypeArguments)
    {
        var type = method.ContainingType;

        var declaringType = type is null
            ? "?"
            : type.OriginalDefinition.ToDisplayString(TypeFormat);

        var arguments = includeTypeArguments && type is { IsGenericType: true }
            ? "<" + string.Join(",", type.TypeArguments.Select(a => a.Name)) + ">"
            : string.Empty;

        var parameters = string.Join(",", method.Parameters.Select(p => p.Type.Name));

        return $"{declaringType}{arguments}.{method.Name}({parameters})";
    }

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
}
