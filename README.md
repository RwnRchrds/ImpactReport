# ImpactReport

ImpactReport is a Roslyn-based CLI tool that analyses a .NET solution and reports which projects, namespaces, and logical **impact areas** are affected when methods change.

---

## What it does

ImpactReport analyses a solution and:

- Finds all call sites for one or more methods
- Groups references by **project**
- Highlights dominant **namespaces**
- Infers higher-level **impact areas** (optional)
- Ranks changes by **risk** (fan-out across projects)
- Produces a clean, readable **Markdown report**

It supports:
- **Single-method mode** (target one method)
- **Changed-mode** (analyse all changed methods vs a base branch)

---

## Requirements

- .NET **8.0 SDK or newer**
- The solution’s required SDKs and targeting packs installed
- Access to the solution source (no build required)

---

## Installation

### Create a NuGet Package

```bash
dotnet pack -c Release -o ./nupkg
```

### Install globally

```bash
dotnet tool install -g ImpactReport --add-source ./nupkg
```
### Verify Installation

```bash
impactreport --help
```
### Update to Newer Version

```bash
dotnet tool update -g ImpactReport --add-source ./nupkg
```

-------------
Usage
-----

Single method analysis:

  `impactreport --sln <solution.sln> --type <Full.Type.Name> --method <MethodName> [options]`

Changed-methods (PR-style) analysis:

  `impactreport --sln <solution.sln> --changed [options]`

Options
-------

  `--sln <path>`           Path to the .sln file (required)

  `--type <type>`          Fully qualified type name
  `--method <method>`      Method name to analyse
                         (required unless --changed is specified)

  `--changed`              Analyse methods changed compared to a git base ref
  `--base <ref>`           Git base ref to diff against (default: origin/main)

  `--top <n>`              Show only the top N methods (by risk). Default: 25
  
  `--all`                  Show all methods (disables --top)
  
  `--min-refs <n>`         Only include methods with at least N references (default: 1)
  
  `--min-projects <n>`     Only include methods impacting at least N projects (default: 1)
  
  `--include-zero`         Include methods with 0 references (noisy; for debugging)

  `--out <file>`           Output Markdown file (default: impact-report.md)
  
  `--areas <file>`         JSON file mapping namespaces to impact areas
  
  `--max <n>`              Max sample call sites per project (default: 10)

  `--include-tests`        Include *.Tests projects in analysis

  `-h, --help`             Show this help and exit

EXAMPLES
--------

Analyse a specific method:

  `impactreport --sln MySolution.sln --type MyApp.Services.UserService --method GetById`

Analyse changed methods in current branch:

  `impactreport --sln MySolution.sln --changed`

Analyse against a different base:

  `impactreport --sln MySolution.sln --changed --base origin/develop`

