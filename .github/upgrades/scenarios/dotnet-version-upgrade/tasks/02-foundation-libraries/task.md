# 02-foundation-libraries: Upgrade foundation library projects

Upgrade library projects with no internal dependencies (Tier 0): FabricaHilos.Sire, FabricaHilos.Notificaciones, and FabricaHilos.DocumentExtractor.

**Scope**: Update TargetFramework to net10.0 in project files, update all NuGet package references to versions compatible with .NET 10 (14 packages with recommended updates identified), compile and fix any code incompatibilities (API changes, behavioral differences). Foundation libraries are low-risk with <5 API issues each affecting a small codebase footprint.

**Assessment context**: FabricaHilos.Sire has 30 potential API issues (mostly behavioral changes); FabricaHilos.Notificaciones has 5 issues including 3 deprecated NuGet packages; FabricaHilos.DocumentExtractor has minimal API issues. All three are critical dependencies for the main applications.

**Research starting points**: Check for obsolete EF Core APIs, review package release notes for breaking changes, look for any custom MSBuild logic that may conflict with .NET 10.

**Done when**:
- All three projects build successfully with net10.0 target
- No compilation errors or warnings related to framework version mismatch
- Package restore completes without conflicts
- All recommended NuGet packages are updated to net10.0-compatible versions
