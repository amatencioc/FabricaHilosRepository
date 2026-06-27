$f = "D:\.Net\Dev\FabricaHilosRepository\FabricaHilos\Views\Produccion\Planeamiento\RegistroPedido.cshtml"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)

$start = $c.IndexOf("        @* F. Registro (servidor) *@")
$end   = $c.IndexOf("@* ── TABLA DIN")

Write-Host "Start=$start  End=$end"

if ($start -ge 0 -and $end -gt $start) {
    # Remove old form remnant block (from start to just before table comment)
    $newContent = $c.Substring(0, $start) + $c.Substring($end)
    [System.IO.File]::WriteAllText($f, $newContent, [System.Text.Encoding]::UTF8)
    Write-Host "Removed $($end - $start) chars of old form content. File saved."
} else {
    Write-Host "ERROR: Block not found. start=$start end=$end"
}
