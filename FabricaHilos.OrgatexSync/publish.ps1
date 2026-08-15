<#
.SYNOPSIS
	Compila FabricaHilos.OrgatexSync en Release y publica el resultado al share de red,
	borrando primero todo el contenido existente en el destino.

.DESCRIPTION
	1) Borra completamente el contenido de \\10.0.7.14\FabricaHilos_RepositoryPublish\OrgatexSync
	   (si existe).
	2) Ejecuta 'dotnet publish -c Release' contra FabricaHilos.OrgatexSync.csproj con ese
	   mismo destino como -o, dejando el binario recién compilado sin residuos de una
	   publicación anterior (DLLs viejas, appsettings.Production.json obsoleto, etc.).

.NOTES
	- Antes de ejecutar, detén el servicio de Windows "FabricaHilos OrgatexSync" en el
	  servidor si el destino es la carpeta que consume el servicio directamente (los
	  archivos en uso no se pueden sobrescribir).
	- Requiere acceso de escritura al share \\10.0.7.14\FabricaHilos_RepositoryPublish\OrgatexSync.
#>

[CmdletBinding()]
param(
	[string]$Configuracion = "Release",
	[string]$RutaDestino   = "\\10.0.7.14\FabricaHilos_RepositoryPublish\OrgatexSync"
)

$ErrorActionPreference = "Stop"

# Carpeta del proyecto = carpeta donde vive este script.
$rutaProyecto = Join-Path $PSScriptRoot "FabricaHilos.OrgatexSync.csproj"

if (-not (Test-Path $rutaProyecto)) {
	throw "No se encontró el proyecto en '$rutaProyecto'."
}

Write-Host "=== 1) Limpiando destino: $RutaDestino ===" -ForegroundColor Cyan

if (Test-Path $RutaDestino) {
	# Borra todo el CONTENIDO (archivos + subcarpetas) pero conserva la carpeta destino en sí,
	# por si el share tiene permisos/ACL específicos configurados sobre esa carpeta.
	Get-ChildItem -LiteralPath $RutaDestino -Force | Remove-Item -Recurse -Force
	Write-Host "Contenido anterior eliminado." -ForegroundColor Green
}
else {
	Write-Host "El destino no existe todavía; se creará durante el publish." -ForegroundColor Yellow
	New-Item -ItemType Directory -Path $RutaDestino -Force | Out-Null
}

Write-Host "=== 2) Publicando ($Configuracion) -> $RutaDestino ===" -ForegroundColor Cyan

dotnet publish $rutaProyecto -c $Configuracion -o $RutaDestino

if ($LASTEXITCODE -ne 0) {
	throw "dotnet publish falló con código de salida $LASTEXITCODE."
}

Write-Host "=== Publicación completada correctamente en $RutaDestino ===" -ForegroundColor Green
