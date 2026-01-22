# ImpactReport

ImpactReport is a small Roslyn-based CLI tool that analyses a .NET solution and reports which projects, namespaces, and logical “impact areas” are affected when a specific method is changed.

---

## What it does

Given a solution, type, and method name, ImpactReport will:

- Locate all references to the specified method across the solution
- Group references by **project**
- Highlight the most common **namespaces**
- Optionally infer higher-level **impact areas** 
- Generate a **Markdown report** suitable for PRs and incident reviews

---

## Requirements

- .NET 8+ SDK (for the tool runtime)
- The solution’s required SDKs and targeting packs

---

## Usage

```bash
dotnet run -- \
  --sln path/to/YourSolution.sln \
  --type Fully.Qualified.TypeName \
  --method MethodName \
  --out impact-report.md \
  --areas areas.json
