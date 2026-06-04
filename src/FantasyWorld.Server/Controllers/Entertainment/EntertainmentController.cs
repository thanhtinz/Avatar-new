// FantasyWorld.Server/Controllers/Entertainment/EntertainmentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FantasyWorld.Server.Controllers.Economy;
using FantasyWorld.Server.Services.Content;
using FantasyWorld.Server.Services.Entertainment;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Controllers.Entertainment;

// ─── Casino ──────────────────────────────────────────────────

[ApiController, Route("api/casino"), Authorize]
public class CasinoController(ICasinoService casinoSvc) : GameControllerBase
{
    [HttpGet("games")]
    public async Task<IActionResult> GetGames() =>
        Ok(new { success = true, data = await casinoSvc.GetGamesAsync() });

    [HttpPost("play")]
    public async Task<IActionResult> Play([FromBody] CasinoBetRequest req)
    {
        try
        {
            var result = await casinoSvc.PlayAsync(CharId, req, Lang);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int count = 20) =>
        Ok(new { success = true, data = await casinoSvc.GetHistoryAsync(CharId, count) });

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var (won, lost, net) = await casinoSvc.GetStatsAsync(CharId);
        return Ok(new { success = true, data = new { won, lost, net } });
    }
}

// ─── Mini Game ───────────────────────────────────────────────

