// FantasyWorld.Server/Controllers/Social/SocialController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FantasyWorld.Server.Services;
using FantasyWorld.Server.Services.Social;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Controllers.Social;

// ─── Friend Controller ───────────────────────────────────────

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendController(
    IFriendService       friendSvc,
    ILocalizationService loc) : ControllerBase
{
    private string Lang   => LangFromHeader();
    private long CharId   => long.Parse(User.FindFirst("charId")?.Value ?? "0");

    [HttpGet]
    public async Task<IActionResult> GetFriends() =>
        Ok(new { success = true, data = await friendSvc.GetFriendsAsync(CharId) });

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests() =>
        Ok(new { success = true, data = await friendSvc.GetPendingRequestsAsync(CharId) });

    [HttpPost("request/{targetId}")]
    public async Task<IActionResult> SendRequest(long targetId)
    {
        var (ok, msg) = await friendSvc.SendRequestAsync(CharId, targetId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("request/{requestId}/accept")]
    public async Task<IActionResult> Accept(long requestId)
    {
        var (ok, msg) = await friendSvc.AcceptRequestAsync(requestId, CharId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("request/{requestId}/decline")]
    public async Task<IActionResult> Decline(long requestId)
    {
        var (ok, msg) = await friendSvc.DeclineRequestAsync(requestId, CharId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : NotFound(new { success = false, message = msg });
    }

    [HttpDelete("{friendId}")]
    public async Task<IActionResult> RemoveFriend(long friendId)
    {
        var (ok, msg) = await friendSvc.RemoveFriendAsync(CharId, friendId, Lang);
        return ok ? Ok(new { success = true })
                  : NotFound(new { success = false, message = msg });
    }

    [HttpPost("block/{targetId}")]
    public async Task<IActionResult> Block(long targetId)
    {
        var (ok, msg) = await friendSvc.BlockCharacterAsync(CharId, targetId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("block/{targetId}")]
    public async Task<IActionResult> Unblock(long targetId)
    {
        var (ok, msg) = await friendSvc.UnblockAsync(CharId, targetId, Lang);
        return ok ? Ok(new { success = true })
                  : NotFound(new { success = false, message = msg });
    }

    private string LangFromHeader() =>
        Request.Headers["Accept-Language"].FirstOrDefault()
            ?.Split(',')[0].Split('-')[0] ?? "vi";
}

// ─── Clan Controller ─────────────────────────────────────────

[ApiController]
[Route("api/clans")]
[Authorize]
public class ClanController(
    IClanService         clanSvc,
    ILocalizationService loc) : ControllerBase
{
    private string Lang => LangFromHeader();
    private long CharId => long.Parse(User.FindFirst("charId")?.Value ?? "0");

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClanRequest req)
    {
        var (ok, msg, clanId) = await clanSvc.CreateAsync(CharId, req, Lang);
        return ok ? StatusCode(201, new { success = true, message = msg, clanId })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("{clanId}")]
    public async Task<IActionResult> GetClan(int clanId)
    {
        var clan = await clanSvc.GetByIdAsync(clanId);
        return clan is null
            ? NotFound(new { success = false, message = loc.Get("error.not_found", Lang) })
            : Ok(new { success = true, data = clan });
    }

    [HttpGet("{clanId}/members")]
    public async Task<IActionResult> GetMembers(int clanId) =>
        Ok(new { success = true, data = await clanSvc.GetMembersAsync(clanId) });

    [HttpGet("{clanId}/storage")]
    public async Task<IActionResult> GetStorage(int clanId)
    {
        var myClanId = await clanSvc.GetCharClanIdAsync(CharId);
        if (myClanId != clanId)
            return Forbid();
        var items = await clanSvc.GetStorageAsync(clanId);
        return Ok(new { success = true, data = items.Select(s => new
        {
            s.ItemId, s.Item.Name, s.Quantity, s.DepositedBy, s.DepositedAt
        })});
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        var (ok, msg) = await clanSvc.LeaveAsync(CharId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{clanId}/kick/{targetId}")]
    public async Task<IActionResult> Kick(long targetId)
    {
        var (ok, msg) = await clanSvc.KickAsync(CharId, targetId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{clanId}/promote/{targetId}")]
    public async Task<IActionResult> Promote(long targetId, [FromBody] PromoteRequest req)
    {
        var (ok, msg) = await clanSvc.PromoteAsync(CharId, targetId, req.Role, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{clanId}/deposit-gold")]
    public async Task<IActionResult> DepositGold([FromBody] GoldRequest req)
    {
        var (ok, msg) = await clanSvc.DepositGoldAsync(CharId, req.Amount, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{clanId}/withdraw-gold")]
    public async Task<IActionResult> WithdrawGold([FromBody] GoldRequest req)
    {
        var (ok, msg) = await clanSvc.WithdrawGoldAsync(CharId, req.Amount, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{clanId}/deposit-item")]
    public async Task<IActionResult> DepositItem([FromBody] ItemTransferRequest req)
    {
        var (ok, msg) = await clanSvc.DepositItemAsync(CharId, req.ItemId, req.Qty, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{clanId}/withdraw-item")]
    public async Task<IActionResult> WithdrawItem([FromBody] ItemTransferRequest req)
    {
        var (ok, msg) = await clanSvc.WithdrawItemAsync(CharId, req.ItemId, req.Qty, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("{clanId}")]
    public async Task<IActionResult> Disband(int clanId)
    {
        var (ok, msg) = await clanSvc.DisbandAsync(CharId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    private string LangFromHeader() =>
        Request.Headers["Accept-Language"].FirstOrDefault()
            ?.Split(',')[0].Split('-')[0] ?? "vi";
}

// ─── Party Controller ────────────────────────────────────────

[ApiController]
[Route("api/parties")]
[Authorize]
public class PartyController(
    IPartyService        partySvc,
    ILocalizationService loc) : ControllerBase
{
    private string Lang => LangFromHeader();
    private long CharId => long.Parse(User.FindFirst("charId")?.Value ?? "0");

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePartyRequest req)
    {
        var (ok, msg, partyId) = await partySvc.CreateAsync(CharId, req.MaxMembers, Lang);
        return ok ? StatusCode(201, new { success = true, partyId })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("{partyId}")]
    public async Task<IActionResult> GetParty(long partyId)
    {
        var party = await partySvc.GetPartyAsync(partyId);
        return party is null
            ? NotFound()
            : Ok(new { success = true, data = party });
    }

    [HttpPost("{partyId}/invite/{targetId}")]
    public async Task<IActionResult> Invite(long partyId, long targetId)
    {
        var (ok, msg) = await partySvc.InviteAsync(partyId, CharId, targetId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("invites/{inviteId}/accept")]
    public async Task<IActionResult> Accept(long inviteId)
    {
        var (ok, msg) = await partySvc.AcceptInviteAsync(inviteId, CharId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("invites/{inviteId}/decline")]
    public async Task<IActionResult> Decline(long inviteId)
    {
        var (ok, msg) = await partySvc.DeclineInviteAsync(inviteId, CharId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        var (ok, msg) = await partySvc.LeaveAsync(CharId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{partyId}/kick/{targetId}")]
    public async Task<IActionResult> Kick(long partyId, long targetId)
    {
        var (ok, msg) = await partySvc.KickAsync(CharId, targetId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("{partyId}")]
    public async Task<IActionResult> Disband(long partyId)
    {
        var (ok, msg) = await partySvc.DisbandAsync(CharId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    private string LangFromHeader() =>
        Request.Headers["Accept-Language"].FirstOrDefault()
            ?.Split(',')[0].Split('-')[0] ?? "vi";
}

// ─── Relationship Controller ──────────────────────────────────

[ApiController]
[Route("api/relationships")]
[Authorize]
public class RelationshipController(
    IRelationshipService relSvc,
    ILocalizationService loc) : ControllerBase
{
    private string Lang => LangFromHeader();
    private long CharId => long.Parse(User.FindFirst("charId")?.Value ?? "0");

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await relSvc.GetAllAsync(CharId) });

    [HttpPost("{targetId}/{type}")]
    public async Task<IActionResult> Propose(long targetId, RelType type)
    {
        var (ok, msg) = await relSvc.ProposeAsync(CharId, targetId, type, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("{targetId}/{type}")]
    public async Task<IActionResult> Remove(long targetId, RelType type)
    {
        var (ok, msg) = await relSvc.RemoveAsync(CharId, targetId, type, Lang);
        return ok ? Ok(new { success = true })
                  : NotFound(new { success = false, message = msg });
    }

    private string LangFromHeader() =>
        Request.Headers["Accept-Language"].FirstOrDefault()
            ?.Split(',')[0].Split('-')[0] ?? "vi";
}

// ─── Request records ─────────────────────────────────────────

public record PromoteRequest(string Role);
public record GoldRequest(long Amount);
public record ItemTransferRequest(int ItemId, int Qty);
public record CreatePartyRequest(int MaxMembers = 4);
