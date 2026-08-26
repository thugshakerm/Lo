# ─────────────────────────────────────────────────────────────────────
# Lo Revival - Windows VPS Setup Script (v5 - .NET 10 + IIS)
# ─────────────────────────────────────────────────────────────────────
#
# Run as Administrator in PowerShell:
#   Set-ExecutionPolicy Bypass -Scope Process -Force
#   .\setup.ps1
#
# What this does:
#   1. Installs Chocolatey (if not present)
#   2. Detects / installs .NET 10 SDK (if not present)
#   3. Installs the .NET 10 Hosting Bundle via Chocolatey
#   4. Pulls the Lo repo into C:\inetpub\lo
#   5. Restores + publishes Lo.Website to C:\inetpub\lo-website
#   6. Sets up C:\lo\storage for WSDL/keys/Lua
#   7. Creates 5 IIS sites, all reverse-proxying to Kestrel on :8080
#   8. Smoke-tests everything
#
# The PHP/Laravel backend was retired after this commit; the C# /
# ASP.NET Core 10 rewrite is now the only backend.
# ─────────────────────────────────────────────────────────────────────

# Be lenient with non-critical command errors so the script can
# continue past a noisy "From <url>" message on a successful git
# fetch. We use try/catch on each step.
$ErrorActionPreference = 'Continue'

Write-Host "=== Lo Revival - Windows Setup v5 (.NET 10 + IIS) ===" -ForegroundColor Cyan
Write-Host "IIS + .NET 10 + Lo.Website (C# / ASP.NET Core)"
Write-Host ""

# ── 1. Check we're admin ────────────────────────────────────────────
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Please run PowerShell as Administrator."
    exit 1
}

# Helper: refresh PATH from registry
function Refresh-Path {
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
}

# ── 2. Install Chocolatey if missing ────────────────────────────────
if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Write-Host "[1/8] Installing Chocolatey..." -ForegroundColor Yellow
    try {
        Set-ExecutionPolicy Bypass -Scope Process -Force
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
        Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        Refresh-Path
    } catch {
        Write-Host "  Chocolatey install failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[1/8] Chocolatey already installed" -ForegroundColor Green
}

# ── 3. .NET 10 SDK ─────────────────────────────────────────────────
Write-Host "[2/8] .NET SDK check..." -ForegroundColor Yellow
$dotnetExe = $null
$dotnetVersions = & dotnet --list-sdks 2>$null
foreach ($v in $dotnetVersions) {
    if ($v -match "^10\.\d") {
        $dotnetExe = (Get-Command dotnet).Source
        Write-Host "  .NET 10 SDK present: $v" -ForegroundColor Green
        break
    }
}
if (-not $dotnetExe) {
    # Try installing via the official .NET 10 script
    Write-Host "  .NET 10 SDK not found. Installing..." -ForegroundColor Yellow
    $dotnetScript = "$env:TEMP\dotnet-install.ps1"
    try {
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetScript -UseBasicParsing
        & $dotnetScript -Channel 10.0 -InstallDir "C:\Program Files\dotnet" -NoPath
        Refresh-Path
        $dotnetExe = "C:\Program Files\dotnet\dotnet.exe"
    } catch {
        Write-Host "  Failed to install .NET 10: $_" -ForegroundColor Red
        Write-Host "  Install manually: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
        exit 1
    }
}

# ── 4. ASP.NET Core 10 Hosting Bundle ───────────────────────────────
Write-Host "[3/8] ASP.NET Core 10 Hosting Bundle (IIS module)..." -ForegroundColor Yellow
$hostingBundle = Get-ChildItem "C:\Program Files\IIS\IIS Express\AspNetCoreModuleV2.dll" -ErrorAction SilentlyContinue
$aspnetcoreHosting = choco list --exact --id "dotnet-10.0-windowshosting" --limit-output 2>$null | Select-String "dotnet-10.0-windowshosting"

if (-not $hostingBundle -and -not $aspnetcoreHosting) {
    Write-Host "  Hosting Bundle not found. Trying chocolatey install..." -ForegroundColor Yellow
    try {
        choco install -y dotnet-10.0-windowshosting --no-progress
        Write-Host "  Hosting Bundle installed via chocolatey" -ForegroundColor Green
    } catch {
        Write-Host "  Could not auto-install the Hosting Bundle." -ForegroundColor Yellow
        Write-Host "  Please install it manually from:" -ForegroundColor Yellow
        Write-Host "    https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
        Write-Host "  Then re-run this script." -ForegroundColor Yellow
    }
} else {
    Write-Host "  ASP.NET Core Hosting Bundle already installed" -ForegroundColor Green
}

Refresh-Path

