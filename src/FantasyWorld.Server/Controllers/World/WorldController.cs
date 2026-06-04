// FantasyWorld.Server/Controllers/World/WorldController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FantasyWorld.Server.Controllers.Economy;
using FantasyWorld.Server.Services.World;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Controllers.World;

// ─── Pet Controller ──────────────────────────────────────────

[ApiController, Route("api/pets"), Authorize]
public class PetController(IPetService petSvc) : GameControllerBase
{
    [HttpGet("wild/{mapId}")]
    public async Task<IActionResult> GetWild(int mapId) =>
        Ok(new { success = true, data = await petSvc.GetWildPetsOnMapAsync(mapId) });

    [HttpGet("my")]
    public async Task<IActionResult> GetMy() =>
        Ok(new { success = true, data = await petSvc.GetMyPetsAsync(CharId) });

    [HttpPost("catch")]
    public async Task<IActionResult> Catch([FromBody] CatchPetRequest req)
    {
        var result = await petSvc.TryCatchAsync(CharId, req, Lang);
        return Ok(new { success = result.Success, data = result });
    }

    [HttpPatch("{petId}/active")]
    public async Task<IActionResult> SetActive(long petId)
    {
        var (ok, msg) = await petSvc.SetActiveAsync(CharId, petId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPatch("nickname")]
    public async Task<IActionResult> Nickname([FromBody] PetNicknameRequest req)
    {
        var (ok, msg) = await petSvc.NicknameAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("{petId}")]
    public async Task<IActionResult> Release(long petId)
    {
        var (ok, msg) = await petSvc.ReleaseAsync(CharId, petId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{petId}/ranch/{ranchId}")]
    public async Task<IActionResult> SendToRanch(long petId, long ranchId)
    {
        var (ok, msg) = await petSvc.SendToRanchAsync(CharId, petId, ranchId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{petId}/feed")]
    public async Task<IActionResult> Feed(long petId, [FromBody] FeedPetRequest req)
    {
        var (ok, msg) = await petSvc.FeedPetAsync(CharId, petId, req.FoodItemId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }
}

// ─── Fishing Controller ──────────────────────────────────────

[ApiController, Route("api/fishing"), Authorize]
public class FishingController(IFishingService fishingSvc) : GameControllerBase
{
    [HttpPost("cast/{mapId}")]
    public async Task<IActionResult> Cast(int mapId)
    {
        var result = await fishingSvc.CastAsync(CharId, mapId, Lang);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("record")]
    public async Task<IActionResult> GetRecord() =>
        Ok(new { success = true, data = await fishingSvc.GetRecordAsync(CharId) });

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int count = 20) =>
        Ok(new { success = true, data = await fishingSvc.GetRecentLogsAsync(CharId, count) });
}

// ─── Farm Controller ─────────────────────────────────────────

[ApiController, Route("api/farm"), Authorize]
public class FarmController(IFarmService farmSvc) : GameControllerBase
{
    [HttpGet("plots")]
    public async Task<IActionResult> GetPlots() =>
        Ok(new { success = true, data = await farmSvc.GetPlotsAsync(CharId) });

    [HttpPost("plots")]
    public async Task<IActionResult> CreatePlot([FromBody] CreatePlotRequest req)
    {
        var (ok, msg) = await farmSvc.CreatePlotAsync(CharId, req.MapId, req.X, req.Y, Lang);
        return ok ? StatusCode(201, new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("plant")]
    public async Task<IActionResult> Plant([FromBody] PlantRequest req)
    {
        var (ok, msg) = await farmSvc.PlantAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("plots/{plotId}/water")]
    public async Task<IActionResult> Water(long plotId)
    {
        var (ok, msg) = await farmSvc.WaterAsync(CharId, plotId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("plots/{plotId}/fertilize")]
    public async Task<IActionResult> Fertilize(long plotId, [FromBody] FertilizeRequest req)
    {
        var (ok, msg) = await farmSvc.FertilizeAsync(CharId, plotId, req.ItemId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("plots/{plotId}/harvest")]
    public async Task<IActionResult> Harvest(long plotId)
    {
        var (ok, msg, result) = await farmSvc.HarvestAsync(CharId, plotId, Lang);
        return ok ? Ok(new { success = true, data = result })
                  : BadRequest(new { success = false, message = msg });
    }
}

// ─── World Event Controller ──────────────────────────────────

[ApiController, Route("api/events")]
public class WorldEventController(IWorldEventService eventSvc) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetActive() =>
        Ok(new { success = true, data = await eventSvc.GetActiveAsync() });

    [HttpGet("{instanceId}")]
    public async Task<IActionResult> GetById(long instanceId)
    {
        var e = await eventSvc.GetByInstanceAsync(instanceId);
        return e is null ? NotFound() : Ok(new { success = true, data = e });
    }

    [HttpPost("{instanceId}/join"), Authorize]
    public async Task<IActionResult> Join(long instanceId)
    {
        var (ok, msg) = await eventSvc.JoinAsync(CharId, instanceId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("contribute"), Authorize]
    public async Task<IActionResult> Contribute([FromBody] ContributeEventRequest req)
    {
        var (ok, msg) = await eventSvc.ContributeAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{instanceId}/claim"), Authorize]
    public async Task<IActionResult> Claim(long instanceId)
    {
        var (ok, msg) = await eventSvc.ClaimRewardAsync(CharId, instanceId, Lang);
        return ok ? Ok(new { success = true }) : BadRequest(new { success = false, message = msg });
    }

    // GM only
    [HttpPost("trigger/{eventId}"), Authorize]
    public async Task<IActionResult> TriggerManual(int eventId)
    {
        if (!IsGm) return Forbid();
        var result = await eventSvc.TriggerManualAsync(eventId, CharId);
        return result is null ? NotFound() : Ok(new { success = true, data = result });
    }
}

// ─── NPC Controller ──────────────────────────────────────────

[ApiController, Route("api/npcs")]
public class NpcController(INpcAiService npcSvc) : GameControllerBase
{
    [HttpGet("map/{mapId}"), Authorize]
    public async Task<IActionResult> GetOnMap(int mapId) =>
        Ok(new { success = true, data = await npcSvc.GetNpcsOnMapAsync(mapId, CharId) });

    [HttpPost("interact"), Authorize]
    public async Task<IActionResult> Interact([FromBody] NpcInteractRequest req)
    {
        var (ok, msg) = await npcSvc.InteractAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true, dialog = msg })
                  : BadRequest(new { success = false, message = msg });
    }
}

// ─── Extended base controller ────────────────────────────────
public abstract class GameControllerBase : ControllerBase
{
    protected long   CharId => long.Parse(User.FindFirst("charId")?.Value ?? "0");
    protected string Lang   => Request.Headers["Accept-Language"]
        .FirstOrDefault()?.Split(',')[0].Split('-')[0] ?? "vi";
    protected bool   IsGm   => User.FindFirst("role")?.Value is "gm" or "admin";
}

// ─── Request records ─────────────────────────────────────────
public record FeedPetRequest(int FoodItemId);
public record CreatePlotRequest(int MapId, float X, float Y);
public record FertilizeRequest(int ItemId);
