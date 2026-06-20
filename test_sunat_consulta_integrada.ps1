<#
.SYNOPSIS
  Prueba la API de Consulta Integrada de Validez de CPE de SUNAT.
  Manual: "Manual-de-Consulta-Integrada-de-Comprobante-de-Pago-por-ServicioWEB_v2_0"

.DESCRIPCION
  Metodo 1 (SIRE - clientessol + password): usa las mismas credenciales que SIRE.
    Auth: POST https://api-seguridad.sunat.gob.pe/v1/clientessol/{clientId}/oauth2/token/
    grant_type=password, scope=https://api.sunat.gob.pe/v1/contribuyente/contribuyentes
    username={RUC}{UsuarioSol}, password={ClaveSol}
    Funciona si el clientId tiene habilitado el scope de Consulta Integrada.

  Metodo 2 (clientesextranet + client_credentials): requiere credenciales distintas.
    Se crean en SUNAT SOL: Empresas > CPE > Consulta de Validez > Credenciales de API SUNAT
    Usar solo si el Metodo 1 falla con 400/401.

  Servicio: POST https://api.sunat.gob.pe/v1/contribuyente/contribuyentes/{RUC}/validarcomprobante
  Respuesta: estadoCp: 0=NO EXISTE, 1=ACEPTADO, 2=ANULADO, 3=AUTORIZADO, 4=NO AUTORIZADO

.PARAMETROS
  -Periodo    : Periodo YYYYMM a consultar (default: 202606)
  -Tipo       : 'compras' o 'ventas' (default: compras)
  -ClientId   : Client ID (default: credencial SIRE de appsettings.json)
  -ClientSecret: Client Secret (default: credencial SIRE de appsettings.json)
  -RUC        : RUC empresa (default: 20100096260)
  -UsuarioSol : Usuario SOL (default: SISTEM10)
  -ClaveSol   : Clave SOL (default: Sistemas09$)
  -MaxDocs    : Maximos documentos a probar (0 = todos). Default: 20
  -Metodo     : 1=clientessol/password (default), 2=clientesextranet/client_credentials
#>
param(
    [string]$Periodo      = "202606",
    [string]$Tipo         = "compras",
    [string]$ClientId     = "2abcb7db-c537-4414-9d27-aa4e27350552",
    [string]$ClientSecret = "HMtGdUoF44gxnCq5o7Soxg==",
    [string]$RUC          = "20100096260",
    [string]$UsuarioSol   = "SISTEM10",
    [string]$ClaveSol     = "Sistemas09`$",
    [int]   $MaxDocs      = 20,
    [int]   $Metodo       = 2
)

$rucEmpresa  = $RUC
$scopeVal    = "https://api.sunat.gob.pe/v1/contribuyente/contribuyentes"
$apiUrl      = "https://api.sunat.gob.pe/v1/contribuyente/contribuyentes/$rucEmpresa/validarcomprobante"

$tipoDb = if ($Tipo -eq "ventas") { "1" } else { "2" }

$metodoNombre = if ($Metodo -eq 2) { "Metodo 2: clientesextranet/client_credentials" } else { "Metodo 1: clientessol/password (credenciales SIRE)" }

Write-Host ""
Write-Host "  SUNAT - Consulta Integrada de Validez de CPE" -ForegroundColor Cyan
Write-Host "  Periodo: $Periodo  |  Tipo: $Tipo  |  MaxDocs: $(if($MaxDocs -eq 0){'TODOS'}else{$MaxDocs})" -ForegroundColor Cyan
Write-Host "  $metodoNombre" -ForegroundColor Cyan
Write-Host ""

# 1. Obtener token OAuth2
Write-Host "[1/3] Obteniendo token OAuth2..." -ForegroundColor Yellow

$token = $null

