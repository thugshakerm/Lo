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
browser User-Agent and serves the badge svgs. The bot talks only to the proxy,
so Cloudflare in front of MadXka never sees the Discord bot.

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
`https://<name>.onrender.com/health` and `.../badges/verified.svg`.

## 2. Set up the bot

1. Create a bot at <https://discord.com/developers>, copy the token.
   No privileged gateway intents are needed (slash commands only).
2. Install and configure:

   ```bash
   python3 -m venv .venv
   .venv/bin/pip install -r requirements.txt
   cp .env.example .env   # set DISCORD_TOKEN and MADXKA_BASE_URL to your proxy url
   ```

3. Invite the bot with the `bot` + `applications.commands` scopes and the
   **Send Messages / Embed Links / Attach Files** permissions.
4. Run:

   ```bash
   .venv/bin/python bot.py
   ```

   Global command sync can take a few minutes to show up. For instant
   (re)sync during development, set `DISCORD_GUILD_ID` in `.env` to your
   server ID.

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
