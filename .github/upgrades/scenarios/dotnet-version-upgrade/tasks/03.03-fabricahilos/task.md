# 03.03-fabricahilos: Upgrade FabricaHilos (main Razor Pages app) to net10.0 (96 issues, 18 mandatory, high-complexity)

# 03.03-fabricahilos: Upgrade FabricaHilos main Razor Pages application

## Objective

Upgrade FabricaHilos (the critical main application) from net8.0 to net10.0. This is the most complex project with 96 issues including 17 mandatory binary incompatibilities, requiring systematic API and package updates.

## Scope

1. Update `.csproj` TargetFramework from `net8.0` → `net10.0`
2. Update 7 recommended NuGet packages to 10.0.10:
   - Microsoft.AspNetCore.Identity.EntityFrameworkCore: 8.0.0 → 10.0.10
   - Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation: 8.0.* → 10.0.10
   - Microsoft.EntityFrameworkCore.Design: 8.0.0 → 10.0.10
   - Microsoft.EntityFrameworkCore.Sqlite: 8.0.0 → 10.0.10
   - Microsoft.EntityFrameworkCore.Tools: 8.0.0 → 10.0.10
   - Microsoft.Extensions.Caching.Memory: 8.0.1 → 10.0.10
   - System.IO.Packaging: 6.0.1 → 10.0.10
3. Build and fix 17 mandatory binary incompatibilities and 33 source incompatibilities

## Assessment Data

- **Current TFM**: net8.0 → **Target**: net10.0
- **SDK-style**: ✅ Yes
- **Project Kind**: AspNetCore (Razor Pages)
- **Issue Count**: 96 total (18 Mandatory, 78 Potential)
- **Files**: 532 files (largest project)
- **Issues by Category**:
  - `Project.0002` (Mandatory): Change target framework [1 occurrence]
  - `Api.0001` (Mandatory): Binary incompatibilities [17 occurrences] ⚠️⚠️
  - `Api.0002` (Potential): Source incompatibilities [33 occurrences]
  - `Api.0003` (Potential): Behavioral changes [38 occurrences]
  - `NuGet.0002` (Potential): Package updates [7 occurrences]
- **Package Updates Required**: 7 packages to 10.0.10 (see above)
- **Risk**: Medium-High (17 binary incompatibilities, 532 files to validate)
- **Estimated Effort**: 2-4 hours

## Key Technologies

- Razor Pages web framework
- Entity Framework Core (multiple versions: Design, Tools, Sqlite)
- ASP.NET Core Identity + EntityFrameworkCore integration
- Oracle.ManagedDataAccess.Core 23.26.100 (compatible)
- Serilog.AspNetCore (logging)
- ClosedXML (Excel generation)
- QuestPDF (PDF generation, compatible)
- SixLabors.ImageSharp (image processing)
- System.IO.Packaging (packaging APIs)
- Dapper (micro-ORM, compatible)
- Razor runtime compilation (development feature)

## Critical Areas to Review

1. **EF Core Migrations**: Verify all migrations apply correctly with .NET 10
2. **Razor Pages Runtime Compilation**: Check configuration is compatible
3. **Identity Configuration**: Review startup configuration and DbContext setup
4. **Oracle.ManagedDataAccess**: Verify connection pooling works as expected
5. **Custom Middleware**: Audit any pipeline configuration for pipeline order changes
6. **Serialization**: Check System.Text.Json configurations if used

## Done When

- ✅ FabricaHilos TargetFramework updated to net10.0
- ✅ Project builds successfully in Release mode
- ✅ All 7 recommended packages updated to 10.0.10
- ✅ No compilation errors
- ✅ All 17 binary incompatibilities resolved
- ✅ No runtime-related warnings
- ✅ Razor Pages runtime compilation verified working
- ✅ Database connections and migrations verified

## Related Skills

- `managing-target-frameworks`
- `managing-package-references`
- `building-projects`

