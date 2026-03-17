$repo  = "JoseGlezHerrera/CafeteriaInsti"
$runId = "23167404671"

$credInput = "protocol=https`nhost=github.com`n`n"
$tokenLine = ($credInput | & git credential fill 2>$null) | Where-Object { $_ -match "^password=" }
$token = $tokenLine -replace "^password=", ""

$headers = @{
    Authorization          = "token $token"
    Accept                 = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

# Obtener job ID
$jobs   = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/actions/runs/$runId/jobs" -Headers $headers
$jobId  = $jobs.jobs[0].id

# Descargar logs del job (zip)
$logUrl = "https://api.github.com/repos/$repo/actions/jobs/$jobId/logs"
$tmpZip = "$env:TEMP\gh-job-log.txt"

Invoke-WebRequest -Uri $logUrl -Headers $headers -OutFile $tmpZip -UseBasicParsing

# Mostrar las ultimas 100 lineas centradas en el error de Restore
Get-Content $tmpZip | Select-String -Pattern "Restore|error|Error|failed|Failed|PackageReference|Could not" -Context 2,2 | Select-Object -First 60
