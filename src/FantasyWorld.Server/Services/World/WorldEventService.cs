// FantasyWorld.Server/Services/World/WorldEventService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.World;

public interface IWorldEventService
{
    Task<List<WorldEventDto>>   GetActiveAsync();
    Task<WorldEventDto?>        GetByInstanceAsync(long instanceId);
    Task<(bool Ok, string Msg)> JoinAsync(long charId, long instanceId, string lang);
    Task<(bool Ok, string Msg)> ContributeAsync(long charId, ContributeEventRequest req, string lang);
    Task<(bool Ok, string Msg)> ClaimRewardAsync(long charId, long instanceId, string lang);
    Task                        TriggerRandomEventsAsync();   // cron
    Task                        ProcessExpiredInstancesAsync(); // cron
    Task<WorldEventDto?>        TriggerManualAsync(int eventId, long? gmCharId); // GM tool
}

public class WorldEventService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<WorldEventService> logger) : IWorldEventService
{
    // ─── Get active ──────────────────────────────────────────
    public async Task<List<WorldEventDto>> GetActiveAsync()
    {
        return await db.WorldEventInstances
            .Where(i => i.Status == "active" || i.Status == "announced")
            .Include(i => i.Event)
            .OrderByDescending(i => i.StartedAt)
            .Select(i => MapToDto(i))
            .ToListAsync();
    }

    public async Task<WorldEventDto?> GetByInstanceAsync(long instanceId)
    {
        var i = await db.WorldEventInstances
            .Include(i => i.Event)
            .FirstOrDefaultAsync(i => i.Id == instanceId);
        return i is null ? null : MapToDto(i);
    }

    // ─── Join ────────────────────────────────────────────────
    public async Task<(bool, string)> JoinAsync(
        long charId, long instanceId, string lang)
    {
        var instance = await db.WorldEventInstances
            .Include(i => i.Event)
            .FirstOrDefaultAsync(i => i.Id == instanceId
                && (i.Status == "active" || i.Status == "announced"));

        if (instance is null) return (false, loc.Get("error.not_found", lang));

        var exists = await db.WorldEventParticipants
            .AnyAsync(p => p.InstanceId == instanceId && p.CharacterId == charId);
        if (exists) return (false, loc.Get("error.bad_request", lang));

        db.WorldEventParticipants.Add(new WorldEventParticipant
        {
            InstanceId  = instanceId,
            CharacterId = charId,
        });

        instance.Participants++;
        if (instance.Status == "announced") instance.Status = "active";

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Contribute ──────────────────────────────────────────
    public async Task<(bool, string)> ContributeAsync(
        long charId, ContributeEventRequest req, string lang)
    {
        var participant = await db.WorldEventParticipants
            .Include(p => p.Instance).ThenInclude(i => i.Event)
            .FirstOrDefaultAsync(p =>
                p.InstanceId == req.InstanceId
                && p.CharacterId == charId
                && p.Instance.Status == "active");

        if (participant is null) return (false, loc.Get("error.not_found", lang));

        participant.Contribution += req.Amount;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Claim reward ────────────────────────────────────────
    public async Task<(bool, string)> ClaimRewardAsync(
        long charId, long instanceId, string lang)
    {
        var p = await db.WorldEventParticipants
            .Include(p => p.Instance).ThenInclude(i => i.Event)
            .FirstOrDefaultAsync(p =>
                p.InstanceId == instanceId
                && p.CharacterId == charId
                && !p.RewardClaimed
                && p.Instance.Status == "ended");

        if (p is null) return (false, loc.Get("error.not_found", lang));

        // Tính reward dựa vào đóng góp
        var totalContrib = await db.WorldEventParticipants
            .Where(x => x.InstanceId == instanceId)
            .SumAsync(x => x.Contribution);

        var ratio = totalContrib > 0
            ? (double)p.Contribution / totalContrib : 0;

        // Parse base reward và scale theo ratio
        var rewardJson = p.Instance.Event.RewardJson ?? "{}";
        int expReward  = (int)(1000 * (1 + ratio));
        int goldReward = (int)(500  * (1 + ratio));

        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + goldReward)
                .SetProperty(c => c.Exp,  c => c.Exp  + expReward));

        p.RewardClaimed = true;
        p.RewardJson    = System.Text.Json.JsonSerializer.Serialize(
            new { exp = expReward, gold = goldReward });

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Trigger random events (cron mỗi giờ) ────────────────
    public async Task TriggerRandomEventsAsync()
    {
        var events = await db.WorldEvents
            .Where(e => e.TriggerType == "random" && e.IsActive)
            .ToListAsync();

        var rng = new Random();
        foreach (var evt in events)
        {
            // Kiểm tra cooldown
            var recent = await db.WorldEventInstances
                .AnyAsync(i => i.EventId == evt.Id
                    && i.StartedAt > DateTime.UtcNow.AddHours(-evt.CooldownHours));
            if (recent) continue;

            // Roll
            if (rng.NextDouble() >= evt.TriggerChance) continue;

            await SpawnInstanceAsync(evt);
        }
    }

    // ─── Process expired (cron mỗi 2 phút) ───────────────────
    public async Task ProcessExpiredInstancesAsync()
    {
        var expired = await db.WorldEventInstances
            .Where(i => (i.Status == "active" || i.Status == "announced")
                && i.EndsAt.HasValue && i.EndsAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var inst in expired)
        {
            inst.Status = "ended";
            logger.LogInformation("World event ended: instanceId={Id}", inst.Id);
        }

        if (expired.Count > 0)
            await db.SaveChangesAsync();
    }

    // ─── Manual trigger (GM) ─────────────────────────────────
    public async Task<WorldEventDto?> TriggerManualAsync(int eventId, long? gmCharId)
    {
        var evt = await db.WorldEvents.FindAsync(eventId);
        if (evt is null) return null;

        var inst = await SpawnInstanceAsync(evt, gmCharId);
        return MapToDto(inst);
    }

    // ─── Private ─────────────────────────────────────────────
    private async Task<WorldEventInstance> SpawnInstanceAsync(
        WorldEvent evt, long? triggeredBy = null)
    {
        var inst = new WorldEventInstance
        {
            EventId      = evt.Id,
            TriggeredBy  = triggeredBy,
            Status       = "announced",
            StartedAt    = DateTime.UtcNow,
            EndsAt       = DateTime.UtcNow.AddMinutes(evt.DurationMin),
            Participants = 0,
        };

        db.WorldEventInstances.Add(inst);
        await db.SaveChangesAsync();

        logger.LogInformation("World event triggered: {Name} (instanceId={Id})",
            evt.Name, inst.Id);

        return inst;
    }

    private static WorldEventDto MapToDto(WorldEventInstance i) => new(
        i.Id,
        i.Event.Name,
        i.Event.EventType,
        i.Event.Description ?? i.Event.Name,
        i.Event.Description ?? i.Event.Name,
        i.EndsAt.HasValue ? new DateTimeOffset(i.EndsAt.Value).ToUnixTimeMilliseconds() : 0,
        i.Participants,
        i.Status);
}
