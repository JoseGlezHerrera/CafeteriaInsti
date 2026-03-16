# ==============================================================================
# CaféIES — Genera el keystore de firma para Google Play Store
# ==============================================================================
# Requisitos:
#   - JDK instalado (keytool en PATH)
#     winget install Microsoft.OpenJDK.21  o  JDK incluido con Android SDK
#
# Uso:
#   .\infra\generar-keystore.ps1
#
# El archivo .keystore generado (cafeies-release.keystore) NO se sube a git.
# Guárdalo en un lugar seguro (gestor de contraseñas, Azure Key Vault, etc.)
# Si lo pierdes NO podrás actualizar la app en Play Store.
# ==============================================================================

$keystorePath = "$PSScriptRoot\cafeies-release.keystore"
$alias        = "cafeies"

if (Test-Path $keystorePath) {
    Write-Host "Ya existe un keystore en: $keystorePath" -ForegroundColor Yellow
    Write-Host "Elimínalo manualmente si quieres regenerarlo." -ForegroundColor Yellow
    exit 0
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Generando keystore de firma para CaféIES Play Store" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANTE: Las contraseñas que introduzcas aqui son permanentes."
Write-Host "Guardalas en un gestor de contraseñas (Bitwarden, 1Password, etc.)"
Write-Host ""

$storePass = Read-Host "Contraseña del keystore (min 6 chars)" -AsSecureString
$storePassTxt = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($storePass))

$keyPass = Read-Host "Contraseña de la clave (Enter = misma que keystore)" -AsSecureString
$keyPassTxt = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($keyPass))
if ([string]::IsNullOrEmpty($keyPassTxt)) { $keyPassTxt = $storePassTxt }

Write-Host ""
Write-Host "Datos del certificado (aparecerán en Play Console):"
$dname = "CN=CafeIES App, OU=Mobile, O=CafeIES, L=Spain, S=Spain, C=ES"

$keytool = "keytool"
# Buscar keytool en ubicaciones comunes si no está en PATH
$candidates = @(
    "C:\Program Files\Microsoft\jdk-21.0.3.9-hotspot\bin\keytool.exe",
    "C:\Program Files\Eclipse Adoptium\jdk-21.*\bin\keytool.exe",
    "C:\Program Files\Java\jdk*\bin\keytool.exe",
    "C:\Android\android-studio\jbr\bin\keytool.exe"
)
foreach ($c in $candidates) {
    $found = Get-Item $c -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $keytool = $found.FullName; break }
}
Write-Host "Usando keytool: $keytool"

& $keytool -genkeypair `
    -keystore $keystorePath `
    -alias $alias `
    -keyalg RSA `
    -keysize 2048 `
    -validity 10000 `
    -storepass $storePassTxt `
    -keypass $keyPassTxt `
    -dname $dname

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error generando keystore. Asegurate de que el JDK esta instalado y keytool esta en PATH."
    exit 1
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Keystore generado correctamente" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Archivo:  $keystorePath"
Write-Host " Alias:    $alias"
Write-Host ""
Write-Host "Configura estas variables de entorno antes de hacer la build:"
Write-Host '  $env:CAFEIES_KEYSTORE_PATH = "' + $keystorePath + '"'
Write-Host '  $env:CAFEIES_STORE_PASS    = "<tu-contraseña-keystore>"'
Write-Host '  $env:CAFEIES_KEY_PASS      = "<tu-contraseña-clave>"'
Write-Host ""
Write-Host "Despues ejecuta: infra\build-android-release.ps1"
