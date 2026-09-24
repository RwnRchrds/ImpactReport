namespace ImpactReport.Cli;

public sealed class ConsoleProgress(bool quiet)
{
    public static ConsoleProgress Silent { get; } = new(quiet: true);

    public void Report(string message)
    {
        if (!quiet)
            Console.WriteLine(message);
    }
}
