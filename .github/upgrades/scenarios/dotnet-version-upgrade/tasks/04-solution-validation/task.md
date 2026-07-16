# 04-solution-validation: Full build, tests, and finalization

Perform a complete solution-wide build and run all unit test projects to validate the entire upgraded solution.

**Scope**: Build the entire solution to net10.0 in Release configuration (to catch any conditional compilation issues), run test projects, document any deferred manual fixes or known issues, and perform a final health check on the application startup/initialization sequence.

**Done when**:
- Full solution builds with 0 errors in both Debug and Release configurations
- All unit tests pass (or deferred failures are documented with mitigation plans)
- No warnings from compiler or NuGet related to framework compatibility
- LaColonial and FabricaHilos start successfully in development environment
- Assessment-flagged issues are either resolved or formally documented with rationale
