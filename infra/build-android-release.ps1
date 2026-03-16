# ==============================================================================
# CaféIES — Build Android Release (AAB firmado para Play Store)
# ==============================================================================
# Prerequisitos:
#   1. Keystore generado: .\infra\generar-keystore.ps1
#   2. Variables de entorno configuradas:
#        $env:CAFEIES_KEYSTORE_PATH  → ruta al .keystore
#        $env:CAFEIES_STORE_PASS     → contraseña del keystore
#        $env:CAFEIES_KEY_PASS       → contraseña de la clave
#   3. Firebase configurado: google-services.json con datos reales
#      (Platforms\Android\google-services.json)
#
# Uso:
#   .\infra\build-android-release.ps1
#
# Salida: CafeIES.MAUI\bin\Release\net9.0-android\publish\es.cafeies.app-Signed.aab
# ==============================================================================

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path $PSScriptRoot -Parent
$mauiProj   = Join-Path $repoRoot 'CafeIES.MAUI\CafeIES.MAUI.csproj'
$outputDir  = Join-Path $repoRoot 'CafeIES.MAUI\bin\Release\net9.0-android\publish'

# ── Verificar variables de entorno ────────────────────────────────────────────
foreach ($var in @('CAFEIES_KEYSTORE_PATH','CAFEIES_STORE_PASS','CAFEIES_KEY_PASS')) {
    if (-not [Environment]::GetEnvironmentVariable($var)) {
        Write-Error "Variable de entorno '$var' no definida. Consulta infra\generar-keystore.ps1"
        exit 1
    }
}
$keystorePath = $env:CAFEIES_KEYSTORE_PATH
if (-not (Test-Path $keystorePath)) {
    Write-Error "No se encuentra el keystore: $keystorePath. Ejecuta infra\generar-keystore.ps1"
    exit 1
}

# ── Verificar google-services.json real ───────────────────────────────────────
$gsPath  = Join-Path $repoRoot 'CafeIES.MAUI\Platforms\Android\google-services.json'
$gsContent = Get-Content $gsPath -Raw | ConvertFrom-Json
if ($gsContent.client[0].client_info.mobilesdk_app_id -like 'REPLACE_ME*') {
    Write-Warning "google-services.json contiene valores de placeholder."
    Write-Warning "Las notificaciones push FCM no funcionaran hasta sustituirlo con el archivo real."
    Write-Warning "Descargalo de: Firebase Console → Configuracion del proyecto → Android"
    $resp = Read-Host "Continuar de todas formas? (s/N)"
    if ($resp -notmatch '^[sS]') { exit 1 }
}

# ── Build ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " CafeIES — Android Release Build" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Proyecto:  $mauiProj"
Write-Host " Keystore:  $keystorePath"
Write-Host " Salida:    $outputDir"
Write-Host ""

$startTime = Get-Date

dotnet publish $mauiProj `
    -f net9.0-android `
    -c Release `
    -p:AndroidPackageFormat=aab `
    -p:AndroidKeyStore=true `
    -p:AndroidSigningKeyStore=$keystorePath `
    -p:AndroidSigningKeyAlias=cafeies `
    -p:AndroidSigningKeyPass=$env:CAFEIES_KEY_PASS `
    -p:AndroidSigningStorePass=$env:CAFEIES_STORE_PASS

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build fallida. Revisa los errores anteriores."
    exit 1
}

$elapsed = (Get-Date) - $startTime
$aab = Get-ChildItem $outputDir -Filter '*-Signed.aab' -ErrorAction SilentlyContinue | Select-Object -First 1

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " BUILD COMPLETADA en $([math]::Round($elapsed.TotalMinutes, 1)) min" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
if ($aab) {
    $sizeMB = [math]::Round($aab.Length / 1MB, 1)
    Write-Host " AAB: $($aab.FullName)"
    Write-Host " Tamaño: $sizeMB MB"
} else {
    Write-Host " Busca el .aab en: $outputDir"
}
Write-Host ""
Write-Host "Proximos pasos:"
Write-Host "  1. Accede a https://play.google.com/console"
Write-Host "  2. Tu aplicacion → Produccion (o Prueba interna) → Crear nueva version"
Write-Host "  3. Sube el archivo .aab"
Write-Host "  4. Rellena la ficha: descripcion, capturas de pantalla, clasificacion de contenido"
Write-Host "  5. Envia para revision"
