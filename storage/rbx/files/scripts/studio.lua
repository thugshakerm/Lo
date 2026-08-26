-- ─────────────────────────────────────────────────────────────────────
-- 2018M Studio.lua — Lo Revival
-- ─────────────────────────────────────────────────────────────────────
--
-- This is the Lua body for Studio's Test mode (Play Solo). It's
-- almost identical to gameserver.lua but:
--   - No game server registration / heartbeat (there's no
--     persistent server process).
--   - The "creator" is the local Studio user.
--   - The personal server starter script is always added.
--
-- Served at /Game/Studio.ashx by GameController::studio.
-- ─────────────────────────────────────────────────────────────────────

local placeId, port, domain, creatorId = ...

local apex       = domain or "gazeee.xyz"
local baseurl    = "http://www."       .. apex
local assetgame  = "http://assetgame." .. apex
local api        = "http://api."       .. apex

pcall(function() settings().Network.UseInstancePacketCache = true end)
pcall(function() settings().Network.UsePhysicsPacketCache  = true end)
pcall(function() settings()["Task Scheduler"].PriorityMethod = Enum.PriorityMethod.AccumulatedError end)

local scriptContext = game:GetService("ScriptContext")
scriptContext.ScriptsDisabled = true
scriptContext:SetTimeout(10)

game:SetPlaceID(placeId, false)
pcall(function() game:SetUniverseId(0) end)
game:SetCreatorID(creatorId, Enum.CreatorType.User)
game:GetService("ChangeHistoryService"):SetEnabled(false)

if baseurl ~= nil then
    pcall(function() game:GetService("ScriptInformationProvider"):SetAssetUrl(baseurl .. "/Asset/") end)
    pcall(function() game:GetService("ContentProvider"):SetBaseUrl(baseurl .. "/") end)
    pcall(function() game:GetService("ContentProvider"):SetThreadPool(16) end)
    pcall(function() game:GetService("BadgeService"):SetPlaceId(placeId) end)
    pcall(function() game:GetService("SocialService"):SetFriendUrl(assetgame .. "/Game/LuaWebService/HandleSocialRequest.ashx?method=IsFriendsWith&playerid=%d&userid=%d") end)
    pcall(function() game:GetService("GamePassService"):SetPlayerHasPassUrl(assetgame .. "/Game/GamePass/GamePassHandler.ashx?Action=HasPass&UserID=%d&PassID=%d") end)
    pcall(function() game:GetService("InsertService"):SetAssetUrl(baseurl .. "/Asset/?id=%d") end)
    pcall(function() game:GetService("MarketplaceService"):SetProductInfoUrl(api .. "/marketplace/productinfo?assetId=%d") end)
    pcall(function() game:GetService("MarketplaceService"):SetPlayerOwnsAssetUrl(api .. "/ownership/hasasset?userId=%d&assetId=%d") end)
end

pcall(function()
    local ns = game:GetService("NetworkServer")
    ns:Start(port)
end)

if placeId ~= nil and baseurl ~= nil then
    game:Load(assetgame .. "/asset/?id=" .. tostring(placeId))
end

pcall(function()
    game:GetService("ScriptContext"):AddStarterScript(124885177)
end)

scriptContext.ScriptsDisabled = false
game:GetService("RunService"):Run()
