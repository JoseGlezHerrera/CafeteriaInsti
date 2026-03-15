# ==============================================================================
# CaféIES — Creación de recursos Azure + secrets de GitHub
# ==============================================================================
# Requisitos:
#   - Azure CLI  (winget install Microsoft.AzureCLI)
#   - GitHub CLI (winget install GitHub.cli)
#
# Uso:
#   1. Edita la sección "CONFIGURACIÓN" con tus valores reales.
#   2. Abre PowerShell como administrador y ejecuta:
#        Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
#        .\infra\azure-setup.ps1
# ==============================================================================

# ── CONFIGURACIÓN ─────────────────────────────────────────────────────────────

$RESOURCE_GROUP    = "cafeies-rg"
$LOCATION          = "westeurope"          # cambia si prefieres otra región

$APP_SERVICE_PLAN  = "cafeies-plan"
$APP_SERVICE_NAME  = "cafeies-api"         # debe ser único en Azure

$SQL_SERVER_NAME   = "cafeies-sql"         # debe ser único en Azure
$SQL_DB_NAME       = "cafeiesdb"
$SQL_ADMIN_USER    = "cafeiesadmin"
$SQL_ADMIN_PASS    = ""                    # ← RELLENA: mín 8 chars, mayúscula, número, símbolo

$STORAGE_ACCOUNT   = "cafeiesimgs"        # 3-24 chars, solo minúsculas y números, único en Azure
$BLOB_CONTAINER    = "productos"

$STATIC_WEB_APP    = "cafeies-admin"

# Claves de servicios externos — pon tus valores reales antes de ejecutar
$JWT_KEY           = ""                    # ← RELLENA: mín 32 caracteres aleatorios
$STRIPE_SECRET     = ""                    # ← sk_live_...  (o sk_test_... para pruebas)
$STRIPE_PK         = ""                    # ← pk_live_...
$STRIPE_WEBHOOK    = ""                    # ← whsec_... (configura después en Stripe Dashboard)
$FCM_PROJECT_ID    = ""                    # ← project-id de Firebase (opcional)
$FCM_SA_JSON       = ""                    # ← contenido del service-account.json (opcional)

# Repositorio GitHub (formato: usuario/repo)
$GITHUB_REPO       = "JoseGlezHerrera/CafeteriaInsti"

# ── VALIDACIÓN DE CAMPOS OBLIGATORIOS ─────────────────────────────────────────

$missing = @()
if (-not $SQL_ADMIN_PASS) { $missing += "SQL_ADMIN_PASS" }
if (-not $JWT_KEY)         { $missing += "JWT_KEY" }
if (-not $STRIPE_SECRET)   { $missing += "STRIPE_SECRET" }
if (-not $STRIPE_PK)       { $missing += "STRIPE_PK" }

if ($missing.Count -gt 0) {
    Write-Error "Faltan los siguientes valores en la sección CONFIGURACIÓN: $($missing -join ', ')"
    exit 1
}

# ── LOGIN ─────────────────────────────────────────────────────────────────────

Write-Host "`n[1/9] Login en Azure..." -ForegroundColor Cyan
az login --only-show-errors

Write-Host "`n[2/9] Login en GitHub..." -ForegroundColor Cyan
gh auth login

# ── GRUPO DE RECURSOS ─────────────────────────────────────────────────────────

Write-Host "`n[3/9] Creando grupo de recursos '$RESOURCE_GROUP'..." -ForegroundColor Cyan
az group create --name $RESOURCE_GROUP --location $LOCATION --only-show-errors | Out-Null

# ── AZURE SQL ─────────────────────────────────────────────────────────────────

Write-Host "`n[4/9] Creando Azure SQL Server y base de datos..." -ForegroundColor Cyan

az sql server create `
    --name $SQL_SERVER_NAME `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --admin-user $SQL_ADMIN_USER `
    --admin-password $SQL_ADMIN_PASS `
    --only-show-errors | Out-Null

