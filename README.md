# Lo — 2018M Roblox Revival (C# / ASP.NET Core 8)

A revival backend for the 2018M era of Roblox, written in C# on
ASP.NET Core 8. Serves the full 2018M protocol surface over HTTP
(plus SOAP for the RCC) so a patched 2018M `RobloxPlayerBeta.exe`
or `RobloxStudioBeta.exe` can join places.

The project structure mirrors the leaked `Roblox.Website` /
`Roblox.Libraries` layout — `Lo.Website` corresponds to
`Roblox.Website`, and `Lo.Libraries/Lo.Rcc` corresponds to
`Roblox.Libraries/Roblox.Rcc`.

## Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 8 (LTS) |
| Web server | Kestrel (IIS reverse-proxies in production) |
| Database | PostgreSQL 17 (via Npgsql) |
| SOAP | Hand-rolled via HttpClient + XDocument |
| Crypto | `System.Security.Cryptography.RSA` |
| JSON | `System.Text.Json` |

## Solution layout

```
Lo.sln

Lo.Website/                          # mirrors Roblox.Website/
├── Controllers/                     # 14 minimal-API controllers
├── Views/                           # (placeholder; Razor views deferred)
├── Code/
│   ├── Config/
│   │   └── RevivalConfig.cs         # bound from appsettings.json "Revival"
│   ├── Data/
│   │   ├── AppDb.cs                 # thin Npgsql wrapper
│   │   └── RccCallback.cs           # bridge Lo.Rcc -> AppDb
│   ├── Middleware/
│   │   ├── SubdomainRouter.cs       # sets HttpContext.Items["subdomain"]
│   │   ├── SubdomainGuard.cs        # rejects mismatched-subdomain requests
│   │   └── SetClientIp.cs           # honors CF-Connecting-IP / XFF
│   └── Sign/
│       ├── KeyManager.cs            # loads RSA-1024 private key
│       └── SecurityNotary.cs        # signs Lua scripts in V1/V2 format
├── Models/                          # User, Asset, Place, GameServer, ...
├── Properties/
│   └── launchSettings.json
├── Program.cs                       # entry point
├── appsettings.json
└── Lo.Website.csproj

Lo.Libraries/                        # mirrors Roblox.Libraries/
└── Lo.Rcc/                          # mirrors Roblox.Libraries/Roblox.Rcc
    ├── Job.cs
    ├── LuaType.cs
    ├── LuaValue.cs
    ├── RccConfig.cs
    ├── RccClient.cs                 # hand-rolled SOAP client
    ├── IRccCallback.cs              # host-side hooks (OnJobOpened, OnSoapFault)
    └── Lo.Rcc.csproj

deploy/
└── windows/
    ├── setup.ps1                    # one-shot installer
    ├── setup-cloudflared.ps1
    ├── test-local.ps1
    └── README.md

db/
└── schema.sql                       # consolidated PostgreSQL schema
```

## The 5 subdomains

| Subdomain | Routes |
|---|---|
| `www.gazeee.xyz` | Most ashx endpoints (Login, Game, Asset, Badges, GamePass, ...) |
| `api.gazeee.xyz` | Modern web API (universes/*, marketplace/*, ownership/*) |
| `assetgame.gazeee.xyz` | Aliases of www routes (the binary uses both) |
| `clientsettingscdn.gazeee.xyz` | FFlags (`/v1/settings/application`) |
| `applicationcompatibility.gazeee.xyz` | Version compat (`/v1/compatibility`) |

The bare apex `gazeee.xyz` is untouched; only the 5 above are routed
to Lo.Website. The patched client has the apex swapped in via binary
patch (gazeee.xyz is exactly 10 chars, drop-in for roblox.com).

## Why C# and not PHP

Originally this was a PHP/Laravel app. Three issues in 20 minutes
(Chocolatey's `composer` install script failing, the `apache-httpd`
package being removed, the `php` package shipping only 40 of 85
extensions) made it clear that PHP on Windows was the wrong
foundation. ASP.NET Core 8 is the platform-native choice; this
rewrite is ~40% smaller, deploys in one `dotnet publish`, and the
Kestrel + IIS integration needs no Apache / nginx / third-party zip.

## How the Lo.Rcc <-> Lo.Website split works

`Lo.Rcc` is a **leaf library** that doesn't know about the website's
database. When `RccClient.OpenJobAsync` succeeds, it calls
`IRccCallback.OnJobOpenedAsync` — the website registers an
`RccCallback` implementation that writes the row via `AppDb`.

This mirrors how `Roblox.Rcc` is a leaf in the leaked repo and
`Roblox.Website` wires it into `Roblox.DataAccess` through a
similar bridge.

## Quick start (local)

```powershell
cd Lo.Website
dotnet restore
dotnet build
dotnet run
# Kestrel listens on http://0.0.0.0:8080
```

## Production (Windows VPS)

```powershell
# Run as Administrator
Set-ExecutionPolicy Bypass -Scope Process -Force
.\deploy\windows\setup.ps1

# Edit DB credentials
notepad C:\inetpub\lo-website\appsettings.Production.json

# Apply the schema
psql -U lo -d lo -f C:\inetpub\lo\db\schema.sql
```

See `deploy/windows/README.md` for the full deployment guide,
including the Cloudflare tunnel setup.

## Status

- ✅ Lo.Website / Lo.Rcc project structure (mirrors Roblox.Website / Roblox.Libraries)
- ✅ All 14 controllers ported
- ✅ RCC client (hand-rolled SOAP over HttpClient)
- ✅ RSA-1024 script signing
- ✅ Subdomain routing middleware
- ✅ 5 IIS sites configured
- ✅ Consolidated PostgreSQL schema (`db/schema.sql`)
- ⏳ Build not yet verified (sandbox has no .NET SDK)
- ⏳ Frontend (deferred)
- ⏳ Patched 2018M client + RCCService binaries
