$az   = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$gh   = 'C:\Program Files\GitHub CLI\gh.exe'
$repo = 'JoseGlezHerrera/CafeteriaInsti'
$app  = 'cafeies-api'
$rg   = 'cafeies-rg'

$tmpFile = "$env:TEMP\pp-cafeies.xml"

Write-Host "Descargando publish profile..." -ForegroundColor Cyan
& $az webapp deployment list-publishing-profiles `
    --name $app `
    --resource-group $rg `
    --xml | Set-Content -Path $tmpFile -Encoding UTF8

$size = (Get-Item $tmpFile).Length
Write-Host "Tamaño del XML: $size bytes"

if ($size -lt 100) {
    Write-Error "El publish profile parece vacío o inválido."
    exit 1
}

Write-Host "Subiendo secret AZURE_WEBAPP_PUBLISH_PROFILE..." -ForegroundColor Cyan
& $gh secret set AZURE_WEBAPP_PUBLISH_PROFILE --repo $repo --body-file $tmpFile 2>&1

if ($LASTEXITCODE -ne 0) {
    # Fallback: leer y pasar como string directamente
    Write-Host "Intentando con --body..." -ForegroundColor Yellow
    $content = Get-Content $tmpFile -Raw
    & $gh secret set AZURE_WEBAPP_PUBLISH_PROFILE --repo $repo --body $content 2>&1
}

Remove-Item $tmpFile -ErrorAction SilentlyContinue
Write-Host "Listo. Re-ejecutando pipeline..." -ForegroundColor Green

& $gh workflow run deploy-api.yml --repo $repo --ref main 2>&1
