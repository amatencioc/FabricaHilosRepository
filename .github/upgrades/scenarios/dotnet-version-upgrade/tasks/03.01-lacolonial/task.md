# 03.01-lacolonial: Upgrade LaColonial web app to net10.0 (3 issues, low-risk)

# 03.01-lacolonial: Upgrade LaColonial web app

## Objective

Upgrade LaColonial web application from net8.0 to net10.0. This is the lowest-complexity application with only 3 issues total and no package updates required.

## Scope

1. Update `.csproj` TargetFramework from `net8.0` → `net10.0`
2. No package updates needed (all packages already compatible)
3. Build and fix any behavioral changes (2 potential issues expected)

## Assessment Data

- **Current TFM**: net8.0 → **Target**: net10.0
- **SDK-style**: ✅ Yes
- **Issue Count**: 3 total (1 Mandatory, 2 Potential)
- **Files**: 113 files
- **Issues by Category**:
  - `Project.0002` (Mandatory): Change target framework [1 occurrence]
  - `Api.0003` (Potential): Behavioral changes in .NET 10 [2 occurrences]
- **Package Updates**: None required
- **Risk**: Very Low
- **Estimated Effort**: 30 minutes

## Done When

- ✅ LaColonial TargetFramework updated to net10.0
- ✅ Project builds successfully in Release mode
- ✅ No compilation errors
- ✅ Behavioral changes identified and understood (likely documentation only)

## Related Skills

- `managing-target-frameworks`
- `building-projects`

