# ─────────────────────────────────────────────────────────────────────
# Lo Revival - Local Smoke Test (C# edition)
# ─────────────────────────────────────────────────────────────────────
#
# Run after setup.ps1 to verify everything is wired up correctly
# BEFORE going through the Cloudflare tunnel.
#
# This hits the local IIS sites directly, which reverse-proxy to
# Kestrel (Lo.Website) on localhost:8080. If this works, the tunnel
# will work.
#
# ─────────────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnetCmd) { $dotnetCmd.Source } else { "C:\Program Files\dotnet\dotnet.exe" }
$publishPath = "C:\inetpub\lo-website"

Write-Host "=== Lo Revival - Local Smoke Test ===" -ForegroundColor Cyan

# ── 1. .NET runtime + Kestrel process ──────────────────────────────
Write-Host ""
Write-Host "[1/6] .NET runtime present?" -ForegroundColor Yellow
if ($dotnetExe -and (Test-Path $dotnetExe)) {
    $ver = & $dotnetExe --version 2>&1
    Write-Host "  dotnet $ver" -ForegroundColor Green
} else {
    Write-Host "  .NET SDK not found ($dotnetExe)" -ForegroundColor Red
    Write-Host "  Install from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    exit 1
}

# ── 2. Publish output present? ───────────────────────────────────
Write-Host ""
Write-Host "[2/6] Lo.Website published?" -ForegroundColor Yellow
if (Test-Path "$publishPath\Lo.Website.dll") {
    Write-Host "  Lo.Website.dll: PRESENT" -ForegroundColor Green
} else {
    Write-Host "  Lo.Website.dll: MISSING ($publishPath\Lo.Website.dll)" -ForegroundColor Red
    exit 1
}

# ── 3. W3SVC running? ───────────────────────────────────────────
Write-Host ""
Write-Host "[3/6] W3SVC (IIS) running?" -ForegroundColor Yellow
$w3svc = Get-Service W3SVC -ErrorAction SilentlyContinue
if ($w3svc -and $w3svc.Status -eq 'Running') {
    Write-Host "  W3SVC: RUNNING" -ForegroundColor Green
} else {
    Write-Host "  W3SVC: NOT RUNNING (try: Start-Service W3SVC)" -ForegroundColor Red
    exit 1
}

# ── 4. PostgreSQL connectivity ───────────────────────────────────
Write-Host ""
Write-Host "[4/6] PostgreSQL connectivity?" -ForegroundColor Yellow
$prodSettingsPath = "$publishPath\appsettings.Production.json"
if (Test-Path $prodSettingsPath) {
    $json = Get-Content $prodSettingsPath -Raw | ConvertFrom-Json
    $connStr = $json.ConnectionStrings.Postgres
    # crude parse
    $dbHost = "127.0.0.1"; if ($connStr -match 'Host=([^;]+)') { $dbHost = $Matches[1] }
    $dbPort = "5432";     if ($connStr -match 'Port=([^;]+)') { $dbPort = $Matches[1] }
    $dbName = "lo";       if ($connStr -match 'Database=([^;]+)') { $dbName = $Matches[1] }
    $dbUser = "lo";       if ($connStr -match 'Username=([^;]+)') { $dbUser = $Matches[1] }
    $dbPass = "";         if ($connStr -match 'Password=([^;]+)') { $dbPass = $Matches[1] }
    try {
        $env:PGPASSWORD = $dbPass
        & psql -U $dbUser -h $dbHost -p $dbPort -d $dbName -c "SELECT 1" -t 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  PostgreSQL: CONNECTED ($($dbHost):$($dbPort)/$($dbName))" -ForegroundColor Green
        } else {
            Write-Host "  PostgreSQL: FAILED (psql exit $($LASTEXITCODE))" -ForegroundColor Red
        }
    } catch {
        Write-Host "  PostgreSQL: FAILED" -ForegroundColor Red
        Write-Host "    $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  No appsettings.Production.json at $prodSettingsPath (skip)" -ForegroundColor Yellow
}

# ── 5. HTTP responses from local IIS ─────────────────────────────
Write-Host ""
Write-Host "[5/6] HTTP responses from local IIS?" -ForegroundColor Yellow

# Add the 5 subdomains to the hosts file so they resolve to 127.0.0.1
$hostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
$hostEntries = @(
    "127.0.0.1`tgazeee.xyz",
    "127.0.0.1`twww.gazeee.xyz",
    "127.0.0.1`tapi.gazeee.xyz",
    "127.0.0.1`tassetgame.gazeee.xyz",
    "127.0.0.1`tclientsettingscdn.gazeee.xyz",
    "127.0.0.1`tapplicationcompatibility.gazeee.xyz"
)
$currentHosts = Get-Content $hostsFile
$needsUpdate = $false
foreach ($entry in $hostEntries) {
    if ($currentHosts -notcontains $entry) { $needsUpdate = $true }
}
if ($needsUpdate) {
    Write-Host "  Adding 5 subdomain entries to hosts file..." -ForegroundColor Yellow
    Add-Content -Path $hostsFile -Value "`r`n# Lo Revival subdomains" -Force
    foreach ($entry in $hostEntries) {
        if ($currentHosts -notcontains $entry) {
            Add-Content -Path $hostsFile -Value $entry -Force
        }
    }
}

$tests = @(
    @{ Name = "Apex (www)";     Url = "http://www.gazeee.xyz/";                              ExpectStatus = 200 },
    @{ Name = "FFlags";         Url = "http://clientsettingscdn.gazeee.xyz/v1/settings/application"; ExpectStatus = 200 },
    @{ Name = "Compat";         Url = "http://applicationcompatibility.gazeee.xyz/v1/compatibility"; ExpectStatus = 200 },
    @{ Name = "Asset (no id)";  Url = "http://assetgame.gazeee.xyz/Asset/";                  ExpectStatus = 200 },
    @{ Name = "Negotiate";      Url = "http://www.gazeee.xyz/Login/Negotiate.ashx";          ExpectStatus = 200 },
    @{ Name = "Healthz (any)";  Url = "http://gazeee.xyz/healthz";                          ExpectStatus = 200 }
)
foreach ($test in $tests) {
    try {
        $response = Invoke-WebRequest -Uri $test.Url -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq $test.ExpectStatus) {
            Write-Host "  $($test.Name): OK ($($response.StatusCode))" -ForegroundColor Green
        } else {
            Write-Host "  $($test.Name): UNEXPECTED $($response.StatusCode)" -ForegroundColor Yellow
        }
    } catch {
        $code = if ($_.Exception -and $_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "CONN_ERR" }
        Write-Host "  $($test.Name): FAILED ($code)" -ForegroundColor Red
        if ($_.Exception.Message) { Write-Host "    $($_.Exception.Message)" -ForegroundColor Red }
    }
}

# ── 6. RSA key + WSDL present ────────────────────────────────────
Write-Host ""
Write-Host "[6/6] RSA key present?" -ForegroundColor Yellow
$keyPath = "C:\lo\storage\privateKey1024.pem"
if (Test-Path $keyPath) {
    Write-Host "  RSA private key: PRESENT" -ForegroundColor Green
} else {
    Write-Host "  RSA private key: MISSING ($keyPath)" -ForegroundColor Red
    Write-Host "    openssl genrsa -out $keyPath 1024" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan
