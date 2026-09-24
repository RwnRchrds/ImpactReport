# ImpactReport

ImpactReport is a Roslyn-based CLI tool that analyses a .NET solution and tells you
which projects, namespaces and logical **impact areas** are affected by a change.

Point it at a branch and it answers the question a reviewer actually asks:

```
Impact Areas: Invoice Rebuild, Order Listing

Affected Projects:
  MyApp.Web                        2 member(s)     2 call site(s)  nearest hop 1
  MyApp.Services                   1 member(s)     1 call site(s)  nearest hop 1

Affected entry points (nothing else calls these):
  OrderListController.List                 hop 1  MyApp.Web  [Order Listing]
  BillingRebuildController.Rebuild         hop 2  MyApp.Web  [Invoice Rebuild]

Methods analysed: 1 of 1 changed  |  Affected members: 3  |  Call sites: 3
Highest fan-out: MyApp.Services.Orders.OrderService.List(DateTime) (3 call sites across 2 project(s))

Full report: impact-report.md
```

---

## What it does

- Finds every call site of one or more methods
- **Follows callers transitively**, so a repository change still surfaces the UI
  screens above it
- Resolves calls made **through interfaces and base classes**, which is how most
  DI-based code actually calls things
- Names the **members** that depend on the change, and flags which of them are
  **entry points** that nothing else calls
- Rolls those up into **affected projects** and your own **impact areas**
- Ranks methods by risk (fan-out across projects)
- Writes a Markdown report for the PR, and a summary to the terminal

Two modes:

- **Changed mode** - analyse everything that changed against a git base ref
- **Single-method mode** - analyse one specific method

---

## Requirements

- .NET **8.0 SDK or newer**
- The solution's required SDKs and targeting packs installed
- `git` on `PATH` (changed mode only)
- Access to the solution source; no build required

---

## Installation

```bash
dotnet pack -c Release -o ./nupkg
dotnet tool install -g ImpactReport --add-source ./nupkg
impactreport --help
```

To update:

```bash
dotnet tool update -g ImpactReport --add-source ./nupkg
```

---

## Usage

Changed-methods (PR-style) analysis:

```bash
impactreport --sln MySolution.sln --changed --areas areas.json
```

Single method analysis:

```bash
impactreport --sln MySolution.sln --type MyApp.Services.OrderService --method GetById
```

---

## Impact areas

Without `--areas`, the report gives you projects and namespaces. An areas file turns
those into the names your team uses.

Create `areas.json`:

```json
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
      "paths": ["**/Orders/Listing/**"]
    }
  ]
}
```

A call site belongs to an area if it matches **any** of that area's patterns:

| Key          | Matches                                                        |
|--------------|----------------------------------------------------------------|
| `namespaces` | Namespace prefix, on a dot boundary (`MyApp.Ops` ≠ `MyApp.OpsAdmin`) |
| `paths`      | Glob against the file path; `**` spans directories             |
| `projects`   | Exact project name                                             |
| `types`      | Substring of the fully qualified type name                     |

A call site can belong to more than one area. The older flat format
`{ "Namespace.Prefix": "Area Name" }` is still accepted.

See [`areas.sample.json`](areas.sample.json) for a working example.

---

## "Will it tell me that feature X is affected?"

The usual question. Say someone changes `OrderService.List`, and somewhere
downstream a nightly invoice rebuild depends on it. There are four levels of
answer, and it is worth knowing which one you are relying on.

**1. Affected Members** — always available, no configuration needed. Every method
that transitively depends on the change, by name:

| Member | Project | Hop |
|--------|---------|----:|
| `OrderListController.List` | `MyApp.Web` | 1 |
| `InvoiceRebuildService.Rebuild` | `MyApp.Services` | 1 |
| `BillingRebuildController.Rebuild` | `MyApp.Web` | 2 |

**2. Affected Entry Points** — the subset that nothing else in the solution calls.
These are the features, as opposed to the plumbing between them. If
`BillingRebuildController.Rebuild` appears here, the invoice rebuild is reachable
from your change.

**3. Affected Projects** — the same data rolled up per project, with the nearest
hop and the areas inside it.

**4. Impact Areas** — only if you write an `areas.json` rule naming "Invoice
Rebuild". This is the one that needs setting up; the three above work out of the
box.

A member is only marked an entry point if the tool actually searched for *its*
callers. One sitting at the `--depth` limit is marked `*` in the report instead,
because "we did not look" is not the same as "nothing calls it".

### What it cannot see

Roslyn finds C# call sites. If a feature is triggered by a message on a queue, a
Hangfire or Quartz job resolved by name, a Razor view, reflection, or
convention-based DI registration, **it will not appear at any depth**. An empty
result means "no compile-time caller found", never "safe to change".

