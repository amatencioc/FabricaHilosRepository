<#
.SYNOPSIS
  Prueba la API CPE GEM de SUNAT con TODOS los documentos de la propuesta período 202606
  (estados OK, AVISO, SOLO_SUNAT — excluyendo EXCLUIDO y SOLO_LEGACY).
  Obtiene token OAuth2 y llama al endpoint por cada comprobante.

.NOTAS
  BASE URL: https://api-cpe.sunat.gob.pe/v1/contribuyente/gem/comprobantes/{ruc}/{tipdoc}/{serie}/{numero}
  El número NO lleva padding (TrimStart '0').
  Solo se prueban documentos tipdoc 01 (facturas/boletas electrónicas).
#>

# ── Credenciales (tomadas de appsettings.json) ──────────────────────────────
$clientId     = "a66bb2c8-a684-49c3-a283-cf68c96282eb"
$clientSecret = "U01btxUJzVlhmyPL7Pv93g=="
$rucEmpresa   = "20100096260"
$usuarioSol   = "SISTEM10"
$claveSol     = "Sistemas09$"
$scope        = "https://sunat.gob.pe"

$authUrl  = "https://api-seguridad.sunat.gob.pe/v1/clientessol/$clientId/oauth2/token/"
$cpeBase  = "https://api-cpe.sunat.gob.pe/v1/contribuyente/gem/comprobantes"

# ── 1. Obtener token ─────────────────────────────────────────────────────────
Write-Host "`n[1/2] Obteniendo token OAuth2 de SUNAT..." -ForegroundColor Cyan

$formBody = @{
    grant_type    = "password"
    scope         = $scope
    client_id     = $clientId
    client_secret = $clientSecret
    username      = "$rucEmpresa$usuarioSol"
    password      = $claveSol
}

try {
    $tokenResp = Invoke-RestMethod -Method Post -Uri $authUrl `
        -ContentType "application/x-www-form-urlencoded" `
        -Body $formBody -ErrorAction Stop
    $token = $tokenResp.access_token
    Write-Host "  Token obtenido OK. Expira en: $($tokenResp.expires_in)s" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR obteniendo token: $_" -ForegroundColor Red
    exit 1
}

