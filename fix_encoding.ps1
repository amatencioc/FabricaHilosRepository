$base = "FabricaHilos\Views\Contabilidad\ActivoFijo"
$utf8 = New-Object System.Text.UTF8Encoding($false)

$fixes = @{
	"â€""  = [char]0x2014  # — em dash
	"â€""  = [char]0x2013  # – en dash  
	"â€™"  = [char]0x2019  # ' right single quote
	"â€˜"  = [char]0x2018  # ' left single quote
	"â€œ"  = [char]0x201C  # " left double quote
	"â€"   = [char]0x201D  # " right double quote
	"Ã³"   = "ó"
	"Ã¡"   = "á"
	"Ã©"   = "é"
	"Ã­"   = "í"
	"Ãº"   = "ú"
	"Ã±"   = "ñ"
	"Ã""   = "Á"
	"Ã‰"   = "É"
	"Ã"    = "Í"
	"Ã""   = "Ó"
	"Ãš"   = "Ú"
	"Ã'"   = "Ñ"
	"Â¿"   = "¿"
	"Â¡"   = "¡"
	"Â·"   = "·"
	"Â°"   = "°"
	"Ã "   = "à"
	"Ãœ"   = "ü"
	"Ã¼"   = "ü"
}

$files = Get-ChildItem $base -Filter "*.cshtml"
foreach ($f in $files) {
	$txt = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
	$changed = $false
	foreach ($k in $fixes.Keys) {
		if ($txt.Contains($k)) {
			$txt = $txt.Replace($k, [string]$fixes[$k])
			$changed = $true
		}
	}
	if ($changed) {
		[System.IO.File]::WriteAllText($f.FullName, $txt, $utf8)
		Write-Host "FIXED: $($f.Name)"
	} else {
		Write-Host "SKIP:  $($f.Name)"
	}
}
Write-Host "DONE"