---

## Frontend

`--frontend` adds two things the C# analysis cannot reach on its own.

**Changed frontend files are mapped to areas** by path, so a branch that only
touches Angular still reports impact areas:

| File | Feature | Areas |
|------|---------|-------|
| `src/app/features/billing/…/settings-page.component.ts` | billing | Invoicing |

**Screens that call an affected endpoint are named.** When the backend walk
reaches a controller, the tool reports which frontend features call it:

| Feature | Files | Via endpoint | Areas |
|---------|------:|--------------|-------|
| `orders` | 1 | OrderLine | Order Listing |

The join is convention-based: it extracts `api/<Name>` from the frontend sources
and matches it to `<Name>Controller`. It also tries the member name, which
catches HTTP-triggered functions the frontend calls directly. Endpoints with no
frontend caller are listed at the end of the section rather than hidden, so you
can see what the convention missed.

This is deliberately **not** a TypeScript call graph. There is no analysis
*within* the frontend: a changed component that another component imports will
not pull that second component into the report.

## How depth works

Impact areas depend on how far the tool walks outwards from the change:

```
OrderRepository.GetForRange   <- you changed this
  hop 1  OrderScheduleService.Schedule
  hop 2    DashboardController.Index               <- "Customer Dashboard"
```

At `--depth 1` you only see the service layer. The default of **3** clears the usual
repository → service → controller stack, which is what makes UI-level area names
appear. Raise it for deeper architectures; `--max-nodes` caps the walk so a hub
method cannot blow up into the whole solution.

Reference counts are transitive, so a method reached at hop 3 still counts. The
`Hop` column in the Markdown report tells you how far away each call site is.

---

## Options

| Option | Description |
|--------|-------------|
| `--sln <path>` | Path to the `.sln`/`.slnx` file (required) |
| `--changed` | Analyse methods changed compared to a git base ref |
| `--base <ref>` | Git base ref to diff against (default: `origin/main`) |
| `--type <type>` | Fully qualified type name (single-method mode) |
| `--method <name>` | Method name to analyse (single-method mode) |
| `--areas <file>` | JSON file naming the impact areas of your codebase |
| `--depth <n>` | Caller hops to follow (default: 3) |
| `--max-nodes <n>` | Cap on methods visited while walking (default: 2000) |
| `--top <n>` | Show only the top N methods by risk (default: 25) |
| `--all` | Show all methods (disables `--top`) |
| `--min-refs <n>` | Only include methods with at least N references (default: 1) |
| `--min-projects <n>` | Only include methods impacting at least N projects (default: 1) |
| `--include-zero` | Include methods with 0 references (noisy; for debugging) |
| `--include-tests` | Include test projects in the analysis |
| `--out <file>` | Output Markdown file (default: `impact-report.md`) |
| `--max <n>` | Max sample call sites per project (default: 10) |
| `--quiet` | Suppress progress output; print the summary only |
| `--frontend` | Also analyse the frontend (see below) |
| `--frontend-root <dir>` | Where to scan for frontend sources, relative to the repo root; implies `--frontend` |
| `--frontend-ext <list>` | Extensions counted as frontend changes (default: `.ts,.html,.scss,.css`) |
| `-h`, `--help` | Show help and exit |

Exit codes: `0` success, `1` unexpected error, `2` bad command line, `3` git problem,
`130` cancelled.

---

## Examples

```bash
# What does this branch touch?
impactreport --sln MySolution.sln --changed --areas areas.json

# Against a different base, looking further out
impactreport --sln MySolution.sln --changed --base origin/develop --depth 4

# One method, including test call sites
impactreport --sln MySolution.sln \
  --type MyApp.Services.OrderService --method GetById --include-tests
```

---

## Limitations

- Only C# call sites are found. Razor views, DI registration by convention,
  reflection and serialisation boundaries are invisible to Roslyn's reference
  search, so treat the report as a strong hint rather than proof.
- Changed mode detects changed **methods, constructors and accessors**. A changed
  field initialiser or type declaration is not itself a seed.
- A method reached through an interface counts against every implementation that
  could satisfy the call, which can overstate impact in codebases with many
  implementations of one interface.
- Analysis runs against the working tree, not the base commit, so a method that was
  deleted on your branch has nothing left to analyse.
- "Entry point" means nothing in *this solution* calls it. A public API consumed by
  another repository will look like an entry point because, locally, it is one.
- Call-site counts are transitive: a member reached at hop 3 still counts, so the
  totals answer "how much code is downstream" rather than "how many lines call it".

---

## Development

```bash
dotnet build
dotnet test
```

---

## Licence

MIT - see [LICENSE.txt](LICENSE.txt).