# Permitir acceso desde todos los IPs de Azure (App Service incluido)
az sql server firewall-rule create `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --name "AllowAzureServices" `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --only-show-errors | Out-Null

az sql db create `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --name $SQL_DB_NAME `
    --edition Basic `
    --capacity 5 `
    --only-show-errors | Out-Null

$SQL_CONN_STR = "Server=tcp:$SQL_SERVER_NAME.database.windows.net,1433;Initial Catalog=$SQL_DB_NAME;Persist Security Info=False;User ID=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASS;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
Write-Host "    SQL creado: $SQL_SERVER_NAME.database.windows.net" -ForegroundColor Green

# ── AZURE BLOB STORAGE ────────────────────────────────────────────────────────

Write-Host "`n[5/9] Creando cuenta de almacenamiento y contenedor '$BLOB_CONTAINER'..." -ForegroundColor Cyan

az storage account create `
    --name $STORAGE_ACCOUNT `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --sku Standard_LRS `
    --kind StorageV2 `
    --only-show-errors | Out-Null

$STORAGE_CONN_STR = $(az storage account show-connection-string `
    --name $STORAGE_ACCOUNT `
    --resource-group $RESOURCE_GROUP `
    --query connectionString -o tsv)

az storage container create `
    --name $BLOB_CONTAINER `
    --account-name $STORAGE_ACCOUNT `
    --public-access blob `
    --connection-string $STORAGE_CONN_STR `
    --only-show-errors | Out-Null

Write-Host "    Storage creado: $STORAGE_ACCOUNT" -ForegroundColor Green

# ── APP SERVICE ───────────────────────────────────────────────────────────────

Write-Host "`n[6/9] Creando App Service Plan y Web App..." -ForegroundColor Cyan

az appservice plan create `
    --name $APP_SERVICE_PLAN `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --sku B1 `
    --is-linux `
    --only-show-errors | Out-Null

az webapp create `
    --name $APP_SERVICE_NAME `
    --resource-group $RESOURCE_GROUP `
    --plan $APP_SERVICE_PLAN `
    --runtime "DOTNETCORE:9.0" `
    --only-show-errors | Out-Null

# Application Settings (equivalen a appsettings.json en producción)
$appSettings = @(
    "ASPNETCORE_ENVIRONMENT=Production"
    "ConnectionStrings__DefaultConnection=$SQL_CONN_STR"
    "Jwt__Key=$JWT_KEY"
    "Jwt__Issuer=CafeIES"
    "Jwt__Audience=CafeIES"
    "Stripe__SecretKey=$STRIPE_SECRET"
    "Stripe__PublishableKey=$STRIPE_PK"
    "Stripe__WebhookSecret=$STRIPE_WEBHOOK"
    "AzureStorage__ConnectionString=$STORAGE_CONN_STR"
)

if ($FCM_PROJECT_ID) {
    $appSettings += "Fcm__ProjectId=$FCM_PROJECT_ID"
    $appSettings += "Fcm__ServiceAccountJson=$FCM_SA_JSON"
}

az webapp config appsettings set `
    --name $APP_SERVICE_NAME `
    --resource-group $RESOURCE_GROUP `
    --settings @appSettings `
    --only-show-errors | Out-Null

$API_URL = "https://$APP_SERVICE_NAME.azurewebsites.net/"
Write-Host "    App Service creado: $API_URL" -ForegroundColor Green

# ── AZURE STATIC WEB APPS ─────────────────────────────────────────────────────

Write-Host "`n[7/9] Creando Azure Static Web App (Blazor Admin)..." -ForegroundColor Cyan

az staticwebapp create `
    --name $STATIC_WEB_APP `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --only-show-errors | Out-Null

$STATIC_URL     = "https://$(az staticwebapp show --name $STATIC_WEB_APP --resource-group $RESOURCE_GROUP --query 'defaultHostname' -o tsv)"
$STATIC_API_TOKEN = $(az staticwebapp secrets list --name $STATIC_WEB_APP --resource-group $RESOURCE_GROUP --query 'properties.apiKey' -o tsv)

Write-Host "    Static Web App creada: $STATIC_URL" -ForegroundColor Green

# Actualizar CORS en appsettings.Production.json con la URL real
$prodSettings = Get-Content "..\CafeIES.API\appsettings.Production.json" | ConvertFrom-Json
$prodSettings.Cors.AllowedOrigins = @($STATIC_URL)
$prodSettings | ConvertTo-Json -Depth 10 | Set-Content "..\CafeIES.API\appsettings.Production.json"
Write-Host "    appsettings.Production.json actualizado con CORS: $STATIC_URL" -ForegroundColor Green

# Actualizar URL de producción en MauiProgram.cs
$mauiFile = "..\CafeIES.MAUI\MauiProgram.cs"
(Get-Content $mauiFile) -replace 'https://cafeies-api\.azurewebsites\.net/', $API_URL | Set-Content $mauiFile
Write-Host "    MauiProgram.cs actualizado con URL: $API_URL" -ForegroundColor Green

# ── PUBLISH PROFILE ───────────────────────────────────────────────────────────

Write-Host "`n[8/9] Descargando publish profile del App Service..." -ForegroundColor Cyan
$PUBLISH_PROFILE = $(az webapp deployment list-publishing-profiles `
    --name $APP_SERVICE_NAME `
    --resource-group $RESOURCE_GROUP `
    --xml)

# ── SECRETS DE GITHUB ─────────────────────────────────────────────────────────

Write-Host "`n[9/9] Configurando secrets en GitHub ($GITHUB_REPO)..." -ForegroundColor Cyan

gh secret set AZURE_WEBAPP_NAME            --body $APP_SERVICE_NAME      --repo $GITHUB_REPO
gh secret set AZURE_WEBAPP_PUBLISH_PROFILE --body $PUBLISH_PROFILE       --repo $GITHUB_REPO
gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --body $STATIC_API_TOKEN   --repo $GITHUB_REPO
gh secret set API_BASE_URL                 --body $API_URL                --repo $GITHUB_REPO

Write-Host "`n✅ Todo listo. Secrets configurados en GitHub." -ForegroundColor Green

# ── RESUMEN ───────────────────────────────────────────────────────────────────

Write-Host "`n══════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host " RESUMEN DE RECURSOS CREADOS" -ForegroundColor Yellow
Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host " API (App Service):   $API_URL"
Write-Host " Admin (Static WAP):  $STATIC_URL"
Write-Host " SQL Server:          $SQL_SERVER_NAME.database.windows.net"
Write-Host " Storage Account:     $STORAGE_ACCOUNT"
Write-Host "══════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host ""
Write-Host " PRÓXIMOS PASOS MANUALES:" -ForegroundColor Cyan
Write-Host "  1. Ejecutar la migración EF Core contra Azure SQL:"
Write-Host "       cd CafeIES.API"
Write-Host "       `$env:ConnectionStrings__DefaultConnection='$SQL_CONN_STR'"
Write-Host "       dotnet ef database update"
Write-Host ""
Write-Host "  2. Hacer git add + commit + push para activar los pipelines:"
Write-Host "       git add CafeIES.API/appsettings.Production.json CafeIES.MAUI/MauiProgram.cs"
Write-Host "       git commit -m 'config: URLs de produccion actualizadas'"
Write-Host "       git push"
Write-Host ""
Write-Host "  3. Configurar el webhook de Stripe en producción:"
Write-Host "       https://dashboard.stripe.com/webhooks"
Write-Host "       URL: $($API_URL)api/pagos/webhook"
Write-Host "       Evento: payment_intent.succeeded"
Write-Host "       Copiar el 'whsec_...' y actualizar el App Setting Stripe__WebhookSecret"
Write-Host ""
