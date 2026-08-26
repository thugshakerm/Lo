# Lo.Api — C# 2018M Revival Backend

ASP.NET Core 8 implementation of the Lo revival protocol surface.
This is the C# rewrite of the original PHP/Laravel backend (see the
git history; the PHP files are gone after this commit).

## Why C#?

The original PHP/Laravel version kept hitting Windows-PHP pain
points (missing `intl`/`bcmath`/etc. extensions in the Chocolatey
build, manual Apache installs, third-party zip URLs that 308).
ASP.NET Core 8 on Windows is the platform-native choice: IIS is
already on the box, the .NET runtime ships with all the cryptography
and HTTP machinery, and the C# rewrite is ~40% smaller than the
PHP code it replaces.

## Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 8 (LTS) |
| Web server | Kestrel (IIS reverse-proxies in production) |
| Framework | ASP.NET Core 8 Minimal API |
| Database | PostgreSQL 17 (via Npgsql) |
| SOAP | Hand-rolled via HttpClient + XDocument (RccClient.cs) |
| Crypto | `System.Security.Cryptography.RSA` (built into .NET) |
| JSON | `System.Text.Json` (built into .NET) |

## Layout

```
Lo.Api/
├── Program.cs                    # entry point + middleware pipeline
├── appsettings.json              # revival config (mirrors PHP config/rbx.php)
├── Config/
│   └── RevivalConfig.cs          # bound from "Revival" config section
├── Data/
│   └── AppDb.cs                  # thin Npgsql wrapper (no ORM)
├── Signing/
│   ├── KeyManager.cs             # loads RSA-1024 private key
│   └── SecurityNotary.cs         # signs Lua scripts in V1/V2 format
├── Rcc/
│   ├── Job.cs                    # RCC job description
│   ├── ScriptExecution.cs        # Lua script to run on a job
│   ├── LuaType.cs                # enum: TNIL/TBOOLEAN/TNUMBER/TSTRING/TTABLE
│   ├── LuaValue.cs               # typed Lua value
│   └── RccClient.cs              # SOAP client (hand-rolled, no WCF)
├── Middleware/
│   ├── SubdomainRouter.cs        # sets HttpContext.Items["subdomain"]
│   ├── SubdomainGuard.cs         # rejects mismatched-subdomain requests
│   ├── SetClientIp.cs            # honors CF-Connecting-IP / X-Forwarded-For
│   └── RateLimit.cs              # crude in-memory per-IP rate limit
├── Models/
│   ├── User.cs
│   ├── Asset.cs
│   ├── AssetOwnership.cs
│   ├── Place.cs
│   ├── GameServer.cs
│   └── GamePass.cs
└── Controllers/
    ├── AuthController.cs         # /Login/Negotiate.ashx + Default.aspx + Logout
    ├── AssetController.cs        # /Asset, /Asset/{id}, Range support
    ├── AvatarController.cs       # /Asset/CharacterFetch.ashx, /Asset/BodyColors.ashx
    ├── BadgesController.cs       # /Game/Badges/BadgeHandler.ashx
    ├── CompatibilityController.cs # /v1/compatibility, /v1/client-version
    ├── GameController.cs         # /Game/PlaceLauncher.ashx, /Game/Join.ashx, /Game/Gameserver.lua
    ├── GamePassController.cs     # /Game/GamePass/GamePassHandler.ashx
    ├── GameServerApiController.cs # /Game/ServerPing, /Game/KillServer, /api/gameserver/*
    ├── InsertController.cs       # /Game/Tools/InsertAsset.ashx
    ├── LuaWebController.cs       # /Game/LuaWebService/HandleSocialRequest.ashx
    ├── MarketplaceController.cs  # /marketplace/productinfo, /ownership/hasasset
    ├── PlaceController.cs        # /universes/validate-place-join, /universes/{id}/game-start-info
    ├── SettingController.cs      # /v1/settings/application
    └── ThumbnailController.cs    # /Game/Tools/ThumbnailAsset.ashx
```

## The 5 subdomains

The 2018M client expects 5 subdomains. They all hit the same Kestrel
process; the `SubdomainRouter` middleware reads the `Host:` header
and stashes the subdomain, then `SubdomainGuard` enforces that the
route's `SubdomainKey` metadata matches.

| Subdomain | Routes |
|---|---|
| `www.<domain>` | Most ashx endpoints (Login, Game, Asset, Badges, GamePass, etc.) |
| `api.<domain>` | Modern web API (universes/*, marketplace/*, ownership/*) |
| `assetgame.<domain>` | Aliases of www routes (the binary uses both) |
| `clientsettingscdn.<domain>` | FFlags (`/v1/settings/application`) |
| `applicationcompatibility.<domain>` | Version compat (`/v1/compatibility`) |

The bare apex (`<domain>` without a subdomain) is untouched; only the
5 above are routed to Lo.Api.

## Running locally

```powershell
# Restore + build
cd Lo.Api
dotnet restore
dotnet build

# Set the DB connection (defaults work for local Postgres)
$env:ConnectionStrings__Postgres = "Host=127.0.0.1;Port=5432;Database=lo;Username=lo;Password=lo"

# Run
dotnet run

# Smoke test
curl http://www.gazeee.xyz:8080/healthz
#   (assuming you have a hosts entry pointing www.gazeee.xyz at 127.0.0.1)
```

## Running behind IIS (production)

1. Install the **.NET 8 Hosting Bundle** on the Windows VPS:
   https://dotnet.microsoft.com/download/dotnet/8.0
   (Get the "Hosting Bundle" — it includes the ASP.NET Core Module
   for IIS.)

2. Publish the app:
   ```powershell
   dotnet publish -c Release -o C:\inetpub\lo-api
   ```

3. Create the IIS site. Run `deploy\windows\setup-iis.ps1` (new
   script, to be added) or do it manually:
   - AppPool: `lo-api-pool`, no managed code
   - Site physical path: `C:\inetpub\lo-api`
   - 5 sites for the 5 subdomains, all on port 80 with Host: headers
   - Per-site `web.config` rewrites to `http://127.0.0.1:8080` (the
     Kestrel binding), or use ARR as a reverse proxy.

## Database

The C# version uses raw Npgsql + hand-written SQL. The original
Laravel migrations (in `../database/migrations/`) define the schema;
apply them with `psql` before first run:

```sql
-- From psql:
\i ../database/migrations/2025_01_01_000001_create_users_table.sql
\i ../database/migrations/2025_01_01_000002_create_assets_table.sql
-- ... etc
```

(You can also re-create the schema by running the `up()` logic
in those files; the original code was Laravel migrations in PHP
syntax, so the SQL needs to be extracted once. See the C# port
`AppDb.cs` for the exact column list each query expects.)

## Tests

The original PHP test suite (`tests/Feature/Rbx/AssetEndpointTest.php`)
was a Laravel/PHPUnit artifact. The C# rewrite doesn't ship its own
test runner yet — TODO: add xUnit + WebApplicationFactory.

For a quick smoke test:

```powershell
.\deploy\windows\test-local.ps1
```

This hits each of the 5 subdomains over HTTP and verifies the
right handler responds. (Same script as the PHP version; it just
talks to the C# endpoints now.)
