# Upgrade Plan — FabricaHilos to .NET 10

## Selected Strategy

**All-At-Once** — All projects upgraded simultaneously in a single operation.

**Rationale**: 6 modern .NET projects (all .NET 8/9), all SDK-style csproj format, clear 2-tier dependency structure (foundation libraries → applications). Single atomic upgrade maximizes validation efficiency and allows for coordinated testing across the entire solution.

---

## Task Breakdown

### 01-prerequisites: Verify SDK and toolchain compatibility

Ensure the local development environment is ready for .NET 10 development. Verify .NET 10 SDK is installed, compatible with global.json flags (if present), and that all build tools are updated. Review any custom build scripts or CI/CD configurations that may need adjustment for .NET 10.

This is a prerequisite that must complete before any project files are modified.

**Done when**: 
- .NET 10 SDK is installed and available in PATH
- `dotnet --version` reports .NET 10.x
- global.json (if present) is compatible with .NET 10
- MSBuild and build environment verified as ready

---

### 02-foundation-libraries: Upgrade foundation library projects

Upgrade library projects with no internal dependencies (Tier 0): FabricaHilos.Sire, FabricaHilos.Notificaciones, and FabricaHilos.DocumentExtractor.

**Scope**: Update TargetFramework to net10.0 in project files, update all NuGet package references to versions compatible with .NET 10 (14 packages with recommended updates identified), compile and fix any code incompatibilities (API changes, behavioral differences). Foundation libraries are low-risk with <5 API issues each affecting a small codebase footprint.

**Assessment context**: FabricaHilos.Sire has 30 potential API issues (mostly behavioral changes); FabricaHilos.Notificaciones has 5 issues including 3 deprecated NuGet packages; FabricaHilos.DocumentExtractor has minimal API issues. All three are critical dependencies for the main applications.

**Research starting points**: Check for obsolete EF Core APIs, review package release notes for breaking changes, look for any custom MSBuild logic that may conflict with .NET 10.

**Done when**:
- All three projects build successfully with net10.0 target
- No compilation errors or warnings related to framework version mismatch
- Package restore completes without conflicts
- All recommended NuGet packages are updated to net10.0-compatible versions

---

### 03-mid-tier-and-applications: Upgrade mid-tier and application projects

Upgrade the remaining projects: FabricaHilos.LecturaCorreos (Worker Service, depends on Notificaciones), FabricaHilos (main Razor Pages web app, depends on Sire and Notificaciones), and LaColonial (standalone web application).

**Scope**: Update TargetFramework to net10.0, update all NuGet package references, address API incompatibilities, fix behavioral changes. These projects are more complex: FabricaHilos carries 96 issues (18 mandatory) — mostly API-related and behavioral. FabricaHilos.LecturaCorreos is a Worker Service with 60 issues (2 mandatory), including possible changes in hosting/DI infrastructure.

**Assessment context**: FabricaHilos (Razor Pages) is the critical application with Oracle.ManagedDataAccess, Entity Framework Core, and custom middleware likely in use. FabricaHilos.LecturaCorreos is a background service with dependency on external email/HTTP APIs. LaColonial has minimal issues (3 total, 1 mandatory).

**Research starting points**: Review EF Core migrations for compatibility, check Oracle.ManagedDataAccess compatibility with .NET 10, verify background service hosting (BackgroundService vs. WindowsService), look for Razor Pages runtime issues, audit any custom configuration/injection logic.

**Known risks**: 88+ API issues in FabricaHilos alone; potential EF Core query translation changes; possible System.Text.Json serialization differences; middleware pipeline changes in .NET 10.

**Done when**:
- FabricaHilos, FabricaHilos.LecturaCorreos, and LaColonial all build with net10.0 target
- No compilation errors; all runtime-related warnings addressed
- Package updates complete and restore succeeds
- All recommended NuGet upgrades applied across projects

---

### 04-solution-validation: Full build, tests, and finalization

Perform a complete solution-wide build and run all unit test projects to validate the entire upgraded solution.

**Scope**: Build the entire solution to net10.0 in Release configuration (to catch any conditional compilation issues), run test projects, document any deferred manual fixes or known issues, and perform a final health check on the application startup/initialization sequence.

**Done when**:
- Full solution builds with 0 errors in both Debug and Release configurations
- All unit tests pass (or deferred failures are documented with mitigation plans)
- No warnings from compiler or NuGet related to framework compatibility
- LaColonial and FabricaHilos start successfully in development environment
- Assessment-flagged issues are either resolved or formally documented with rationale

---

## Nullable Reference Types Modernization

Nullable Reference Types (NRTs) are enabled as part of the upgrade (per confirmed options). During execution, projects will have NRTs enabled progressively, and warnings will be resolved as part of the code-fix tasks above. This improves long-term code safety and aligns with modern .NET best practices.

---

## Legacy Configuration & Continuity

All projects are already modern .NET and SDK-style. No binding redirects, config transformation, or legacy migration patterns are required. The upgrade is a forward-moving modernization, not a migration from legacy frameworks.