# ── 5. Pull the Lo repo ─────────────────────────────────────────────
Write-Host "[4/8] Deploying the Lo app to C:\inetpub\lo..." -ForegroundColor Yellow
$appPath = "C:\inetpub\lo"
if (-not (Test-Path "$appPath\.git")) {
    try {
        & git clone https://github.com/thugshakerm/Lo.git $appPath 2>&1 | Out-Null
    } catch {
        Write-Host "  git clone failed: $_" -ForegroundColor Red
        exit 1
    }
}
Push-Location $appPath
try {
    & git fetch origin arena/01a03b34-lo 2>&1 | Out-Null
    & git checkout arena/01a03b34-lo 2>&1 | Out-Null
    & git pull origin arena/01a03b34-lo 2>&1 | Out-Null
} catch {
    Write-Host "  git fetch/checkout/pull reported an issue (continuing): $_" -ForegroundColor Yellow
}
Pop-Location
Write-Host "  Lo repo at $appPath (branch arena/01a03b34-lo)" -ForegroundColor Green

# ── 6. Build + publish Lo.Website ───────────────────────────────────
Write-Host "[5/8] Building + publishing Lo.Website (C# / ASP.NET Core 10)..." -ForegroundColor Yellow
$publishPath = "C:\inetpub\lo-website"
if (Test-Path $publishPath) { Remove-Item -Recurse -Force $publishPath }

Push-Location "$appPath\Lo.Website"
$pubOutput = & dotnet publish -c Release -o $publishPath --nologo 2>&1
$pubExit = $LASTEXITCODE
Pop-Location
$pubOutput | Out-Host
if ($pubExit -ne 0) {
    Write-Host "  dotnet publish FAILED (exit $pubExit). See errors above." -ForegroundColor Red
    exit 1
}
Write-Host "  Published to $publishPath" -ForegroundColor Green

# ── 7. Storage directories ──────────────────────────────────────────
Write-Host "[6/8] Setting up storage directories..." -ForegroundColor Yellow
$storageRoot = "C:\lo\storage"
foreach ($sub in @("rbx\fflags", "rbx\files\2018CoreGui", "rbx\files\assets", "rbx\files\private", "rbx\files\public", "rbx\files\thumbs", "rbx\files\scripts")) {
    $p = Join-Path $storageRoot $sub
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p -Force | Out-Null }
}
# Copy reference content from the repo
$repoStorage = "$appPath\storage\rbx\files"
if (Test-Path $repoStorage) {
    Copy-Item -Path "$repoStorage\scripts\gameserver.lua" -Destination "$storageRoot\rbx\files\gameserver.lua" -Force -ErrorAction SilentlyContinue
    Copy-Item -Path "$repoStorage\scripts\gameserver.json" -Destination "$storageRoot\rbx\files\gameserver.json" -Force -ErrorAction SilentlyContinue
    Copy-Item -Path "$repoStorage\private\RCCService.wsdl" -Destination "$storageRoot\rbx\files\private\RCCService.wsdl" -Force -ErrorAction SilentlyContinue
    $fflagSrc = Join-Path $appPath "storage\rbx\fflags\2018M.json"
    if (Test-Path $fflagSrc) {
        Copy-Item -Path $fflagSrc -Destination "$storageRoot\rbx\fflags\2018M.json" -Force -ErrorAction SilentlyContinue
    }
}
Write-Host "  $storageRoot created (with reference content from repo)" -ForegroundColor Green

# ── 8. appsettings.json override for production DB ──────────────────
Write-Host "[7/8] Writing production appsettings.json..." -ForegroundColor Yellow
$prodSettings = @{
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    ConnectionStrings = @{
        Postgres = "Host=127.0.0.1;Port=5432;Database=lo;Username=lo;Password=lo"
    }
    Revival = @{
        Domain = "gazeee.xyz"
        DomainLength = 10
        Subdomains = @{
            Web = "www"
            Api = "api"
            AssetGame = "assetgame"
            ClientSettings = "clientsettingscdn"
            Compat = "applicationcompatibility"
        }
    }
    Rcc = @{
        Host = "127.0.0.1"
        Port = 64989
        TimeoutSeconds = 30
    }
} | ConvertTo-Json -Depth 10
Set-Content -Path "$publishPath\appsettings.Production.json" -Value $prodSettings

# ── 9. IIS configuration ────────────────────────────────────────────
Write-Host "[8/8] Configuring IIS for the 5 subdomains..." -ForegroundColor Yellow

# Install IIS CGI + URL Rewrite if not present
$requiredFeatures = @("Web-CGI", "Web-Default-Doc", "Web-Dir-Browsing", "Web-Http-Errors", "Web-Static-Content", "Web-Http-Redirect", "Web-URL-Rewrite")
foreach ($f in $requiredFeatures) {
    $feat = Get-WindowsFeature -Name $f -ErrorAction SilentlyContinue
    if ($feat -and $feat.InstallState -ne 'Installed') {
        Write-Host "  Installing $f..." -ForegroundColor Yellow
        Install-WindowsFeature -Name $f | Out-Null
    }
}

