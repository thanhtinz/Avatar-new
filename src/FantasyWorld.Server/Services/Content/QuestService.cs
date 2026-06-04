// FantasyWorld.Server/Services/Content/QuestService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Content;

public interface IQuestService
{
    Task<List<QuestDto>>           GetAvailableAsync(long charId, string lang);
    Task<List<QuestDto>>           GetActiveAsync(long charId, string lang);
    Task<(bool Ok, string Msg)>    AcceptAsync(long charId, int questId, string lang);
    Task<(bool Ok, string Msg)>    AbandonAsync(long charId, int questId, string lang);
    Task<(bool Ok, string Msg, QuestRewardDto? Reward)> CompleteAsync(long charId, int questId, string lang);
    Task UpdateObjectiveAsync(long charId, string objectiveType, int targetId, int amount);
    Task<List<QuestDto>>           GetCompletedAsync(long charId, string lang);
    Task CheckAndGrantTitleAsync(long charId);
    Task CheckAndGrantAchievementAsync(long charId, string category, int value);
}

public class QuestService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<QuestService> logger) : IQuestService
{
    // ─── Get available ───────────────────────────────────────
    public async Task<List<QuestDto>> GetAvailableAsync(long charId, string lang)
    {
        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_ is null) return [];

        // Quests đã nhận hoặc hoàn thành
        var takenIds = await db.CharacterQuests
            .Where(cq => cq.CharacterId == charId && cq.Status != "cooldown")
            .Select(cq => cq.QuestId)
            .ToListAsync();

        // Lấy quests phù hợp level
        var quests = await db.Quests
            .Include(q => q.CharacterQuests)
            .Where(q => !takenIds.Contains(q.Id) && q.MinLevel <= char_!.Level)
            .ToListAsync();

