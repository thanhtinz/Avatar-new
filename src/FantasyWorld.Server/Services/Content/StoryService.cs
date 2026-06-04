// FantasyWorld.Server/Services/Content/StoryService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Content;

public interface IStoryService
{
    Task<List<StoryProgressDto>>  GetProgressAsync(long charId, string lang);
    Task<StoryNodeDto?>           StartChapterAsync(long charId, int chapterId, string lang);
    Task<StoryNodeDto?>           GetCurrentNodeAsync(long charId, int chapterId, string lang);
    Task<StoryNodeDto?>           MakeChoiceAsync(long charId, MakeChoiceRequest req, string lang);
    Task<StoryNodeDto?>           AdvanceAsync(long charId, int chapterId, string lang);
    Task<List<CharacterStoryEnding>> GetEndingsAsync(long charId);
}

public class StoryService(
    GameDbContext        db,
    IQuestService        questSvc,
    ILocalizationService loc,
    ILogger<StoryService> logger) : IStoryService
{
    // ─── Get all chapter progress ────────────────────────────
    public async Task<List<StoryProgressDto>> GetProgressAsync(long charId, string lang)
    {
        var isVi = lang == "vi";
        var chapters = await db.StoryChapters
            .OrderBy(c => c.ChapterNumber)
            .ToListAsync();

        var progresses = await db.CharacterStoryProgress
            .Where(p => p.CharacterId == charId)
            .ToListAsync();

        return chapters.Select(c =>
        {
            var p = progresses.FirstOrDefault(x => x.ChapterId == c.Id);
            return new StoryProgressDto(
                c.Id,
                isVi ? c.Title : c.TitleEn,
                p?.Status ?? "not_started",
                p?.CurrentNodeId);
        }).ToList();
    }

    // ─── Start chapter ───────────────────────────────────────
    public async Task<StoryNodeDto?> StartChapterAsync(
        long charId, int chapterId, string lang)
    {
        var chapter = await db.StoryChapters
            .Include(c => c.Nodes)
            .FirstOrDefaultAsync(c => c.Id == chapterId);

        if (chapter is null) return null;

        // Kiểm tra prerquisite
        if (chapter.PrereqChapterId.HasValue)
        {
            var prereqDone = await db.CharacterStoryProgress
                .AnyAsync(p => p.CharacterId == charId
                    && p.ChapterId == chapter.PrereqChapterId
                    && p.Status == "completed");
            if (!prereqDone) return null;
        }

        // Kiểm tra level
        var char_ = await db.Characters.FindAsync(charId);
        if (char_?.Level < chapter.PrereqLevel) return null;

        // Tạo hoặc lấy progress
        var progress = await db.CharacterStoryProgress
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.ChapterId == chapterId);

        if (progress is null)
        {
            var firstNode = chapter.Nodes.OrderBy(n => n.Id).FirstOrDefault();
            progress = new CharacterStoryProgress
            {
                CharacterId   = charId,
                ChapterId     = chapterId,
                CurrentNodeId = firstNode?.Id,
                Status        = "in_progress",
                StartedAt     = DateTime.UtcNow,
                FlagsJson     = "{}",
            };
            db.CharacterStoryProgress.Add(progress);
            await db.SaveChangesAsync();
        }

        if (progress.CurrentNodeId is null) return null;
        return await BuildNodeDtoAsync(progress.CurrentNodeId.Value, progress, lang);
    }

    // ─── Get current node ────────────────────────────────────
    public async Task<StoryNodeDto?> GetCurrentNodeAsync(
        long charId, int chapterId, string lang)
    {
        var progress = await db.CharacterStoryProgress
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.ChapterId == chapterId);

        if (progress?.CurrentNodeId is null) return null;
        return await BuildNodeDtoAsync(progress.CurrentNodeId.Value, progress, lang);
    }

    // ─── Make choice ─────────────────────────────────────────
    public async Task<StoryNodeDto?> MakeChoiceAsync(
        long charId, MakeChoiceRequest req, string lang)
    {
        var progress = await db.CharacterStoryProgress
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.ChapterId == req.ChapterId
                && p.Status == "in_progress");

        if (progress?.CurrentNodeId is null) return null;

        var node = await db.StoryNodes.FindAsync(progress.CurrentNodeId.Value);
        if (node?.ChoicesJson is null) return null;

        var choices = JsonSerializer.Deserialize<List<NodeChoice>>(node.ChoicesJson) ?? [];
        if (req.ChoiceIndex < 0 || req.ChoiceIndex >= choices.Count) return null;

        var choice = choices[req.ChoiceIndex];

        // Kiểm tra condition flag
        if (choice.ConditionFlag is not null)
        {
            var flags = JsonSerializer.Deserialize<Dictionary<string, bool>>(progress.FlagsJson) ?? [];
            if (!flags.GetValueOrDefault(choice.ConditionFlag, false)) return null;
        }

        // Set flag từ choice
        if (choice.SetFlag is not null)
        {
            var flags = JsonSerializer.Deserialize<Dictionary<string, bool>>(progress.FlagsJson) ?? [];
            flags[choice.SetFlag] = true;
            progress.FlagsJson = JsonSerializer.Serialize(flags);
        }

        // Di chuyển đến node tiếp theo
        progress.CurrentNodeId = choice.NextNodeId;

        await db.SaveChangesAsync();

        // Kiểm tra kết thúc
        if (choice.NextNodeId is null)
            return await ResolveEndingAsync(charId, req.ChapterId, progress, lang);

        return await BuildNodeDtoAsync(choice.NextNodeId.Value, progress, lang);
    }

    // ─── Advance (auto-next) ─────────────────────────────────
    public async Task<StoryNodeDto?> AdvanceAsync(
        long charId, int chapterId, string lang)
    {
        var progress = await db.CharacterStoryProgress
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.ChapterId == chapterId && p.Status == "in_progress");

        if (progress?.CurrentNodeId is null) return null;

        var node = await db.StoryNodes.FindAsync(progress.CurrentNodeId.Value);
        if (node is null) return null;

        // Xử lý reward tại node hiện tại
        if (node.RewardJson is not null)
            await ApplyNodeRewardAsync(charId, node.RewardJson);

        // Set flag nếu có
        if (node.FlagSet is not null)
        {
            var flags = JsonSerializer.Deserialize<Dictionary<string, bool>>(progress.FlagsJson) ?? [];
            flags[node.FlagSet] = true;
            progress.FlagsJson = JsonSerializer.Serialize(flags);
        }

        // Tiến đến node tiếp theo
        if (node.NextNodeId.HasValue)
        {
            progress.CurrentNodeId = node.NextNodeId;
            await db.SaveChangesAsync();
            return await BuildNodeDtoAsync(node.NextNodeId.Value, progress, lang);
        }

        // Không có next → kết thúc chapter
        return await ResolveEndingAsync(charId, chapterId, progress, lang);
    }

    // ─── Get endings ─────────────────────────────────────────
    public async Task<List<CharacterStoryEnding>> GetEndingsAsync(long charId)
    {
        return await db.CharacterStoryEndings
            .Where(e => e.CharacterId == charId)
            .ToListAsync();
    }

    // ─── Private ─────────────────────────────────────────────
    private async Task<StoryNodeDto> BuildNodeDtoAsync(
        int nodeId, CharacterStoryProgress progress, string lang)
    {
        var node = await db.StoryNodes
            .Include(n => n.Npc)
            .FirstOrDefaultAsync(n => n.Id == nodeId);

        if (node is null)
            return new StoryNodeDto(0, "end", "", "", null, null, [], false);

        var isVi = lang == "vi";
        var content = isVi ? node.Content ?? "" : node.ContentEn ?? node.Content ?? "";

        List<StoryChoiceDto> choices = [];
        if (node.ChoicesJson is not null)
        {
            var flags = JsonSerializer.Deserialize<Dictionary<string, bool>>(progress.FlagsJson) ?? [];
            var rawChoices = JsonSerializer.Deserialize<List<NodeChoice>>(node.ChoicesJson) ?? [];
            choices = rawChoices.Select((c, i) => new StoryChoiceDto(
                i,
                isVi ? c.TextVi : c.TextEn,
                c.ConditionFlag is null || flags.GetValueOrDefault(c.ConditionFlag, false)
            )).ToList();
        }

        return new StoryNodeDto(
            node.Id, node.NodeType,
            isVi ? node.Title : node.Title,
            content,
            node.NpcId,
            node.Npc?.Name,
            choices,
            node.RewardJson is not null);
    }

    private async Task<StoryNodeDto?> ResolveEndingAsync(
        long charId, int chapterId,
        CharacterStoryProgress progress, string lang)
    {
        // Xác định kết thúc dựa vào flags
        var flags = JsonSerializer.Deserialize<Dictionary<string, bool>>(progress.FlagsJson) ?? [];

        int endingId;
        string nameVi, nameEn;
        int? titleId = null;

        if (flags.GetValueOrDefault("chose_light", false))
        {
            endingId = 1; nameVi = "Con Đường Ánh Sáng"; nameEn = "Path of Light"; titleId = 1;
        }
        else if (flags.GetValueOrDefault("chose_dark", false))
        {
            endingId = 2; nameVi = "Con Đường Bóng Tối"; nameEn = "Path of Darkness"; titleId = 2;
        }
        else
        {
            endingId = 3; nameVi = "Con Đường Trung Lập"; nameEn = "Neutral Path";
        }

        // Ghi kết thúc
        var existingEnding = await db.CharacterStoryEndings
            .AnyAsync(e => e.CharacterId == charId && e.ChapterId == chapterId);

        if (!existingEnding)
        {
            db.CharacterStoryEndings.Add(new CharacterStoryEnding
            {
                CharacterId     = charId,
                ChapterId       = chapterId,
                EndingId        = endingId,
                EndingName      = nameVi,
                EndingNameEn    = nameEn,
                UnlockedTitleId = titleId,
            });

            if (titleId.HasValue)
                await questSvc.CheckAndGrantTitleAsync(charId);
        }

        progress.Status      = "completed";
        progress.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Story chapter completed: charId={Char} chapter={Ch} ending={E}",
            charId, chapterId, endingId);

        // Trả về "end" node
        return new StoryNodeDto(
            0, "end",
            lang == "vi" ? nameVi : nameEn,
            lang == "vi"
                ? $"Chương đã kết thúc với kết thúc: {nameVi}"
                : $"Chapter ended with: {nameEn}",
            null, null, [], false);
    }

    private async Task ApplyNodeRewardAsync(long charId, string rewardJson)
    {
        var reward = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rewardJson) ?? [];

        if (reward.TryGetValue("gold", out var gold))
            await db.Characters.Where(c => c.Id == charId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Gold, c => c.Gold + gold.GetInt32()));

        if (reward.TryGetValue("exp", out var exp))
            await db.Characters.Where(c => c.Id == charId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Exp, c => c.Exp + exp.GetInt32()));
    }

    // Records for JSON deserialization
    private record NodeChoice(
        string TextVi, string TextEn,
        int? NextNodeId,
        string? SetFlag,
        string? ConditionFlag);
}
