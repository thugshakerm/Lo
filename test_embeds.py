"""Offline smoke test: builds all four embeds against a mocked MadxkaClient.

Run:  .venv/bin/python test_embeds.py
"""
import asyncio
import os
import sys

os.environ["DISCORD_TOKEN"] = "dummy-token-for-offline-test"

import bot  # noqa: E402
import madxka  # noqa: E402


class FakeClient:
    """Mimics MadxkaClient methods with bubbablox-v2-shaped payloads.

    Image urls are relative on purpose — like madxka.com returns them
    (the real MadxkaClient prefixes BASE_URL; here we pre-resolve to
    https://madxka.com to mimic that behavior).
    """
    BASE = "https://madxka.com"

    user_by_name = {
        "Builderman": {"id": 1, "name": "Builderman", "requestedName": "Builderman", "displayName": "Builderman"},
        "coolplayer": {"id": 123456, "name": "coolplayer", "requestedName": "coolplayer", "displayName": "coolplayer"},
        "staffy": {"id": 777, "name": "staffy", "requestedName": "staffy", "displayName": "staffy"},
    }
    user_info = {
        1: {
            "id": 1, "name": "Builderman", "displayName": "Builderman",
            "description": "Roblox creator. I like building things. 🧱",
            "created": "2006-11-10T18:16:41.789Z",
            "totalrap": 420, "isBanned": False, "isVerified": True,
        },
        123456: {
            "id": 123456, "name": "coolplayer", "displayName": "coolplayer",
            "description": "", "created": "2021-05-01T12:34:56.7890000Z",
            "totalrap": 0, "isBanned": False, "isVerified": False,
        },
        777: {
            "id": 777, "name": "staffy", "displayName": "staffy",
            "description": "madxka admin", "created": "2025-11-16T16:55:38.488117Z",
            "totalrap": 10, "isBanned": False, "isVerified": True, "isStaff": True,
        },
    }
    headshots = {
        1: BASE + "/images/thumbnails/aaa_headshot.png",
        123456: BASE + "/images/thumbnails/bbb_headshot.png",
        777: BASE + "/images/thumbnails/ccc_headshot.png",
    }
    full_avatars = {
        1: BASE + "/images/thumbnails/aaa_thumbnail.png",
        123456: BASE + "/images/thumbnails/bbb_thumbnail.png",
        777: BASE + "/images/thumbnails/ccc_thumbnail.png",
    }

    item_details = [
        {
            "id": 999, "assetType": 8, "name": "Doge Hat", "description": "a classic doge hat. very hat.",
            "genres": [7], "creatorType": 0, "creatorTargetId": 1, "creatorName": "Builderman",
            "offsaleDeadline": None, "isForSale": True, "price": 500, "priceTickets": None,
            "itemRestrictions": ["Limited"], "saleCount": 12345, "itemType": "Asset",
            "favoriteCount": 67890, "lowestPrice": 350,
            "lowestSellerData": {"userId": 42, "username": "seller", "userAssetId": 1, "price": 350, "assetId": 999},
            "unitsAvailableForConsumption": 812, "serialCount": 9999,
            "is18Plus": False, "moderationStatus": 1,
            "createdAt": "2019-07-04T09:00:00Z", "updatedAt": "2023-01-15T08:30:00Z",
        }
    ]
    item_search = [{"itemType": "Asset", "id": 999}, {"itemType": "Asset", "id": 1000}, {"itemType": "Asset", "id": 1001}]
    item_thumbs = {999: "https://cdn.example.com/items/999.png"}
    more_items = [
        {"id": 1000, "name": "Doge Hat 2000", "assetType": 8, "creatorName": "x"},
        {"id": 1001, "name": "Super Doge", "assetType": 8, "creatorName": "y"},
    ]

    games_list = [
        {
            "universeId": 555, "name": "Obby Paradise", "placeId": 777,
            "gameDescription": "Jump and die.", "playerCount": 1234, "visitCount": 9876543,
            "creatorId": 1, "creatorType": 0, "creatorName": "Builderman",
            "genre": "Adventure", "year": 2018, "totalUpVotes": 5000, "totalDownVotes": 300,
            "price": None,
        }
    ]
    game_info = [
        {
            "id": 555, "rootPlaceId": 777, "name": "Obby Paradise",
            "description": "Jump and die. 500 stages of pure chaos.",
            "genre": "Adventure", "year": 2018, "favoritedCount": 45000,
            "isFavoritedByUser": False,
            "created": "2020-02-29T00:00:00Z", "updated": "2026-08-01T10:00:00Z",
            "maxPlayers": 100, "visits": 9876543, "createVipServersAllowed": True,
            "price": None,
            "creator": {"id": 1, "name": "Builderman", "type": "User", "isRNVAccount": False},
        }
    ]
    game_icons = {555: "https://cdn.example.com/games/555.png"}
    game_votes = {555: {"id": 555, "upVotes": 5000, "downVotes": 300}}
    place_details = [{"placeId": 777, "universeId": 555, "name": "Obby Paradise"}]

    # --- fake MadxkaClient interface -------------------------------------
    async def get_user_by_username(self, username):
        return self.user_by_name.get(username)

    async def get_user(self, user_id):
        return self.user_info.get(user_id)

    async def get_user_status_text(self, user_id):
        return "in game" if user_id == 1 else None

    async def get_user_headshots(self, user_ids):
        return {i: self.headshots[i] for i in user_ids if i in self.headshots}

    async def get_user_full_avatars(self, user_ids):
        return {i: self.full_avatars[i] for i in user_ids if i in self.full_avatars}

    async def get_item_details(self, asset_ids):
        out = []
        for i in asset_ids:
            for it in self.item_details + self.more_items:
                if it["id"] == i:
                    out.append(it)
        return out

    async def search_items(self, keyword, limit=10):
        return self.item_search[:limit]

    async def get_item_thumbnails(self, asset_ids):
        return {i: self.item_thumbs[i] for i in asset_ids if i in self.item_thumbs}

    async def search_games(self, keyword, max_rows=10):
        return self.games_list[:max_rows]

    async def get_games(self, universe_ids):
        return [g for g in self.game_info if g["id"] in universe_ids]

    async def get_game_icons(self, universe_ids):
        return {i: self.game_icons[i] for i in universe_ids if i in self.game_icons}

    async def get_game_votes(self, universe_ids):
        return {i: self.game_votes[i] for i in universe_ids if i in self.game_votes}

    async def get_place_details(self, place_ids):
        return [p for p in self.place_details if p["placeId"] in place_ids]


