# =============================================================================
# Test script for StarGate Policy Cache Management Endpoints
# =============================================================================
# Usage:
#   1. Start the application: dotnet run --project src/StarGate.Api
#   2. Run this script: .\scripts\Test-PolicyCacheEndpoints.ps1
# =============================================================================

# Session Setup (execute once per PowerShell session)
Write-Host "Configuring PowerShell session..." -ForegroundColor Cyan
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Configuration
# HTTP: port 49244
# HTTPS: port 49243
$BaseUrl = "http://localhost:49244"
Write-Host "Base URL: $BaseUrl" -ForegroundColor Green
Write-Host ""

# =============================================================================
# Test 1: Health Check
# =============================================================================
Write-Host "[TEST 1] Health Check" -ForegroundColor Yellow
Write-Host "GET $BaseUrl/health/live" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/health/live" -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Test 2: Get Cache Statistics (Initial State)
# =============================================================================
Write-Host "[TEST 2] Get Cache Statistics (Initial)" -ForegroundColor Yellow
Write-Host "GET $BaseUrl/api/policies/cache/statistics" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/policies/cache/statistics" -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Test 3: Refresh Policy Cache
# =============================================================================
Write-Host "[TEST 3] Refresh Policy Cache" -ForegroundColor Yellow
Write-Host "POST $BaseUrl/api/policies/cache/refresh" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/policies/cache/refresh" -Method POST -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Test 4: Get Cache Statistics (After Refresh)
# =============================================================================
Write-Host "[TEST 4] Get Cache Statistics (After Refresh)" -ForegroundColor Yellow
Write-Host "GET $BaseUrl/api/policies/cache/statistics" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/policies/cache/statistics" -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Test 5: Invalidate Process Type Policy
# =============================================================================
Write-Host "[TEST 5] Invalidate Process Type Policy (order)" -ForegroundColor Yellow
Write-Host "DELETE $BaseUrl/api/policies/cache/order" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/policies/cache/order" -Method DELETE -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Test 6: Invalidate Client Override Policy
# =============================================================================
Write-Host "[TEST 6] Invalidate Client Override Policy (client-vip, order)" -ForegroundColor Yellow
Write-Host "DELETE $BaseUrl/api/policies/cache/order?clientId=client-vip" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/policies/cache/order?clientId=client-vip" -Method DELETE -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Test 7: Get Cache Statistics (Final State)
# =============================================================================
Write-Host "[TEST 7] Get Cache Statistics (Final)" -ForegroundColor Yellow
Write-Host "GET $BaseUrl/api/policies/cache/statistics" -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/policies/cache/statistics" -UseBasicParsing
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor White
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# =============================================================================
# Summary
# =============================================================================
Write-Host "=============================================================================" -ForegroundColor Cyan
Write-Host "All tests completed!" -ForegroundColor Green
Write-Host "=============================================================================" -ForegroundColor Cyan
