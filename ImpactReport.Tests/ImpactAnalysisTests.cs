using ImpactReport.Analysis;
using ImpactReport.Analysis.Models;
using ImpactReport.Areas;
using Xunit;

namespace ImpactReport.Tests;

public class ImpactAnalysisTests
{
    private static async Task<TestSolution> BuildAppAsync()
    {
        var solution = new TestSolution()
            .AddProject("MyApp.Data")
            .AddProject("MyApp.Services", "MyApp.Data")
            .AddProject("MyApp.Web", "MyApp.Services", "MyApp.Data");

        solution.AddDocument("MyApp.Data", "OrderRepository.cs", """
            namespace MyApp.Data;

            public class OrderRepository
            {
                public string GetById(int id) => id.ToString();
            }
            """);

        solution.AddDocument("MyApp.Services", "IOrderService.cs", """
            namespace MyApp.Services;

            public interface IOrderService
            {
                string List();
            }
            """);

        solution.AddDocument("MyApp.Services", "OrderService.cs", """
            using MyApp.Data;

            namespace MyApp.Services;

            public class OrderService : IOrderService
            {
                private readonly OrderRepository _repository = new();

                public string List() => _repository.GetById(1);
            }
            """);

        solution.AddDocument("MyApp.Web", "DashboardController.cs", """
            using MyApp.Services;

            namespace MyApp.Web.Features.Dashboard;

            public class DashboardController
            {
                private readonly IOrderService _service;

                public DashboardController(IOrderService service) => _service = service;

                public string Index() => _service.List();
            }
            """);

        await solution.AssertCompilesAsync();
        return solution;
    }

    private static CallGraphOptions Options(int depth) =>
        new(MaxDepth: depth, MaxNodes: 1000, IncludeTests: false, MaxCallSitesPerProject: 10);

    private static AreaMap Areas() => AreaMap.LoadOrEmpty(WriteAreas("""
        {
          "areas": [
            { "name": "Customer Dashboard", "namespaces": ["MyApp.Web.Features.Dashboard"] },
            { "name": "Order Listing", "namespaces": ["MyApp.Services"] }
          ]
        }
        """));

    private static string WriteAreas(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"areas-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public async Task A_call_site_is_attributed_to_the_caller_not_the_callee()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), AreaMap.Empty, Options(depth: 1));

        var hit = Assert.Single(result.Projects.SelectMany(p => p.SampleHits));

        Assert.Equal("MyApp.Services", hit.ContainingNamespace);
        Assert.Equal("MyApp.Services.OrderService", hit.ContainingType);
        Assert.Equal("List", hit.ContainingMember);
    }

