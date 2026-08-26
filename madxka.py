"""
Async client for the MadXka public API.

MadXka runs bubbablox-v2 (repo: harryzawg/bubbablox-v2, branch "2021" —
the bubbablox v2 source that MadXka's website is built on).

Every endpoint path and response shape in this module was taken directly
from that codebase:

  Roblox.Website/Controllers/v1/Users.cs       -> /apisite/users/v1/...
  Roblox.Website/Controllers/v1/Catalog.cs     -> /apisite/catalog/v1/...
  Roblox.Website/Controllers/v1/Games.cs       -> /apisite/games/v1/...
  Roblox.Website/Controllers/v1/Thumbnails.cs  -> /apisite/thumbnails/v1/...

  * user lookup by username  POST /apisite/users/v1/usernames/users
  * user lookup by id        GET  /apisite/users/v1/users/{userId}
  * user status text         GET  /apisite/users/v1/users/{userId}/status
  * user headshot            GET  /apisite/thumbnails/v1/users/avatar-headshot?userIds=
  * user full-body avatar    GET  /apisite/thumbnails/v1/users/avatar?userIds=
  * item details             POST /apisite/catalog/v1/catalog/items/details
  * item search              GET  /apisite/catalog/v1/search/items?keyword=
  * item thumbnails          GET  /apisite/thumbnails/v1/assets?assetIds=
  * game search              GET  /apisite/games/v1/games/list?keyword=
  * game details             GET  /apisite/games/v1/games?universeIds=
  * game icons               GET  /apisite/thumbnails/v1/games/icons?universeIds=
  * game votes               GET  /apisite/games/v1/games/votes?universeIds=
  * place -> universe        GET  /apisite/games/v1/games/multiget-place-details?placeIds=
"""

from __future__ import annotations

import os
import re
from datetime import datetime
from typing import Any, Optional, Sequence

import aiohttp

DEFAULT_BASE_URL = "https://madxka.com"

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
)

# Roblox.Models.Assets.Type from bubbablox-v2
ASSET_TYPES: dict[int, str] = {
    1: "Image",
    2: "TeeShirt",
    3: "Audio",
    4: "Mesh",
    5: "Lua",
    8: "Hat",
    9: "Place",
    10: "Model",
    11: "Shirt",
    12: "Pants",
    13: "Decal",
    17: "Head",
    18: "Face",
    19: "Gear",
    21: "Badge",
    24: "Animation",
    27: "Torso",
    28: "RightArm",
    29: "LeftArm",
    30: "LeftLeg",
    31: "RightLeg",
    32: "Package",
    34: "GamePass",
    38: "Plugin",
    39: "SolidModel",
    40: "MeshPart",
    41: "HairAccessory",
    42: "FaceAccessory",
    43: "NeckAccessory",
    44: "ShoulderAccessory",
    45: "FrontAccessory",
    46: "BackAccessory",
    47: "WaistAccessory",
    48: "ClimbAnimation",
    49: "DeathAnimation",
    50: "FallAnimation",
    51: "IdleAnimation",
    52: "JumpAnimation",
    53: "RunAnimation",
    54: "SwimAnimation",
    55: "WalkAnimation",
    56: "PoseAnimation",
    61: "EmoteAnimation",
    500: "Special",
}

# Roblox.Models.Assets.Genre from bubbablox-v2
GENRES: dict[int, str] = {
    0: "All",
    1: "Town & City",
    2: "Medieval",
    3: "Sci-Fi",
    4: "Fighting",
    5: "Horror",
    6: "Naval",
    7: "Adventure",
    8: "Sports",
    9: "Comedy",
    10: "Western",
    11: "Military",
    13: "Building",
    14: "FPS",
    15: "RPG",
    18: "Skatepark",
}


class MadxkaError(Exception):
    """Raised when the MadXka API is unreachable or answers with an error."""


_DATE_RE = re.compile(
    r"^(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2})(?:\.(\d+))?(Z|[+-]\d{2}:?\d{2})?$"
)


