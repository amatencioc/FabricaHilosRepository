# 01-prerequisites: Verify SDK and toolchain compatibility

Ensure the local development environment is ready for .NET 10 development. Verify .NET 10 SDK is installed, compatible with global.json flags (if present), and that all build tools are updated. Review any custom build scripts or CI/CD configurations that may need adjustment for .NET 10.

This is a prerequisite that must complete before any project files are modified.

**Done when**: 
- .NET 10 SDK is installed and available in PATH
- `dotnet --version` reports .NET 10.x
- global.json (if present) is compatible with .NET 10
- MSBuild and build environment verified as ready
