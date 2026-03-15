$az   = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$gh   = 'C:\Program Files\GitHub CLI\gh.exe'
$repo = 'JoseGlezHerrera/CafeteriaInsti'

$subId = (& $az account show --query id -o tsv 2>&1)
Write-Host "Subscription: $subId"

$scope = "/subscriptions/$subId/resourceGroups/cafeies-rg"
Write-Host "Creando service principal en scope: $scope"

$creds = (& $az ad sp create-for-rbac `
    --name "cafeies-github" `
    --role contributor `
    --scopes $scope `
    --json-auth 2>&1) -join ""

Write-Host "SP creado. Guardando secret AZURE_CREDENTIALS..."
$creds | & $gh secret set AZURE_CREDENTIALS --repo $repo

Write-Host "Listo." -ForegroundColor Green
Write-Host $creds