if ($Metodo -eq 1) {
    # Metodo 1: mismas credenciales SIRE (clientessol + password grant)
    # Funciona si el clientId tiene habilitado el scope de Consulta Integrada en SUNAT
    $authUrl1 = "https://api-seguridad.sunat.gob.pe/v1/clientessol/$ClientId/oauth2/token/"
    $solUser  = $RUC + $UsuarioSol
    Write-Host "  Intentando clientessol + password (usuario SOL: $solUser)..." -ForegroundColor DarkGray
    $formBody1 = @{
        grant_type    = "password"
        scope         = $scopeVal
        client_id     = $ClientId
        client_secret = $ClientSecret
        username      = $solUser
        password      = $ClaveSol
    }
    try {
        $tokenResp = Invoke-RestMethod -Method Post -Uri $authUrl1 `
            -ContentType "application/x-www-form-urlencoded" `
            -Body $formBody1 -ErrorAction Stop
        $token = $tokenResp.access_token
        Write-Host "  Token obtenido OK (Metodo 1). Expira en: $($tokenResp.expires_in)s" -ForegroundColor Green
    }
    catch {
        $msg1 = $_.Exception.Message
        Write-Host "  Metodo 1 fallo: $msg1" -ForegroundColor DarkYellow
        Write-Host "  El clientId no tiene el scope de Consulta Integrada habilitado." -ForegroundColor DarkYellow
        Write-Host "  Para crear credenciales Consulta Integrada en SUNAT SOL:" -ForegroundColor DarkYellow
        Write-Host "    Empresas > CPE > Consulta de Validez > Credenciales de API SUNAT" -ForegroundColor DarkYellow
        Write-Host "  Luego: .\test_sunat_consulta_integrada.ps1 -Metodo 2 -ClientId X -ClientSecret Y" -ForegroundColor DarkYellow
        exit 1
    }
}
else {
    # Metodo 2: clientesextranet + client_credentials (credenciales especificas de Consulta Integrada)
    $authUrl2 = "https://api-seguridad.sunat.gob.pe/v1/clientesextranet/$ClientId/oauth2/token/"
    Write-Host "  Intentando clientesextranet + client_credentials..." -ForegroundColor DarkGray
    $formBody2 = @{
        grant_type    = "client_credentials"
        scope         = $scopeVal
        client_id     = $ClientId
        client_secret = $ClientSecret
    }
    try {
        $tokenResp = Invoke-RestMethod -Method Post -Uri $authUrl2 `
            -ContentType "application/x-www-form-urlencoded" `
            -Body $formBody2 -ErrorAction Stop
        $token = $tokenResp.access_token
        Write-Host "  Token obtenido OK (Metodo 2). Expira en: $($tokenResp.expires_in)s" -ForegroundColor Green
    }
    catch {
        $msg2 = $_.Exception.Message
        Write-Host "  ERROR obteniendo token (Metodo 2): $msg2" -ForegroundColor Red
        Write-Host "  Verifica ClientId/ClientSecret creados para Consulta de Validez en SUNAT SOL." -ForegroundColor DarkYellow
        exit 1
    }
}

# 2. Cargar documentos desde Oracle
Write-Host "`n[2/3] Consultando documentos desde Oracle SIRE_PROPUESTA..." -ForegroundColor Yellow

$oracleDll = "D:\.Net\Dev\FabricaHilosRepository\FabricaHilos\bin\Debug\net8.0\Oracle.ManagedDataAccess.dll"
$docs = @()

if (Test-Path $oracleDll) {
    try {
        Add-Type -Path $oracleDll -ErrorAction Stop
        $connStr = "Data Source=10.0.7.11:1521/ORCL;User Id=SIG;Password=STARK;Pooling=false"
        $conn    = New-Object Oracle.ManagedDataAccess.Client.OracleConnection($connStr)
        $conn.Open()

        $limitClause = if ($MaxDocs -gt 0) { "AND ROWNUM <= $MaxDocs" } else { "" }
        $sql = "SELECT p.RUC, p.TIPDOC, p.SERIE, p.NUMERO," +
               " TO_CHAR(p.F_EMISION,'DD/MM/YYYY') AS FCH_EMISION," +
               " TO_CHAR(p.TOTAL_CP, 'FM99999990.00') AS MONTO," +
               " p.MONEDA, c.ESTADO AS ESTADO_CONCIL" +
               " FROM SIG.SIRE_PROPUESTA p" +
               " JOIN SIG.SIRE_CONCIL c ON c.ID_PROP = p.ID_PROP" +
               " WHERE p.TIPO = '$tipoDb'" +
               " AND p.PERIODO = $Periodo" +
               " AND p.TIPDOC IN ('01','03')" +
               " AND c.ESTADO NOT IN ('EXCLUIDO','SOLO_LEGACY')" +
               " AND p.F_EMISION IS NOT NULL AND p.TOTAL_CP IS NOT NULL" +
               " $limitClause" +
               " ORDER BY DECODE(c.ESTADO,'SOLO_SUNAT',1,'OK',2,'AVISO',3,4),p.RUC,p.SERIE,p.NUMERO"
        $cmd    = New-Object Oracle.ManagedDataAccess.Client.OracleCommand($sql, $conn)
        $reader = $cmd.ExecuteReader()

        while ($reader.Read()) {
            $docs += [pscustomobject]@{
                RUC          = $reader["RUC"].ToString().Trim()
                TIPDOC       = $reader["TIPDOC"].ToString().Trim()
                SERIE        = $reader["SERIE"].ToString().Trim()
                NUMERO       = $reader["NUMERO"].ToString().Trim()
                FCH_EMISION  = $reader["FCH_EMISION"].ToString().Trim()
                MONTO        = [decimal]($reader["MONTO"].ToString().Trim())
                MONEDA       = $reader["MONEDA"].ToString().Trim()
                ESTADO_BD    = $reader["ESTADO_CONCIL"].ToString().Trim()
            }
        }
        $reader.Close(); $conn.Close()
        Write-Host "  $($docs.Count) documentos cargados desde Oracle." -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: No se pudo conectar a Oracle: $_" -ForegroundColor DarkYellow
        Write-Host "  Continuando con datos hardcodeados de muestra..." -ForegroundColor DarkYellow
    }
}
else {
    Write-Host "  WARN: DLL Oracle no encontrado en $oracleDll" -ForegroundColor DarkYellow
}

