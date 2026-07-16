# Upgrade Options — FabricaHilos

Assessment: 6 modern .NET projects (4 on .NET 8, 2 on .NET 9), all SDK-style, 14 packages need updates, 195 issues detected (mostly API behavioral changes and package updates).

## Strategy

### Upgrade Strategy
All projects are modern .NET (no .NET Framework), allowing for a coordinated atomic upgrade across the entire solution.

| Value | Description |
|-------|-------------|
| **All-at-once** (selected) | Upgrade all projects together in a single, coordinated pass. Best for modern .NET solutions without legacy .NET Framework dependencies. Simpler validation: build the entire solution once after upgrade. |
| Bottom-up | Upgrade leaf projects first (libraries), then dependents. Adds complexity due to multi-targeting and inter-project validation overhead. Recommended only for deep dependency trees with architectural concerns. |
| Top-down | Upgrade applications first, libraries later with multi-targeting. Adds multi-targeting complexity. Recommended only when applications have hard dependencies on legacy library APIs. |

## Project Structure

### Project Approach
All 6 projects are modern .NET applications and libraries; no .NET Framework projects requiring side-by-side migration.

| Value | Description |
|-------|-------------|
| **Direct upgrade** (selected) | Update each project's TargetFramework directly to net10.0. All projects are already SDK-style and modern .NET. |
| Side-by-side migration | Not applicable: no .NET Framework projects to migrate alongside new applications. |

### Package Management
The solution uses 33 NuGet packages across 6 projects; no centralized package management (e.g., Directory.Packages.props) detected.

| Value | Description |
|-------|-------------|
| **Per-project NuGet updates** (selected) | Update each project's package references individually during the upgrade. Simpler for this project count. |
| Centralized Package Management | Adopt Directory.Packages.props to manage package versions centrally. Recommended for larger solutions; adds setup overhead for 6 projects. |

## Compatibility

### Unsupported API Handling
195 total issues detected: 19 binary incompatibilities (high), 51 source incompatibilities (medium), 104 behavioral changes (low), 328K+ compatible APIs.

| Value | Description |
|-------|-------------|
| **Fix during upgrade** (selected) | Address all API incompatibilities as part of task execution. Includes code changes, package updates, and testing. |
| Defer post-upgrade | Not recommended: incompatibilities should be resolved immediately to validate the upgrade. |

### Unsupported Packages
5 projects have NuGet packages with recommended updates. No packages are incompatible with .NET 10; all have modern versions available.

| Value | Description |
|-------|-------------|
| **Update all recommended packages** (selected) | Upgrade 14 packages during the upgrade to latest stable versions compatible with .NET 10. Addresses known vulnerabilities and improvements. |
| Update critical only | Update only security-critical packages, defer others. Not recommended: the recommended packages are already identified as necessary. |

## Modernization

### Nullable Reference Types
All projects target .NET 5.0+. Nullable Reference Types (NRTs) are not currently enabled based on assessment signals.

| Value | Description |
|-------|-------------|
| **Enable during upgrade** (selected) | Enable NRTs across all projects as part of the .NET 10 upgrade. Provides compile-time null safety, aligns with modern .NET practices. Requires resolving nullable warnings (estimated 50-100 across codebase). |
| Skip NRTs | Leave nullable disabled. Acceptable if NRT adoption is not a priority; can be enabled later. |

