// FantasyWorld.Server/Data/GameDbContext.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data.Entities;

namespace FantasyWorld.Server.Data;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    // Core
    public DbSet<Account>        Accounts        => Set<Account>();
    public DbSet<Session>        Sessions        => Set<Session>();
    public DbSet<Character>      Characters      => Set<Character>();
    public DbSet<EquipmentSlot>  EquipmentSlots  => Set<EquipmentSlot>();

    // Items
    public DbSet<Item>          Items         => Set<Item>();
    public DbSet<InventoryItem> Inventories   => Set<InventoryItem>();

    // World
    public DbSet<Map>    Maps    => Set<Map>();
    public DbSet<Portal> Portals => Set<Portal>();
    public DbSet<Region> Regions => Set<Region>();

    // Pets
    public DbSet<PetSpecies> PetSpecies => Set<PetSpecies>();
    public DbSet<Pet>        Pets       => Set<Pet>();

    // Social
    public DbSet<Clan>      Clans      => Set<Clan>();
    public DbSet<ClanMember> ClanMembers => Set<ClanMember>();
    public DbSet<Message>   Messages   => Set<Message>();

    // Academy / Faction
    public DbSet<Academy>         Academies         => Set<Academy>();
    public DbSet<CharacterAcademy> CharacterAcademies => Set<CharacterAcademy>();
    public DbSet<Faction>          Factions          => Set<Faction>();
    public DbSet<CharacterFaction> CharacterFactions  => Set<CharacterFaction>();

    // Skills / Quests
    public DbSet<Skill>         Skills          => Set<Skill>();
    public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
    public DbSet<Quest>          Quests          => Set<Quest>();
    public DbSet<CharacterQuest> CharacterQuests  => Set<CharacterQuest>();

    // Events
    public DbSet<Festival>            Festivals           => Set<Festival>();
    public DbSet<WorldEvent>          WorldEvents         => Set<WorldEvent>();
    public DbSet<WorldEventInstance>  WorldEventInstances => Set<WorldEventInstance>();

    // Economy
    public DbSet<Transaction> Transactions => Set<Transaction>();

    // Stats
    public DbSet<CharacterFameStat> CharacterFameStats => Set<CharacterFameStat>();
    public DbSet<FishingRecord>     FishingRecords     => Set<FishingRecord>();
    public DbSet<CitizenCard>       CitizenCards       => Set<CitizenCard>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ─── Account ────────────────────────────────────────
        mb.Entity<Account>(e =>
        {
            e.HasIndex(a => a.Username).IsUnique();
            e.HasIndex(a => a.Email).IsUnique();
            e.Property(a => a.Role).HasConversion<string>();
        });

        // ─── Session ────────────────────────────────────────
        mb.Entity<Session>(e =>
        {
            e.HasIndex(s => s.Token).IsUnique();
            e.Property(s => s.ClientType).HasConversion<string>();
            e.HasOne(s => s.Account)
             .WithMany(a => a.Sessions)
             .HasForeignKey(s => s.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Character ──────────────────────────────────────
        mb.Entity<Character>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
            e.HasIndex(c => c.AccountId);
            e.HasIndex(c => c.MapId);
            e.Property(c => c.Gender).HasConversion<string>();
            e.HasOne(c => c.Account)
             .WithMany(a => a.Characters)
             .HasForeignKey(c => c.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Equipment (1-to-1) ─────────────────────────────
        mb.Entity<EquipmentSlot>(e =>
        {
            e.HasOne(es => es.Character)
             .WithOne(c => c.EquipmentSlot)
             .HasForeignKey<EquipmentSlot>(es => es.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Inventory ──────────────────────────────────────
        mb.Entity<InventoryItem>(e =>
        {
            e.HasIndex(i => i.CharacterId);
            e.HasOne(i => i.Character)
             .WithMany(c => c.Inventory)
             .HasForeignKey(i => i.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Pet ────────────────────────────────────────────
        mb.Entity<Pet>(e =>
        {
            e.HasIndex(p => p.OwnerId);
            e.HasOne(p => p.Owner)
             .WithMany(c => c.Pets)
             .HasForeignKey(p => p.OwnerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Clan ───────────────────────────────────────────
        mb.Entity<ClanMember>(e =>
        {
            e.HasIndex(cm => new { cm.ClanId, cm.CharacterId }).IsUnique();
            e.HasOne(cm => cm.Clan)
             .WithMany(c => c.Members)
             .HasForeignKey(cm => cm.ClanId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(cm => cm.Character)
             .WithMany(c => c.ClanMemberships)
             .HasForeignKey(cm => cm.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Academy ────────────────────────────────────────
        mb.Entity<CharacterAcademy>(e =>
        {
            e.HasIndex(ca => ca.CharacterId).IsUnique(); // 1 char 1 academy
            e.HasOne(ca => ca.Character)
             .WithMany(c => c.AcademyEnrollments)
             .HasForeignKey(ca => ca.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Faction ────────────────────────────────────────
        mb.Entity<CharacterFaction>(e =>
        {
            e.HasIndex(cf => cf.CharacterId).IsUnique(); // 1 char 1 faction
            e.HasOne(cf => cf.Character)
             .WithMany(c => c.FactionMemberships)
             .HasForeignKey(cf => cf.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Message ────────────────────────────────────────
        mb.Entity<Message>(e =>
        {
            e.Property(m => m.Channel).HasConversion<string>();
            e.HasIndex(m => new { m.ReceiverId, m.IsRead });
            e.HasOne(m => m.Sender)
             .WithMany()
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Character 1:1 support tables ───────────────────
        mb.Entity<CharacterFameStat>(e =>
        {
            e.HasOne(s => s.Character)
             .WithOne(c => c.FameStat)
             .HasForeignKey<CharacterFameStat>(s => s.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<FishingRecord>(e =>
        {
            e.HasOne(r => r.Character)
             .WithOne(c => c.FishingRecord)
             .HasForeignKey<FishingRecord>(r => r.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CitizenCard>(e =>
        {
            e.HasIndex(cc => cc.CardNumber).IsUnique();
            e.HasOne(cc => cc.Character)
             .WithOne(c => c.CitizenCard)
             .HasForeignKey<CitizenCard>(cc => cc.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── World Event ────────────────────────────────────
        mb.Entity<WorldEventInstance>(e =>
        {
            e.HasOne(i => i.Event)
             .WithMany(we => we.Instances)
             .HasForeignKey(i => i.EventId);
        });

        // ─── Skill ──────────────────────────────────────────
        mb.Entity<CharacterSkill>(e =>
        {
            e.HasIndex(cs => new { cs.CharacterId, cs.SkillId }).IsUnique();
            e.HasOne(cs => cs.Character)
             .WithMany(c => c.Skills)
             .HasForeignKey(cs => cs.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Quest ──────────────────────────────────────────
        mb.Entity<CharacterQuest>(e =>
        {
            e.HasIndex(cq => new { cq.CharacterId, cq.Status });
            e.HasOne(cq => cq.Character)
             .WithMany(c => c.Quests)
             .HasForeignKey(cq => cq.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Transaction ────────────────────────────────────
        mb.Entity<Transaction>(e =>
        {
            e.HasIndex(t => t.RefCode).IsUnique();
            e.HasOne(t => t.Account)
             .WithMany(a => a.Transactions)
             .HasForeignKey(t => t.AccountId);
        });

        // ─── Portal ─────────────────────────────────────────
        mb.Entity<Portal>(e =>
        {
            e.HasOne(p => p.FromMap)
             .WithMany(m => m.Portals)
             .HasForeignKey(p => p.FromMapId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.ToMap)
             .WithMany()
             .HasForeignKey(p => p.ToMapId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
