$gh   = 'C:\Program Files\GitHub CLI\gh.exe'
$repo = 'JoseGlezHerrera/CafeteriaInsti'

for ($i = 1; $i -le 18; $i++) {
    Start-Sleep -Seconds 20
    $runs = (& $gh run list --repo $repo --limit 4 2>&1) -join "`n"
    Write-Host "[$i] ---"
    Write-Host $runs
    if ($runs -notmatch 'in_progress' -and $runs -notmatch 'queued') {
        Write-Host "`nPipelines completados." -ForegroundColor Green
        break
    }
}
