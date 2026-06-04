// FantasyWorld.Server/Services/World/NpcAiService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Server.BackgroundServices;

namespace FantasyWorld.Server.Services.World;

public interface INpcAiService
{
    Task<List<NpcStateDto>>     GetNpcsOnMapAsync(int mapId, long charId);
    Task<(bool Ok, string Msg)> InteractAsync(long charId, NpcInteractRequest req, string lang);
    Task                        TickNpcSchedulesAsync();   // cron mỗi phút game
}

public class NpcAiService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<NpcAiService> logger) : INpcAiService
{
    // In-memory NPC positions (reset on restart, fine for NPCs)
    private static readonly Dictionary<int, (float X, float Y, string Activity)> _npcPositions = new();

    // ─── Get NPCs on map ─────────────────────────────────────
    public async Task<List<NpcStateDto>> GetNpcsOnMapAsync(int mapId, long charId)
    {
        var npcs = await db.Npcs
            .Where(n => n.MapId == mapId)
            .Include(n => n.AiProfile)
            .ToListAsync();

        var wt = WorldTimeService.Current;

        return npcs.Select(npc =>
        {
            _npcPositions.TryGetValue(npc.Id, out var pos);
            var activity = GetCurrentActivity(npc, wt.GameHour);

            // Lấy affection với nhân vật hiện tại
            var affection = db.NpcCharacterRelations
                .Where(r => r.NpcId == npc.Id && r.CharacterId == charId)
                .Select(r => r.Affection)
                .FirstOrDefault();

            return new NpcStateDto(
                npc.Id, npc.Name, npc.NpcType,
                pos.X == 0 ? npc.PosX : pos.X,
                pos.Y == 0 ? npc.PosY : pos.Y,
                activity, affection);
        }).ToList();
    }

    // ─── Interact ────────────────────────────────────────────
    public async Task<(bool, string)> InteractAsync(
        long charId, NpcInteractRequest req, string lang)
    {
        var npc = await db.Npcs
            .Include(n => n.AiProfile)
            .FirstOrDefaultAsync(n => n.Id == req.NpcId);

        if (npc is null) return (false, loc.Get("error.not_found", lang));

        // Cập nhật quan hệ NPC-nhân vật
        var relation = await db.NpcCharacterRelations
            .FirstOrDefaultAsync(r => r.NpcId == req.NpcId && r.CharacterId == charId);

        if (relation is null)
        {
            relation = new NpcCharacterRelation
            {
                NpcId       = req.NpcId,
                CharacterId = charId,
                Affection   = 0,
            };
            db.NpcCharacterRelations.Add(relation);
        }

        relation.Interactions++;
        relation.LastMetAt = DateTime.UtcNow;

        // Tăng affection khi nói chuyện (nhưng có diminishing returns)
        int affectionGain = Math.Max(1, 5 - relation.Interactions / 20);
        relation.Affection = Math.Min(100, relation.Affection + affectionGain);

        // Lưu ghi chú NPC nhớ player
        var notes = new Dictionary<string, object>
        {
            ["last_action"] = req.ActionType,
            ["total_interactions"] = relation.Interactions
        };
        relation.NotesJson = System.Text.Json.JsonSerializer.Serialize(notes);

        await db.SaveChangesAsync();

        // Trả về dialog theo personality và affection
        var dialog = BuildDialog(npc, relation.Affection, relation.Interactions, lang);
        return (true, dialog);
    }

    // ─── Tick NPC schedules (cron) ───────────────────────────
    public async Task TickNpcSchedulesAsync()
    {
        var wt  = WorldTimeService.Current;
        var npcs = await db.Npcs
            .Include(n => n.Schedules)
            .Include(n => n.AiProfile)
            .ToListAsync();

        foreach (var npc in npcs)
        {
            var schedule = npc.Schedules
                .FirstOrDefault(s =>
                    s.GameHourStart <= wt.GameHour &&
                    s.GameHourEnd   >= wt.GameHour);

            if (schedule is null) continue;

            // Di chuyển NPC đến vị trí theo lịch
            var (targetX, targetY) = schedule.Activity switch
            {
                "work"     => (schedule.TargetPosX > 0 ? schedule.TargetPosX : npc.PosX,
                               schedule.TargetPosY > 0 ? schedule.TargetPosY : npc.PosY),
                "wander"   => GetWanderPosition(npc),
                "sleep"    => (npc.AiProfile?.HomePosX ?? npc.PosX,
                               npc.AiProfile?.HomePosY ?? npc.PosY),
                _          => (schedule.TargetPosX > 0 ? schedule.TargetPosX : npc.PosX,
                               schedule.TargetPosY > 0 ? schedule.TargetPosY : npc.PosY),
            };

            // Smooth movement (10% per tick)
            var current = _npcPositions.GetValueOrDefault(npc.Id,
                (npc.PosX, npc.PosY, schedule.Activity));

            var newX = current.X + (targetX - current.X) * 0.1f;
            var newY = current.Y + (targetY - current.Y) * 0.1f;

            _npcPositions[npc.Id] = (newX, newY, schedule.Activity);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────
    private static string GetCurrentActivity(Npc npc, int gameHour)
    {
        var schedule = npc.Schedules?
            .FirstOrDefault(s => s.GameHourStart <= gameHour && s.GameHourEnd >= gameHour);
        return schedule?.Activity ?? "idle";
    }

    private static (float X, float Y) GetWanderPosition(Npc npc)
    {
        var rng    = new Random();
        var radius = npc.WanderRadius > 0 ? npc.WanderRadius : 10f;
        var angle  = rng.NextDouble() * Math.PI * 2;
        var dist   = (float)(rng.NextDouble() * radius);
        return (
            npc.PosX + dist * (float)Math.Cos(angle),
            npc.PosY + dist * (float)Math.Sin(angle));
    }

    private static string BuildDialog(Npc npc, int affection, int interactions, string lang)
    {
        var personality = npc.AiProfile?.Personality ?? "friendly";
        var isVi = lang == "vi";

        if (interactions <= 1)
        {
            return isVi
                ? $"Chào mừng đến đây, ta là {npc.Name}!"
                : $"Welcome, I am {npc.Name}!";
        }

        if (affection >= 80)
        {
            return isVi
                ? $"Ồ, {npc.Name} rất vui khi gặp lại cậu! Cậu là bạn tốt của ta."
                : $"Oh, I'm so glad to see you again! You're a good friend of mine.";
        }

        return personality switch
        {
            "grumpy"    => isVi ? "Hmph. Cậu lại đến rồi." : "Hmph. You again.",
            "shy"       => isVi ? "Ơ... chào cậu..." : "Um... hello...",
            "energetic" => isVi ? "CHÀO CẬU! Hôm nay tuyệt vời lắm!" : "HEY! Great day isn't it!",
            "wise"      => isVi ? "Mỗi cuộc gặp đều có ý nghĩa riêng..." : "Every meeting has its purpose...",
            "mysterious"=> isVi ? "... (nhìn cậu bí ẩn)" : "... (watches you mysteriously)",
            _           => isVi ? $"Ồ chào cậu! {npc.Name} đây." : $"Oh hello there! It's {npc.Name}.",
        };
    }
}