# Fallback: muestra hardcodeada si Oracle no disponible
if ($docs.Count -eq 0) {
    Write-Host "  Usando muestra hardcodeada (5 docs SOLO_SUNAT de 202606 compras)..." -ForegroundColor DarkYellow
    $docs = @(
        [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FC05"; NUMERO="5125861"; FCH_EMISION="02/06/2026"; MONTO=[decimal]490;    MONEDA="PEN"; ESTADO_BD="SOLO_SUNAT" }
        [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="815803";  FCH_EMISION="01/06/2026"; MONTO=[decimal]301.96; MONEDA="USD"; ESTADO_BD="SOLO_SUNAT" }
        [pscustomobject]@{ RUC="20347646891"; TIPDOC="01"; SERIE="FF15"; NUMERO="24897";   FCH_EMISION="08/06/2026"; MONTO=[decimal]5565.95;MONEDA="USD"; ESTADO_BD="SOLO_SUNAT" }
        [pscustomobject]@{ RUC="20100199743"; TIPDOC="01"; SERIE="F203"; NUMERO="1347";    FCH_EMISION="06/06/2026"; MONTO=[decimal]1000;   MONEDA="PEN"; ESTADO_BD="SOLO_SUNAT" }
        [pscustomobject]@{ RUC="20418896915"; TIPDOC="01"; SERIE="F226"; NUMERO="729339";  FCH_EMISION="10/06/2026"; MONTO=[decimal]500;    MONEDA="PEN"; ESTADO_BD="SOLO_SUNAT" }
    )
}

# 3. Consultar validez para cada documento
$estadoCpDesc = @{
    "0" = "NO EXISTE"
    "1" = "ACEPTADO"
    "2" = "ANULADO"
    "3" = "AUTORIZADO (imprenta)"
    "4" = "NO AUTORIZADO (imprenta)"
}

Write-Host "`n[3/3] Consultando validez en SUNAT ($($docs.Count) documentos)...`n" -ForegroundColor Yellow
Write-Host ("{0,-14} {1,-5} {2,-6} {3,-10} {4,-12} {5,-8} {6,-11} {7,-11} {8}" -f `
    "RUC","TIP","SERIE","NUMERO","FCH_EMIS","MONTO","HTTP","ESTADO_CP","RESULTADO")
Write-Host ("-" * 110)

$results = @()

foreach ($d in $docs) {
    # El n?mero en el body puede llevar ceros a la izquierda (es string en la API)
    $body = @{
        numRuc       = $d.RUC
        codComp      = $d.TIPDOC
        numeroSerie  = $d.SERIE
        numero       = [int]($d.NUMERO.TrimStart('0') -replace '[^\d]','')
        fechaEmision = $d.FCH_EMISION
        monto        = $d.MONTO
    } | ConvertTo-Json -Compress

    $httpStatus = ""
    $estadoCp   = ""
    $resultado  = ""
    $color      = "White"

    try {
        $resp = Invoke-RestMethod -Method Post -Uri $apiUrl `
            -Headers @{
                "Authorization" = "Bearer $token"
                "Content-Type"  = "application/json"
            } `
            -Body $body -ErrorAction Stop

        $httpStatus = 200
        $estadoCp   = [string]$resp.data.estadoCp
        $descEstado = if ($estadoCpDesc.ContainsKey($estadoCp)) { $estadoCpDesc[$estadoCp] } else { "estadoCp=$estadoCp" }
        $resultado  = "$descEstado | RUC: $($resp.data.estadoRuc) | Dom: $($resp.data.condDomiRuc)"

        $color = switch ($estadoCp) {
            "1" { "Green" }
            "2" { "Red" }
            "0" { "DarkYellow" }
            default { "Cyan" }
        }
    }
    catch {
        $msg = $_.Exception.Message
        if    ($msg -match '404') { $httpStatus = 404; $resultado = "Endpoint no encontrado (404)";    $color = "DarkYellow" }
        elseif($msg -match '401') { $httpStatus = 401; $resultado = 'No autorizado (401) - token?';    $color = 'Red' }
        elseif($msg -match '403') { $httpStatus = 403; $resultado = "Acceso denegado (403)";           $color = "Red" }
        elseif($msg -match '400') { $httpStatus = 400; $resultado = 'Bad Request (400) - parametros?'; $color = 'Red' }
        elseif($msg -match '(\d{3})') { $httpStatus = [int]$Matches[1]; $resultado = "HTTP $httpStatus" ; $color = "Red" }
        else  { $httpStatus = "ERR"; $resultado = $msg.Substring(0, [Math]::Min(80,$msg.Length));      $color = "Red" }
    }

    $row = [pscustomobject]@{
        RUC         = $d.RUC
        TIPDOC      = $d.TIPDOC
        SERIE       = $d.SERIE
        NUMERO      = $d.NUMERO
        FCH_EMISION = $d.FCH_EMISION
        MONTO       = $d.MONTO
        MONEDA      = $d.MONEDA
        ESTADO_BD   = $d.ESTADO_BD
        HTTP        = $httpStatus
        ESTADO_CP   = $estadoCp
        RESULTADO   = $resultado
    }
    $results += $row

    Write-Host ("{0,-14} {1,-5} {2,-6} {3,-10} {4,-12} {5,-8} {6,-11} {7,-11} {8}" -f `
        $d.RUC, $d.TIPDOC, $d.SERIE, $d.NUMERO, $d.FCH_EMISION, $d.MONTO, $httpStatus, $estadoCp, $resultado) `
        -ForegroundColor $color

    Start-Sleep -Milliseconds 200
}

# Resumen
$aceptados = @($results | Where-Object { $_.ESTADO_CP -eq "1" }).Count
$anulados  = @($results | Where-Object { $_.ESTADO_CP -eq "2" }).Count
$noExisten = @($results | Where-Object { $_.ESTADO_CP -eq "0" }).Count
$errores   = @($results | Where-Object { $_.HTTP -notin @(200) }).Count

Write-Host "`n?? RESUMEN ???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "  Total consultados: $($results.Count)" -ForegroundColor White
Write-Host "  ACEPTADOS  (estadoCp=1): $aceptados"  -ForegroundColor Green
Write-Host "  ANULADOS   (estadoCp=2): $anulados"   -ForegroundColor Red
Write-Host "  NO EXISTEN (estadoCp=0): $noExisten"  -ForegroundColor DarkYellow
Write-Host "  Errores HTTP:            $errores"     -ForegroundColor Red
Write-Host ""

if ($errores -gt 0 -and $results.Count -gt 0) {
    $first = $results[0]
    if ($first.HTTP -eq 401) {
        Write-Host '  API devuelve 401: el token no tiene permiso para validarcomprobante.' -ForegroundColor Red
        Write-Host '  El clientId SIRE obtiene token OK pero el endpoint rechaza el scope.' -ForegroundColor DarkYellow
        Write-Host '  SOLUCION: Crear credenciales especificas en SUNAT SOL:' -ForegroundColor DarkYellow
        Write-Host '    Empresas > CPE > Consulta de Validez > Credenciales de API SUNAT' -ForegroundColor DarkYellow
        Write-Host '  Luego ejecutar con -Metodo 2 -ClientId X -ClientSecret Y' -ForegroundColor DarkYellow
    }
    elseif ($first.HTTP -eq 400) {
        Write-Host '  Bad Request (400): revisar formato de parametros en el body.' -ForegroundColor Red
    }
}

# Exportar CSV
$csvPath = "D:\sunat\test_consulta_integrada_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
Write-Host "  Resultados exportados a: $csvPath" -ForegroundColor Cyan
Write-Host ""