def dump(label, embed):
    print(f"\n{'=' * 70}\n{label}\ntitle: {embed.title!r}")
    print(f"url:   {embed.url}")
    print(f"thumb: {embed.thumbnail.url if embed.thumbnail else None}")
    print(f"image: {embed.image.url if embed.image else None}")
    for f in embed.fields:
        print(f"  [{f.name}] {f.value} (inline={f.inline})")
    print(f"footer: {embed.footer.text}")
    # sanity: discord field limits
    assert len(embed.title) <= 256
    for f in embed.fields:
        assert len(f.name) <= 256, f.name
        assert len(f.value) <= 1024, (f.name, len(f.value))
    return embed


def author_icon(embed):
    return embed.author.icon_url if embed.author and embed.author.icon_url else None


async def main():
    client = FakeClient()
    e, f = await bot.build_user_embed(client, "Builderman")
    dump("USER (username, verified -> author icon)", e)
    e, f = await bot.build_user_embed(client, "123456")
    dump("USER (by id, no badges)", e)
    e, f = await bot.build_user_embed(client, "staffy")
    dump("USER (verified + staff -> two separate attachments)", e)
    e, f = await bot.build_item_embed(client, "999")
    dump("ITEM (by id)", e)
    e, f = await bot.build_item_embed(client, "doge hat")
    dump("ITEM (keyword)", e)
    e, f = await bot.build_game_embed(client, "obby")
    dump("GAME (keyword)", e)
    e, f = await bot.build_game_embed(client, "555")
    dump("GAME (universe id)", e)
    e, f = await bot.build_game_embed(client, "777")
    dump("GAME (place id)", e)
    e, f = await bot.build_avatar_embed(client, "coolplayer")
    dump("AVATAR", e)

    # badge assertions
    e, files = await bot.build_user_embed(client, "Builderman")  # verified only
    assert author_icon(e) == bot.VERIFIED_BADGE_URL, author_icon(e)
    assert files == []
    e, files = await bot.build_user_embed(client, "123456")  # none
    assert author_icon(e) is None and files == []
    e, files = await bot.build_user_embed(client, "staffy")  # both
    assert author_icon(e) is None
    assert len(files) == 2, files  # the two originals, side by side in Discord
    assert files[0].filename == "verified.png" and files[1].filename == "admin.png"
    e, files = await bot.build_avatar_embed(client, "staffy")  # both (avatar cmd)
    assert author_icon(e) is None and len(files) == 2
    # staff-only (no verified)
    assert bot.badge_flags({"isStaff": True}) == (False, True)
    assert bot.badge_flags({"staff": True}) == (False, True)
    assert bot.badge_flags({"isAdmin": True}) == (False, True)
    assert bot.badge_flags({"isModerator": True}) == (False, True)
    assert bot.badge_flags({"isVerified": True}) == (True, False)
    assert bot.badge_flags({}) == (False, False)
    # badge files must exist (they are what gets attached)
    assert bot.VERIFIED_BADGE_FILE.exists() and bot.ADMIN_BADGE_FILE.exists()
    print("\nbadge assertions OK")

    # genre handling: madxka returns strings, upstream bubba returns ints
    assert madxka.genre_name("Adventure") == "Adventure"
    assert madxka.genre_name(7) == "Adventure"
    assert madxka.genre_name(None) == "Unknown"
    print("genre_name OK")

    # relative thumbnail urls get the base url prefixed (madxka live behavior)
    from madxka import MadxkaClient
    mc = MadxkaClient("https://proxy.onrender.com")
    out = mc._thumbnail_map({"data": [{"targetId": 1, "imageUrl": "/images/thumbnails/x_headshot.png"},
                                      {"targetId": 2, "imageUrl": "https://cdn.example.com/abs.png"}]})
    assert out[1] == "https://proxy.onrender.com/images/thumbnails/x_headshot.png"
    assert out[2] == "https://cdn.example.com/abs.png"
    print("relative thumbnail urls OK")

    # proxy badge store: serves bundled originals + png renders
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), "proxy"))
    from proxy import BadgeStore  # noqa: E402
    store = BadgeStore(None)  # session unused for the bundled-fallback paths
    data, ctype = await store.get("verified", "png")
    assert ctype == "image/png" and data[:8] == b"\x89PNG\r\n\x1a\n"
    data, ctype = await store.get("admin", "svg")
    assert ctype == "image/svg+xml" and b"#E2231A" in data  # original shield
    data, ctype = await store.get("verified", "svg")
    assert ctype == "image/svg+xml" and b"#0066FF" in data  # original check
    assert await store.get("nope", "png") is None
    assert await store.get("verified", "gif") is None
    print("proxy BadgeStore OK")

    # not-found paths
    assert (await bot.build_user_embed(client, "nosuchuser")) is None
    assert (await bot.build_item_embed(client, "424242")) is None
    assert (await bot.build_game_embed(client, "99999999")) is None
    assert (await bot.build_avatar_embed(client, "ghost")) is None
    print("not-found paths OK")

    # parse_datetime sanity
    assert madxka.parse_datetime("2021-05-01T12:34:56.7890000Z") is not None
    assert madxka.parse_datetime("bogus") is None
    print("parse_datetime OK")
    print("\nALL EMBED TESTS PASSED")


if __name__ == "__main__":
    asyncio.run(main())
