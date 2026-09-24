using ImpactReport.Analysis;
using ImpactReport.Cli;
using ImpactReport.Frontend;
using ImpactReport.Git;
using ImpactReport.Reporting;
using ImpactReport.Utils;
using ImpactReport.Workspace;

namespace ImpactReport;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var options = ImpactReportOptions.Parse(args);
            if (options is null)
            {
                Console.WriteLine(HelpText.Text);
                return 0;
            }

            return await RunAsync(options, cancellation.Token).ConfigureAwait(false);
        }
        catch (CommandLineException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (GitException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 3;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<int> RunAsync(ImpactReportOptions options, CancellationToken cancellationToken)
    {
        MsBuildBootstrapper.Register();

        var progress = new ConsoleProgress(options.Quiet);

        string? repoRoot = null;
        if (options.ChangedMode)
        {
            repoRoot = GitRunner.GetRepoRoot(Path.GetDirectoryName(options.SolutionPath));

            if (!GitRunner.RefExists(options.BaseRef, repoRoot))
            {
                throw new GitException(
                    $"Git ref '{options.BaseRef}' does not exist in this repository.\n" +
                    "Pass a different ref with --base (e.g. --base origin/master), or fetch it first.");
            }
        }

        progress.Report($"Loading {Path.GetFileName(options.SolutionPath)}...");

        using var workspace = await SolutionLoader.OpenAsync(options.SolutionPath, progress, cancellationToken)
            .ConfigureAwait(false);

        var solution = workspace.CurrentSolution;
        var finder = new ReferenceFinder(solution);

        var graphOptions = new CallGraphOptions(
            MaxDepth: options.Depth,
            MaxNodes: options.MaxNodes,
            IncludeTests: options.IncludeTests,
            MaxCallSitesPerProject: options.MaxCallSitesPerProject);

        if (!options.ChangedMode)
            return await RunSingleAsync(options, solution, finder, graphOptions, progress, cancellationToken)
                .ConfigureAwait(false);

        return await RunChangedAsync(options, solution, finder, graphOptions, progress, repoRoot!, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> RunSingleAsync(
        ImpactReportOptions options,
        Microsoft.CodeAnalysis.Solution solution,
        ReferenceFinder finder,
        CallGraphOptions graphOptions,
        ConsoleProgress progress,
        CancellationToken cancellationToken)
    {
        var method = await SymbolResolver
            .FindMethodAsync(solution, options.TypeName!, options.MethodName!, cancellationToken)
            .ConfigureAwait(false);

        progress.Report($"Walking callers up to {options.Depth} hop(s)...");

        var result = await ReferenceAnalyzer
            .AnalyzeAsync(method, finder, options.AreaMap, graphOptions, cancellationToken)
            .ConfigureAwait(false);

        var frontend = AnalyzeFrontendScreens(options, result.Members, progress, cancellationToken);

        MarkdownReportWriter.WriteToFile(result, frontend, options.OutputPath);
        ConsoleSummaryWriter.Write(result, frontend, options.OutputPath, options.AreaMap.HasMappings);

        return 0;
    }

    private static FrontendImpact AnalyzeFrontendScreens(
        ImpactReportOptions options,
        IReadOnlyList<Analysis.Models.AffectedMember> members,
        ConsoleProgress progress,
        CancellationToken cancellationToken)
    {
        if (!options.Frontend)
            return FrontendImpact.Empty;

        var solutionDirectory = Path.GetDirectoryName(options.SolutionPath)!;
        var root = ResolveFrontendRoot(options, solutionDirectory);

        progress.Report($"Scanning frontend sources under {root}...");

        var index = FrontendScanner.Scan(root, cancellationToken);
        progress.Report($"Frontend files calling an API: {index.Count} ({index.Endpoints.Count} endpoint(s)).");

        return FrontendAnalyzer.Analyze(index, [], members, root, options.AreaMap);
    }

    private static string ResolveFrontendRoot(ImpactReportOptions options, string fallback)
    {
        string baseDirectory;

        try
        {
            baseDirectory = GitRunner.GetRepoRoot(fallback);
        }
        catch (GitException)
        {
            baseDirectory = fallback;
        }

        var root = options.FrontendRoot is null
            ? baseDirectory
            : Path.GetFullPath(Path.Combine(baseDirectory, options.FrontendRoot));

        if (!Directory.Exists(root))
            throw new CommandLineException($"Frontend root not found: {root}");

        return root;
    }

    private static async Task<int> RunChangedAsync(
        ImpactReportOptions options,
        Microsoft.CodeAnalysis.Solution solution,
        ReferenceFinder finder,
        CallGraphOptions graphOptions,
        ConsoleProgress progress,
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var changedFiles = ChangedFilesProvider.GetChangedCsFiles(options.BaseRef, repoRoot, options.IncludeUncommitted);
        progress.Report($"Changed .cs files vs {options.BaseRef}: {changedFiles.Count}");

        var changedMethods = await ChangedMethodDetector
            .FindChangedMethodsAsync(solution, changedFiles, repoRoot, options.IncludeTests, cancellationToken)
            .ConfigureAwait(false);

        progress.Report($"Changed methods detected: {changedMethods.Count}");

        if (changedMethods.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine($"No changed methods found against {options.BaseRef}.");
            Console.WriteLine("If that is unexpected, check that --sln covers the projects you changed.");
            return 0;
        }

        var ranked = await MultiMethodReferenceAnalyzer
            .PreAnalyzeManyAsync(changedMethods, finder, options.IncludeTests, cancellationToken)
            .ConfigureAwait(false);

        var filtered = MultiMethodReferenceAnalyzer.ApplyFiltersAndTakeTop(ranked, options);
        progress.Report($"Analysing {filtered.Count} method(s) up to {options.Depth} hop(s) deep...");

        var multi = await MultiMethodReferenceAnalyzer
            .AnalyzeManyAsync(
                filtered.Select(x => x.Method),
                finder,
                options.AreaMap,
                graphOptions,
                changedMethods.Count,
                cancellationToken)
            .ConfigureAwait(false);

        var frontend = AnalyzeFrontend(options, repoRoot, multi.Members, progress, cancellationToken);

        MarkdownReportWriter.WriteToFile(multi, frontend, options.OutputPath);
        ConsoleSummaryWriter.Write(multi, frontend, options.OutputPath, options.AreaMap.HasMappings);

        return 0;
    }

    private static FrontendImpact AnalyzeFrontend(
        ImpactReportOptions options,
        string repoRoot,
        IReadOnlyList<Analysis.Models.AffectedMember> members,
        ConsoleProgress progress,
        CancellationToken cancellationToken)
    {
        if (!options.Frontend)
            return FrontendImpact.Empty;

        var root = options.FrontendRoot is null
            ? repoRoot
            : Path.GetFullPath(Path.Combine(repoRoot, options.FrontendRoot));

        if (!Directory.Exists(root))
            throw new CommandLineException($"Frontend root not found: {root}");

        progress.Report($"Scanning frontend sources under {root}...");

        var index = FrontendScanner.Scan(root, cancellationToken);
        progress.Report($"Frontend files calling an API: {index.Count} ({index.Endpoints.Count} endpoint(s)).");

        var changedPaths = ChangedFilesProvider.GetChangedPathsWithExtension(
            options.BaseRef,
            repoRoot,
            options.FrontendExtensions,
            options.IncludeUncommitted);

        return FrontendAnalyzer.Analyze(index, changedPaths, members, repoRoot, options.AreaMap);
    }
}
