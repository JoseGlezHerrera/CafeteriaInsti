$az   = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$gh   = 'C:\Program Files\GitHub CLI\gh.exe'
$repo = 'JoseGlezHerrera/CafeteriaInsti'

$subId    = (& $az account show --query id       -o tsv 2>$null).Trim()
$tenantId = (& $az account show --query tenantId -o tsv 2>$null).Trim()
$scope    = "/subscriptions/$subId/resourceGroups/cafeies-rg"

Write-Host "Sub: $subId | Tenant: $tenantId"

# Eliminar SP existente si hay uno
$existingId = (& $az ad sp list --display-name cafeies-github --query '[0].id' -o tsv 2>$null).Trim()
if ($existingId -and $existingId -ne 'None') {
    Write-Host "Eliminando SP existente: $existingId"
    & $az ad sp delete --id $existingId 2>$null | Out-Null
    Start-Sleep -Seconds 5
}

# Crear nuevo SP - stdout solo al fichero, stderr a null
$tmpOut = "$env:TEMP\sp-out.json"
$tmpErr = "$env:TEMP\sp-err.txt"

& $az ad sp create-for-rbac `
    --name "cafeies-github" `
    --role contributor `
    --scopes $scope `
    --json-auth `
    --output json `
    2>$tmpErr | Set-Content $tmpOut -Encoding UTF8

$errText = Get-Content $tmpErr -Raw -ErrorAction SilentlyContinue
$jsonRaw = Get-Content $tmpOut -Raw

Write-Host "Stderr: $errText"
Write-Host "JSON chars: $($jsonRaw.Length)"

if ($jsonRaw.Length -lt 50) {
    Write-Error "No se obtuvo JSON válido del SP. Stderr: $errText"
    exit 1
}

# Verificar que es JSON válido
try {
    $null = $jsonRaw | ConvertFrom-Json
    Write-Host "JSON validado OK" -ForegroundColor Green
} catch {
    Write-Error "JSON inválido: $_"
    exit 1
}

# Subir el secret
$jsonRaw.Trim() | & $gh secret set AZURE_CREDENTIALS --repo $repo
Write-Host "Secret AZURE_CREDENTIALS actualizado." -ForegroundColor Green

Remove-Item $tmpOut, $tmpErr -ErrorAction SilentlyContinue