    [Fact]
    public async Task Calls_through_an_interface_count_against_the_implementation()
    {
        using var app = await BuildAppAsync();

        var method = await app.GetMethodAsync("MyApp.Services", "MyApp.Services.OrderService", "List");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), AreaMap.Empty, Options(depth: 1));

        Assert.Equal(1, result.TotalReferences);
        Assert.Equal("MyApp.Web", Assert.Single(result.Projects).ProjectName);
    }

    [Fact]
    public async Task One_hop_does_not_reach_the_ui_layer()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), Areas(), Options(depth: 1));

        Assert.Equal(["Order Listing"], result.Areas.Select(a => a.Area));
    }

    [Fact]
    public async Task Walking_further_reaches_the_ui_area_above_the_change()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), Areas(), Options(depth: 3));

        Assert.Equal(["Order Listing", "Customer Dashboard"], result.Areas.Select(a => a.Area));

        Assert.Equal(1, result.Areas[0].NearestDepth);
        Assert.Equal(2, result.Areas[1].NearestDepth);
        Assert.Equal(2, result.MaxDepthReached);
    }

    [Fact]
    public async Task Recursion_does_not_loop_forever()
    {
        var solution = new TestSolution().AddProject("Recursive");

        solution.AddDocument("Recursive", "Cycle.cs", """
            namespace Recursive;

            public class Cycle
            {
                public void A() => B();
                public void B() => A();
            }
            """);

        await solution.AssertCompilesAsync();

        using var _ = solution;
        var method = await solution.GetMethodAsync("Recursive", "Recursive.Cycle", "A");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(solution.Solution), AreaMap.Empty, Options(depth: 10));

        Assert.False(result.Truncated);
        Assert.True(result.TotalReferences > 0);
    }

    [Fact]
    public async Task The_search_for_each_method_runs_only_once()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");
        var finder = new ReferenceFinder(app.Solution);

        await ReferenceAnalyzer.AnalyzeAsync(method, finder, AreaMap.Empty, Options(depth: 1));
        var afterFirst = finder.SearchCount;

        await ReferenceAnalyzer.AnalyzeAsync(method, finder, AreaMap.Empty, Options(depth: 1));

        Assert.Equal(afterFirst, finder.SearchCount);
    }
    [Fact]
    public async Task Affected_members_name_the_code_that_depends_on_the_change()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), Areas(), Options(depth: 3));

        Assert.Equal(
            ["DashboardController.Index", "OrderService.List"],
            result.Members.Select(m => m.Display).OrderBy(d => d, StringComparer.Ordinal));

        var controller = result.Members.Single(m => m.MemberName == "Index");
        Assert.Equal(2, controller.NearestDepth);
        Assert.Equal("MyApp.Web", controller.ProjectName);
        Assert.Equal(["Customer Dashboard"], controller.Areas);
    }

    [Fact]
    public async Task Only_the_outermost_caller_is_an_entry_point()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), Areas(), Options(depth: 3));

        var entryPoints = result.Members.Where(m => m.IsEntryPoint).Select(m => m.Display).ToList();

        Assert.Equal(["DashboardController.Index"], entryPoints);
    }

    [Fact]
    public async Task A_member_at_the_depth_limit_is_not_claimed_to_be_an_entry_point()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), Areas(), Options(depth: 1));

        var service = Assert.Single(result.Members);

        Assert.Equal("OrderService.List", service.Display);
        Assert.True(service.BeyondDepthLimit);
        Assert.False(service.IsEntryPoint);
    }

    [Fact]
    public async Task Affected_projects_roll_up_members_and_areas()
    {
        using var app = await BuildAppAsync();
        var method = await app.GetMethodAsync("MyApp.Data", "MyApp.Data.OrderRepository", "GetById");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), Areas(), Options(depth: 3));

        var projects = ReferenceAnalyzer.BuildAffectedProjects(result.Members);

        Assert.Equal(["MyApp.Services", "MyApp.Web"], projects.Select(p => p.ProjectName).OrderBy(p => p, StringComparer.Ordinal));

        var web = projects.Single(p => p.ProjectName == "MyApp.Web");
        Assert.Equal(1, web.MemberCount);
        Assert.Equal(2, web.NearestDepth);
        Assert.Equal(["Customer Dashboard"], web.Areas);
    }
    [Fact]
    public void Merging_walks_trusts_the_one_that_actually_searched()
    {
        var searchedNoCallers = Member("A.Run", depth: 3, isEntryPoint: true, beyondDepthLimit: false);
        var neverSearched = Member("A.Run", depth: 1, isEntryPoint: false, beyondDepthLimit: true);

        var merged = Assert.Single(ReferenceAnalyzer.MergeMembers([searchedNoCallers, neverSearched]));

        Assert.False(merged.BeyondDepthLimit);
        Assert.True(merged.IsEntryPoint);
        Assert.Equal(1, merged.NearestDepth);
    }

    [Fact]
    public void A_member_with_callers_in_any_walk_is_not_an_entry_point()
    {
        var hasCallers = Member("A.Run", depth: 1, isEntryPoint: false, beyondDepthLimit: false);
        var looksTerminal = Member("A.Run", depth: 2, isEntryPoint: true, beyondDepthLimit: false);

        var merged = Assert.Single(ReferenceAnalyzer.MergeMembers([hasCallers, looksTerminal]));

        Assert.False(merged.IsEntryPoint);
    }

    [Fact]
    public void A_member_no_walk_searched_stays_unknown()
    {
        var first = Member("A.Run", depth: 3, isEntryPoint: false, beyondDepthLimit: true);
        var second = Member("A.Run", depth: 3, isEntryPoint: false, beyondDepthLimit: true);

        var merged = Assert.Single(ReferenceAnalyzer.MergeMembers([first, second]));

        Assert.True(merged.BeyondDepthLimit);
        Assert.False(merged.IsEntryPoint);
    }

    private static AffectedMember Member(string key, int depth, bool isEntryPoint, bool beyondDepthLimit) =>
        new(
            Key: key,
            TypeName: "Ns.A",
            MemberName: "Run",
            ProjectName: "P",
            Namespace: "Ns",
            NearestDepth: depth,
            CallSites: 1,
            Areas: [],
            IsEntryPoint: isEntryPoint,
            BeyondDepthLimit: beyondDepthLimit);
}
