# 03-mid-tier-and-applications: Upgrade mid-tier and application projects

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