def parse_datetime(value: Any) -> Optional[datetime]:
    """Parse a .NET-flavoured ISO timestamp into a datetime, or None."""
    if not value or not isinstance(value, str):
        return None
    text = value.strip()
    try:
        return datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError:
        pass
    match = _DATE_RE.match(text)
    if not match:
        return None
    base, frac, tz = match.groups()
    frac = (frac or "")[:6].ljust(6, "0")
    tz = "+00:00" if tz in (None, "Z") else tz.replace(":", "")
    tz = f"{tz[:3]}:{tz[3:]}" if len(tz) == 5 else tz
    candidate = f"{base}.{frac}{tz}" if frac else f"{base}{tz}"
    try:
        return datetime.fromisoformat(candidate)
    except ValueError:
        return None


class MadxkaClient:
    """Tiny async wrapper around the MadXka (bubbablox v2) public API."""

    def __init__(
        self,
        base_url: str = DEFAULT_BASE_URL,
        session: Optional[aiohttp.ClientSession] = None,
        timeout: float = 15.0,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.session = session
        self.timeout = timeout
        # optional madxka.com browser cookie — forwarded to the proxy, which
        # sends it upstream so the API sees a logged-in browser session
        self.cookie = os.environ.get("MADXKA_COOKIE", "").strip()

    # ------------------------------------------------------------------ core
    async def _request(
        self,
        method: str,
        path: str,
        *,
        params: Optional[dict] = None,
        json_body: Optional[dict] = None,
    ) -> Optional[dict]:
        """GET/POST a JSON API path. 404 -> None, other errors -> MadxkaError."""
        url = f"{self.base_url}{path}"
        try:
            headers = {"User-Agent": USER_AGENT, "Accept": "application/json"}
            if self.cookie:
                headers["x-madxka-cookie"] = self.cookie
            async with self.session.request(
                method,
                url,
                params=params,
                json=json_body,
                headers=headers,
                timeout=aiohttp.ClientTimeout(total=self.timeout),
            ) as resp:
                if resp.status == 404:
                    return None
                if resp.status >= 400:
                    detail = (await resp.text())[:200]
                    raise MadxkaError(f"MadXka API returned {resp.status}: {detail}")
                try:
                    return await resp.json(content_type=None)
                except ValueError as exc:  # JSONDecodeError / ContentTypeError...
                    snippet = (await resp.text(errors="replace"))[:120]
                    raise MadxkaError(
                        f"MadXka API returned non-JSON from {path}: {snippet!r}"
                    ) from exc
        except aiohttp.ClientError as exc:
            raise MadxkaError(f"could not reach {self.base_url} ({exc})") from exc

    @staticmethod
    def _id_csv(ids: Sequence[int]) -> str:
        return ",".join(str(i) for i in ids)

    # ----------------------------------------------------------------- users
    async def get_user_by_username(self, username: str) -> Optional[dict]:
        """POST /apisite/users/v1/usernames/users -> first match or None."""
        data = await self._request(
            "POST",
            "/apisite/users/v1/usernames/users",
            json_body={"usernames": [username]},
        )
        entries = (data or {}).get("data") or []
        return entries[0] if entries else None

    async def get_user(self, user_id: int) -> Optional[dict]:
        """GET /apisite/users/v1/users/{id} -> user info dict (or None).

        Live shape (madxka): {id,name,displayName,description,created,totalrap,
        isBanned,isVerified[,isStaff/staff/isAdmin/isModerator]}
        """
        data = await self._request("GET", f"/apisite/users/v1/users/{user_id}")
        return data if data and data.get("id") else None

    async def get_user_status_text(self, user_id: int) -> Optional[str]:
        """GET /apisite/users/v1/users/{id}/status -> {status} (short text)."""
        data = await self._request("GET", f"/apisite/users/v1/users/{user_id}/status")
        status = (data or {}).get("status")
        return str(status) if status else None

    async def get_user_headshots(self, user_ids: Sequence[int]) -> dict[int, str]:
        """GET /apisite/thumbnails/v1/users/avatar-headshot -> {userId: imageUrl}."""
        data = await self._request(
            "GET",
            "/apisite/thumbnails/v1/users/avatar-headshot",
            params={"userIds": self._id_csv(user_ids)},
        )
        return self._thumbnail_map(data)

    async def get_user_full_avatars(self, user_ids: Sequence[int]) -> dict[int, str]:
        """GET /apisite/thumbnails/v1/users/avatar -> {userId: full body imageUrl}."""
        data = await self._request(
            "GET",
            "/apisite/thumbnails/v1/users/avatar",
            params={"userIds": self._id_csv(user_ids)},
        )
        return self._thumbnail_map(data)

    # ----------------------------------------------------------------- items
    async def get_item_details(self, asset_ids: Sequence[int]) -> list[dict]:
        """POST /apisite/catalog/v1/catalog/items/details -> [MultiGetEntry]."""
        data = await self._request(
            "POST",
            "/apisite/catalog/v1/catalog/items/details",
            json_body={"items": [{"id": int(i)} for i in asset_ids]},
        )
        return (data or {}).get("data") or []

    async def search_items(self, keyword: str, limit: int = 10) -> list[dict]:
        """GET /apisite/catalog/v1/search/items -> [{itemType, id, ...}]."""
        data = await self._request(
            "GET",
            "/apisite/catalog/v1/search/items",
            params={"keyword": keyword, "limit": limit},
        )
        return (data or {}).get("data") or []

    async def get_item_thumbnails(self, asset_ids: Sequence[int]) -> dict[int, str]:
        """GET /apisite/thumbnails/v1/assets -> {assetId: imageUrl}."""
        data = await self._request(
            "GET",
            "/apisite/thumbnails/v1/assets",
            params={"assetIds": self._id_csv(asset_ids)},
        )
        return self._thumbnail_map(data)

    # ----------------------------------------------------------------- games
    async def search_games(self, keyword: str, max_rows: int = 10) -> list[dict]:
        """GET /apisite/games/v1/games/list -> {games: [GameListEntry]}."""
        data = await self._request(
            "GET",
            "/apisite/games/v1/games/list",
            params={"keyword": keyword, "maxRows": max_rows},
        )
        return (data or {}).get("games") or []

    async def get_games(self, universe_ids: Sequence[int]) -> list[dict]:
        """GET /apisite/games/v1/games?universeIds= -> [MultiGetUniverseEntry]."""
        data = await self._request(
            "GET",
            "/apisite/games/v1/games",
            params={"universeIds": self._id_csv(universe_ids)},
        )
        return (data or {}).get("data") or []

    async def get_game_icons(self, universe_ids: Sequence[int]) -> dict[int, str]:
        """GET /apisite/thumbnails/v1/games/icons -> {universeId: imageUrl}."""
        data = await self._request(
            "GET",
            "/apisite/thumbnails/v1/games/icons",
            params={"universeIds": self._id_csv(universe_ids)},
        )
        return self._thumbnail_map(data)

    async def get_game_votes(self, universe_ids: Sequence[int]) -> dict[int, dict]:
        """GET /apisite/games/v1/games/votes -> {universeId: {upVotes, downVotes}}."""
        data = await self._request(
            "GET",
            "/apisite/games/v1/games/votes",
            params={"universeIds": self._id_csv(universe_ids)},
        )
        out: dict[int, dict] = {}
        for entry in (data or {}).get("data") or []:
            if entry.get("id") is not None:
                out[int(entry["id"])] = entry
        return out

    async def get_place_details(self, place_ids: Sequence[int]) -> list[dict]:
        """GET /apisite/games/v1/games/multiget-place-details -> [PlaceEntry]."""
        data = await self._request(
            "GET",
            "/apisite/games/v1/games/multiget-place-details",
            params={"placeIds": self._id_csv(place_ids)},
        )
        if data is None:
            return []
        # This endpoint returns a bare list in some builds.
        if isinstance(data, list):
            return data
        return data.get("data") or []

    # ------------------------------------------------------------------ util
    def _thumbnail_map(self, data: Optional[dict]) -> dict[int, str]:
        out: dict[int, str] = {}
        for entry in (data or {}).get("data") or []:
            url = entry.get("imageUrl")
            target = entry.get("targetId")
            if url and target is not None:
                if url.startswith("/"):  # madxka returns relative image paths
                    url = self.base_url + url
                out[int(target)] = url
        return out


def genre_name(value: Any) -> str:
    """Game/item genre comes back as a string on madxka ("All") or an int upstream."""
    if isinstance(value, str):
        return value or "Unknown"
    if isinstance(value, int):
        return GENRES.get(value, "Unknown")
    return "Unknown"
