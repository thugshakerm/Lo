$ErrorActionPreference = 'Stop'

$tunnelToken = $env:CLOUDFLARE_TUNNEL_TOKEN
if (-not $tunnelToken) {
    Write-Host "ERROR: Set the CLOUDFLARE_TUNNEL_TOKEN environment variable first." -ForegroundColor Red
    Write-Host "  \$env:CLOUDFLARE_TUNNEL_TOKEN = 'eyJhIjoi...'" -ForegroundColor Yellow
    Write-Host "  .\setup-cloudflared.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Lo Revival - Cloudflare Tunnel Setup ===" -ForegroundColor Cyan

$cfDir = "C:\tools\cloudflared"
if (-not (Test-Path $cfDir)) { New-Item -ItemType Directory -Path $cfDir -Force }
$cfExe = "$cfDir\cloudflared.exe"
if (-not (Test-Path $cfExe)) {
    Write-Host "[1/3] Downloading cloudflared.exe..." -ForegroundColor Yellow
    $url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
    Invoke-WebRequest -Uri $url -OutFile $cfExe
}

Write-Host "[2/3] Writing tunnel config..." -ForegroundColor Yellow
$configDir = "C:\Windows\System32\config\systemprofile\.cloudflared"
if (-not (Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir -Force }

$configContent = @"
tunnel: lo-revival
credentials-file: $configDir\lo-revival.json

ingress:
  - hostname: www.gazeee.xyz
    service: http://localhost:80
  - hostname: api.gazeee.xyz
    service: http://localhost:80
  - hostname: assetgame.gazeee.xyz
    service: http://localhost:80
  - hostname: clientsettingscdn.gazeee.xyz
    service: http://localhost:80
  - hostname: applicationcompatibility.gazeee.xyz
    service: http://localhost:80
  - service: http_status:404
"@

$tokenBytes = [Convert]::FromBase64String($tunnelToken)
$jsonPath = "$configDir\lo-revival.json"
[System.IO.File]::WriteAllBytes($jsonPath, $tokenBytes)

$configPath = "$configDir\config.yml"
Set-Content -Path $configPath -Value $configContent

Write-Host "[3/3] Installing cloudflared as a Windows service..." -ForegroundColor Yellow
$cfService = Get-Service cloudflared -ErrorAction SilentlyContinue
if ($cfService) {
    Stop-Service cloudflared -ErrorAction SilentlyContinue
    & $cfExe service uninstall 2>&1 | Out-Null
}

& $cfExe service install $tunnelToken
Start-Service cloudflared -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Tunnel installed! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps in the Cloudflare Zero Trust dashboard:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Go to https://one.dash.cloudflare.com/"
Write-Host "2. Networks -> Tunnels -> your tunnel"
Write-Host "3. Under 'Public Hostnames', add 5 routes pointing to http://localhost:80:"
Write-Host "     www.gazeee.xyz  -> http://localhost:80"
Write-Host "     api.gazeee.xyz  -> http://localhost:80"
Write-Host "     assetgame.gazeee.xyz -> http://localhost:80"
Write-Host "     clientsettingscdn.gazeee.xyz -> http://localhost:80"
Write-Host "     applicationcompatibility.gazeee.xyz -> http://localhost:80"
Write-Host ""
Write-Host "4. Verify from the public internet:"
Write-Host "     https://www.gazeee.xyz/  -> returns Lo service JSON"
Write-Host "     https://clientsettingscdn.gazeee.xyz/v1/settings/application -> returns FFlag JSON"
Write-Host ""
Write-Host "If those return JSON, your tunnel + IIS + ASP.NET Core 10 stack is fully live!"
