// FantasyWorld.Server/Data/Entities/ContentEntities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Data.Entities;

// ─── Quest System ────────────────────────────────────────────

[Table("quest_objectives")]
public class QuestObjective
{
    [Key] public int Id { get; set; }
    public int    QuestId      { get; set; }
    public int    OrderNum     { get; set; }
    [MaxLength(32)] public string ObjectiveType { get; set; } = "kill";
    // kill|collect|talk|reach_map|craft|fish|catch_pet|use_skill|escort|deliver
    public int?   TargetId     { get; set; }    // monster_id / item_id / npc_id / map_id
    public int    TargetCount  { get; set; } = 1;
    [MaxLength(128)] public string Description { get; set; } = "";
    [MaxLength(128)] public string DescriptionEn { get; set; } = "";
    public bool   IsOptional   { get; set; }

    public Quest Quest { get; set; } = null!;
}

[Table("quest_rewards")]
public class QuestReward
{
    [Key] public int Id { get; set; }
    public int  QuestId    { get; set; }
    public int  Gold       { get; set; }
    public int  Exp        { get; set; }
    public int? TitleId    { get; set; }
    public int? ItemId     { get; set; }
    public int  ItemQty    { get; set; } = 1;
    public int? SkillId    { get; set; }
    public int? ReputationRegionId  { get; set; }
    public int  ReputationPoints    { get; set; }

    public Quest Quest { get; set; } = null!;
}

[Table("quest_prerequisites")]
public class QuestPrerequisite
{
    [Key] public int Id { get; set; }
    public int QuestId      { get; set; }
    [MaxLength(32)] public string PrereqType { get; set; } = "quest"; // quest|level|reputation|faction|item
    public int PrereqId     { get; set; }
    public int PrereqValue  { get; set; }

    public Quest Quest { get; set; } = null!;
}

[Table("character_quest_objectives")]
public class CharacterQuestObjective
{
    [Key] public long Id { get; set; }
    public long CharQuestId  { get; set; }
    public int  ObjectiveId  { get; set; }
    public int  CurrentCount { get; set; }
    public bool IsCompleted  { get; set; }

    public CharacterQuest  CharQuest  { get; set; } = null!;
    public QuestObjective  Objective  { get; set; } = null!;
}

// ─── Academy System ──────────────────────────────────────────

[Table("academy_exams")]
public class AcademyExam
{
    [Key] public int Id { get; set; }
    public int  AcademyId     { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int  Difficulty    { get; set; } = 1;    // 1-5
    public string? QuestionsJson { get; set; }       // [{q, options[], answer_idx}]
    public string? RewardJson  { get; set; }
    public int  CooldownHours { get; set; } = 24;
    public int  MinGrade      { get; set; } = 1;    // cần grade tối thiểu để thi

    public Academy Academy { get; set; } = null!;
    public ICollection<AcademyExamResult> Results { get; set; } = [];
}

[Table("academy_exam_results")]
public class AcademyExamResult
{
    [Key] public long Id { get; set; }
    public long  CharacterId { get; set; }
    public int   ExamId      { get; set; }
    public int   Score       { get; set; }      // 0-100
    public bool  Passed      { get; set; }
    public DateTime TakenAt  { get; set; } = DateTime.UtcNow;

    public Character   Character { get; set; } = null!;
    public AcademyExam Exam      { get; set; } = null!;
}

[Table("academy_tournaments")]
public class AcademyTournament
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int  AcademyAId   { get; set; }
    public int  AcademyBId   { get; set; }
    public int? WinnerId     { get; set; }
    public DateTime HeldAt   { get; set; }
    public string? ScoresJson { get; set; }    // {"fire":1520,"ice":980}
    [MaxLength(16)] public string Status { get; set; } = "scheduled"; // scheduled|ongoing|finished

    public Academy AcademyA { get; set; } = null!;
    public Academy AcademyB { get; set; } = null!;
}

[Table("academy_tournament_participants")]
public class AcademyTournamentParticipant
{
    [Key] public long Id { get; set; }
    public int  TournamentId { get; set; }
    public long CharacterId  { get; set; }
    public int  Score        { get; set; }
    public int  Rank         { get; set; }
    public bool RewardClaimed { get; set; }

    public AcademyTournament Tournament { get; set; } = null!;
    public Character          Character { get; set; } = null!;
}

// ─── Dungeon Combat ──────────────────────────────────────────

[Table("dungeon_floors")]
public class DungeonFloor
{
    [Key] public int Id { get; set; }
    public int  DungeonId    { get; set; }
    public int  FloorNumber  { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public string? MonstersJson  { get; set; }  // [{monster_id, count, pos_x, pos_y}]
    public string? TrapJson      { get; set; }  // [{type, pos_x, pos_y, damage}]
    public string? PuzzleJson    { get; set; }  // {type, solution}
    public bool   HasBoss        { get; set; }
    public int?   BossId         { get; set; }
    public string? LootJson      { get; set; }

    public Dungeon Dungeon { get; set; } = null!;
}

[Table("dungeon_cooldowns")]
public class DungeonCooldown
{
    [Key] public long Id { get; set; }
    public long CharacterId  { get; set; }
    public int  DungeonId    { get; set; }
    public DateTime UnlocksAt { get; set; }

