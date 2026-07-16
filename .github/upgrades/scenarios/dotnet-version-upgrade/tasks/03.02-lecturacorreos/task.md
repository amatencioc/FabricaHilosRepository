# 03.02-lecturacorreos: Upgrade FabricaHilos.LecturaCorreos Worker Service to net10.0 (60 issues, 2 mandatory)

# 03.02-lecturacorreos: Upgrade FabricaHilos.LecturaCorreos Worker Service

## Objective

Upgrade FabricaHilos.LecturaCorreos (a .NET Worker Service handling email reading) from net9.0 to net10.0. This project has 60 issues including 2 mandatory ones, primarily related to behavioral changes and hosting infrastructure.

## Scope

1. Update `.csproj` TargetFramework from `net9.0` → `net10.0`
2. Update 2 recommended NuGet packages:
   - Microsoft.Extensions.Hosting.WindowsServices: 9.0.0 → 10.0.10
   - Microsoft.Extensions.Http: 9.0.0 → 10.0.10
3. Build and fix API incompatibilities

## Assessment Data

- **Current TFM**: net9.0 → **Target**: net10.0
- **SDK-style**: ✅ Yes
- **Project Kind**: DotNetCoreApp (Worker Service)
- **Issue Count**: 60 total (2 Mandatory, 58 Potential)
- **Files**: 56 files
- **Issues by Category**:
  - `Project.0002` (Mandatory): Change target framework [1 occurrence]
  - `Api.0001` (Mandatory): Binary incompatibility [1 occurrence] ⚠️
  - `Api.0002` (Potential): Source incompatibility [18 occurrences]
  - `Api.0003` (Potential): Behavioral changes [38 occurrences]
  - `NuGet.0002` (Potential): Package updates [2 occurrences]
- **Package Updates Required**:
  - Microsoft.Extensions.Hosting.WindowsServices: 9.0.0 → 10.0.10 ✅
  - Microsoft.Extensions.Http: 9.0.0 → 10.0.10 ✅
  - Oracle.ManagedDataAccess.Core: 23.7.0 (compatible) ✅
  - All Serilog packages: 9.0.0 / 6.0.0 (compatible, no updates) ✅
- **Risk**: Low-Medium (binary incompatibility likely resolved by package updates)
- **Estimated Effort**: 1-2 hours

## Key Technologies

- Worker Service (BackgroundService pattern)
- Serilog logging
- MailKit + MimeKit (email handling)
- Oracle.ManagedDataAccess.Core (database)
- HTTP clients (dependency injection + HttpClientFactory)

## Done When

- ✅ FabricaHilos.LecturaCorreos TargetFramework updated to net10.0
- ✅ Project builds successfully in Release mode
- ✅ Both recommended packages updated to 10.0.10
- ✅ No compilation errors
- ✅ Binary incompatibility (Api.0001) resolved
- ✅ BackgroundService hosting layer verified as compatible

## Related Skills

- `managing-target-frameworks`
- `managing-package-references`
- `building-projects`

