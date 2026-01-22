using System.Diagnostics;

namespace ImpactReport.Git;

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

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments} failed:\n{stderr}");

        return stdout;
    }

    public static string GetRepoRoot()
    {
        var root = Run("rev-parse --show-toplevel").Trim();
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Could not determine git repo root (are you in a git repo?).");
        return root;
    }
}