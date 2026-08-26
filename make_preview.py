"""Render all four embeds (via real embed.to_dict()) into a Discord-styled
HTML page: preview.html. Lets you eyeball the design without a bot token.

Run:  .venv/bin/python make_preview.py
"""
import asyncio
import html
import os

os.environ["DISCORD_TOKEN"] = "dummy-token-for-offline-test"

import bot  # noqa: E402
from test_embeds import FakeClient  # noqa: E402

COLOR = {
    "user": "#2B7FFF",
    "item": "#2B7FFF",
    "game": "#2B7FFF",
    "avatar": "#2B7FFF",
}


def esc(s):
    return html.escape(str(s or ""))


def local_badge_path(url):
    """Map the proxy badge url to the bundled svg file for offline preview."""
    if url and "/badges/" in url:
        name = url.rsplit("/", 1)[-1]
        p = os.path.join(os.path.dirname(__file__), "proxy", "badges", name)
        return os.path.relpath(p, os.path.dirname(__file__)) if os.path.exists(p) else None
    return None


def render_embed(d, color):
    fields = "".join(
        f"""
        <div class="field {'inline' if f.get('inline') else ''}">
          <div class="f-name">{esc(f['name'])}</div>
          <div class="f-value">{esc(f['value']).replace(chr(10), '<br>')}</div>
        </div>"""
        for f in d.get("fields", [])
    )
    thumb = d.get("thumbnail")
    image = d.get("image")
    thumb_html = (
        f'<img class="thumb" src="{esc(thumb["url"])}" alt="thumb">' if thumb else ""
    )
    image_html = f'<img class="big" src="{esc(image["url"])}" alt="image">' if image else ""
    title = d.get("title") or ""
    title_link = (
        f'<a href="{esc(d["url"])}" target="_blank">{esc(title)}</a>'
        if d.get("url")
        else esc(title)
    )
    author = d.get("author") or {}
    author_html = ""
    if author.get("name"):
        icon_url = author.get("icon_url")
        src = local_badge_path(icon_url) or icon_url
        icon = f'<img class="badge" src="{esc(src)}" alt="badge">' if src else ""
        author_html = f'<div class="eb-author-row">{icon}<span>{esc(author["name"])}</span></div>'
    return f"""
    <div class="embed" style="border-color:{color}">
      {author_html}
      <div class="eb-title" style="color:{color}">{title_link}</div>
      <div class="eb-body">
        <div class="eb-fields">
          <div class="eb-col">{fields}</div>
        </div>
        {image_html}
        {thumb_html}
      </div>
      <div class="eb-foot">
        <span class="eb-author">MadXka bot</span>
        <span class="eb-time">{esc(d.get("timestamp",""))}</span>
        <span class="eb-footer">{esc((d.get("footer") or {}).get("text"))}</span>
      </div>
    </div>"""


async def main():
    client = FakeClient()
    embeds = [
        ("👤 /user — headshot + verified badge", await bot.build_user_embed(client, "Builderman")),
        ("👤 /user — verified + staff: two separate badges, side by side", await bot.build_user_embed(client, "staffy")),
        ("📦 /item — thumbnail", await bot.build_item_embed(client, "doge hat")),
        ("🎮 /game — icon", await bot.build_game_embed(client, "obby")),
        ("🖼️ /avatar — full body", await bot.build_avatar_embed(client, "coolplayer")),
    ]
    cards = []
    for label, (e, files) in embeds:
        if e is None:
            cards.append(f"<div class='missing'>{esc(label)}: none</div>")
            continue
        card = (
            f"<div class='card'><div class='card-label'>{esc(label)}</div>"
            + render_embed(e.to_dict(), COLOR["user"])
        )
        if files:
            imgs = "".join(
                f"<img class='attach' src='{os.path.relpath(str(f.fp.name), os.path.dirname(__file__))}' alt='badge'>"
                for f in files
            )
            card += f"<div class='attach-row'>{imgs}</div>"
        card += "</div>"
        cards.append(card)

    doc = f"""<!doctype html>
<html><head><meta charset="utf-8"><title>MadXka bot — embed preview</title>
<style>
  body {{ background:#1e1f22; color:#dbdee1; font-family:gg sans, 'Segoe UI', sans-serif; margin:0; padding:24px; }}
  h1 {{ font-size:18px; }}
  .card {{ margin-bottom:28px; }}
  .card-label {{ font-size:13px; color:#949ba4; margin:0 0 8px 2px; font-weight:600; }}
  .embed {{ background:#2b2d31; border-left:4px solid; border-radius:4px; max-width:520px; padding:10px 14px 8px; }}
  .eb-author-row {{ display:flex; align-items:center; gap:6px; margin-bottom:6px; font-size:14px; font-weight:700; color:#dbdee1; }}
  .badge {{ height:18px; width:auto; display:block; }}
  .attach-row {{ display:flex; gap:10px; margin:10px 2px 0; }}
  .attach {{ height:90px; width:90px; object-fit:contain; }}
  .eb-title {{ font-weight:700; font-size:15px; margin-bottom:8px; word-break:break-word; }}
  .eb-title a {{ color:inherit; text-decoration:none; }}
  .eb-body {{ display:flex; gap:12px; }}
  .eb-fields {{ flex:1; min-width:0; }}
  .field {{ width:33.33%; display:inline-block; vertical-align:top; margin-right:-4px; box-sizing:border-box; padding:0 4px; }}
  .field:not(.inline) {{ width:100%; }}
  .f-name {{ font-size:12px; font-weight:700; margin-bottom:2px; color:#dbdee1; }}
  .f-value {{ font-size:13px; color:#dbdee1; word-break:break-word; white-space:pre-wrap; }}
  .thumb {{ width:72px; height:72px; object-fit:cover; border-radius:8px; flex-shrink:0; }}
  .big {{ width:160px; object-fit:cover; border-radius:8px; flex-shrink:0; max-height:220px; }}
  .eb-foot {{ margin-top:10px; font-size:11px; color:#949ba4; display:flex; gap:8px; align-items:center; }}
  .eb-author {{ font-weight:700; color:#dbdee1; }}
  .missing {{ color:#f23f43; }}
</style></head>
<body>
  <h1>MadXka Discord bot — embed preview (built from the real <code>embed.to_dict()</code>)</h1>
  <p style="color:#949ba4; font-size:13px">Mock data. Real thumbnails won't load in this offline preview, but layout/fields are exactly what Discord renders.</p>
  {''.join(cards)}
</body></html>"""
    out = os.path.join(os.path.dirname(__file__), "preview.html")
    with open(out, "w") as f:
        f.write(doc)
    print("wrote", out)


if __name__ == "__main__":
    asyncio.run(main())
