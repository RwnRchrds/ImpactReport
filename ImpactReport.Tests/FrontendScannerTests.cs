using ImpactReport.Frontend;
using Xunit;

namespace ImpactReport.Tests;

public class FrontendScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fe-{Guid.NewGuid():N}");

    private string Write(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public void Extracts_api_segments_from_a_service()
    {
        Write("src/app/features/invoice/services/invoice.service.ts", """
            private baseURL = `${environment.apiBaseUrl}api/Invoice`;
            get(id) { return this.http.get(`${this.baseURL}/${id}`); }
            check() { return this.http.get(`${environment.apiBaseUrl}api/InvoiceValidation/all`); }
            """);

        var index = FrontendScanner.Scan(_root);
        var file = Assert.Single(index.Files);

        Assert.Equal(["Invoice", "InvoiceValidation"], file.ApiSegments);
        Assert.Equal("invoice", file.Feature);
    }

    [Fact]
    public void Files_with_no_api_call_are_skipped()
    {
        Write("src/app/features/invoice/invoice.model.ts", "export interface Invoice { id: number; }");

        Assert.Equal(0, FrontendScanner.Scan(_root).Count);
    }

    [Fact]
    public void Node_modules_and_declaration_files_are_ignored()
    {
        Write("node_modules/thing/index.ts", "fetch('api/Invoice')");
        Write("src/app/features/x/typings.d.ts", "declare const x: 'api/Invoice';");

        Assert.Equal(0, FrontendScanner.Scan(_root).Count);
    }

    [Fact]
    public void A_controller_maps_to_its_route_segment()
    {
        Assert.Equal(["Invoice"], FrontendScanner.EndpointKeysFor("MyApp.Api.Controllers.InvoiceController", null));
    }

    [Fact]
    public void A_member_name_is_also_an_endpoint_candidate()
    {
        var keys = FrontendScanner.EndpointKeysFor("Fn.ExportFunctions", "RebuildNightlyExport");

        Assert.Equal(["RebuildNightlyExport"], keys);
    }

    [Fact]
    public void Feature_falls_back_to_the_folder_under_app()
    {
        Assert.Equal("core", FrontendScanner.FeatureOf("C:/repo/src/app/core/services/thing.service.ts"));
        Assert.Null(FrontendScanner.FeatureOf("C:/repo/scripts/build.ts"));
    }

    [Fact]
    public void Lookup_by_endpoint_is_case_insensitive()
    {
        Write("src/app/features/order/order.service.ts", "url = 'api/order';");

        var index = FrontendScanner.Scan(_root);

        Assert.True(index.Knows("Order"));
        Assert.Single(index.CallersOf("ORDER"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }
}
