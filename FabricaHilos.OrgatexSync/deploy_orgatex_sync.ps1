# Script de redeploy seguro para FabricaHilos-OrgatexSync (correr EN el server \\10.0.7.14)
# Verifica que el proceso viejo realmente termine antes de sobrescribir archivos.

$svcName    = "FabricaHilos-OrgatexSync"
$exeName    = "FabricaHilos.OrgatexSync"
$publishDir = "D:\Development\Code\WorkSpace\Publish\OrgatexSync"
$deployDir  = "C:\FabricaHilos_ServiciosWindows\OrgatexSync"

Write-Host "== 1. Deteniendo servicio =="
Stop-Service -Name $svcName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "== 2. Verificando que NO quede el proceso vivo (zombie) =="
$proc = Get-Process -Name $exeName -ErrorAction SilentlyContinue
if ($proc) {
    Write-Warning "Proceso $exeName seguia vivo tras Stop-Service (PID $($proc.Id)). Forzando kill..."
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 2
}
$proc = Get-Process -Name $exeName -ErrorAction SilentlyContinue
if ($proc) {
    throw "El proceso $exeName sigue vivo (PID $($proc.Id)). Abortando deploy para no dejar binario a medio copiar."
}
Write-Host "OK: no hay proceso $exeName corriendo."

Write-Host "== 3. Eliminando servicio =="
sc.exe delete $svcName | Out-Null
Start-Sleep -Seconds 1

Write-Host "== 4. Copiando binarios nuevos =="
robocopy $publishDir $deployDir /MIR /R:2 /W:2 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy fallo con codigo $LASTEXITCODE" }

Write-Host "== 5. Verificando contenido del .dll desplegado =="
$deployedDll = Join-Path $deployDir "$exeName.dll"
$bytes = [System.IO.File]::ReadAllBytes($deployedDll)
$text  = [System.Text.Encoding]::Unicode.GetString($bytes)
Write-Host ("Contiene 'RecipeAmount': {0}" -f $text.Contains("RecipeAmount"))
Write-Host ("Contiene 'RecipeUnit'  : {0}" -f $text.Contains("RecipeUnit"))
Write-Host ("Contiene 'P_TOTAL'     : {0}" -f $text.Contains("P_TOTAL"))
if (-not ($text.Contains("RecipeAmount") -and $text.Contains("RecipeUnit") -and $text.Contains("P_TOTAL"))) {
    throw "El .dll desplegado NO contiene el codigo esperado. No continuar, revisar publishDir."
}
Get-FileHash $deployedDll -Algorithm SHA256 | Format-List

Write-Host "== 6. Creando y arrancando servicio =="
sc.exe create $svcName binPath= "$deployDir\$exeName.exe" displayName= "FabricaHilos - Orgatex Sync - Carga" start= auto | Out-Null
Start-Service -Name $svcName

Write-Host "== 7. Confirmando estado final =="
Get-Service -Name $svcName | Format-List Name, Status
Get-Process -Name $exeName -ErrorAction SilentlyContinue | Select-Object Id, StartTime, Path
