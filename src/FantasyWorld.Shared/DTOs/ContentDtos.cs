// FantasyWorld.Shared/DTOs/ContentDtos.cs
using MessagePack;

namespace FantasyWorld.Shared.DTOs;

// ─── Quest ───────────────────────────────────────────────────

[MessagePackObject]
public record QuestDto(
    [property: Key(0)]  int    Id,
    [property: Key(1)]  string Name,
    [property: Key(2)]  string QuestType,
    [property: Key(3)]  string Description,
    [property: Key(4)]  string Status,
    [property: Key(5)]  List<QuestObjectiveDto> Objectives,
    [property: Key(6)]  QuestRewardDto Reward,
    [property: Key(7)]  int    MinLevel,
    [property: Key(8)]  bool   IsRepeatable,
    [property: Key(9)]  long?  AcceptedAtMs
);

[MessagePackObject]
public record QuestObjectiveDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Type,
    [property: Key(2)] string Description,
    [property: Key(3)] int    Current,
    [property: Key(4)] int    Target,
    [property: Key(5)] bool   IsCompleted,
    [property: Key(6)] bool   IsOptional
);

[MessagePackObject]
public record QuestRewardDto(
    [property: Key(0)] int    Gold,
    [property: Key(1)] int    Exp,
    [property: Key(2)] string? TitleName,
    [property: Key(3)] string? ItemName,
    [property: Key(4)] int    ItemQty,
    [property: Key(5)] string? SkillName,
    [property: Key(6)] string? ReputationRegion,
    [property: Key(7)] int    ReputationPoints
);

[MessagePackObject]
public record AcceptQuestRequest([property: Key(0)] int QuestId);

[MessagePackObject]
public record QuestProgressUpdate(
    [property: Key(0)] int  QuestId,
    [property: Key(1)] int  ObjectiveId,
    [property: Key(2)] int  Increment
);

// ─── Academy ─────────────────────────────────────────────────

[MessagePackObject]
public record AcademyDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Element,
    [property: Key(3)] string Description,
    [property: Key(4)] int    MemberCount,
    [property: Key(5)] bool   IsEnrolled,
    [property: Key(6)] int    MyGrade,
    [property: Key(7)] int    MyRankPoints
);

[MessagePackObject]
public record AcademyExamDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Difficulty,
    [property: Key(3)] int    MinGrade,
    [property: Key(4)] bool   CanTake,
    [property: Key(5)] long?  CooldownUntilMs
);

[MessagePackObject]
public record ExamQuestionDto(
    [property: Key(0)] int    QuestionIndex,
    [property: Key(1)] string Question,
    [property: Key(2)] List<string> Options
);

[MessagePackObject]
public record SubmitExamRequest(
    [property: Key(0)] int      ExamId,
    [property: Key(1)] List<int> Answers   // answer index per question
);

[MessagePackObject]
public record ExamResultDto(
    [property: Key(0)] int  Score,
    [property: Key(1)] bool Passed,
    [property: Key(2)] int  CorrectCount,
    [property: Key(3)] int  TotalQuestions,
    [property: Key(4)] int  NewGrade,
    [property: Key(5)] int  RankPointsEarned
);

[MessagePackObject]
public record AcademyRankDto(
    [property: Key(0)] int    Rank,
    [property: Key(1)] long   CharId,
    [property: Key(2)] string CharName,
    [property: Key(3)] int    Grade,
    [property: Key(4)] int    RankPoints,
    [property: Key(5)] int    ExamPasses
);

// ─── Dungeon ─────────────────────────────────────────────────

[MessagePackObject]
public record DungeonEnterRequest(
    [property: Key(0)] int  DungeonId,
    [property: Key(1)] long PartyId
);

[MessagePackObject]
public record DungeonFloorDto(
    [property: Key(0)] int    FloorNumber,
    [property: Key(1)] string Name,
    [property: Key(2)] bool   HasBoss,
    [property: Key(3)] bool   HasTraps,
    [property: Key(4)] bool   HasPuzzle,
    [property: Key(5)] List<FloorEnemyDto> Enemies
);

[MessagePackObject]
public record FloorEnemyDto(
    [property: Key(0)] int    MonsterId,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] int    Hp,
    [property: Key(4)] float  PosX,
    [property: Key(5)] float  PosY
);

[MessagePackObject]
public record CombatActionRequest(
    [property: Key(0)] long   RunId,
    [property: Key(1)] string ActionType,  // attack|skill|item|flee
    [property: Key(2)] long   TargetId,
    [property: Key(3)] int?   SkillId,
    [property: Key(4)] int?   ItemInventoryId
);

[MessagePackObject]
public record CombatResultDto(
    [property: Key(0)] string  ActionType,
    [property: Key(1)] long    AttackerId,
    [property: Key(2)] long    DefenderId,
    [property: Key(3)] int     Damage,
    [property: Key(4)] bool    IsCrit,
    [property: Key(5)] bool    DefenderDied,
    [property: Key(6)] int     AttackerHpLeft,
    [property: Key(7)] int     DefenderHpLeft,
    [property: Key(8)] string? StatusEffect
);

[MessagePackObject]
public record DungeonCompleteDto(
    [property: Key(0)] bool          Success,
    [property: Key(1)] int           ExpEarned,
    [property: Key(2)] int           GoldEarned,
    [property: Key(3)] List<string>  ItemsDropped,
    [property: Key(4)] long          DurationMs
);

// ─── Story ───────────────────────────────────────────────────

[MessagePackObject]
public record StoryNodeDto(
    [property: Key(0)] int    NodeId,
    [property: Key(1)] string NodeType,
    [property: Key(2)] string Title,
    [property: Key(3)] string Content,
    [property: Key(4)] int?   NpcId,
    [property: Key(5)] string? NpcName,
    [property: Key(6)] List<StoryChoiceDto> Choices,
    [property: Key(7)] bool   HasReward
);

[MessagePackObject]
public record StoryChoiceDto(
    [property: Key(0)] int    Index,
    [property: Key(1)] string Text,
    [property: Key(2)] bool   IsAvailable   // có thể bị lock bởi condition_flag
);

[MessagePackObject]
public record MakeChoiceRequest(
    [property: Key(0)] int ChapterId,
    [property: Key(1)] int ChoiceIndex
);

[MessagePackObject]
public record StoryProgressDto(
    [property: Key(0)] int    ChapterId,
    [property: Key(1)] string ChapterTitle,
    [property: Key(2)] string Status,
    [property: Key(3)] int?   CurrentNodeId
);

// ─── Title / Achievement ─────────────────────────────────────

[MessagePackObject]
public record TitleDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Description,
    [property: Key(3)] bool   IsActive,
    [property: Key(4)] long   EarnedAtMs
);

[MessagePackObject]
public record AchievementDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Category,
    [property: Key(3)] int    Progress,
    [property: Key(4)] int    Target,
    [property: Key(5)] bool   IsCompleted,
    [property: Key(6)] int    Points,
    [property: Key(7)] string? BadgeIcon
);
