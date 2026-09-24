namespace ImpactReport.Cli;

public static class HelpText
{
    public const string Text =
"""
ImpactReport - Roslyn impact analysis for .NET solutions

Reports what a change reaches, at three levels:
  Affected Members      the specific methods that depend on the change
  Affected Entry Points the outermost of those - nothing else calls them
  Affected Projects     the projects those members live in
  Impact Areas          your own names for them, when --areas is supplied

USAGE
-----
  Changed-methods (PR-style) analysis:
    impactreport --sln <solution.sln> --changed [options]

  Single method analysis:
    impactreport --sln <solution.sln> --type <Full.Type.Name> --method <MethodName> [options]

OPTIONS
-------
  --sln <path>           Path to the .sln/.slnx file (required)

  --changed              Analyse methods changed compared to a git base ref
  --base <ref>           Git base ref to diff against (default: origin/main)

  --type <type>          Fully qualified type name       } single-method mode,
  --method <method>      Method name to analyse          } required without --changed

  --areas <file>         JSON file naming the impact areas of your codebase.
                         Without it the report has projects and namespaces but no
                         area names. See AREAS below.

  --depth <n>            How many caller hops to follow (default: 3).
                         1 = direct callers only. Raise it to reach UI-level code
                         that sits further above the change.
  --max-nodes <n>        Safety cap on methods visited while walking (default: 2000)

  --top <n>              Show only the top N methods, ranked by fan-out (default: 25)
  --all                  Show all methods (disables --top)
  --min-refs <n>         Only include methods with at least N references (default: 1)
  --min-projects <n>     Only include methods impacting at least N projects (default: 1)
  --include-zero         Include methods with 0 references (noisy; for debugging)
  --include-tests        Include test projects in the analysis

  --out <file>           Output Markdown file (default: impact-report.md)
  --max <n>              Max sample call sites per project (default: 10)
  --quiet                Suppress progress output; print the summary only
  --uncommitted          Include working-tree edits, not just committed ones

  --frontend             Also analyse the frontend: map changed .ts/.html/.scss files
                         to areas, and report which screens call the affected
                         API endpoints
  --frontend-root <dir>  Where to scan for frontend sources, relative to the repo
                         root (default: the whole repo). Implies --frontend.
  --frontend-ext <list>  Extensions counted as frontend changes
                         (default: .ts,.html,.scss,.css)

  -h, --help             Show this help and exit

AREAS
-----
An areas file turns projects and namespaces into names your team uses. A call
site matches an area if it matches ANY of that area's patterns.

  {
    "areas": [
      {
        "name": "Customer Dashboard",
        "namespaces": ["MyApp.Web.Features.Dashboard"],
        "paths": ["**/Features/Dashboard/**"]
      },
      {
        "name": "Invoicing",
        "namespaces": ["MyApp.Billing.Invoices", "MyApp.Web.Features.Billing"],
        "types": ["InvoiceController"]
      },
      {
        "name": "Order Listing",
        "projects": ["MyApp.Orders"],
        "paths": ["**/Order/Listing/**"]
      }
    ]
  }

Keys: namespaces (prefix match), paths (glob, ** spans directories),
projects (exact name), types (substring of the full type name).
The older flat format { "Namespace.Prefix": "Area Name" } is still accepted.

WHAT IT CANNOT SEE
------------------
Only C# call sites are found. A feature reached through a message queue, a job
scheduled by name, a Razor view, reflection or convention-based DI registration
will NOT appear, however many hops you allow. The --frontend join
relies on the "api/<Name>" -> "<Name>Controller" convention; a route attribute
that renames a controller breaks that link. Treat an empty result as "no
compile-time caller", not as "safe to change".

EXAMPLES
--------
  impactreport --sln MySolution.sln --changed --areas areas.json
  impactreport --sln MySolution.sln --changed --base origin/develop --depth 4
  impactreport --sln MySolution.sln --type MyApp.Services.OrderService --method GetById
""";
}
