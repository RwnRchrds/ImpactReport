using ImpactReport.Analysis;
using ImpactReport.Areas;
using Xunit;

namespace ImpactReport.Tests;

public class GenericDispatchTests
{
    private const string Source = """
        using System.Threading.Tasks;

        namespace App;

        public interface ISettingsService<T>
        {
            Task<T> GetAsync();
        }

        public class PortalSettings { }
        public class AvinodeSettings { }

        public class PortalSettingsService : ISettingsService<PortalSettings>
        {
            public Task<PortalSettings> GetAsync() => Task.FromResult(new PortalSettings());
        }

        public class AvinodeSettingsService : ISettingsService<AvinodeSettings>
        {
            public Task<AvinodeSettings> GetAsync() => Task.FromResult(new AvinodeSettings());
        }

        public class PortalCaller
        {
            private readonly ISettingsService<PortalSettings> _settings;
            public PortalCaller(ISettingsService<PortalSettings> settings) => _settings = settings;
            public Task<PortalSettings> Run() => _settings.GetAsync();
        }

        public class AvinodeCaller
        {
            private readonly ISettingsService<AvinodeSettings> _settings;
            public AvinodeCaller(ISettingsService<AvinodeSettings> settings) => _settings = settings;
            public Task<AvinodeSettings> Run() => _settings.GetAsync();
        }

        public class Repository<T>
        {
            public virtual Task<T> LoadAsync() => Task.FromResult(default(T));
        }

        public class OrderRepository : Repository<PortalSettings> { }
        public class InvoiceRepository : Repository<AvinodeSettings> { }

        public class SharedCallers
        {
            public Task<PortalSettings> A(OrderRepository r) => r.LoadAsync();
            public Task<AvinodeSettings> B(InvoiceRepository r) => r.LoadAsync();
        }
        """;

    private static CallGraphOptions Options() =>
        new(MaxDepth: 2, MaxNodes: 1000, IncludeTests: false, MaxCallSitesPerProject: 10);

    private static TestSolution Build()
    {
        var solution = new TestSolution().AddProject("App");
        solution.AddDocument("App", "App.cs", Source);
        return solution;
    }

    [Fact]
    public async Task One_implementation_of_a_generic_interface_does_not_absorb_the_others()
    {
        using var app = Build();
        await app.AssertCompilesAsync();

        var method = await app.GetMethodAsync("App", "App.PortalSettingsService", "GetAsync");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), AreaMap.Empty, Options());

        var callers = result.Members.Select(m => m.Display).ToList();

        Assert.Contains("PortalCaller.Run", callers);
        Assert.DoesNotContain("AvinodeCaller.Run", callers);
    }

    [Fact]
    public async Task Shared_generic_base_code_still_matches_every_construction()
    {
        using var app = Build();
        await app.AssertCompilesAsync();

        var method = await app.GetMethodAsync("App", "App.Repository`1", "LoadAsync");

        var result = await ReferenceAnalyzer.AnalyzeAsync(
            method, new ReferenceFinder(app.Solution), AreaMap.Empty, Options());

        var callers = result.Members.Select(m => m.Display).ToList();

        Assert.Contains("SharedCallers.A", callers);
        Assert.Contains("SharedCallers.B", callers);
    }
}
