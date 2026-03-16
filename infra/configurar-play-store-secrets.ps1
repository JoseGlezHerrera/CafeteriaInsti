# configurar-play-store-secrets.ps1
# Guia paso a paso para configurar los GitHub Secrets necesarios para el
# pipeline de despliegue automatico a Google Play Store.
#
# Ejecutar desde la raiz del repositorio:
#   .\infra\configurar-play-store-secrets.ps1
#
# Requiere:
#   - GitHub CLI (gh) autenticado: gh auth login
#   - Google Cloud SDK (gcloud) para crear el service account (opcional, puedes hacerlo desde la consola web)
#   - El keystore generado con infra/generar-keystore.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Colores ────────────────────────────────────────────────────────────────────
function Write-Step  { param($n, $msg) Write-Host "`n[$n] $msg" -ForegroundColor Cyan }
function Write-Ok    { param($msg)     Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn  { param($msg)     Write-Host "    AVISO: $msg" -ForegroundColor Yellow }
function Write-Instr { param($msg)     Write-Host "    > $msg" -ForegroundColor White }

Write-Host "`n============================================================" -ForegroundColor Magenta
Write-Host "  Configuracion de GitHub Secrets para Google Play Store" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta

# ── PASO 1: Verificar keystore ─────────────────────────────────────────────────
Write-Step 1 "Verificar keystore de firma"
$keystorePath = Join-Path $PSScriptRoot "cafeies-release.keystore"
$credPath     = Join-Path $PSScriptRoot "keystore-credentials.local.txt"

if (-not (Test-Path $keystorePath)) {
    Write-Warn "No se encontro el keystore en: $keystorePath"
    Write-Instr "Ejecuta primero: .\infra\generar-keystore.ps1"
    exit 1
}
Write-Ok "Keystore encontrado: $keystorePath"

if (-not (Test-Path $credPath)) {
    Write-Warn "No se encontro el archivo de credenciales: $credPath"
    Write-Warn "Necesitas las contrasenas del keystore para continuar."
    exit 1
}

# Leer contrasenas del archivo de credenciales
$storePass = ""
$keyPass   = ""
Get-Content $credPath | ForEach-Object {
    if ($_ -match "^Keystore password:\s*(.+)$") { $storePass = $Matches[1].Trim() }
    if ($_ -match "^Key password:\s*(.+)$")      { $keyPass   = $Matches[1].Trim() }
}
if (-not $storePass -or -not $keyPass) {
    Write-Warn "No se pudieron leer las contrasenas de $credPath"
    exit 1
}
Write-Ok "Credenciales leidas correctamente."

# ── PASO 2: Codificar keystore en Base64 ──────────────────────────────────────
Write-Step 2 "Codificar keystore en Base64"
$keystoreBytes  = [System.IO.File]::ReadAllBytes($keystorePath)
$keystoreBase64 = [System.Convert]::ToBase64String($keystoreBytes)
Write-Ok "Keystore codificado ($($keystoreBytes.Length) bytes -> $($keystoreBase64.Length) chars Base64)"

# ── PASO 3: Service account de Google Play ───────────────────────────────────
Write-Step 3 "Crear service account de Google Play (instrucciones manuales)"
Write-Host ""
Write-Host "  Si aun no tienes el JSON del service account, sigue estos pasos:" -ForegroundColor Yellow
Write-Host ""
Write-Instr "3.1 Ir a: https://console.cloud.google.com/"
Write-Instr "    - Selecciona o crea un proyecto de Google Cloud."
Write-Instr ""
Write-Instr "3.2 Habilitar la API:"
Write-Instr "    - APIs y servicios > Biblioteca"
Write-Instr "    - Busca 'Google Play Android Developer API' y habilítala."
Write-Instr ""
Write-Instr "3.3 Crear service account:"
Write-Instr "    - IAM y administracion > Cuentas de servicio > Crear cuenta de servicio"
Write-Instr "    - Nombre: github-play-deploy"
Write-Instr "    - Rol: NO asignes rol de proyecto (se gestiona desde Play Console)"
Write-Instr "    - Crear clave JSON > Descarga el archivo .json"
Write-Instr ""
Write-Instr "3.4 Vincular en Google Play Console:"
Write-Instr "    - https://play.google.com/console/ > Configuracion > Acceso a API"
Write-Instr "    - 'Vincular proyecto de Google Cloud' > selecciona el proyecto del paso 3.1"
Write-Instr "    - En 'Cuentas de servicio' > la cuenta creada > Conceder acceso"
Write-Instr "    - Permisos minimos necesarios:"
Write-Instr "      * Version releases (Publicar versiones de lanzamiento)"
Write-Instr "      * Edit and delete draft apps (para internal/alpha/beta)"
Write-Instr ""

$jsonPath = Read-Host "  Introduce la ruta al archivo JSON del service account (o ENTER para omitir)"
if ($jsonPath -and (Test-Path $jsonPath)) {
    $serviceAccountJson = Get-Content $jsonPath -Raw
    Write-Ok "JSON del service account leido ($($serviceAccountJson.Length) chars)"
} else {
    $serviceAccountJson = ""
    Write-Warn "Se omitio el JSON del service account. Tendras que configurar GOOGLE_PLAY_SERVICE_ACCOUNT_JSON manualmente."
}

# ── PASO 4: Verificar gh CLI ──────────────────────────────────────────────────
Write-Step 4 "Verificar GitHub CLI"
try {
    $ghVersion = gh --version 2>&1 | Select-Object -First 1
    Write-Ok "gh encontrado: $ghVersion"
    gh auth status 2>&1 | Out-Null
    Write-Ok "gh autenticado correctamente"
} catch {
    Write-Warn "GitHub CLI no encontrado o no autenticado."
    Write-Instr "Instala gh: https://cli.github.com/"
    Write-Instr "Luego ejecuta: gh auth login"
    Write-Host ""
    Write-Host "  Secrets a configurar manualmente en GitHub > Settings > Secrets > Actions:" -ForegroundColor Yellow
    Write-Host "    ANDROID_KEYSTORE_BASE64       = (ver abajo)" -ForegroundColor White
    Write-Host "    ANDROID_KEYSTORE_PASSWORD     = $storePass" -ForegroundColor White
    Write-Host "    ANDROID_KEY_PASSWORD          = $keyPass" -ForegroundColor White
    Write-Host "    GOOGLE_PLAY_SERVICE_ACCOUNT_JSON = (contenido del JSON)" -ForegroundColor White
    Write-Host ""
    Write-Host "  Base64 del keystore (copiar completo):" -ForegroundColor Yellow
    Write-Host $keystoreBase64 -ForegroundColor Gray
    exit 0
}

# ── PASO 5: Detectar repo de GitHub ──────────────────────────────────────────
Write-Step 5 "Detectar repositorio de GitHub"
try {
    $repoInfo = gh repo view --json nameWithOwner -q ".nameWithOwner" 2>&1
    Write-Ok "Repositorio: $repoInfo"
} catch {
    Write-Warn "No se pudo detectar el repositorio. Asegurate de estar en la carpeta del repo."
    exit 1
}

# ── PASO 6: Configurar secrets ────────────────────────────────────────────────
Write-Step 6 "Configurar GitHub Secrets"

Write-Host "    Configurando ANDROID_KEYSTORE_BASE64..." -NoNewline
$keystoreBase64 | gh secret set ANDROID_KEYSTORE_BASE64
Write-Host " OK" -ForegroundColor Green

Write-Host "    Configurando ANDROID_KEYSTORE_PASSWORD..." -NoNewline
$storePass | gh secret set ANDROID_KEYSTORE_PASSWORD
Write-Host " OK" -ForegroundColor Green

Write-Host "    Configurando ANDROID_KEY_PASSWORD..." -NoNewline
$keyPass | gh secret set ANDROID_KEY_PASSWORD
Write-Host " OK" -ForegroundColor Green

if ($serviceAccountJson) {
    Write-Host "    Configurando GOOGLE_PLAY_SERVICE_ACCOUNT_JSON..." -NoNewline
    $serviceAccountJson | gh secret set GOOGLE_PLAY_SERVICE_ACCOUNT_JSON
    Write-Host " OK" -ForegroundColor Green
} else {
    Write-Warn "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON no configurado. Hazlo manualmente cuando tengas el JSON."
}

# ── PASO 7: Primera subida manual ────────────────────────────────────────────
Write-Step 7 "IMPORTANTE: Primera version en Play Store"
Write-Host ""
Write-Host "  Google Play requiere que la PRIMERA version se suba MANUALMENTE." -ForegroundColor Yellow
Write-Host "  El pipeline automatico solo funciona a partir de la segunda version." -ForegroundColor Yellow
Write-Host ""
Write-Instr "7.1 Ve a: https://play.google.com/console/"
Write-Instr "7.2 Crea la app con package: es.cafeies.app"
Write-Instr "7.3 En 'Prueba interna' > Versiones > Crear nueva version"
Write-Instr "7.4 Sube el AAB generado por: .\infra\build-android-release.ps1"
Write-Instr "    Ubicacion del AAB: CafeIES.MAUI\bin\Release\net9.0-android\publish\*-Signed.aab"
Write-Instr "7.5 Rellena el formulario de contenido (clasificacion, politica de privacidad, etc.)"
Write-Instr "7.6 Publica en prueba interna."
Write-Host ""
Write-Host "  Tras la primera subida manual, el pipeline automatico tomara el control." -ForegroundColor Green
Write-Host ""

# ── Resumen ────────────────────────────────────────────────────────────────────
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host "  Configuracion completada" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  Secrets configurados en GitHub:" -ForegroundColor White
Write-Host "    [OK] ANDROID_KEYSTORE_BASE64" -ForegroundColor Green
Write-Host "    [OK] ANDROID_KEYSTORE_PASSWORD" -ForegroundColor Green
Write-Host "    [OK] ANDROID_KEY_PASSWORD" -ForegroundColor Green
if ($serviceAccountJson) {
    Write-Host "    [OK] GOOGLE_PLAY_SERVICE_ACCOUNT_JSON" -ForegroundColor Green
} else {
    Write-Host "    [--] GOOGLE_PLAY_SERVICE_ACCOUNT_JSON  (pendiente)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "  Para disparar el pipeline manualmente:" -ForegroundColor White
Write-Host "    gh workflow run deploy-android.yml --field track=internal" -ForegroundColor Cyan
Write-Host ""
