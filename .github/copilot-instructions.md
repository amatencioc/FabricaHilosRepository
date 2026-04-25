# Copilot Instructions

## General Guidelines
- When the user asks to make validation "equal" or "the same" as another field's validation, they mean the validation MESSAGE TEXT should follow the same format/pattern, NOT that the validation logic should change.
- Use hardcoded emoji icons for dashboard charts instead of dynamic/generic solutions to enhance visual richness, even if data changes slightly.
- Every alert or notification must use a Bootstrap modal instead of the native browser alert() dialog.

## Configuration Management
- Maintain `appsettings-template.json` in sync with `appsettings.json`: Whenever a key is added, modified, or removed in `appsettings.json` (especially in the sections "Menus", "ConnectionStrings", "EmpresaTema", routes, notifications, etc.), update `appsettings-template.json` with the same structure but without sensitive data (use placeholders like HOST, USUARIO, CLAVE, SERVIDOR, correo@empresa.com, 00000000000). The flags in "Menus" in the template should always be set to false.

## Database Guidelines
- The user uses Oracle 10g as the database and Toad as the administration tool. Avoid modern Oracle functions like LISTAGG(DISTINCT ...); always use subqueries with DISTINCT + standard LISTAGG for compatibility with Oracle 10g.

## Data Interpretation Rules
- In the UBIGEO table:
  - When PAIS='01', it means Peru (NOM_DPT=departamento, NOM_DTT=distrito).
  - When PAIS≠'01' and EXPORTACION='N', NOM_DPT is a foreign country name and NOM_DTT is the city/district of that country (purchased in Peru).
  - When PAIS≠'01' and EXPORTACION='S', NOM_DPT is a foreign country name and NOM_DTT is the city/district of that country (export).
- In the CLIENTES table, C.PAIS='PE' represents Peru.

## Architecture Rules
- When creating a new module (folder/sub-folders/views) in FabricaHilos, perform the following steps:
  1. Add the necessary flags in `appsettings.json` (section "Menus") and in `MenuOptions.cs`.
  2. Create the module's Controller with its `Index()` action that conditionally builds the list of cards (SgcModuloDto / equivalent pattern) using the flags from `IMenuService`.
  3. Wrap the sidebar items in `_Layout.cshtml` with `@if (menus.XxxFlag)`, respecting the structure of sections (sidebar-section), parent items, and collapsible sub-items, similar to existing modules.
  4. Update `IMenuService` / `MenuService` to expose the new flags.
  5. Update any menu options/configuration classes (MenuOptions, etc.) with the new boolean properties.
  6. Update `appsettings-template.json` with the new flags set to `false` and no sensitive data.
  7. Always add the corresponding card in the Home/Index dashboard (HomeController + Views/Home/Index.cshtml) when creating a new module with a sidebar menu entry. The sidebar and the dashboard cards must always be in sync.
- Complete these steps BEFORE proceeding with the business logic of the new module.

## JSON Configuration Sync Rules
- Whenever a key is added, modified, or removed in `appsettings.json`, always update `appsettings-template.json` with the same structure but replacing sensitive data with placeholders:
  - IPs / hostnames → `HOST` or `SERVIDOR`
  - Passwords → `CLAVE`
  - Usernames → `USUARIO`
  - Real email addresses → `correo@empresa.com`
  - RUC / tax IDs → `00000000000`
  - Real network paths → `\\\\SERVIDOR\\CARPETA`
- All flags in the `"Menus"` section of `appsettings-template.json` must always be set to `false`.