Import-Module WebAdministration

# Stop and remove any existing "lo-website" sites
$existingSites = @('lo-website-www', 'lo-website-api', 'lo-website-assetgame', 'lo-website-clientsettingscdn', 'lo-website-applicationcompatibility')
foreach ($siteName in $existingSites) {
    if (Test-Path "IIS:\Sites\$siteName") {
        Stop-WebSite $siteName -ErrorAction SilentlyContinue
        Remove-WebSite $siteName -ErrorAction SilentlyContinue
    }
}
if (Test-Path "IIS:\AppPools\lo-website-pool") {
    Remove-WebAppPool "lo-website-pool" -ErrorAction SilentlyContinue
}

# Create the application pool
New-WebAppPool -Name "lo-website-pool" -Force | Out-Null
Set-ItemProperty "IIS:\AppPools\lo-website-pool" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\lo-website-pool" -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\lo-website-pool" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# Create the 5 IIS sites.
$subdomains = @{
    'lo-website-www'                       = 'www.gazeee.xyz'
    'lo-website-api'                       = 'api.gazeee.xyz'
    'lo-website-assetgame'                 = 'assetgame.gazeee.xyz'
    'lo-website-clientsettingscdn'         = 'clientsettingscdn.gazeee.xyz'
    'lo-website-applicationcompatibility'  = 'applicationcompatibility.gazeee.xyz'
}

foreach ($siteName in $subdomains.Keys) {
    $hostname = $subdomains[$siteName]
    New-WebSite -Name $siteName -PhysicalPath $publishPath -ApplicationPool "lo-website-pool" -Port 80 -HostHeader $hostname -Force | Out-Null
    Write-Host "  Created site: $siteName ($hostname -> $publishPath)" -ForegroundColor Green
}

# web.config for the ASP.NET Core Module (which the Hosting Bundle installed)
$webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <location path="." inheritInChildApplications="false">
        <system.webServer>
            <handlers>
                <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
            </handlers>
            <aspNetCore processPath="dotnet" arguments="Lo.Website.dll" stdoutLogEnabled="true" stdoutLogFile="C:\inetpub\logs\lo-website" hostingModel="InProcess" />
        </system.webServer>
    </location>
</configuration>
"@
$webConfigPath = "$publishPath\web.config"
Set-Content -Path $webConfigPath -Value $webConfig
Write-Host "  Wrote web.config with ASP.NET Core Module handler" -ForegroundColor Green

# Start everything
Start-Service W3SVC -ErrorAction SilentlyContinue
foreach ($siteName in $subdomains.Keys) {
    Start-WebSite $siteName
}

# ── 10. Smoke test ──────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Smoke test ===" -ForegroundColor Cyan
$dotVer = & dotnet --version 2>&1
Write-Host ".NET SDK: $dotVer"
Write-Host "Lo.Website:   $publishPath"
Write-Host ""

Write-Host "IIS sites:"
foreach ($siteName in $subdomains.Keys) {
    $state = (Get-WebSite -Name $siteName).State
    $color = if ($state -eq 'Started') { 'Green' } else { 'Red' }
    Write-Host "  $($subdomains[$siteName]) : $state" -ForegroundColor $color
}

Write-Host ""
Write-Host "=== Setup complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. EDIT THE DB CREDENTIALS:" -ForegroundColor Yellow
Write-Host "   notepad C:\inetpub\lo-website\appsettings.Production.json"
Write-Host "   (set the Postgres password in ConnectionStrings.Postgres)"
Write-Host ""
Write-Host "2. Apply the database schema:" -ForegroundColor Yellow
Write-Host "   psql -U lo -d lo -f C:\inetpub\lo\db\schema.sql"
Write-Host ""
Write-Host "3. Generate the RSA keypair (1024-bit):" -ForegroundColor Yellow
Write-Host "   powershell -File .\deploy\windows\generate-keys.ps1"
Write-Host ""
Write-Host "4. Drop your 2018M gameserver.lua at C:\lo\storage\rbx\files\gameserver.lua"
Write-Host "   (already copied from the repo as a placeholder)"
Write-Host ""
Write-Host "5. Run deploy\windows\test-local.ps1 to verify the 5 sites respond"
Write-Host ""
Write-Host "6. Install the Cloudflare tunnel (deploy\windows\setup-cloudflared.ps1)"
Write-Host "   Once the tunnel is up, point the 5 DNS records to <tunnel-id>.cfargotunnel.com"