    public Character Character { get; set; } = null!;
    public Dungeon   Dungeon   { get; set; } = null!;
}

// ─── Story System ────────────────────────────────────────────

[Table("story_chapters")]
public class StoryChapter
{
    [Key] public int Id { get; set; }
    public int    ChapterNumber  { get; set; }
    [MaxLength(128)] public string Title   { get; set; } = "";
    [MaxLength(128)] public string TitleEn { get; set; } = "";
    public string? Description   { get; set; }
    public string? DescriptionEn { get; set; }
    public int?   PrereqChapterId { get; set; }
    public int    PrereqLevel    { get; set; } = 1;
    public bool   IsMainStory    { get; set; } = true;

    public StoryChapter? PrereqChapter { get; set; }
    public ICollection<StoryNode> Nodes { get; set; } = [];
    public ICollection<CharacterStoryProgress> Progress { get; set; } = [];
}

[Table("story_nodes")]
public class StoryNode
{
    [Key] public int Id { get; set; }
    public int    ChapterId   { get; set; }
    [MaxLength(32)] public string NodeType { get; set; } = "dialogue";
    // dialogue|choice|battle|cutscene|reward|end|branch_check
    [MaxLength(128)] public string Title    { get; set; } = "";
    public string? Content      { get; set; }   // Vi text
    public string? ContentEn    { get; set; }   // En text
    public int?   NpcId         { get; set; }
    public string? ChoicesJson  { get; set; }
    // [{"text_vi":"...","text_en":"...","next_node_id":5,"flag":"chose_a","condition_flag":"prev_flag"}]
    public string? RewardJson   { get; set; }
    [MaxLength(64)] public string? FlagSet  { get; set; }  // flag được set khi đến node này
    public int?   NextNodeId    { get; set; }   // auto-next nếu không có choices

    public StoryChapter Chapter { get; set; } = null!;
    public Npc? Npc             { get; set; }
}

[Table("character_story_progress")]
public class CharacterStoryProgress
{
    [Key] public long Id { get; set; }
    public long  CharacterId    { get; set; }
    public int   ChapterId      { get; set; }
    public int?  CurrentNodeId  { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "not_started"; // not_started|in_progress|completed
    public string FlagsJson     { get; set; } = "{}"; // {"chose_a":true, "helped_npc":false}
    public DateTime? StartedAt  { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Character     Character     { get; set; } = null!;
    public StoryChapter  Chapter       { get; set; } = null!;
    public StoryNode?    CurrentNode   { get; set; }
}

[Table("character_story_endings")]
public class CharacterStoryEnding
{
    [Key] public long Id { get; set; }
    public long CharacterId    { get; set; }
    public int  EndingId       { get; set; }
    [MaxLength(64)] public string EndingName   { get; set; } = "";
    [MaxLength(64)] public string EndingNameEn { get; set; } = "";
    public int? UnlockedTitleId { get; set; }
    public DateTime UnlockedAt  { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
}

// ─── Title / Achievement ─────────────────────────────────────

[Table("titles")]
public class Title
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name   { get; set; } = "";
    [MaxLength(64)] public string NameEn { get; set; } = "";
    public string? Description   { get; set; }
    public string? DescriptionEn { get; set; }
    public string? ConditionJson { get; set; }  // {"type":"level","value":10}
    public string? BonusJson     { get; set; }  // {"atk":2}

    public ICollection<CharacterTitle> CharacterTitles { get; set; } = [];
}

[Table("character_titles")]
public class CharacterTitle
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  TitleId     { get; set; }
    public bool IsActive    { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
    public Title     Title     { get; set; } = null!;
}

[Table("achievements")]
public class Achievement
{
    [Key] public int Id { get; set; }
    [MaxLength(128)] public string Name   { get; set; } = "";
    [MaxLength(128)] public string NameEn { get; set; } = "";
    public string? Description   { get; set; }
    public string? DescriptionEn { get; set; }
    [MaxLength(32)]  public string Category    { get; set; } = "general";
    public string? ConditionJson { get; set; }
    public string? RewardJson    { get; set; }
    public int Points            { get; set; } = 10;
    [MaxLength(64)] public string? BadgeIcon   { get; set; }
}

[Table("character_achievements")]
public class CharacterAchievement
{
    [Key] public long Id { get; set; }
    public long  CharacterId   { get; set; }
    public int   AchievementId { get; set; }
    public int   Progress      { get; set; }  // current progress value
    public int   Target        { get; set; }  // target value
    public bool  IsCompleted   { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Character   Character   { get; set; } = null!;
    public Achievement Achievement { get; set; } = null!;
}

// ─── Reputation ──────────────────────────────────────────────

[Table("character_reputation")]
public class CharacterReputation
{
    [Key] public long Id { get; set; }
    public long  CharacterId { get; set; }
    public int   RegionId    { get; set; }
    public int   Points      { get; set; }
    [MaxLength(16)] public string Rank { get; set; } = "unknown";
    // unknown|familiar|friendly|honored|revered|exalted

    public Character Character { get; set; } = null!;
    public Region    Region    { get; set; } = null!;
}
