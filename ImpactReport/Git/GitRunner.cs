using System.Diagnostics;

namespace ImpactReport.Git;

public sealed class GitException(string message) : Exception(message);

public static class GitRunner
{
    public static string Run(string arguments, string? workingDir = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["LC_ALL"] = "C.UTF-8";

        using var process = StartOrThrow(psi);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
            throw new GitException($"git {arguments}\nfailed with exit code {process.ExitCode}:\n{stderr.Trim()}");

        return stdout;
    }

    public static string GetRepoRoot(string? startingDirectory = null)
    {
        var root = Run("-c core.quotepath=false rev-parse --show-toplevel", startingDirectory).Trim();

        if (string.IsNullOrWhiteSpace(root))
            throw new GitException("Could not determine the git repository root (are you inside a git repository?).");

        return root;
    }

    public static string MergeBase(string reference, string workingDir)
    {
        var mergeBase = Run($"merge-base {reference} HEAD", workingDir).Trim();

        if (string.IsNullOrWhiteSpace(mergeBase))
            throw new GitException($"Could not find a merge base between {reference} and HEAD.");

        return mergeBase;
    }

    public static bool RefExists(string reference, string workingDir)
    {
        try
        {
            Run($"rev-parse --verify --quiet \"{reference}^{{commit}}\"", workingDir);
            return true;
        }
        catch (GitException)
        {
            return false;
        }
    }

    private static Process StartOrThrow(ProcessStartInfo psi)
    {
        try
        {
            return Process.Start(psi) ?? throw new GitException("Failed to start git.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new GitException("git was not found on PATH. Install git, or omit --changed.");
        }
    }
}
