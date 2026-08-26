# ─────────────────────────────────────────────────────────────────────
# Lo Revival - Cloudflare Tunnel Setup (Windows side)
# ─────────────────────────────────────────────────────────────────────
#
# What this does:
#   1. Downloads cloudflared.exe (the Cloudflare Tunnel client)
#   2. Installs it as a Windows service that auto-starts on boot
#   3. Writes a config.yml that routes the 5 subdomains to localhost:80
#
# PREREQUISITES:
#   1. You have a Cloudflare account with gazeee.xyz added to it
#      (free plan is fine).
#   2. You have already created a Tunnel in the Cloudflare Zero Trust
#      dashboard, and have its TUNNEL_TOKEN (a long JWT-style string).
#      Dashboard path: Zero Trust -> Networks -> Tunnels -> Create
#      Name: lo-revival
#      Then in the dashboard, for each of the 5 subdomains, add a
#      "Public Hostname" that points at http://localhost:80.
#      OR you can do it all in this config (see step 4 below).
#
# After this script:
#   - The cloudflared service is running.
#   - The 5 subdomains are routable from the public internet, through
#     Cloudflare, into your Windows VPS, into Apache, into Laravel.
#
# ─────────────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'

$tunnelToken = $env:CLOUDFLARE_TUNNEL_TOKEN
if (-not $tunnelToken) {
    Write-Host "ERROR: Set the CLOUDFLARE_TUNNEL_TOKEN environment variable first." -ForegroundColor Red
    Write-Host "  \$env:CLOUDFLARE_TUNNEL_TOKEN = 'eyJhIjoi...'" -ForegroundColor Yellow
    Write-Host "  .\setup-cloudflared.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Lo Revival - Cloudflare Tunnel Setup ===" -ForegroundColor Cyan

# ── 1. Download cloudflared.exe ─────────────────────────────────────
$cfDir = "C:\tools\cloudflared"
if (-not (Test-Path $cfDir)) { New-Item -ItemType Directory -Path $cfDir -Force }
$cfExe = "$cfDir\cloudflared.exe"
if (-not (Test-Path $cfExe)) {
    Write-Host "[1/3] Downloading cloudflared.exe..." -ForegroundColor Yellow
    $url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
    Invoke-WebRequest -Uri $url -OutFile $cfExe
}

# ── 2. Write the tunnel config ──────────────────────────────────────
Write-Host "[2/3] Writing tunnel config..." -ForegroundColor Yellow
$configDir = "C:\Windows\System32\config\systemprofile\.cloudflared"
if (-not (Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir -Force }

# Tunnel ID is embedded in the JWT token; cloudflared figures it out.
# We only need to define the ingress rules (which subdomain -> where).
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

# Convert the token to a JSON credentials file
# The token IS the credentials file content (base64-decoded)
$tokenBytes = [Convert]::FromBase64String($tunnelToken)
$jsonPath = "$configDir\lo-revival.json"
[System.IO.File]::WriteAllBytes($jsonPath, $tokenBytes)

# Write the config
$configPath = "$configDir\config.yml"
Set-Content -Path $configPath -Value $configContent

# ── 3. Install + start as a service ─────────────────────────────────
Write-Host "[3/3] Installing cloudflared as a Windows service..." -ForegroundColor Yellow
& $cfExe service install
Start-Service cloudflared

# ── Done ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Tunnel installed! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps in the Cloudflare Zero Trust dashboard:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Go to https://one.dash.cloudflare.com/"
Write-Host "2. Networks -> Tunnels -> lo-revival"
Write-Host "3. Under 'Public Hostnames', add 5 routes:"
Write-Host "     www.gazeee.xyz  -> http://localhost:80"
Write-Host "     api.gazeee.xyz  -> http://localhost:80"
Write-Host "     assetgame.gazeee.xyz -> http://localhost:80"
Write-Host "     clientsettingscdn.gazeee.xyz -> http://localhost:80"
Write-Host "     applicationcompatibility.gazeee.xyz -> http://localhost:80"
Write-Host ""
Write-Host "4. (Already done if DNS for gazeee.xyz is on Cloudflare.)"
Write-Host "   The DNS records for those 5 subdomains will be auto-created."
Write-Host ""
Write-Host "5. Test:"
Write-Host "     https://www.gazeee.xyz/  -> should return the Lo service info JSON"
Write-Host "     https://api.gazeee.xyz/v1/settings/application -> should return FFlag JSON"
Write-Host ""
Write-Host "If those return the right things, your tunnel + Apache + Laravel is working."
