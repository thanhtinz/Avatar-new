// FantasyWorld.Server/Services/Content/DungeonService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Content;

public interface IDungeonService
{
    Task<List<DungeonDto>>          GetAvailableAsync(long charId);
    Task<(bool Ok, string Msg, long RunId)> EnterAsync(long charId, DungeonEnterRequest req, string lang);
    Task<DungeonFloorDto?>          GetCurrentFloorAsync(long runId);
    Task<(bool Ok, CombatResultDto? Result)> CombatActionAsync(long charId, CombatActionRequest req, string lang);
    Task<(bool Ok, string Msg)>     AdvanceFloorAsync(long charId, long runId, string lang);
    Task<DungeonCompleteDto?>       GetRunResultAsync(long runId);
    Task<bool>                      IsOnCooldownAsync(long charId, int dungeonId);
}

public class DungeonService(
    GameDbContext         db,
    IQuestService         questSvc,
    ILocalizationService  loc,
    ILogger<DungeonService> logger) : IDungeonService
{
    // In-memory combat state per run (reset on server restart)
    private static readonly Dictionary<long, CombatState> _combatStates = new();

    private record CombatState(
        long RunId,
        int  FloorNumber,
        List<EnemyState> Enemies,
        Dictionary<long, int> PartyHp);

    private record EnemyState(long Id, int MonsterId, string Name,
        int Level, int Hp, int HpMax, int Atk, int Def, int Spd, bool IsAlive);

    // ─── Get available ───────────────────────────────────────
    public async Task<List<DungeonDto>> GetAvailableAsync(long charId)
    {
        var char_ = await db.Characters.FindAsync(charId);
        if (char_ is null) return [];

        var dungeons = await db.Dungeons
            .Where(d => d.MinLevel <= char_.Level)
            .ToListAsync();

        var result = new List<DungeonDto>();
        foreach (var d in dungeons)
        {
            var onCooldown = await IsOnCooldownAsync(charId, d.Id);
            result.Add(new DungeonDto(
                d.Id, d.Name, d.DungeonType,
                d.MinPlayers, d.MaxPlayers, d.MinLevel,
                d.Floors, d.HasTraps, d.HasPuzzles, onCooldown));
        }
        return result;
    }

    // ─── Enter dungeon ───────────────────────────────────────
    public async Task<(bool, string, long)> EnterAsync(
        long charId, DungeonEnterRequest req, string lang)
    {
        if (await IsOnCooldownAsync(charId, req.DungeonId))
            return (false, loc.Get("dungeon.cooldown", lang, new { hours = 24 }), 0);

        var dungeon = await db.Dungeons.FindAsync(req.DungeonId);
        if (dungeon is null) return (false, loc.Get("error.not_found", lang), 0);

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Level < dungeon.MinLevel)
            return (false, loc.Get("error.bad_request", lang), 0);

        // Tạo DungeonRun
        var run = new DungeonRun
        {
            DungeonId = req.DungeonId,
            PartyId   = req.PartyId,
            Floor     = 1,
            Status    = "in_progress",
        };
        db.DungeonRuns.Add(run);
        await db.SaveChangesAsync();

        // Thêm member vào run
        db.DungeonRunMembers.Add(new DungeonRunMember
        {
            RunId       = run.Id,
            CharacterId = charId,
            IsAlive     = true,
        });
        await db.SaveChangesAsync();

        // Khởi tạo combat state cho floor 1
        await InitFloorCombatAsync(run.Id, req.DungeonId, 1, [charId]);

        logger.LogInformation("Dungeon entered: charId={Char} dungeon={Dungeon} run={Run}",
            charId, dungeon.Name, run.Id);

        return (true, loc.Get("dungeon.enter", lang, new { name = dungeon.Name }), run.Id);
    }

    // ─── Get current floor ───────────────────────────────────
    public async Task<DungeonFloorDto?> GetCurrentFloorAsync(long runId)
    {
        var run = await db.DungeonRuns
            .Include(r => r.Dungeon)
            .FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null) return null;

        var floor = await db.DungeonFloors
            .FirstOrDefaultAsync(f => f.DungeonId == run.DungeonId
                && f.FloorNumber == run.Floor);

        if (!_combatStates.TryGetValue(runId, out var state))
            return null;

        var enemies = state.Enemies.Where(e => e.IsAlive).Select(e =>
            new FloorEnemyDto(e.MonsterId, e.Name, e.Level, e.Hp, 0, 0)).ToList();

        return new DungeonFloorDto(
            run.Floor, floor?.Name ?? $"Tầng {run.Floor}",
            floor?.HasBoss ?? false,
            floor is not null && !string.IsNullOrEmpty(floor.TrapJson),
            floor is not null && !string.IsNullOrEmpty(floor.PuzzleJson),
            enemies);
    }

    // ─── Combat action ───────────────────────────────────────
    public async Task<(bool, CombatResultDto?)> CombatActionAsync(
        long charId, CombatActionRequest req, string lang)
    {
        if (!_combatStates.TryGetValue(req.RunId, out var state))
            return (false, null);

        var char_ = await db.Characters
            .Include(c => c.EquipmentSlot)
            .FirstOrDefaultAsync(c => c.Id == charId);
        if (char_ is null) return (false, null);

        var rng = new Random();

        switch (req.ActionType)
        {
            case "attack":
            {
                // Tìm enemy
                var enemy = state.Enemies
                    .FirstOrDefault(e => e.Id == req.TargetId && e.IsAlive);
                if (enemy is null) return (false, null);

                // Tính damage
                var atk  = await GetTotalAtkAsync(char_);
                var dmg  = Math.Max(1, atk - enemy.Def + rng.Next(-3, 4));
                bool crit = rng.Next(100) < 15;
                if (crit) dmg = (int)(dmg * 1.5);

                // Áp damage
                var newHp   = Math.Max(0, enemy.Hp - dmg);
                var isDead  = newHp == 0;
                var updated = enemy with { Hp = newHp, IsAlive = !isDead };
                var idx     = state.Enemies.IndexOf(enemy);
                state.Enemies[idx] = updated;

                if (isDead)
                {
                    // Loot
                    var monster = await db.Monsters.FindAsync(enemy.MonsterId);
                    if (monster is not null)
                    {
                        var run = await db.DungeonRuns.FindAsync(req.RunId);
                        if (run is not null)
                        {
                            run.TotalExp  += monster.ExpReward;
                            run.TotalGold += monster.GoldReward;
                        }

                        // Roll loot
                        await RollMonsterLootAsync(charId, monster);

                        // Quest update
                        await questSvc.UpdateObjectiveAsync(charId, "kill", monster.Id, 1);
                    }
                }

                // Enemy counter-attack nếu còn sống
                var memberHp = state.PartyHp.GetValueOrDefault(charId, char_.Hp);
                int takenDmg = 0;
                if (!isDead)
                {
                    takenDmg = Math.Max(1, enemy.Atk - char_.Def + rng.Next(-2, 3));
                    memberHp = Math.Max(0, memberHp - takenDmg);
                    state.PartyHp[charId] = memberHp;

                    // Cập nhật HP thực trong DB
                    await db.Characters.Where(c => c.Id == charId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(c => c.Hp, Math.Max(0, char_.Hp - takenDmg)));
                }

                var result = new CombatResultDto(
                    "attack", charId, req.TargetId,
                    dmg, crit, isDead,
                    memberHp, newHp, null);

                // Kiểm tra tất cả enemy chết
                if (!state.Enemies.Any(e => e.IsAlive))
                    await FinishFloorAsync(req.RunId);

                await db.SaveChangesAsync();
                return (true, result);
            }

            case "flee":
            {
                // 50% cơ hội thoát
                bool fled = rng.Next(100) < 50;
                if (fled)
                {
                    var run = await db.DungeonRuns.FindAsync(req.RunId);
                    if (run is not null) run.Status = "failed";
                    _combatStates.Remove(req.RunId);
                    await db.SaveChangesAsync();
                }
                return (fled, new CombatResultDto(
                    "flee", charId, 0, 0, false, false,
                    char_.Hp, 0, fled ? "escaped" : "failed_flee"));
            }

            default:
                return (false, null);
        }
    }

    // ─── Advance floor ───────────────────────────────────────
    public async Task<(bool, string)> AdvanceFloorAsync(
        long charId, long runId, string lang)
    {
        var run = await db.DungeonRuns
            .Include(r => r.Dungeon)
            .FirstOrDefaultAsync(r => r.Id == runId && r.Status == "in_progress");
        if (run is null) return (false, loc.Get("error.not_found", lang));

        // Kiểm tra floor hiện tại đã clear
        if (_combatStates.TryGetValue(runId, out var state)
            && state.Enemies.Any(e => e.IsAlive))
            return (false, loc.Get("error.bad_request", lang));

        if (run.Floor >= run.Dungeon.Floors)
        {
            // Hoàn thành dungeon
            run.Status     = "completed";
            run.FinishedAt = DateTime.UtcNow;

            // Cộng exp/gold
            await db.Characters.Where(c => c.Id == charId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Gold, c => c.Gold + run.TotalGold)
                    .SetProperty(c => c.Exp,  c => c.Exp  + run.TotalExp));

            // Set cooldown
            db.DungeonCooldowns.Add(new DungeonCooldown
            {
                CharacterId = charId,
                DungeonId   = run.DungeonId,
                UnlocksAt   = DateTime.UtcNow.AddHours(run.Dungeon.CooldownHours),
            });

            // Quest update
            await questSvc.UpdateObjectiveAsync(charId, "dungeon", run.DungeonId, 1);
            await questSvc.CheckAndGrantAchievementAsync(charId, "dungeon", 1);

            _combatStates.Remove(runId);
            await db.SaveChangesAsync();

            logger.LogInformation("Dungeon completed: charId={Char} dungeon={D} exp={E} gold={G}",
                charId, run.DungeonId, run.TotalExp, run.TotalGold);

            return (true, loc.Get("dungeon.completed", lang,
                new { name = run.Dungeon.Name }));
        }

        // Tiến lên floor tiếp theo
        run.Floor++;
        var partyIds = await db.DungeonRunMembers
            .Where(m => m.RunId == runId && m.IsAlive)
            .Select(m => m.CharacterId)
            .ToListAsync();

        await InitFloorCombatAsync(runId, run.DungeonId, run.Floor, partyIds);
        await db.SaveChangesAsync();

        return (true, $"Floor {run.Floor}");
    }

    // ─── Get run result ──────────────────────────────────────
    public async Task<DungeonCompleteDto?> GetRunResultAsync(long runId)
    {
        var run = await db.DungeonRuns
            .FirstOrDefaultAsync(r => r.Id == runId
                && (r.Status == "completed" || r.Status == "failed"));
        if (run is null) return null;

        var lootItems = new List<string>();
        if (run.LootJson is not null)
        {
            var loot = JsonSerializer.Deserialize<List<string>>(run.LootJson) ?? [];
            lootItems.AddRange(loot);
        }

        var duration = run.FinishedAt.HasValue
            ? (long)(run.FinishedAt.Value - run.StartedAt).TotalMilliseconds : 0;

        return new DungeonCompleteDto(
            run.Status == "completed",
            run.TotalExp, run.TotalGold,
            lootItems, duration);
    }

    // ─── Cooldown check ──────────────────────────────────────
    public async Task<bool> IsOnCooldownAsync(long charId, int dungeonId)
    {
        return await db.DungeonCooldowns
            .AnyAsync(cd => cd.CharacterId == charId
                && cd.DungeonId == dungeonId
                && cd.UnlocksAt > DateTime.UtcNow);
    }

    // ─── Private helpers ─────────────────────────────────────
    private async Task InitFloorCombatAsync(
        long runId, int dungeonId, int floorNum, List<long> partyIds)
    {
        var floor = await db.DungeonFloors
            .FirstOrDefaultAsync(f => f.DungeonId == dungeonId
                && f.FloorNumber == floorNum);

        var enemies = new List<EnemyState>();

        if (floor?.MonstersJson is not null)
        {
            var configs = JsonSerializer.Deserialize<List<FloorMonsterConfig>>(floor.MonstersJson) ?? [];
            long fakeId = 1000 * runId + floorNum * 100;

            foreach (var cfg in configs)
            {
                var monster = await db.Monsters.FindAsync(cfg.MonsterId);
                if (monster is null) continue;
                for (int i = 0; i < cfg.Count; i++)
                    enemies.Add(new EnemyState(
                        fakeId++, monster.Id, monster.Name,
                        monster.Level, monster.HpMax, monster.HpMax,
                        monster.Atk, monster.Def, monster.Spd, true));
            }
        }
        else
        {
            // Fallback: sinh quái ngẫu nhiên theo level dungeon
            var dungeon  = await db.Dungeons.FindAsync(dungeonId);
            var monsters = await db.Monsters
                .Where(m => m.Level <= (dungeon?.MinLevel ?? 1) + 5 && !m.IsBoss)
                .Take(3)
                .ToListAsync();

            long fakeId = 1000 * runId + floorNum * 100;
            foreach (var m in monsters)
                enemies.Add(new EnemyState(
                    fakeId++, m.Id, m.Name, m.Level,
                    m.HpMax, m.HpMax, m.Atk, m.Def, m.Spd, true));
        }

        // Party HP từ DB
        var partyHp = new Dictionary<long, int>();
        foreach (var pid in partyIds)
        {
            var hp = await db.Characters
                .Where(c => c.Id == pid).Select(c => c.Hp).FirstOrDefaultAsync();
            partyHp[pid] = hp;
        }

        _combatStates[runId] = new CombatState(runId, floorNum, enemies, partyHp);
    }

    private async Task FinishFloorAsync(long runId)
    {
        logger.LogDebug("Floor cleared for run {Run}", runId);
        // Floor clear — server sẽ gửi signal qua socket
        await Task.CompletedTask;
    }

    private async Task RollMonsterLootAsync(long charId, Monster monster)
    {
        if (monster.LootJson is null) return;

        var loot = JsonSerializer.Deserialize<List<LootEntry>>(monster.LootJson) ?? [];
        var rng  = new Random();

        foreach (var entry in loot)
        {
            if (rng.NextDouble() > entry.Rate) continue;

            var item = await db.Items.FindAsync(entry.ItemId);
            if (item is null) continue;

            var maxSlot = await db.Inventories
                .Where(i => i.CharacterId == charId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new InventoryItem
            {
                CharacterId = charId, ItemId = entry.ItemId,
                Quantity = entry.Qty, Slot = maxSlot + 1,
            });
        }
    }

    private async Task<int> GetTotalAtkAsync(Character c)
    {
        var equip = await db.EquipmentSlots.FindAsync(c.Id);
        int bonus  = 0;
        if (equip?.Weapon is not null)
        {
            var invItem = await db.Inventories
                .Include(i => i.Item)
                .FirstOrDefaultAsync(i => i.Id == equip.Weapon);
            if (invItem?.Item.StatsJson is not null)
            {
                var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(invItem.Item.StatsJson) ?? [];
                bonus += stats.GetValueOrDefault("atk", 0);
            }
        }
        return c.Atk + bonus;
    }

    // Deserialization helpers
    private record FloorMonsterConfig(int MonsterId, int Count, float PosX, float PosY);
    private record LootEntry(int ItemId, double Rate, int Qty);
}
