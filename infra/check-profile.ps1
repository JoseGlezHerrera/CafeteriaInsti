$az   = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$gh   = 'C:\Program Files\GitHub CLI\gh.exe'
$repo = 'JoseGlezHerrera/CafeteriaInsti'
$app  = 'cafeies-api'
$rg   = 'cafeies-rg'

# Descargar el publish profile como texto plano (sin BOM, sin CRLF)
$xmlLines = & $az webapp deployment list-publishing-profiles `
    --name $app --resource-group $rg --xml 2>&1

Write-Host "Lineas recibidas: $($xmlLines.Count)"
Write-Host "Preview (100 chars): $($xmlLines[0].Substring(0, [Math]::Min(100, $xmlLines[0].Length)))"
Write-Host "Tamanio total: $(($xmlLines -join '').Length) chars"

# Guardar en temp SIN BOM con LF
$tmpFile = "$env:TEMP\pp-check.xml"
[System.IO.File]::WriteAllText($tmpFile, ($xmlLines -join "`n"), [System.Text.Encoding]::UTF8)

$size = (Get-Item $tmpFile).Length
Write-Host "Fichero temporal: $size bytes"

# Subir via stdin (modo mas compatible)
Write-Host "`nSubiendo via stdin..."
[System.IO.File]::ReadAllText($tmpFile) | & $gh secret set AZURE_WEBAPP_PUBLISH_PROFILE --repo $repo

Write-Host "Secret subido. Lanzando pipeline..."
# Forzar el pipeline modificando un archivo y haciendo push
