$az   = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
$repo = 'JoseGlezHerrera/CafeteriaInsti'

$stripeKey = $env:STRIPE_SECRET_KEY   # export STRIPE_SECRET_KEY=sk_test_...
$webhookUrl = 'https://cafeies-api.azurewebsites.net/api/pagos/webhook'

Write-Host "Creando webhook en Stripe..." -ForegroundColor Cyan

$body = "url=$([Uri]::EscapeDataString($webhookUrl))&enabled_events[]=payment_intent.succeeded&description=CafeIES+produccion"

$response = Invoke-RestMethod `
    -Method Post `
    -Uri 'https://api.stripe.com/v1/webhook_endpoints' `
    -Headers @{ Authorization = "Bearer $stripeKey" } `
    -ContentType 'application/x-www-form-urlencoded' `
    -Body $body

Write-Host "Webhook creado:" -ForegroundColor Green
Write-Host "  ID:     $($response.id)"
Write-Host "  URL:    $($response.url)"
Write-Host "  Secret: $($response.secret)"
Write-Host "  Status: $($response.status)"

# Actualizar el App Setting en Azure App Service
$whsec = $response.secret
Write-Host "`nActualizando Stripe__WebhookSecret en App Service..." -ForegroundColor Cyan
& $az webapp config appsettings set `
    --name cafeies-api `
    --resource-group cafeies-rg `
    --settings "Stripe__WebhookSecret=$whsec" `
    --only-show-errors 2>&1 | Out-Null

Write-Host "  App Setting actualizado." -ForegroundColor Green
Write-Host "`nTodo listo. El webhook de Stripe esta activo." -ForegroundColor Green
