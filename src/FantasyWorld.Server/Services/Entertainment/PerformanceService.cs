// FantasyWorld.Server/Services/Entertainment/PerformanceService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Entertainment;

public interface IPerformanceService
{
    Task<List<PerformanceStage>>     GetStagesAsync(int mapId);
    Task<List<PerformanceDto>>       GetLiveAsync(int mapId);
    Task<(bool Ok, string Msg, long PerfId)> StartAsync(long charId, StartPerformanceRequest req, string lang);
    Task<(bool Ok, string Msg)>      EndAsync(long charId, long performanceId, string lang);
    Task<(bool Ok, string Msg)>      SendTipAsync(long charId, SendTipRequest req, string lang);
    Task<(bool Ok, string Msg)>      JoinGroupAsync(long charId, long performanceId, string role, string lang);
    Task<PerformanceDto?>            GetByIdAsync(long performanceId);
    Task                             AudienceTickAsync(); // cron - NPC audience simulation
}

public class PerformanceService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<PerformanceService> logger) : IPerformanceService
{
    public async Task<List<PerformanceStage>> GetStagesAsync(int mapId) =>
        await db.PerformanceStages
            .Where(s => s.MapId == mapId && s.IsActive)
            .ToListAsync();

    public async Task<List<PerformanceDto>> GetLiveAsync(int mapId)
    {
        return await db.Performances
            .Where(p => p.Stage.MapId == mapId
                && (p.Status == "live" || p.Status == "upcoming"))
            .Include(p => p.Performer)
            .Include(p => p.Stage)
            .Select(p => new PerformanceDto(
                p.Id, p.Performer.Name, p.Stage.Name,
                p.PerfType, p.Title, p.Status,
                p.AudienceCount, p.TotalTips,
                p.StartAt.HasValue
                    ? new DateTimeOffset(p.StartAt.Value).ToUnixTimeMilliseconds()
                    : null))
            .ToListAsync();
    }

    // ─── Start performance ───────────────────────────────────
    public async Task<(bool, string, long)> StartAsync(
        long charId, StartPerformanceRequest req, string lang)
    {
        // Kiểm tra stage trống
        var stageBusy = await db.Performances.AnyAsync(p =>
            p.StageId == req.StageId && p.Status == "live");
        if (stageBusy)
            return (false, loc.Get("error.bad_request", lang), 0);

        // Kiểm tra char không đang perform
        var alreadyPerforming = await db.Performances.AnyAsync(p =>
            p.PerformerId == charId && p.Status == "live");
        if (alreadyPerforming)
            return (false, loc.Get("error.bad_request", lang), 0);

        var perf = new Performance
        {
            PerformerId   = charId,
            StageId       = req.StageId,
            PerfType      = req.PerfType,
            WorkId        = req.WorkId,
            Title         = req.Title,
            Status        = "live",
            StartAt       = DateTime.UtcNow,
        };
        db.Performances.Add(perf);
        await db.SaveChangesAsync();

        logger.LogInformation("Performance started: charId={Char} stage={Stage} type={Type}",
            charId, req.StageId, req.PerfType);

        return (true, "OK", perf.Id);
    }

    // ─── End performance ─────────────────────────────────────
    public async Task<(bool, string)> EndAsync(
        long charId, long performanceId, string lang)
    {
        var perf = await db.Performances
            .Include(p => p.Tips)
            .FirstOrDefaultAsync(p => p.Id == performanceId
                && p.PerformerId == charId && p.Status == "live");

        if (perf is null) return (false, loc.Get("error.not_found", lang));

        perf.Status = "ended";
        perf.EndAt  = DateTime.UtcNow;

        // Cộng tổng tips cho performer
        var totalGold = perf.Tips
            .Where(t => t.TipType == "gold")
            .Sum(t => t.Amount);

        if (totalGold > 0)
        {
            await db.Characters.Where(c => c.Id == charId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Gold, c => c.Gold + totalGold));
        }

        // Cộng cho group members tỷ lệ bằng nhau
        var members = await db.PerformanceGroupMembers
            .Where(m => m.PerformanceId == performanceId)
            .ToListAsync();

        if (members.Count > 0 && totalGold > 0)
        {
            var share = totalGold / (members.Count + 1);
            foreach (var member in members)
            {
                await db.Characters.Where(c => c.Id == member.CharacterId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Gold, c => c.Gold + share));
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Performance ended: id={Id} tips={Tips}",
            performanceId, totalGold);

