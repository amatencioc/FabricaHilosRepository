<#
.SYNOPSIS
    Deploy seguro de FabricaHilos en IIS usando app_offline.htm.
    Los usuarios ven una pagina de mantenimiento mientras dura el despliegue (~30s).

.PARAMETER RutaPublicacion
    Carpeta con los archivos ya compilados (dotnet publish).
    Por defecto: bin\Release\net8.0\publish relativo al script.

.PARAMETER RutaDeploy
    Carpeta raiz del sitio en IIS.

.PARAMETER VerificarUsuarios
    Si se especifica, muestra los usuarios activos antes de deployar y pide confirmacion.

.EXAMPLE
    .\Deploy-FabricaHilos.ps1
    .\Deploy-FabricaHilos.ps1 -VerificarUsuarios
    .\Deploy-FabricaHilos.ps1 -RutaDeploy "D:\inetpub\wwwroot\FabricaHilos"
#>

param(
    [string]$RutaPublicacion = (Join-Path $PSScriptRoot "FabricaHilos\bin\Release\net8.0\publish"),
    [string]$RutaDeploy      = "D:\inetpub\wwwroot\FabricaHilos",
    [string]$UrlApp          = "http://localhost",
    [switch]$VerificarUsuarios
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─────────────────────────────────────────────────────────────────────────────
# 0. Verificar usuarios activos (opcional)
# ─────────────────────────────────────────────────────────────────────────────
if ($VerificarUsuarios) {
    Write-Host ""
    Write-Host "Consultando usuarios activos en $UrlApp/Sistemas/UsuariosActivos/Datos ..." -ForegroundColor Cyan
    try {
        $resp = Invoke-RestMethod "$UrlApp/Sistemas/UsuariosActivos/Datos" -TimeoutSec 5
        if ($resp.total -gt 0) {
            Write-Host ""
            Write-Host "  USUARIOS ACTIVOS AHORA: $($resp.total)" -ForegroundColor Yellow
            $resp.usuarios | ForEach-Object {
                Write-Host ("  [{0}] {1,-20} -> {2}" -f $_.ultimaActividad, $_.usuario, $_.pagina) -ForegroundColor Yellow
            }
            Write-Host ""
            $confirm = Read-Host "Hay usuarios activos. Continuar con el deploy? (s/N)"
            if ($confirm -ne "s") {
                Write-Host "Deploy cancelado." -ForegroundColor Red
                exit 0
            }
        } else {
            Write-Host "  Sin usuarios activos. Procediendo..." -ForegroundColor Green
        }
    } catch {
        Write-Host "  No se pudo consultar usuarios activos (app no disponible o requiere auth). Continuando..." -ForegroundColor DarkYellow
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# 1. Validaciones previas
# ─────────────────────────────────────────────────────────────────────────────
if (-not (Test-Path $RutaPublicacion)) {
    Write-Error "La carpeta de publicacion no existe: $RutaPublicacion"
    exit 1
}
if (-not (Test-Path $RutaDeploy)) {
    Write-Error "La carpeta de deploy no existe: $RutaDeploy"
    exit 1
}

$offlineFile = Join-Path $RutaDeploy "app_offline.htm"

# ─────────────────────────────────────────────────────────────────────────────
# 2. Colocar app_offline.htm — IIS detiene el proceso y sirve este HTML
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[ 1/4 ] Colocando app_offline.htm..." -ForegroundColor Cyan

$htmlOffline = @"
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="refresh" content="20">
  <title>Actualizacion del Sistema</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: 'Segoe UI', sans-serif; background: #f0f2f5;
           display: flex; justify-content: center; align-items: center; height: 100vh; }
    .box { background: #fff; border-radius: 12px; padding: 2.5rem 3rem;
           box-shadow: 0 4px 24px rgba(0,0,0,.12); text-align: center; max-width: 480px; }
    .icon { font-size: 3rem; margin-bottom: 1rem; }
    h2 { color: #1a1a2e; margin-bottom: .75rem; }
    p  { color: #6c757d; line-height: 1.6; }
    .dots { display: inline-block; animation: dots 1.4s infinite; }
    @keyframes dots { 0%,20%{content:'.'}40%{content:'..'}60%,100%{content:'...'} }
  </style>
</head>
<body>
  <div class="box">
    <div class="icon">&#x1F527;</div>
    <h2>Actualizacion en curso</h2>
    <p>El sistema esta siendo actualizado a una nueva version.<br>
       Por favor <strong>espera unos minutos</strong>.<br><br>
       Esta pagina se recargara automaticamente<span class="dots">...</span></p>
  </div>
</body>
</html>
"@

Set-Content -Path $offlineFile -Value $htmlOffline -Encoding UTF8
Write-Host "         app_offline.htm creado. IIS atendra a usuarios con pagina de mantenimiento." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 3. Esperar a que w3wp.exe libere los archivos DLL
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "[ 2/4 ] Esperando que IIS libere el proceso (12s)..." -ForegroundColor Cyan
Start-Sleep -Seconds 12

# ─────────────────────────────────────────────────────────────────────────────
# 4. Copiar archivos nuevos con robocopy (excluye app_offline.htm)
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "[ 3/4 ] Copiando archivos desde: $RutaPublicacion" -ForegroundColor Cyan

$robocopyArgs = @(
    $RutaPublicacion,
    $RutaDeploy,
    "/MIR",           # Mirror: copia nuevos, actualiza modificados, borra eliminados
    "/XF", "app_offline.htm",  # Nunca sobreescribir el offline
    "/NP",            # Sin barra de progreso
    "/NFL",           # Sin listado de archivos
    "/NDL"            # Sin listado de carpetas
)

$result = & robocopy @robocopyArgs
$exitCode = $LASTEXITCODE

# Robocopy: codigos 0-7 son exito; 8+ son error
if ($exitCode -ge 8) {
    Write-Host "ERROR en robocopy (codigo $exitCode). Revisa los archivos manualmente." -ForegroundColor Red
    Write-Host "app_offline.htm permanece activo. Eliminalo manualmente cuando estes listo." -ForegroundColor Yellow
    exit $exitCode
}

Write-Host "         Archivos copiados correctamente (robocopy exit: $exitCode)." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 5. Quitar app_offline.htm — IIS reactiva la aplicacion
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "[ 4/4 ] Reactivando aplicacion (eliminando app_offline.htm)..." -ForegroundColor Cyan
Remove-Item $offlineFile -Force

Write-Host ""
Write-Host "Deploy completado exitosamente." -ForegroundColor Green
Write-Host "URL: $UrlApp" -ForegroundColor Cyan
Write-Host ""
