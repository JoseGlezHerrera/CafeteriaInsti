$az = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$xml = & $az webapp deployment list-publishing-profiles --name cafeies-api --resource-group cafeies-rg --xml 2>&1
Write-Host "Type: $($xml.GetType().FullName)"
Write-Host "Length: $($xml.Length)"
if ($xml -is [string]) {
    Write-Host $xml.Substring(0, [Math]::Min(500, $xml.Length))
} else {
    $str = $xml | Out-String
    Write-Host "As string ($($str.Length) chars):"
    Write-Host $str.Substring(0, [Math]::Min(500, $str.Length))
}
