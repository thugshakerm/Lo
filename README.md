# madxka discord bot

Discord bot for [madxka.com](https://madxka.com) — look up users, items and
games and get a nice embed packed with info (headshot for users, thumbnail for
items, icon for games). All lookups go through the public API that MadXka runs,
which is **bubbablox v2** (the `bubbablox-v2` repo, branch `2021` — the source
MadXka's website is built on; same endpoints).

## Commands

| Command  | What it does                                                                  | Image used                        |
| -------- | ----------------------------------------------------------------------------- | --------------------------------- |
| `/user`  | lookup a user by **username or ID** (bio, RAP, join date, verified, status)    | user **headshot** (thumbnail)     |
| `/item`  | lookup a catalog item by **ID or name** (price, type, limited, sales, creator) | item **thumbnail**                |
| `/game`  | lookup a game by **universe/place ID or name** (visits, votes, players, genre) | game **icon**                     |
| `/avatar`| lookup a user's **full-body avatar**                                          | full body avatar (+headshot thumb)|

All command descriptions are temporarily
`yh basically the name all ts temp tho`.

## Badges (verified / staff)

In the `/user` and `/avatar` embeds:

- `isVerified: true` only → bubba's **verified badge** in the author row,
  right next to the username
- staff only (`isStaff` / `staff` / `isAdmin` / `isModerator` true) → the
  original bubbablox **admin shield** in the author row, next to the username
- **both true → the two badges side by side as two separate images**
  (sent as two attachments, which Discord lays out in a row — no combined
  image)

The badge artwork is the untouched original from the bubbablox-v2 source
(`verified.svg` and `admin_icon_11222016.svg`). Discord only reliably
previews PNG images, so `make_badges_png.py` renders 1:1 PNGs of those
originals (`proxy/badges/*.png`); the SVGs stay the source of truth. The
proxy serves them at `{base}/badges/verified.{svg|png}` and
`{base}/badges/admin.{svg|png}` — for the SVGs it first tries MadXka's live
copy and falls back to the bundled original (MadXka's `/images/*` middleware
400s non-`.png` paths, and `/verified.svg` is flaky from some networks).

## Architecture

```
discord  <-->  bot.py  <-->  Render proxy (proxy/proxy.py)  <-->  madxka.com
```

The proxy (a tiny aiohttp app) forwards every request to MadXka with a normal
browser User-Agent and serves the badge files. The bot talks only to the proxy,
so Cloudflare in front of MadXka never sees the Discord bot.

Two quirks it handles on its own:

- **Brotli**: MadXka/Cloudflare happily answer `Content-Encoding: br`. The
  proxy never forwards your `Accept-Encoding` — it always asks upstream for
  `gzip, deflate` — and the `Brotli` package is installed anyway as a safety
  net.
- **Badges**: the four badge files are also embedded in `proxy.py` as base64
  text, so `/badges/*` keeps working even if the files are missing from the
  deployed image. `GET /debug` shows what's on disk vs what's embedded.
- **CSRF**: MadXka's bubbablox-v2 `CsrfMiddleware` 403s every POST/PUT unless
  it carries a fresh `rbxcsrf4` cookie + matching `x-csrf-token` header. The
  proxy keeps the latest pair from upstream responses and does the
  fail-then-retry dance automatically, so plain POSTs from the bot just work.
  (`/debug` shows whether the csrf pair is currently loaded.)
- **Your cookie (optional)**: set `MADXKA_COOKIE` in the bot's `.env` (the bat
  prompts for it) to make the proxy act as your logged-in MadXka account —
  the bot forwards it to the proxy, which merges it with its own fresh csrf
  cookie before hitting MadXka. The proxy also accepts `MADXKA_COOKIE` as a
  Render env var if you'd rather configure it there.

## 1. Deploy the proxy on Render

Push this repo, then in Render: **New + → Blueprint** → pick this repo
(`render.yaml` creates the `madxka-proxy` web service). Or create a web service
manually:

- Root Directory: `proxy`
- Runtime: Python
- Build: `pip install -r requirements.txt`
- Start: `python proxy.py`
- Health check path: `/health`

You get `https://<name>.onrender.com`. Sanity check:
`https://<name>.onrender.com/health`, `.../badges/verified.svg` and
`.../debug` (shows upstream + which badge files are on disk).

## 2. Set up the bot on a Windows VPS

Everything is one double-click. Get `madxka-bot.bat` onto the VPS
(e.g. `curl -o madxka-bot.bat https://github.com/thugshakerm/Lo/raw/arena/01a03fb3-lo/madxka-bot.bat`
or download it from the GitHub page), then run it. It will:

1. check Python is installed (if not: `winget install Python.Python.3.12`,
   tick "Add python.exe to PATH")
2. `git clone --branch arena/01a03fb3-lo https://github.com/thugshakerm/Lo.git madxka-bot`
   (or pull the latest if already cloned)
3. create a `.venv` and install the dependencies
4. ask for your **Discord bot token** (from
   <https://discord.com/developers> → your app → Bot → Reset Token), the
   **proxy URL** (defaults to `https://ma-ly00.onrender.com`) and an optional
   **server ID** for instant command sync
5. write `.env` and start the bot

Every later run just pulls the latest and starts the bot with the saved
`.env`. To keep it running automatically, schedule `madxka-bot.bat` at
logon (Task Scheduler) or run it under NSSM as a service.

Linux/other: `python3 -m venv .venv && .venv/bin/pip install -r
requirements.txt && cp .env.example .env && .venv/bin/python bot.py`.

Invite the bot with the `bot` + `applications.commands` scopes and the
**Send Messages / Embed Links / Attach Files** permissions. Global command
sync can take a few minutes to show up.

## Endpoints used (bubbablox v2, same as madxka.com)

| Purpose                  | Endpoint                                                        |
| ------------------------ | --------------------------------------------------------------- |
| user by username         | `POST /apisite/users/v1/usernames/users`                        |
| user by id               | `GET /apisite/users/v1/users/{id}`                              |
| user status text         | `GET /apisite/users/v1/users/{id}/status`                       |
| user headshot            | `GET /apisite/thumbnails/v1/users/avatar-headshot?userIds=`     |
| user full-body avatar    | `GET /apisite/thumbnails/v1/users/avatar?userIds=`              |
| item details             | `POST /apisite/catalog/v1/catalog/items/details`                |
| item search              | `GET /apisite/catalog/v1/search/items?keyword=`                 |
| item thumbnails          | `GET /apisite/thumbnails/v1/assets?assetIds=`                   |
| game search              | `GET /apisite/games/v1/games/list?keyword=`                     |
| game details             | `GET /apisite/games/v1/games?universeIds=`                      |
| game icons               | `GET /apisite/thumbnails/v1/games/icons?universeIds=`           |
| game votes               | `GET /apisite/games/v1/games/votes?universeIds=`                |
| place → universe         | `GET /apisite/games/v1/games/multiget-place-details?placeIds=`  |

The client in `madxka.py` mirrors the controller/DTO shapes from
`Roblox.Website/Controllers/v1/{Users,Catalog,Games,Thumbnails}.cs` and
`Roblox.Dto/*` in the bubbablox-v2 repo. MadXka-specific quirks handled:
thumbnail `imageUrl` values are relative paths (the client prefixes the base
url), and game `genre` is a string on MadXka (`"All"`, `"Adventure"`, …)
versus an int in the upstream source.

## Notes

- There is a small 5s per-user cooldown on every command so the MadXka API
  doesn't get hammered.
- `/item <name>` searches and shows the best hit plus a "More results" list;
  `/item <id>` looks the item up directly. `/game` works the same way and
  accepts universe **or** place IDs.
- If a headshot/avatar thumbnail hasn't rendered yet on MadXka's side, the
  embed is sent without an image instead of failing.
- `test_embeds.py` runs the whole embed pipeline offline against
  MadXka-shaped fixtures: `.venv/bin/python test_embeds.py`.
- `make_preview.py` renders the embeds into `preview.html` (Discord-styled)
  so you can eyeball the design without a token.
