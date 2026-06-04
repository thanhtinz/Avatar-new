// FantasyWorld.Server/Controllers/Admin/AdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Controllers.Economy;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Services;
using FantasyWorld.Server.Services.World;

namespace FantasyWorld.Server.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "gm,admin")]
public class AdminController(
    GameDbContext        db,
    IGameStateService    state,
    IWorldEventService   eventSvc,
    ILocalizationService loc) : GameControllerBase
{
    // ─── Server stats ────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var today    = DateTime.UtcNow.Date;
        var newToday = await db.Accounts
            .CountAsync(a => a.CreatedAt >= today);
        var online   = state.OnlineCount;
        var totalAccounts = await db.Accounts.CountAsync();
        var totalChars    = await db.Characters.CountAsync();

        var revenueToday = await db.Transactions
            .Where(t => t.CreatedAt >= today && t.Status == "success")
            .SumAsync(t => (long?)t.AmountVnd) ?? 0;

        var activeEvents = await db.WorldEventInstances
            .CountAsync(i => i.Status == "active");

        return Ok(new
        {
            success = true,
            data = new
            {
                online,
                totalAccounts,
                totalChars,
                newAccountsToday = newToday,
                revenueToday,
                activeEvents,
                serverTime = DateTime.UtcNow,
            }
        });
    }

    // ─── Player management ───────────────────────────────────

    [HttpGet("players")]
    public async Task<IActionResult> GetPlayers(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var q = db.Accounts.Include(a => a.Characters).AsQueryable();

        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.Username.Contains(search)
                || a.Characters.Any(c => c.Name.Contains(search)));

        var total = await q.CountAsync();
        var list  = await q
            .OrderByDescending(a => a.LastLogin)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.Username, a.Email, a.Role,
                a.IsBanned, a.BanReason,
                a.LastLogin, a.LastIp, a.CreatedAt,
                CharCount    = a.Characters.Count,
                Characters   = a.Characters.Select(c => new
                    { c.Id, c.Name, c.Level, c.Gold }).ToList(),
            })
            .ToListAsync();

        return Ok(new { success = true, data = list,
            pagination = new { total, page, pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize) } });
    }

    [HttpGet("players/online")]
    public IActionResult GetOnlinePlayers()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                count = state.OnlineCount,
                serverTime = DateTime.UtcNow,
            }
        });
    }

    // ─── Ban / Unban ─────────────────────────────────────────

    [HttpPost("players/{accountId}/ban")]
    public async Task<IActionResult> Ban(long accountId, [FromBody] BanRequest req)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null) return NotFound();

        if (account.Role is "gm" or "admin")
            return Forbid();

        account.IsBanned   = true;
        account.BanReason  = req.Reason;
        account.BanUntil   = req.DaysTemp > 0
            ? DateTime.UtcNow.AddDays(req.DaysTemp) : null;

        // Xóa hết sessions
        await db.Sessions
            .Where(s => s.AccountId == accountId)
            .ExecuteDeleteAsync();

        await db.SaveChangesAsync();

        return Ok(new { success = true, message = $"Banned {account.Username}" });
    }

    [HttpPost("players/{accountId}/unban")]
    public async Task<IActionResult> Unban(long accountId)
    {
        await db.Accounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsBanned,  false)
                .SetProperty(a => a.BanReason, (string?)null)
                .SetProperty(a => a.BanUntil,  (DateTime?)null));

        return Ok(new { success = true });
    }

    // ─── Give items / gold ───────────────────────────────────

    [HttpPost("players/{charId}/give-gold")]
    public async Task<IActionResult> GiveGold(long charId, [FromBody] GiveGoldRequest req)
    {
        var char_ = await db.Characters.FindAsync(charId);
        if (char_ is null) return NotFound();

        char_.Gold += req.Amount;
        await db.SaveChangesAsync();

        return Ok(new { success = true,
            message = $"Gave {req.Amount} gold to {char_.Name}" });
    }

    [HttpPost("players/{charId}/give-item")]
    public async Task<IActionResult> GiveItem(long charId, [FromBody] GiveItemRequest req)
    {
        var item = await db.Items.FindAsync(req.ItemId);
        if (item is null) return NotFound(new { message = "Item not found" });

        var existing = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == req.ItemId && i.Quantity < item.MaxStack);

        if (existing is not null) existing.Quantity += req.Quantity;
        else
        {
            var maxSlot = await db.Inventories
                .Where(i => i.CharacterId == charId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new Data.Entities.InventoryItem
            {
                CharacterId = charId, ItemId = req.ItemId,
                Quantity = req.Quantity, Slot = maxSlot + 1
            });
        }

        await db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── Broadcast ───────────────────────────────────────────

    [HttpPost("broadcast")]
    public IActionResult Broadcast([FromBody] BroadcastRequest req,
        [FromServices] IServiceProvider sp)
    {
        // Signal đến tất cả connected clients qua IHubContext
        // Implemented ở middleware/socket layer
        return Ok(new { success = true, message = "Broadcast queued" });
    }

    // ─── World Events ────────────────────────────────────────

    [HttpGet("events")]
    public async Task<IActionResult> GetWorldEvents()
    {
        var events = await db.WorldEvents.ToListAsync();
        var instances = await db.WorldEventInstances
            .Where(i => i.Status != "ended")
            .ToListAsync();

        return Ok(new { success = true, data = new { events, activeInstances = instances } });
    }

    [HttpPost("events/{eventId}/trigger")]
    public async Task<IActionResult> TriggerEvent(int eventId)
    {
        var result = await eventSvc.TriggerManualAsync(eventId, CharId);
        return result is null
            ? NotFound()
            : Ok(new { success = true, data = result });
    }

    // ─── Economy Monitor ─────────────────────────────────────

    [HttpGet("economy")]
    public async Task<IActionResult> GetEconomyStats()
    {
        var topItems = await db.MarketPrices
            .Include(m => m.Item)
            .OrderByDescending(m => m.Demand)
            .Take(10)
            .Select(m => new { m.ItemId, m.Item.Name, m.CurrentPrice, m.Supply, m.Demand })
            .ToListAsync();

        var recentTrades = await db.PlayerShopTransactions
            .Include(t => t.Buyer)
            .OrderByDescending(t => t.BoughtAt)
            .Take(20)
            .Select(t => new
            {
                t.BuyerId, BuyerName = t.Buyer.Name,
                t.TotalPrice, t.BoughtAt
            })
            .ToListAsync();

        var topAuctions = await db.AuctionListings
            .Where(a => a.Status == "active")
            .Include(a => a.Item)
            .OrderByDescending(a => a.CurrentBid)
            .Take(5)
            .Select(a => new { a.Id, ItemName = a.Item!.Name, a.CurrentBid, a.EndsAt })
            .ToListAsync();

        return Ok(new { success = true, data = new
        {
            topDemandItems = topItems,
            recentTrades,
            topAuctions,
        }});
    }

    // ─── Review fashion designs ──────────────────────────────

    [HttpGet("fashion/pending")]
    public async Task<IActionResult> GetPendingDesigns()
    {
        var designs = await db.FashionDesigns
            .Where(d => d.Status == "pending_review")
            .Include(d => d.Designer)
            .Select(d => new
            {
                d.Id, d.Name, d.Slot,
                DesignerName = d.Designer.Name,
                d.PriceGold, d.SubmittedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = designs });
    }

    // ─── Restaurant applications ─────────────────────────────

    [HttpGet("restaurants/applications")]
    public async Task<IActionResult> GetRestaurantApps()
    {
        var apps = await db.RestaurantApplications
            .Where(a => a.Status == "voting")
            .Include(a => a.Applicant)
            .Include(a => a.Type)
            .Select(a => new
            {
                a.Id, a.Name, TypeName = a.Type.Name,
                ApplicantName = a.Applicant.Name,
                a.VoteCount, a.VoteEndsAt, a.Status
            })
            .ToListAsync();

        return Ok(new { success = true, data = apps });
    }

    [HttpPost("restaurants/applications/{appId}/approve")]
    public async Task<IActionResult> ApproveRestaurant(long appId)
    {
        var app = await db.RestaurantApplications
            .Include(a => a.Type)
            .FirstOrDefaultAsync(a => a.Id == appId);

        if (app is null) return NotFound();

        var count = await db.Restaurants.CountAsync(r => r.TypeId == app.TypeId);
        if (count >= app.Type.MaxPerServer)
            return BadRequest(new { message = "Server limit reached for this type" });

        app.Status     = "approved";
        app.ApprovedAt = DateTime.UtcNow;

        db.Restaurants.Add(new Data.Entities.Restaurant
        {
            ApplicationId = app.Id,
            OwnerId       = app.ApplicantId,
            TypeId        = app.TypeId,
            Name          = app.Name,
            MapId         = app.MapId,
        });

        await db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── GM Tools ────────────────────────────────────────────

    [HttpPost("maintenance")]
    [Authorize(Roles = "admin")]
    public IActionResult SetMaintenance([FromBody] MaintenanceRequest req)
    {
        // Toggle maintenance mode — signal via broadcast
        return Ok(new { success = true, maintenance = req.Enabled });
    }

    [HttpDelete("sessions/{accountId}")]
    public async Task<IActionResult> KickPlayer(long accountId)
    {
        await db.Sessions
            .Where(s => s.AccountId == accountId)
            .ExecuteDeleteAsync();
        return Ok(new { success = true });
    }

    [HttpGet("logs/battles")]
    public async Task<IActionResult> GetBattleLogs(
        [FromQuery] int hours = 1, [FromQuery] int limit = 100)
    {
        var since = DateTime.UtcNow.AddHours(-hours);
        var logs  = await db.BattleLogs
            .Where(b => b.FoughtAt >= since)
            .Include(b => b.Attacker)
            .OrderByDescending(b => b.FoughtAt)
            .Take(limit)
            .Select(b => new
            {
                b.Id, AttackerName = b.Attacker.Name,
                b.DefenderType, b.Outcome,
                b.DamageDealt, b.ExpGained, b.GoldGained, b.FoughtAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = logs });
    }
}

// ─── Request records ─────────────────────────────────────────
public record BanRequest(string Reason, int DaysTemp = 0);
public record GiveGoldRequest(long Amount, string? Reason = null);
public record GiveItemRequest(int ItemId, int Quantity = 1);
public record BroadcastRequest(string MessageVi, string MessageEn, string Type = "announcement");
public record MaintenanceRequest(bool Enabled, string? Message = null);
