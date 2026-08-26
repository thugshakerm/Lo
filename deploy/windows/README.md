# Lo Revival - Windows VPS Deployment

This directory contains the scripts and configs for deploying the Lo
revival backend to a **Windows VPS**.

## What's here

| File | What it does |
|---|---|
| `setup.ps1` | One-shot installer: PHP 8.3, IIS CGI module, Composer, the Lo app, 5 IIS sites. Run as Administrator. |
| `setup-cloudflared.ps1` | Installs the Cloudflare Tunnel client and routes the 5 subdomains into your IIS. |
| `test-local.ps1` | Smoke test: checks PHP, IIS sites, DB, RSA key, WSDL, and HTTP responses for all 5 subdomains. |

## Why IIS, not Apache?

- **IIS is already installed on every Windows Server SKU.** No 14MB download from a URL that might 308 redirect.
- **Microsoft-supported PHP integration** via the IIS CGI module (`php-cgi.exe` via FastCGI).
- **HTTP/2, request filtering, auto-start, all built in.**
- The 5 subdomain vhosts become 5 IIS sites, all binding to port 80 on different `Host:` headers. The Cloudflare tunnel delivers everything to `localhost:80`.
- Apache on Windows is officially "unsupported" by the Apache team (you'd need Apache Lounge, which is a community build). IIS is the platform-native choice.

## Order of operations

```
┌─ 1. setup.ps1           (on the Windows VPS, as Administrator)
│     installs PHP + IIS CGI + Composer + Laravel
│
├─ 2. edit .env           (DB credentials, RCC host, etc.)
│
├─ 3. php artisan migrate (creates the database tables)
│
├─ 4. .\deploy\windows\generate-keys.ps1 (generates RSA keypair via .NET 10)
│
├─ 5. test-local.ps1      (smoke test everything locally)
│
├─ 6. setup-cloudflared.ps1 (with CLOUDFLARE_TUNNEL_TOKEN set)
│
├─ 7. add 5 CNAMEs in Cloudflare dashboard
│
└─ 8. test from the public internet:
        https://www.gazeee.xyz/  -> should return the Lo service info JSON
```

## Architecture

```
Internet
   │
   │  HTTPS to www.gazeee.xyz, api.gazeee.xyz, assetgame.gazeee.xyz,
   │  clientsettingscdn.gazeee.xyz, applicationcompatibility.gazeee.xyz
   │
   ▼
Cloudflare edge (free plan)
   │
   │  Cloudflare Tunnel (outbound HTTPS from the Windows box)
   │
   ▼
Windows VPS
   │
   │  cloudflared.exe -> forwards to localhost:80
   │
   ▼
IIS (W3SVC) on port 80
   │
   │  5 sites, all PhysicalPath = C:\inetpub\lo\public
   │  Each site has a Host: header binding for one subdomain
   │
   ▼
PHP-CGI (FastCGI) + Laravel 12 (the Lo app)
   │
   │ PostgreSQL 17 (already installed)
   │
   ▼
Patched RCCService.exe (port 64989, SOAP)
```

## Why a tunnel

The Windows VPS doesn't need port forwarding, doesn't need a public IP,
doesn't need a firewall exception. Cloudflare Tunnel works over outbound
HTTPS to Cloudflare's edge, so all the firewall needs is to allow outbound
HTTPS to Cloudflare's IPs (which is almost certainly already open).

The 5 subdomains are configured in the Cloudflare Zero Trust dashboard
and resolved to `<tunnel-id>.cfargotunnel.com`, which Cloudflare then
routes to the tunnel client on your box.

## The 5 IIS sites

After `setup.ps1` runs, these exist in IIS Manager:

| Site name | Binding | Physical path |
|---|---|---|
| `lo-www` | `http :80 :www.gazeee.xyz` | `C:\inetpub\lo\public` |
| `lo-api` | `http :80 :api.gazeee.xyz` | `C:\inetpub\lo\public` |
| `lo-assetgame` | `http :80 :assetgame.gazeee.xyz` | `C:\inetpub\lo\public` |
| `lo-clientsettingscdn` | `http :80 :clientsettingscdn.gazeee.xyz` | `C:\inetpub\lo\public` |
| `lo-applicationcompatibility` | `http :80 :applicationcompatibility.gazeee.xyz` | `C:\inetpub\lo\public` |

All share the `lo-pool` application pool. Laravel's `public/web.config`
has the URL rewrite rules (IIS equivalent of `.htaccess`).

## RSA keys

The RSA-1024 keypair is used for signing Lua scripts. The public blob
goes into the patched client binary at the `BgIAA...` location; the
private key goes into `C:\lo\storage\privateKey1024.pem`.

Generate with:
```powershell
.\deploy\windows\generate-keys.ps1
```
(Or run `openssl genrsa -out C:\lo\storage\privateKey1024.pem 1024` if openssl is installed).

## The 5 subdomains (DNS at Cloudflare)

When you add the 5 public hostnames in the Zero Trust dashboard,
Cloudflare auto-creates the DNS records:

| Subdomain | Routed to |
|---|---|
| `www.gazeee.xyz` | `http://localhost:80` |
| `api.gazeee.xyz` | `http://localhost:80` |
| `assetgame.gazeee.xyz` | `http://localhost:80` |
| `clientsettingscdn.gazeee.xyz` | `http://localhost:80` |
| `applicationcompatibility.gazeee.xyz` | `http://localhost:80` |

The bare apex `gazeee.xyz` (and any other subdomains like `blog.`,
`mail.`) are NOT touched by this setup. Your existing project on
`gazeee.xyz` continues to work as it does today.

## What runs the patched client?

For testing your revival, you'll want a patched 2018M Roblox client.
The same Windows VPS can run it (it has the GPU + audio drivers), but
you'll need to:

1. Download a 2018M `RobloxPlayerBeta.exe` and `RCCService.exe`
   (use a Roblox deployment downloader; the wiki's
   `articles/resources/roblox-deployment-downloader.md` has references)
2. Patch both binaries per the 2018M patching guide
3. Start RCCService.exe (it listens on 127.0.0.1:64989)
4. Launch the patched client; it should hit gazeee.xyz, find your
   app, and let you join a place.

## What's NOT here

- **RCCService.exe patching guide** (separate document; see
  `deploy/RCC-PATCHING.md` if you want me to write one)
- **RobloxPlayerBeta.exe patching guide** (separate document; see
  `deploy/CLIENT-PATCHING.md` if you want me to write one)
- **The frontend** (still a placeholder; the next phase)