[ApiController, Route("api/minigames"), Authorize]
public class MiniGameController(IMiniGameService mgSvc) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await mgSvc.GetAllAsync(CharId) });

    [HttpPost("{gameId}/start")]
    public async Task<IActionResult> Start(int gameId)
    {
        var (ok, msg) = await mgSvc.StartAsync(CharId, gameId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitScoreRequest req)
    {
        var result = await mgSvc.SubmitScoreAsync(CharId, req, Lang);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{gameId}/leaderboard")]
    public async Task<IActionResult> GetLeaderboard(int gameId,
        [FromQuery] int top = 20) =>
        Ok(new { success = true,
            data = await mgSvc.GetLeaderboardAsync(gameId, top) });
}

// ─── Mount Race ──────────────────────────────────────────────

[ApiController, Route("api/races"), Authorize]
public class MountRaceController(IMountRaceService raceSvc) : GameControllerBase
{
    [HttpGet("map/{mapId}")]
    public async Task<IActionResult> GetAvailable(int mapId) =>
        Ok(new { success = true, data = await raceSvc.GetAvailableAsync(mapId) });

    [HttpPost("create/{trackId}")]
    public async Task<IActionResult> Create(int trackId)
    {
        var (ok, msg, raceId) = await raceSvc.CreateRaceAsync(CharId, trackId, Lang);
        return ok ? StatusCode(201, new { success = true, raceId })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinRaceRequest req)
    {
        var (ok, msg) = await raceSvc.JoinAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{raceId}/leave")]
    public async Task<IActionResult> Leave(long raceId)
    {
        var (ok, msg) = await raceSvc.LeaveAsync(CharId, raceId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("{raceId}/state")]
    public async Task<IActionResult> GetState(long raceId) =>
        Ok(new { success = true,
            data = await raceSvc.GetRaceStateAsync(raceId) });

    [HttpGet("{raceId}/results")]
    public async Task<IActionResult> GetResults(long raceId) =>
        Ok(new { success = true,
            data = await raceSvc.GetResultsAsync(raceId) });
}

// ─── Fashion ─────────────────────────────────────────────────

[ApiController, Route("api/fashion"), Authorize]
public class FashionController(IFashionService fashionSvc) : GameControllerBase
{
    [HttpGet("shop")]
    public async Task<IActionResult> GetShop([FromQuery] string? slot) =>
        Ok(new { success = true,
            data = await fashionSvc.GetShopItemsAsync(CharId, slot) });

    [HttpPost("shop/{itemId}/buy")]
    public async Task<IActionResult> Buy(int itemId,
        [FromQuery] bool diamond = false)
    {
        var (ok, msg) = await fashionSvc.BuyItemAsync(CharId, itemId, diamond, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("wardrobe")]
    public async Task<IActionResult> GetWardrobe() =>
        Ok(new { success = true,
            data = await fashionSvc.GetWardrobeAsync(CharId) });

    [HttpGet("outfit")]
    public async Task<IActionResult> GetOutfit() =>
        Ok(new { success = true,
            data = await fashionSvc.GetOutfitAsync(CharId) });

    [HttpPost("wear")]
    public async Task<IActionResult> Wear([FromBody] WearRequest req)
    {
        var (ok, msg) = await fashionSvc.WearAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("presets/save")]
    public async Task<IActionResult> SavePreset([FromBody] SavePresetRequest req)
    {
        var (ok, msg) = await fashionSvc.SavePresetAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("presets/{slot}/load")]
    public async Task<IActionResult> LoadPreset(int slot)
    {
        var (ok, msg) = await fashionSvc.LoadPresetAsync(CharId, slot, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("presets")]
    public async Task<IActionResult> GetPresets() =>
        Ok(new { success = true,
            data = await fashionSvc.GetPresetsAsync(CharId) });

    [HttpPost("designs/submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitDesignRequest req)
    {
        var (ok, msg, id) = await fashionSvc.SubmitDesignAsync(CharId, req, Lang);
        return ok ? StatusCode(201, new { success = true, designId = id })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("designs/{designId}/review"), Authorize]
    public async Task<IActionResult> Review(long designId, [FromBody] ReviewRequest req)
    {
        if (!IsGm) return Forbid();
        var (ok, msg) = await fashionSvc.ReviewDesignAsync(
            CharId, designId, req.Approve, req.Note, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("designs")]
    public async Task<IActionResult> GetDesigns(
        [FromQuery] string? slot = null,
        [FromQuery] int page = 1) =>
        Ok(new { success = true,
            data = await fashionSvc.GetApprovedDesignsAsync(slot, page) });

    [HttpPost("designs/{designId}/buy")]
    public async Task<IActionResult> BuyDesign(long designId)
    {
        var (ok, msg) = await fashionSvc.BuyDesignAsync(CharId, designId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }
}

// ─── Performance ─────────────────────────────────────────────

[ApiController, Route("api/performances"), Authorize]
public class PerformanceController(IPerformanceService perfSvc) : GameControllerBase
{
    [HttpGet("stages/{mapId}")]
    public async Task<IActionResult> GetStages(int mapId) =>
        Ok(new { success = true,
            data = await perfSvc.GetStagesAsync(mapId) });

    [HttpGet("live/{mapId}")]
    public async Task<IActionResult> GetLive(int mapId) =>
        Ok(new { success = true,
            data = await perfSvc.GetLiveAsync(mapId) });

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartPerformanceRequest req)
    {
        var (ok, msg, id) = await perfSvc.StartAsync(CharId, req, Lang);
        return ok ? StatusCode(201, new { success = true, performanceId = id })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{performanceId}/end")]
    public async Task<IActionResult> End(long performanceId)
    {
        var (ok, msg) = await perfSvc.EndAsync(CharId, performanceId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("tip")]
    public async Task<IActionResult> Tip([FromBody] SendTipRequest req)
    {
        var (ok, msg) = await perfSvc.SendTipAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{performanceId}/join-group")]
    public async Task<IActionResult> JoinGroup(long performanceId,
        [FromBody] JoinGroupRequest req)
    {
        var (ok, msg) = await perfSvc.JoinGroupAsync(CharId, performanceId, req.Role, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("{performanceId}")]
    public async Task<IActionResult> GetById(long performanceId)
    {
        var p = await perfSvc.GetByIdAsync(performanceId);
        return p is null ? NotFound() : Ok(new { success = true, data = p });
    }
}

// ─── Photo ───────────────────────────────────────────────────

[ApiController, Route("api/photos"), Authorize]
public class PhotoController(IPhotoService photoSvc) : GameControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Take([FromBody] TakePhotoRequest req)
    {
        var (ok, msg, id) = await photoSvc.TakePhotoAsync(CharId, req, Lang);
        return ok ? StatusCode(201, new { success = true, photoId = id })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy([FromQuery] int page = 1) =>
        Ok(new { success = true,
            data = await photoSvc.GetMyPhotosAsync(CharId, page) });

    [HttpGet("map/{mapId}")]
    public async Task<IActionResult> GetPublic(int mapId,
        [FromQuery] int page = 1) =>
        Ok(new { success = true,
            data = await photoSvc.GetPublicPhotosAsync(mapId, page) });

    [HttpPost("{photoId}/like")]
    public async Task<IActionResult> Like(long photoId)
    {
        var (ok, msg) = await photoSvc.LikeAsync(CharId, photoId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("{photoId}")]
    public async Task<IActionResult> Delete(long photoId)
    {
        var (ok, msg) = await photoSvc.DeleteAsync(CharId, photoId, Lang);
        return ok ? Ok(new { success = true })
                  : NotFound(new { success = false, message = msg });
    }

    [HttpGet("frames")]
    public async Task<IActionResult> GetFrames() =>
        Ok(new { success = true, data = await photoSvc.GetFramesAsync() });

    [HttpPost("albums")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumRequest req)
    {
        var (ok, msg, id) = await photoSvc.CreateAlbumAsync(CharId, req.Name, Lang);
        return ok ? StatusCode(201, new { success = true, albumId = id })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("albums/{albumId}/add/{photoId}")]
    public async Task<IActionResult> AddToAlbum(long albumId, long photoId)
    {
        var (ok, msg) = await photoSvc.AddToAlbumAsync(CharId, albumId, photoId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums() =>
        Ok(new { success = true, data = await photoSvc.GetAlbumsAsync(CharId) });
}

// ─── Request records ─────────────────────────────────────────
public record ReviewRequest(bool Approve, string? Note = null);
public record JoinGroupRequest(string Role);
public record CreateAlbumRequest(string Name);