# ── 2. Documentos propuesta SUNAT período 202606 — tipdoc 01 (OK/AVISO/SOLO_SUNAT) ──
# Fuente: SELECT RUC,TIPDOC,SERIE,NUMERO,ESTADO FROM SIG.SIRE_CONCIL WHERE PERIODO='202606' AND TIPO='2'
#         AND ESTADO NOT IN ('EXCLUIDO','SOLO_LEGACY') AND TIPDOC='01' ORDER BY SERIE,NUMERO
$docs = @(
    # ── SERIE E001 ──────────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20147966653"; TIPDOC="01"; SERIE="E001"; NUMERO="1294";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20609773058"; TIPDOC="01"; SERIE="E001"; NUMERO="1320";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20603683219"; TIPDOC="01"; SERIE="E001"; NUMERO="13836";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20603683219"; TIPDOC="01"; SERIE="E001"; NUMERO="13881";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20613310038"; TIPDOC="01"; SERIE="E001"; NUMERO="1461";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20602410740"; TIPDOC="01"; SERIE="E001"; NUMERO="1714";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20492962318"; TIPDOC="01"; SERIE="E001"; NUMERO="1798";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10085524246"; TIPDOC="01"; SERIE="E001"; NUMERO="188";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10085524246"; TIPDOC="01"; SERIE="E001"; NUMERO="189";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10085524246"; TIPDOC="01"; SERIE="E001"; NUMERO="190";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10088311936"; TIPDOC="01"; SERIE="E001"; NUMERO="194";      ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="10088311936"; TIPDOC="01"; SERIE="E001"; NUMERO="195";      ESTADO="OK" }
    [pscustomobject]@{ RUC="10086287965"; TIPDOC="01"; SERIE="E001"; NUMERO="2086";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10086287965"; TIPDOC="01"; SERIE="E001"; NUMERO="2091";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20612036820"; TIPDOC="01"; SERIE="E001"; NUMERO="276";      ESTADO="OK" }
    [pscustomobject]@{ RUC="17180957936"; TIPDOC="01"; SERIE="E001"; NUMERO="2875";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20609147939"; TIPDOC="01"; SERIE="E001"; NUMERO="295";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20609147939"; TIPDOC="01"; SERIE="E001"; NUMERO="297";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20609147939"; TIPDOC="01"; SERIE="E001"; NUMERO="298";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10198490186"; TIPDOC="01"; SERIE="E001"; NUMERO="328";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10086929878"; TIPDOC="01"; SERIE="E001"; NUMERO="3950";     ESTADO="OK" }
    [pscustomobject]@{ RUC="10457524001"; TIPDOC="01"; SERIE="E001"; NUMERO="463";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10105390667"; TIPDOC="01"; SERIE="E001"; NUMERO="658";      ESTADO="OK" }
    [pscustomobject]@{ RUC="20614520320"; TIPDOC="01"; SERIE="E001"; NUMERO="937";      ESTADO="AVISO" }
    # ── SERIE F001 ──────────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20536252666"; TIPDOC="01"; SERIE="F001"; NUMERO="10228";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20536252666"; TIPDOC="01"; SERIE="F001"; NUMERO="10253";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20537506131"; TIPDOC="01"; SERIE="F001"; NUMERO="1026766";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20537506131"; TIPDOC="01"; SERIE="F001"; NUMERO="1026773";  ESTADO="OK" }
    [pscustomobject]@{ RUC="10414535190"; TIPDOC="01"; SERIE="F001"; NUMERO="1036";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10414535190"; TIPDOC="01"; SERIE="F001"; NUMERO="1038";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20515624881"; TIPDOC="01"; SERIE="F001"; NUMERO="11037";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20615387780"; TIPDOC="01"; SERIE="F001"; NUMERO="1107";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100192650"; TIPDOC="01"; SERIE="F001"; NUMERO="1131";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20330096749"; TIPDOC="01"; SERIE="F001"; NUMERO="134476";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20602450890"; TIPDOC="01"; SERIE="F001"; NUMERO="1449";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20101560504"; TIPDOC="01"; SERIE="F001"; NUMERO="148293";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20521587530"; TIPDOC="01"; SERIE="F001"; NUMERO="14893";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20552504641"; TIPDOC="01"; SERIE="F001"; NUMERO="149578";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20566140447"; TIPDOC="01"; SERIE="F001"; NUMERO="15597";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20566140447"; TIPDOC="01"; SERIE="F001"; NUMERO="15670";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10255010528"; TIPDOC="01"; SERIE="F001"; NUMERO="1590";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20607958221"; TIPDOC="01"; SERIE="F001"; NUMERO="208";      ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20607958221"; TIPDOC="01"; SERIE="F001"; NUMERO="210";      ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20607958221"; TIPDOC="01"; SERIE="F001"; NUMERO="211";      ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20607958221"; TIPDOC="01"; SERIE="F001"; NUMERO="212";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20607958221"; TIPDOC="01"; SERIE="F001"; NUMERO="214";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20554387498"; TIPDOC="01"; SERIE="F001"; NUMERO="21414";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20607958221"; TIPDOC="01"; SERIE="F001"; NUMERO="215";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20554387498"; TIPDOC="01"; SERIE="F001"; NUMERO="21509";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20602200958"; TIPDOC="01"; SERIE="F001"; NUMERO="21675";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20601207304"; TIPDOC="01"; SERIE="F001"; NUMERO="241662";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20601207304"; TIPDOC="01"; SERIE="F001"; NUMERO="241720";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20507634479"; TIPDOC="01"; SERIE="F001"; NUMERO="249164";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20428834926"; TIPDOC="01"; SERIE="F001"; NUMERO="26530";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20428834926"; TIPDOC="01"; SERIE="F001"; NUMERO="26635";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20428834926"; TIPDOC="01"; SERIE="F001"; NUMERO="26694";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20517930998"; TIPDOC="01"; SERIE="F001"; NUMERO="2690853";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20514862932"; TIPDOC="01"; SERIE="F001"; NUMERO="27144";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20514862932"; TIPDOC="01"; SERIE="F001"; NUMERO="27161";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20545551641"; TIPDOC="01"; SERIE="F001"; NUMERO="27273";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20545551641"; TIPDOC="01"; SERIE="F001"; NUMERO="27430";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20131044179"; TIPDOC="01"; SERIE="F001"; NUMERO="29030";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20131044179"; TIPDOC="01"; SERIE="F001"; NUMERO="29091";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20107375083"; TIPDOC="01"; SERIE="F001"; NUMERO="29547";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20107375083"; TIPDOC="01"; SERIE="F001"; NUMERO="29577";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20549645870"; TIPDOC="01"; SERIE="F001"; NUMERO="31121";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20506419370"; TIPDOC="01"; SERIE="F001"; NUMERO="3504";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20130464689"; TIPDOC="01"; SERIE="F001"; NUMERO="3635";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20492999238"; TIPDOC="01"; SERIE="F001"; NUMERO="38884";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523690702"; TIPDOC="01"; SERIE="F001"; NUMERO="39260";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20523690702"; TIPDOC="01"; SERIE="F001"; NUMERO="39261";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20523690702"; TIPDOC="01"; SERIE="F001"; NUMERO="39387";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523690702"; TIPDOC="01"; SERIE="F001"; NUMERO="39502";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20430471750"; TIPDOC="01"; SERIE="F001"; NUMERO="39643";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20113186730"; TIPDOC="01"; SERIE="F001"; NUMERO="45501";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20113186730"; TIPDOC="01"; SERIE="F001"; NUMERO="45535";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20113186730"; TIPDOC="01"; SERIE="F001"; NUMERO="45637";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20113186730"; TIPDOC="01"; SERIE="F001"; NUMERO="45638";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20603297688"; TIPDOC="01"; SERIE="F001"; NUMERO="4739";     ESTADO="OK" }
    [pscustomobject]@{ RUC="10156827512"; TIPDOC="01"; SERIE="F001"; NUMERO="49738";    ESTADO="OK" }
    [pscustomobject]@{ RUC="10156827512"; TIPDOC="01"; SERIE="F001"; NUMERO="49813";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20378092419"; TIPDOC="01"; SERIE="F001"; NUMERO="50385";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20615092003"; TIPDOC="01"; SERIE="F001"; NUMERO="557";      ESTADO="OK" }
    [pscustomobject]@{ RUC="20602312969"; TIPDOC="01"; SERIE="F001"; NUMERO="5621";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20566574790"; TIPDOC="01"; SERIE="F001"; NUMERO="5647";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20615092003"; TIPDOC="01"; SERIE="F001"; NUMERO="574";      ESTADO="OK" }
    [pscustomobject]@{ RUC="20432018246"; TIPDOC="01"; SERIE="F001"; NUMERO="58500";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20101073859"; TIPDOC="01"; SERIE="F001"; NUMERO="7093";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20548451117"; TIPDOC="01"; SERIE="F001"; NUMERO="86";       ESTADO="OK" }
    # ── SERIE F002 ──────────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20536416529"; TIPDOC="01"; SERIE="F002"; NUMERO="1088";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20536416529"; TIPDOC="01"; SERIE="F002"; NUMERO="1122";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20536416529"; TIPDOC="01"; SERIE="F002"; NUMERO="1137";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20509730939"; TIPDOC="01"; SERIE="F002"; NUMERO="272";      ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20145038384"; TIPDOC="01"; SERIE="F002"; NUMERO="4057";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20213907477"; TIPDOC="01"; SERIE="F002"; NUMERO="7691";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20506064814"; TIPDOC="01"; SERIE="F002"; NUMERO="81932";    ESTADO="AVISO" }
    # ── SERIE F003-F010 ──────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F003"; NUMERO="1067140";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F003"; NUMERO="1068740";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F003"; NUMERO="1069824";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F003"; NUMERO="1071278";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F003"; NUMERO="1071942";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F003"; NUMERO="1073716";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20608914774"; TIPDOC="01"; SERIE="F004"; NUMERO="4165";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F004"; NUMERO="525458";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F004"; NUMERO="526405";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20388738228"; TIPDOC="01"; SERIE="F008"; NUMERO="32873";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20388738228"; TIPDOC="01"; SERIE="F008"; NUMERO="32930";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20101105998"; TIPDOC="01"; SERIE="F010"; NUMERO="44624";    ESTADO="AVISO" }
    # ── SERIE F026-F090 ──────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20259814210"; TIPDOC="01"; SERIE="F026"; NUMERO="57146";    ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20110964928"; TIPDOC="01"; SERIE="F038"; NUMERO="19967";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20110964928"; TIPDOC="01"; SERIE="F038"; NUMERO="19968";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20110964928"; TIPDOC="01"; SERIE="F038"; NUMERO="19969";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20110964928"; TIPDOC="01"; SERIE="F039"; NUMERO="3025";     ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20554454276"; TIPDOC="01"; SERIE="F046"; NUMERO="2603";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20517182673"; TIPDOC="01"; SERIE="F052"; NUMERO="1934169";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20517182673"; TIPDOC="01"; SERIE="F052"; NUMERO="1936184";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20418896915"; TIPDOC="01"; SERIE="F052"; NUMERO="2216170";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20418896915"; TIPDOC="01"; SERIE="F052"; NUMERO="2218538";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20418896915"; TIPDOC="01"; SERIE="F052"; NUMERO="2218539";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20418896915"; TIPDOC="01"; SERIE="F052"; NUMERO="2218540";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20546034951"; TIPDOC="01"; SERIE="F085"; NUMERO="39746";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20332970411"; TIPDOC="01"; SERIE="F090"; NUMERO="283383";   ESTADO="AVISO" }
    # ── SERIES F1xx-F9xx ────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F156"; NUMERO="5204764";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F160"; NUMERO="1664984";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44707";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44708";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44709";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44789";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44793";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44812";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44813";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F18T"; NUMERO="44861";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F19T"; NUMERO="13477";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20127765279"; TIPDOC="01"; SERIE="F19T"; NUMERO="13541";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100199743"; TIPDOC="01"; SERIE="F203"; NUMERO="1347";     ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20418896915"; TIPDOC="01"; SERIE="F226"; NUMERO="729339";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20113439964"; TIPDOC="01"; SERIE="F301"; NUMERO="95028";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F351"; NUMERO="2509208";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F351"; NUMERO="2509756";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F351"; NUMERO="2509857";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F351"; NUMERO="2512040";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F352"; NUMERO="2537897";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F352"; NUMERO="2541615";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F352"; NUMERO="2542379";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F353"; NUMERO="584629";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20507646051"; TIPDOC="01"; SERIE="F500"; NUMERO="1023160";  ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20414955020"; TIPDOC="01"; SERIE="F548"; NUMERO="554279";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F551"; NUMERO="2253752";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F551"; NUMERO="2257088";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F553"; NUMERO="3359676";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F554"; NUMERO="2616801";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F554"; NUMERO="2617484";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F554"; NUMERO="2618301";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20112273922"; TIPDOC="01"; SERIE="F697"; NUMERO="52909";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F801"; NUMERO="425647";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F805"; NUMERO="1834139";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F805"; NUMERO="1838742";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F805"; NUMERO="1840396";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F805"; NUMERO="1841274";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F805"; NUMERO="1842806";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F806"; NUMERO="2007306";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F806"; NUMERO="2009346";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20269985900"; TIPDOC="01"; SERIE="F842"; NUMERO="1067";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20523621212"; TIPDOC="01"; SERIE="F902"; NUMERO="741126";   ESTADO="OK" }
    # ── SERIES FA-FZ ────────────────────────────────────────────────────────
    [pscustomobject]@{ RUC="20606048026"; TIPDOC="01"; SERIE="FA01"; NUMERO="4149";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20492092313"; TIPDOC="01"; SERIE="FA02"; NUMERO="2141365";  ESTADO="OK" }
    [pscustomobject]@{ RUC="20614607476"; TIPDOC="01"; SERIE="FA02"; NUMERO="248";      ESTADO="OK" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FB02"; NUMERO="61693";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FB03"; NUMERO="2281772";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FB03"; NUMERO="2281801";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FB03"; NUMERO="2283426";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FB03"; NUMERO="2295646";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FB03"; NUMERO="2297985";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FC03"; NUMERO="6895754";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FC03"; NUMERO="6908825";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FC03"; NUMERO="6930336";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FC03"; NUMERO="6961336";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FC05"; NUMERO="5125861";  ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="815803";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="816819";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="816820";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="818277";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="818888";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="820209";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="820756";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FD00"; NUMERO="820757";   ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FE01"; NUMERO="2890683";  ESTADO="AVISO" }
    [pscustomobject]@{ RUC="10763669365"; TIPDOC="01"; SERIE="FE02"; NUMERO="8300";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20546357377"; TIPDOC="01"; SERIE="FF01"; NUMERO="101366";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20492331083"; TIPDOC="01"; SERIE="FF01"; NUMERO="10209";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20389525953"; TIPDOC="01"; SERIE="FF01"; NUMERO="15060";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20389525953"; TIPDOC="01"; SERIE="FF01"; NUMERO="15100";    ESTADO="OK" }
    [pscustomobject]@{ RUC="20389525953"; TIPDOC="01"; SERIE="FF01"; NUMERO="15136";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20609339447"; TIPDOC="01"; SERIE="FF01"; NUMERO="2219";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20609339447"; TIPDOC="01"; SERIE="FF01"; NUMERO="2240";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20609339447"; TIPDOC="01"; SERIE="FF01"; NUMERO="2241";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20610150781"; TIPDOC="01"; SERIE="FF01"; NUMERO="811";      ESTADO="OK" }
    [pscustomobject]@{ RUC="20600473663"; TIPDOC="01"; SERIE="FF01"; NUMERO="8834";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20600473663"; TIPDOC="01"; SERIE="FF01"; NUMERO="8893";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20600473663"; TIPDOC="01"; SERIE="FF01"; NUMERO="8910";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20603278888"; TIPDOC="01"; SERIE="FF02"; NUMERO="13433";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20503442745"; TIPDOC="01"; SERIE="FF03"; NUMERO="5546";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20100737826"; TIPDOC="01"; SERIE="FF04"; NUMERO="5122";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20347646891"; TIPDOC="01"; SERIE="FF06"; NUMERO="71687";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20347646891"; TIPDOC="01"; SERIE="FF15"; NUMERO="24897";    ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20347646891"; TIPDOC="01"; SERIE="FF15"; NUMERO="24899";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100036101"; TIPDOC="01"; SERIE="FF33"; NUMERO="39116";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20512584366"; TIPDOC="01"; SERIE="FFF1"; NUMERO="18772";    ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FI01"; NUMERO="19781705"; ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FI01"; NUMERO="19781755"; ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100244714"; TIPDOC="01"; SERIE="FL02"; NUMERO="289663";   ESTADO="OK" }
    [pscustomobject]@{ RUC="20100244714"; TIPDOC="01"; SERIE="FL02"; NUMERO="290482";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100244714"; TIPDOC="01"; SERIE="FL02"; NUMERO="290844";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100244714"; TIPDOC="01"; SERIE="FL02"; NUMERO="290966";   ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FN01"; NUMERO="46999646"; ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FN01"; NUMERO="47086888"; ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100047218"; TIPDOC="01"; SERIE="FN01"; NUMERO="47215647"; ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20554437266"; TIPDOC="01"; SERIE="FPP2"; NUMERO="7728";     ESTADO="OK" }
    [pscustomobject]@{ RUC="20554437266"; TIPDOC="01"; SERIE="FPP2"; NUMERO="7766";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20554437266"; TIPDOC="01"; SERIE="FPP2"; NUMERO="7807";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20554437266"; TIPDOC="01"; SERIE="FPP2"; NUMERO="7808";     ESTADO="AVISO" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FR05"; NUMERO="5408107";  ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FR05"; NUMERO="5424321";  ESTADO="SOLO_SUNAT" }
    [pscustomobject]@{ RUC="20100130204"; TIPDOC="01"; SERIE="FR05"; NUMERO="5444877";  ESTADO="SOLO_SUNAT" }
)


# ── 3. Probar cada documento ─────────────────────────────────────────────────
Write-Host "`n[2/2] Probando $($docs.Count) documentos contra API CPE SUNAT...`n" -ForegroundColor Cyan
Write-Host ("{0,-14} {1,-6} {2,-7} {3,-10} {4,-6} {5,-10} {6}" -f "RUC","TIPDOC","SERIE","NUMERO","HTTP","ESTADO_BD","RESULTADO")
Write-Host ("-" * 100)

$results = @()

foreach ($d in $docs) {
    # SUNAT API espera número sin ceros a la izquierda
    $numNorm = $d.NUMERO.TrimStart('0')
    if ([string]::IsNullOrEmpty($numNorm)) { $numNorm = "0" }

    $url = "$cpeBase/$($d.RUC)/$($d.TIPDOC)/$($d.SERIE)/$numNorm"

    $status  = ""
    $result  = ""
    $color   = "White"

    try {
        # Invoke-RestMethod lanza excepción en 4xx/5xx en todas las versiones de PS
        $resp   = Invoke-RestMethod -Method Get -Uri $url `
            -Headers @{ Authorization = "Bearer $token"; Accept = "application/json" } `
            -ErrorAction Stop
        $status = 200
        $result = "OK - Disponible en SUNAT"
        $color  = "Green"
    }
    catch {
        # Extraer código HTTP del mensaje de la excepción
        $msg = $_.Exception.Message
        if    ($msg -match '404') { $status = 404; $result = "No encontrado en SUNAT (404)";       $color = "DarkYellow" }
        elseif($msg -match '401') { $status = 401; $result = "No autorizado (401) - token?";       $color = "Red" }
        elseif($msg -match '403') { $status = 403; $result = "Acceso denegado (403)";              $color = "Red" }
        elseif($msg -match '(\d{3})'){ $status = [int]$Matches[1]; $result = "HTTP $status";       $color = "Red" }
        else                     { $status = "ERR"; $result = "Error: $msg";                        $color = "Red" }
    }

    $row = [pscustomobject]@{
        RUC       = $d.RUC
        TIPDOC    = $d.TIPDOC
        SERIE     = $d.SERIE
        NUMERO    = $d.NUMERO
        HTTP      = $status
        ESTADO_BD = $d.ESTADO
        URL       = $url
        RESULTADO = $result
    }
    $results += $row

    Write-Host ("{0,-14} {1,-6} {2,-7} {3,-10} {4,-6} {5,-10} {6}" -f `
        $d.RUC, $d.TIPDOC, $d.SERIE, $d.NUMERO, $status, $d.ESTADO, $result) -ForegroundColor $color

    Start-Sleep -Milliseconds 300   # evitar rate limiting
}

# ── Resumen ──────────────────────────────────────────────────────────────────
$ok       = ($results | Where-Object { $_.HTTP -eq 200  }).Count
$notfound = ($results | Where-Object { $_.HTTP -eq 404  }).Count
$errors   = ($results | Where-Object { $_.HTTP -notin @(200, 404) }).Count

Write-Host "`n── RESUMEN ──────────────────────────────────────────────────" -ForegroundColor Cyan
Write-Host "  DISPONIBLES en SUNAT (200): $ok" -ForegroundColor Green
Write-Host "  NO encontrados    (404):    $notfound" -ForegroundColor DarkYellow
Write-Host "  Errores:                    $errors" -ForegroundColor Red
Write-Host ""

# Exportar CSV
$csvPath = "D:\sunat\test_sunat_cpe_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
Write-Host "  Resultados exportados a: $csvPath" -ForegroundColor Cyan
