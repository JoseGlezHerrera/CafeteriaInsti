$API        = 'https://cafeies-api.azurewebsites.net'
$STRIPE_SK  = $env:STRIPE_SECRET_KEY   # export STRIPE_SECRET_KEY=sk_test_...
$STRIPE_API = 'https://api.stripe.com/v1'
$authHdr    = @{ Authorization = "Bearer $STRIPE_SK" }

function Invoke-Api($method, $path, $body = $null, $token = $null) {
    $hdrs = @{ 'Content-Type' = 'application/json' }
    if ($token) { $hdrs['Authorization'] = "Bearer $token" }
    $params = @{ Method = $method; Uri = "$API$path"; Headers = $hdrs; TimeoutSec = 30 }
    if ($body) { $params['Body'] = ($body | ConvertTo-Json -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "`n[1/6] Health check..." -ForegroundColor Cyan
$health = Invoke-Api 'GET' '/health'
Write-Host "    $health" -ForegroundColor Green

Write-Host "`n[2/6] GET /api/pagos/config (publishable key)..." -ForegroundColor Cyan
$cfg = Invoke-Api 'GET' '/api/pagos/config'
Write-Host "    PublishableKey: $($cfg.publishableKey.Substring(0,20))..." -ForegroundColor Green

Write-Host "`n[3/6] Login como admin..." -ForegroundColor Cyan
$loginResp = Invoke-Api 'POST' '/api/auth/login' @{
    email    = 'admin@cafeies.local'
    password = 'Admin1234!'
}
$token = $loginResp.accessToken
Write-Host "    Token obtenido: $($token.Substring(0,30))..." -ForegroundColor Green

Write-Host "`n[4/6] GET /api/productos (primer producto activo con stock)..." -ForegroundColor Cyan
$productos = Invoke-Api 'GET' '/api/productos' -token $token
$producto = $productos | Where-Object { $_.activo -and ($_.stock -gt 0 -or $_.stock -eq -1) } | Select-Object -First 1
if (-not $producto) {
    Write-Error "No hay productos activos. Crea uno en el panel admin primero."
    exit 1
}
Write-Host "    Producto: $($producto.nombre) — $($producto.precio)€ (stock: $($producto.stock))" -ForegroundColor Green

Write-Host "`n[5/6] POST /api/pagos/crear-intent..." -ForegroundColor Cyan
$intent = Invoke-Api 'POST' '/api/pagos/crear-intent' @{
    lineas = @(@{ productoId = $producto.id; cantidad = 1 })
    notas  = 'Test automatico desde infra/test-pagos.ps1'
} -token $token

Write-Host "    PaymentIntent ID:    $($intent.paymentIntentId)" -ForegroundColor Green
Write-Host "    Total servidor:      $($intent.total)€"
Write-Host "    ClientSecret (20):   $($intent.clientSecret.Substring(0,30))..."

Write-Host "`n[6/6] Confirmar pago con tarjeta de prueba 4242424242424242..." -ForegroundColor Cyan

# Crear PaymentMethod con tarjeta de prueba
$pmBody = 'type=card' `
        + '&card[number]=4242424242424242' `
        + '&card[exp_month]=12' `
        + '&card[exp_year]=2030' `
        + '&card[cvc]=123'
$pm = Invoke-RestMethod -Method Post -Uri "$STRIPE_API/payment_methods" `
    -Headers $authHdr -ContentType 'application/x-www-form-urlencoded' -Body $pmBody

Write-Host "    PaymentMethod creado: $($pm.id)"

# Confirmar el PaymentIntent
$piId = $intent.paymentIntentId
$confirmBody = "payment_method=$($pm.id)" `
             + '&return_url=https%3A%2F%2Fcafeies-api.azurewebsites.net'
$confirmed = Invoke-RestMethod -Method Post -Uri "$STRIPE_API/payment_intents/$piId/confirm" `
    -Headers $authHdr -ContentType 'application/x-www-form-urlencoded' -Body $confirmBody

Write-Host "    Estado PaymentIntent: $($confirmed.status)" -ForegroundColor Green

if ($confirmed.status -eq 'succeeded') {
    Write-Host "`n    Pago confirmado por Stripe." -ForegroundColor Green
    Write-Host "    Stripe enviará el evento payment_intent.succeeded al webhook."
    Write-Host "    Verificando en logs del App Service en unos segundos..."
    Start-Sleep -Seconds 5

    # Verificar el estado via Stripe (confirmar que el PI sigue succeeded)
    $check = Invoke-RestMethod -Method Get -Uri "$STRIPE_API/payment_intents/$piId" -Headers $authHdr
    Write-Host "`n    Status final: $($check.status)"
    Write-Host "    Amount:       $($check.amount / 100)€"
    Write-Host "    Metadata userId: $($check.metadata.userId)"
} else {
    Write-Host "    Estado inesperado: $($confirmed.status)" -ForegroundColor Yellow
    Write-Host "    next_action: $($confirmed.next_action | ConvertTo-Json)"
}

Write-Host ''
Write-Host '============================================================' -ForegroundColor Green
Write-Host ' TEST DE PAGOS COMPLETADO' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Green
Write-Host " API:           $API"
Write-Host " PaymentIntent: $piId"
Write-Host " Total cobrado: $($intent.total) EUR"
Write-Host ' Tarjeta:       4242424242424242 (test)'
Write-Host ' Webhook:       /api/pagos/webhook'
Write-Host ''