        return (true, "OK");
    }

    // ─── Send tip ────────────────────────────────────────────
    public async Task<(bool, string)> SendTipAsync(
        long charId, SendTipRequest req, string lang)
    {
        var perf = await db.Performances
            .FirstOrDefaultAsync(p => p.Id == req.PerformanceId
                && p.Status == "live");
        if (perf is null) return (false, loc.Get("error.not_found", lang));

        if (perf.PerformerId == charId)
            return (false, loc.Get("error.bad_request", lang));

        // Kiểm tra và trừ tiền
        if (req.TipType == "gold")
        {
            var char_ = await db.Characters.FindAsync(charId)!;
            if (char_!.Gold < req.Amount)
                return (false, loc.Get("character.insufficient_gold", lang,
                    new { need = req.Amount, have = char_.Gold }));
            char_.Gold -= req.Amount;
        }
        else if (req.TipType == "diamond")
        {
            var char_ = await db.Characters.FindAsync(charId)!;
            if (char_!.Diamond < req.Amount)
                return (false, loc.Get("character.insufficient_diamond", lang));
            char_.Diamond -= req.Amount;
        }
        else if (req.TipType == "gift_item" && req.ItemId.HasValue)
        {
            var invItem = await db.Inventories
                .FirstOrDefaultAsync(i => i.CharacterId == charId
                    && i.ItemId == req.ItemId && i.Quantity > 0);
            if (invItem is null)
                return (false, loc.Get("inventory.item_not_found", lang));
            invItem.Quantity--;
            if (invItem.Quantity == 0) db.Inventories.Remove(invItem);
        }

        db.PerformanceTips.Add(new PerformanceTip
        {
            PerformanceId = req.PerformanceId,
            TipperId      = charId,
            TipType       = req.TipType,
            Amount        = req.Amount,
            ItemId        = req.ItemId,
            Message       = req.Message,
        });

        perf.TotalTips     += req.TipType == "gold" ? req.Amount : 0;
        perf.AudienceCount  = Math.Max(perf.AudienceCount,
            state.GetOnMap(await GetStageMapIdAsync(perf.StageId)));

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Join group ──────────────────────────────────────────
    public async Task<(bool, string)> JoinGroupAsync(
        long charId, long performanceId, string role, string lang)
    {
        var perf = await db.Performances
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == performanceId
                && p.Status == "live" && p.PerfType == "group");

        if (perf is null) return (false, loc.Get("error.not_found", lang));

        if (perf.Members.Any(m => m.CharacterId == charId))
            return (false, loc.Get("error.bad_request", lang));

        db.PerformanceGroupMembers.Add(new PerformanceGroupMember
        {
            PerformanceId = performanceId,
            CharacterId   = charId,
            Role          = role,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<PerformanceDto?> GetByIdAsync(long performanceId)
    {
        var p = await db.Performances
            .Include(p => p.Performer)
            .Include(p => p.Stage)
            .FirstOrDefaultAsync(p => p.Id == performanceId);

        return p is null ? null : new PerformanceDto(
            p.Id, p.Performer.Name, p.Stage.Name,
            p.PerfType, p.Title, p.Status,
            p.AudienceCount, p.TotalTips,
            p.StartAt.HasValue
                ? new DateTimeOffset(p.StartAt.Value).ToUnixTimeMilliseconds()
                : null);
    }

    // ─── NPC audience simulation (cron mỗi 5 phút) ──────────
    public async Task AudienceTickAsync()
    {
        var live = await db.Performances
            .Where(p => p.Status == "live")
            .ToListAsync();

        var rng = new Random();
        foreach (var perf in live)
        {
            // NPC audience tăng theo thời gian biểu diễn
            var duration = (DateTime.UtcNow - (perf.StartAt ?? DateTime.UtcNow)).TotalMinutes;
            var npcAudience = (int)(Math.Min(duration * 2, 30) + rng.Next(-3, 4));
            perf.AudienceCount = Math.Max(0, npcAudience);

            // NPC tip ngẫu nhiên
            if (rng.Next(100) < 15 && perf.AudienceCount > 5)
            {
                var npcTip = rng.Next(10, 100);
                perf.TotalTips += npcTip;
                db.PerformanceTips.Add(new PerformanceTip
                {
                    PerformanceId = perf.Id,
                    TipperId      = perf.PerformerId, // self (NPC không có charId)
                    TipType       = "gold",
                    Amount        = npcTip,
                    Message       = "🎵",
                });

                await db.Characters.Where(c => c.Id == perf.PerformerId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Gold, c => c.Gold + npcTip));
            }
        }

        if (live.Count > 0) await db.SaveChangesAsync();
    }

    private async Task<int> GetStageMapIdAsync(int stageId) =>
        await db.PerformanceStages
            .Where(s => s.Id == stageId)
            .Select(s => s.MapId)
            .FirstOrDefaultAsync();
}
