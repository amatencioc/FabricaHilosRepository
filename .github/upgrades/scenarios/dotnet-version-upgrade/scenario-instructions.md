# Actualización de Versión de .NET

## Preferencias
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Upgrade Options
- **Upgrade Strategy**: All-at-once
- **Project Approach**: Direct upgrade
- **Package Management**: Per-project NuGet updates
- **Unsupported API Handling**: Fix during upgrade
- **Unsupported Packages**: Update all recommended packages
- **Nullable Reference Types**: Enable during upgrade

## Strategy
**Selected**: All-at-once
**Rationale**: 6 modern .NET projects (all .NET 8/9), all SDK-style csproj format, clear 2-tier dependency structure. Single atomic upgrade maximizes validation efficiency.

### Execution Constraints
- All projects upgraded simultaneously in one pass — no tier-based ordering
- Single bounded build-and-fix cycle: update TFMs → update packages → build → fix all errors at once
- Foundation libraries (Tier 0) must build successfully before applications are validated
- Testing begins only after entire solution builds with 0 errors
- Nullable Reference Types enabled progressively as part of code-fix tasks

