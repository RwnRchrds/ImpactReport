using ImpactReport.Analysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ImpactReport.Tests;

public class DispatchSetTests
{
    private const string Source = """
        namespace App;

        public interface IThing
        {
            string Get(int id);
        }

        public abstract class ThingBase
        {
            public abstract string Get(int id);
        }

        public class Thing : ThingBase, IThing
        {
            public override string Get(int id) => id.ToString();
            public string Get(string id) => id;
        }
        """;

    private static async Task<IMethodSymbol> GetAsync(TestSolution solution, string typeName, int overloadIndex = 0)
    {
        var method = await solution.GetMethodAsync("App", typeName, "Get");

        if (overloadIndex == 0)
            return method;

        var type = method.ContainingType;
        return type.GetMembers("Get").OfType<IMethodSymbol>().ElementAt(overloadIndex);
    }

    private static TestSolution Build()
    {
        var solution = new TestSolution().AddProject("App");
        solution.AddDocument("App", "Thing.cs", Source);
        return solution;
    }

    [Fact]
    public async Task Includes_the_base_method_and_the_interface_member()
    {
        using var solution = Build();
        await solution.AssertCompilesAsync();

        var method = await GetAsync(solution, "App.Thing");
        var set = DispatchSet.For(method);

        Assert.Contains(DispatchSet.KeyOf(await GetAsync(solution, "App.Thing")), set.Direct);
        Assert.Contains(DispatchSet.KeyOf(await GetAsync(solution, "App.ThingBase")), set.Direct);
        Assert.Contains(DispatchSet.ConstructedKeyOf(await GetAsync(solution, "App.IThing")), set.Interface);
    }

    [Fact]
    public async Task Excludes_an_unrelated_overload()
    {
        using var solution = Build();
        await solution.AssertCompilesAsync();

        var overrideMethod = await GetAsync(solution, "App.Thing");
        var stringOverload = await GetAsync(solution, "App.Thing", overloadIndex: 1);

        Assert.DoesNotContain(DispatchSet.KeyOf(stringOverload), DispatchSet.For(overrideMethod).Direct);
    }

    [Fact]
    public async Task The_key_is_stable_across_separate_compilations_of_the_same_source()
    {
        using var first = Build();
        using var second = Build();

        var a = await GetAsync(first, "App.Thing");
        var b = await GetAsync(second, "App.Thing");

        Assert.False(SymbolEqualityComparer.Default.Equals(a, b));
        Assert.Equal(DispatchSet.KeyOf(a), DispatchSet.KeyOf(b));
        Assert.Contains(DispatchSet.KeyOf(b), DispatchSet.For(a).Direct);
    }

    [Fact]
    public async Task Overloads_get_different_keys()
    {
        using var solution = Build();
        await solution.AssertCompilesAsync();

        var intOverload = await GetAsync(solution, "App.Thing");
        var stringOverload = await GetAsync(solution, "App.Thing", overloadIndex: 1);

        Assert.NotEqual(DispatchSet.KeyOf(intOverload), DispatchSet.KeyOf(stringOverload));
    }
}