        var result = new List<QuestDto>();
        foreach (var q in quests)
        {
            if (await CheckPrerequisitesAsync(charId, q.Id))
                result.Add(await MapQuestToDtoAsync(q, null, lang));
        }
        return result;
    }

    // ─── Get active ──────────────────────────────────────────
    public async Task<List<QuestDto>> GetActiveAsync(long charId, string lang)
    {
        var active = await db.CharacterQuests
            .Where(cq => cq.CharacterId == charId && cq.Status == "active")
            .Include(cq => cq.Quest)
            .Include(cq => cq.Objectives).ThenInclude(o => o.Objective)
            .ToListAsync();

        var result = new List<QuestDto>();
        foreach (var cq in active)
            result.Add(await MapQuestToDtoAsync(cq.Quest, cq, lang));
        return result;
    }

    // ─── Accept ──────────────────────────────────────────────
    public async Task<(bool, string)> AcceptAsync(long charId, int questId, string lang)
    {
        var quest = await db.Quests
            .Include(q => q.CharacterQuests.Where(cq => cq.CharacterId == charId))
            .FirstOrDefaultAsync(q => q.Id == questId);

        if (quest is null) return (false, loc.Get("error.not_found", lang));

        var existing = quest.CharacterQuests.FirstOrDefault();
        if (existing is not null && existing.Status == "active")
            return (false, loc.Get("error.bad_request", lang));

        if (!await CheckPrerequisitesAsync(charId, questId))
            return (false, loc.Get("error.forbidden", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Level < quest.MinLevel)
            return (false, loc.Get("error.bad_request", lang));

        // Tạo CharacterQuest
        var charQuest = new CharacterQuest
        {
            CharacterId = charId,
            QuestId     = questId,
            Status      = "active",
            AcceptedAt  = DateTime.UtcNow,
        };
        db.CharacterQuests.Add(charQuest);
        await db.SaveChangesAsync();

        // Tạo objective trackers
        var objectives = await db.QuestObjectives
            .Where(o => o.QuestId == questId)
            .ToListAsync();

        foreach (var obj in objectives)
        {
            db.CharacterQuestObjectives.Add(new CharacterQuestObjective
            {
                CharQuestId  = charQuest.Id,
                ObjectiveId  = obj.Id,
                CurrentCount = 0,
                IsCompleted  = false,
            });
        }
        await db.SaveChangesAsync();

        logger.LogInformation("Quest accepted: charId={Char} questId={Quest}", charId, questId);
        return (true, loc.Get("quest.accepted", lang, new { name = quest.Name }));
    }

    // ─── Abandon ─────────────────────────────────────────────
    public async Task<(bool, string)> AbandonAsync(long charId, int questId, string lang)
    {
        var cq = await db.CharacterQuests
            .FirstOrDefaultAsync(q => q.CharacterId == charId
                && q.QuestId == questId && q.Status == "active");

        if (cq is null) return (false, loc.Get("error.not_found", lang));

        cq.Status = "failed";
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Complete ────────────────────────────────────────────
    public async Task<(bool, string, QuestRewardDto?)> CompleteAsync(
        long charId, int questId, string lang)
    {
        var cq = await db.CharacterQuests
            .Include(q => q.Objectives).ThenInclude(o => o.Objective)
            .FirstOrDefaultAsync(q => q.CharacterId == charId
                && q.QuestId == questId && q.Status == "active");

        if (cq is null) return (false, loc.Get("error.not_found", lang), null);

        // Kiểm tra tất cả objectives bắt buộc
        var incomplete = cq.Objectives
            .Where(o => !o.Objective.IsOptional && !o.IsCompleted)
            .ToList();

        if (incomplete.Any())
            return (false, loc.Get("quest.objectives_incomplete", lang), null);

        // Lấy rewards
        var rewardRow = await db.QuestRewards
            .FirstOrDefaultAsync(r => r.QuestId == questId);

        // Trao thưởng
        var char_ = await db.Characters.FindAsync(charId)!;
        if (rewardRow is not null)
        {
            char_!.Gold += rewardRow.Gold;
            char_.Exp   += rewardRow.Exp;

            // Item
            if (rewardRow.ItemId.HasValue)
            {
                var item = await db.Items.FindAsync(rewardRow.ItemId.Value);
                await AddItemToInventoryAsync(charId, rewardRow.ItemId.Value,
                    rewardRow.ItemQty, item?.MaxStack ?? 99);
            }

            // Skill
            if (rewardRow.SkillId.HasValue)
            {
                var hasSkill = await db.CharacterSkills
                    .AnyAsync(s => s.CharacterId == charId && s.SkillId == rewardRow.SkillId);
                if (!hasSkill)
                    db.CharacterSkills.Add(new CharacterSkill
                    {
                        CharacterId = charId,
                        SkillId     = rewardRow.SkillId.Value,
                    });
            }

            // Title
            if (rewardRow.TitleId.HasValue)
                await GrantTitleAsync(charId, rewardRow.TitleId.Value);

            // Reputation
            if (rewardRow.ReputationRegionId.HasValue && rewardRow.ReputationPoints > 0)
                await AddReputationAsync(charId, rewardRow.ReputationRegionId.Value,
                    rewardRow.ReputationPoints);
        }

        cq.Status      = "completed";
        cq.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Level up check
        await CheckLevelUpAsync(charId);

        // Achievement check
        await CheckAndGrantAchievementAsync(charId, "quest", 1);

        logger.LogInformation("Quest completed: charId={Char} questId={Quest}", charId, questId);

        var quest = await db.Quests.FindAsync(questId)!;
        var rewardDto = rewardRow is null ? new QuestRewardDto(0, 0, null, null, 0, null, null, 0)
            : new QuestRewardDto(
                rewardRow.Gold, rewardRow.Exp,
                rewardRow.TitleId.HasValue ? await GetTitleNameAsync(rewardRow.TitleId.Value, lang) : null,
                rewardRow.ItemId.HasValue  ? await GetItemNameAsync(rewardRow.ItemId.Value) : null,
                rewardRow.ItemQty,
                rewardRow.SkillId.HasValue ? await GetSkillNameAsync(rewardRow.SkillId.Value) : null,
                rewardRow.ReputationRegionId.HasValue ? await GetRegionNameAsync(rewardRow.ReputationRegionId.Value) : null,
                rewardRow.ReputationPoints);

        return (true, loc.Get("quest.completed", lang, new { name = quest!.Name }), rewardDto);
    }

    // ─── Update objective progress ───────────────────────────
    public async Task UpdateObjectiveAsync(
        long charId, string objectiveType, int targetId, int amount)
    {
        // Tìm tất cả quests active của nhân vật
        var activeQuests = await db.CharacterQuests
            .Where(cq => cq.CharacterId == charId && cq.Status == "active")
            .Include(cq => cq.Objectives).ThenInclude(o => o.Objective)
            .ToListAsync();

        bool anyUpdated = false;
        foreach (var cq in activeQuests)
        {
            foreach (var obj in cq.Objectives
                .Where(o => !o.IsCompleted
                    && o.Objective.ObjectiveType == objectiveType
                    && (o.Objective.TargetId == targetId || o.Objective.TargetId == 0)))
            {
                obj.CurrentCount = Math.Min(
                    obj.CurrentCount + amount,
                    obj.Objective.TargetCount);

                if (obj.CurrentCount >= obj.Objective.TargetCount)
                    obj.IsCompleted = true;

                anyUpdated = true;
            }

            // Cập nhật progress JSON
            cq.ProgressJson = JsonSerializer.Serialize(
                cq.Objectives.ToDictionary(
                    o => o.ObjectiveId.ToString(),
                    o => o.CurrentCount));
        }

        if (anyUpdated)
            await db.SaveChangesAsync();
    }

    // ─── Get completed ───────────────────────────────────────
    public async Task<List<QuestDto>> GetCompletedAsync(long charId, string lang)
    {
        var completed = await db.CharacterQuests
            .Where(cq => cq.CharacterId == charId && cq.Status == "completed")
            .Include(cq => cq.Quest)
            .OrderByDescending(cq => cq.CompletedAt)
            .Take(50)
            .ToListAsync();

        return await Task.WhenAll(completed
            .Select(cq => MapQuestToDtoAsync(cq.Quest, cq, lang)));
    }

    // ─── Title / Achievement grants ──────────────────────────
    public async Task CheckAndGrantTitleAsync(long charId)
    {
        var char_ = await db.Characters.FindAsync(charId);
        if (char_ is null) return;

        var allTitles = await db.Titles.ToListAsync();
        var owned = await db.CharacterTitles
            .Where(ct => ct.CharacterId == charId)
            .Select(ct => ct.TitleId)
            .ToListAsync();

        foreach (var title in allTitles.Where(t => !owned.Contains(t.Id)))
        {
            if (title.ConditionJson is null) continue;
            var cond = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(title.ConditionJson);
            if (cond is null) continue;

            bool earned = cond.TryGetValue("type", out var typeEl) && cond.TryGetValue("value", out var valEl)
                && typeEl.GetString() switch
                {
                    "level"     => char_.Level >= valEl.GetInt32(),
                    "pet_count" => await db.Pets.CountAsync(p => p.OwnerId == charId) >= valEl.GetInt32(),
                    "card_count"=> await db.CharacterCards.CountAsync(c => c.CharacterId == charId) >= valEl.GetInt32(),
                    _           => false
                };

            if (earned)
                await GrantTitleAsync(charId, title.Id);
        }
    }

    public async Task CheckAndGrantAchievementAsync(long charId, string category, int value)
    {
        var achievements = await db.Achievements
            .Where(a => a.Category == category)
            .ToListAsync();

        foreach (var ach in achievements)
        {
            var ca = await db.CharacterAchievements
                .FirstOrDefaultAsync(x => x.CharacterId == charId
                    && x.AchievementId == ach.Id);

            if (ca is null)
            {
                var cond = ach.ConditionJson is null ? null
                    : JsonSerializer.Deserialize<Dictionary<string, int>>(ach.ConditionJson);
                var target = cond?.GetValueOrDefault("value", 1) ?? 1;

                ca = new CharacterAchievement
                {
                    CharacterId   = charId,
                    AchievementId = ach.Id,
                    Progress      = value,
                    Target        = target,
                    IsCompleted   = value >= target,
                    CompletedAt   = value >= target ? DateTime.UtcNow : null,
                };
                db.CharacterAchievements.Add(ca);
            }
            else if (!ca.IsCompleted)
            {
                ca.Progress += value;
                if (ca.Progress >= ca.Target)
                {
                    ca.IsCompleted  = true;
                    ca.CompletedAt  = DateTime.UtcNow;
                    logger.LogInformation("Achievement unlocked: charId={Char} ach={Ach}",
                        charId, ach.Name);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    // ─── Private helpers ─────────────────────────────────────
    private async Task<bool> CheckPrerequisitesAsync(long charId, int questId)
    {
        var prereqs = await db.QuestPrerequisites
            .Where(p => p.QuestId == questId)
            .ToListAsync();

        foreach (var p in prereqs)
        {
            bool met = p.PrereqType switch
            {
                "quest" => await db.CharacterQuests.AnyAsync(cq =>
                    cq.CharacterId == charId && cq.QuestId == p.PrereqId
                    && cq.Status == "completed"),
                "level" => (await db.Characters.FindAsync(charId))?.Level >= p.PrereqValue,
                _ => true
            };
            if (!met) return false;
        }
        return true;
    }

    private async Task GrantTitleAsync(long charId, int titleId)
    {
        var exists = await db.CharacterTitles
            .AnyAsync(ct => ct.CharacterId == charId && ct.TitleId == titleId);
        if (exists) return;

        db.CharacterTitles.Add(new CharacterTitle
        {
            CharacterId = charId,
            TitleId     = titleId,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Title granted: charId={Char} titleId={Title}", charId, titleId);
    }

    private async Task AddReputationAsync(long charId, int regionId, int points)
    {
        var rep = await db.CharacterReputations
            .FirstOrDefaultAsync(r => r.CharacterId == charId && r.RegionId == regionId);

        if (rep is null)
        {
            rep = new CharacterReputation
            {
                CharacterId = charId,
                RegionId    = regionId,
                Points      = 0,
            };
            db.CharacterReputations.Add(rep);
        }

        rep.Points += points;
        rep.Rank = rep.Points switch
        {
            >= 21000 => "exalted",
            >= 12000 => "revered",
            >= 6000  => "honored",
            >= 3000  => "friendly",
            >= 1000  => "familiar",
            _        => "unknown",
        };
        await db.SaveChangesAsync();
    }

    private async Task CheckLevelUpAsync(long charId)
    {
        var c = await db.Characters.FindAsync(charId)!;
        if (c is null) return;

        // Simple EXP table check — detailed in CharacterService
        var expNeeded = (long)Math.Floor(100 * Math.Pow(c.Level, 1.5));
        while (c.Level < 200 && c.Exp >= expNeeded)
        {
            c.Exp  -= expNeeded;
            c.Level++;
            expNeeded = (long)Math.Floor(100 * Math.Pow(c.Level, 1.5));

            // HP/MP scale on level up
            c.HpMax += 10;
            c.MpMax += 5;
            c.Hp     = c.HpMax;
            c.Mp     = c.MpMax;
        }
        await db.SaveChangesAsync();
    }

    private async Task AddItemToInventoryAsync(long charId, int itemId, int qty, int maxStack)
    {
        var existing = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == itemId && i.Quantity < maxStack);
        if (existing is not null) { existing.Quantity += qty; return; }

        var maxSlot = await db.Inventories
            .Where(i => i.CharacterId == charId)
            .MaxAsync(i => (int?)i.Slot) ?? -1;
        db.Inventories.Add(new InventoryItem
        {
            CharacterId = charId, ItemId = itemId,
            Quantity = qty, Slot = maxSlot + 1,
        });
    }

    private async Task<QuestDto> MapQuestToDtoAsync(
        Quest q, CharacterQuest? cq, string lang)
    {
        var isVi = lang == "vi";
        var objectives = cq is not null
            ? cq.Objectives.Select(o => new QuestObjectiveDto(
                o.ObjectiveId,
                o.Objective.ObjectiveType,
                isVi ? o.Objective.Description : o.Objective.DescriptionEn,
                o.CurrentCount,
                o.Objective.TargetCount,
                o.IsCompleted,
                o.Objective.IsOptional)).ToList()
            : new List<QuestObjectiveDto>();

        var reward = await db.QuestRewards.FirstOrDefaultAsync(r => r.QuestId == q.Id);
        var rewardDto = reward is null
            ? new QuestRewardDto(0, 0, null, null, 0, null, null, 0)
            : new QuestRewardDto(reward.Gold, reward.Exp,
                null, null, reward.ItemQty, null,
                reward.ReputationRegionId.HasValue
                    ? await GetRegionNameAsync(reward.ReputationRegionId.Value) : null,
                reward.ReputationPoints);

        return new QuestDto(
            q.Id, q.Name, q.QuestType,
            isVi ? q.Description ?? "" : q.Description ?? "",
            cq?.Status ?? "available",
            objectives, rewardDto,
            q.MinLevel, q.IsRepeatable,
            cq?.AcceptedAt is null ? null
                : new DateTimeOffset(cq.AcceptedAt).ToUnixTimeMilliseconds());
    }

    private async Task<string?> GetTitleNameAsync(int id, string lang)
    {
        var t = await db.Titles.FindAsync(id);
        return t is null ? null : (lang == "vi" ? t.Name : t.NameEn);
    }
    private async Task<string?> GetItemNameAsync(int id)
        => (await db.Items.FindAsync(id))?.Name;
    private async Task<string?> GetSkillNameAsync(int id)
        => (await db.Skills.FindAsync(id))?.Name;
    private async Task<string?> GetRegionNameAsync(int id)
        => (await db.Regions.FindAsync(id))?.Name;
}
