using ImpactReport.Areas;
using Xunit;

namespace ImpactReport.Tests;

public class AreaMapTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Matches_by_namespace_prefix()
    {
        var map = Load("""
            { "areas": [ { "name": "Invoicing", "namespaces": ["MyApp.Billing.Invoices"] } ] }
            """);

        Assert.Equal(["Invoicing"], map.MatchAreas("MyApp.Billing.Invoices.Rostering", null, null, null));
    }

    [Fact]
    public void Namespace_prefix_stops_at_a_dot_boundary()
    {
        var map = Load("""
            { "areas": [ { "name": "Ops", "namespaces": ["MyApp.Ops"] } ] }
            """);

        Assert.Equal(["Ops"], map.MatchAreas("MyApp.Ops", null, null, null));
        Assert.Equal(["Ops"], map.MatchAreas("MyApp.Ops.Board", null, null, null));

        Assert.Empty(map.MatchAreas("MyApp.OpsAdmin", null, null, null));
    }

    [Theory]
    [InlineData("**/Features/Dashboard/**", "C:/src/Web/Features/Dashboard/Calendar.cs", true)]
    [InlineData("**/Features/Dashboard/**", "C:/src/Features/Dashboard/x/y/Deep.cs", true)]
    [InlineData("**/Features/Dashboard/**", "C:/src/Web/Features/Billing/Calendar.cs", false)]
    [InlineData("**/*Controller.cs", "C:/src/Web/OrderController.cs", true)]
    [InlineData("**/*Controller.cs", "C:/src/Web/OrderService.cs", false)]
    public void Matches_by_path_glob(string glob, string path, bool expected)
    {
        var map = Load($$"""
            { "areas": [ { "name": "A", "paths": ["{{glob}}"] } ] }
            """);

        Assert.Equal(expected, map.MatchAreas(null, path, null, null).Count == 1);
    }

    [Fact]
    public void Path_globs_match_windows_separators_too()
    {
        var map = Load("""
            { "areas": [ { "name": "A", "paths": ["**/Features/Dashboard/**"] } ] }
            """);

        Assert.Equal(["A"], map.MatchAreas(null, @"C:\src\Web\Features\Dashboard\Calendar.cs", null, null));
    }

    [Fact]
    public void Matches_by_project_and_type()
    {
        var map = Load("""
            {
              "areas": [
                { "name": "Order Listing", "projects": ["MyApp.Orders"] },
                { "name": "Invoicing", "types": ["InvoiceController"] }
              ]
            }
            """);

        Assert.Equal(["Order Listing"], map.MatchAreas(null, null, "MyApp.Orders", null));
        Assert.Equal(["Invoicing"], map.MatchAreas(null, null, null, "MyApp.Web.InvoiceController"));
    }

    [Fact]
    public void A_call_site_can_belong_to_several_areas()
    {
        var map = Load("""
            {
              "areas": [
                { "name": "Customer Dashboard", "namespaces": ["MyApp.Web.Dashboard"] },
                { "name": "Web", "projects": ["MyApp.Web"] }
              ]
            }
            """);

        var areas = map.MatchAreas("MyApp.Web.Dashboard", null, "MyApp.Web", null);

        Assert.Equal(2, areas.Count);
        Assert.Contains("Customer Dashboard", areas);
        Assert.Contains("Web", areas);
    }

    [Fact]
    public void Legacy_flat_format_is_still_supported()
    {
        var map = Load("""
            {
              "MyApp.Billing.Invoices": "Invoicing",
              "MyApp.Web.Billing": "Invoicing",
              "MyApp.Orders": "Order Listing"
            }
            """);

        Assert.Equal(["Invoicing"], map.MatchAreas("MyApp.Web.Billing.Pages", null, null, null));
        Assert.Equal(["Order Listing"], map.MatchAreas("MyApp.Orders.Listing", null, null, null));
    }

    [Fact]
    public void Empty_map_matches_nothing_and_reports_no_mappings()
    {
        var map = AreaMap.LoadOrEmpty(null);

        Assert.False(map.HasMappings);
        Assert.Empty(map.MatchAreas("Anything", "any/path.cs", "AnyProject", "AnyType"));
    }

    [Fact]
    public void A_missing_areas_file_is_an_error_rather_than_silently_ignored()
    {
        Assert.Throws<ImpactReport.Utils.CommandLineException>(
            () => AreaMap.LoadOrEmpty("does-not-exist.json"));
    }

    [Fact]
    public void Malformed_json_reports_the_file_name()
    {
        var path = WriteTemp("{ not json");

        var ex = Assert.Throws<ImpactReport.Utils.CommandLineException>(() => AreaMap.LoadOrEmpty(path));

        Assert.Contains(Path.GetFileName(path), ex.Message);
    }

    private AreaMap Load(string json) => AreaMap.LoadOrEmpty(WriteTemp(json));

    private string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"areas-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            File.Delete(file);

        GC.SuppressFinalize(this);
    }
}
