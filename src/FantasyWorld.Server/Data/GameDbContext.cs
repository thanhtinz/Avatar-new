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

    // Social - Phase 2
    public DbSet<FriendRequest>  FriendRequests  => Set<FriendRequest>();
    public DbSet<Relationship>   Relationships   => Set<Relationship>();
    public DbSet<Party>          Parties         => Set<Party>();
    public DbSet<PartyMember>    PartyMembers    => Set<PartyMember>();
    public DbSet<PartyInvite>    PartyInvites    => Set<PartyInvite>();
    public DbSet<ClanQuest>      ClanQuests      => Set<ClanQuest>();
    public DbSet<ClanBoss>       ClanBosses      => Set<ClanBoss>();
    public DbSet<ClanStorage>    ClanStorages    => Set<ClanStorage>();
    public DbSet<CharacterBlock> CharacterBlocks => Set<CharacterBlock>();

    // Economy - Phase 3
    public DbSet<NpcShop>                NpcShops               => Set<NpcShop>();
    public DbSet<NpcShopItem>            NpcShopItems           => Set<NpcShopItem>();
    public DbSet<PlayerShop>             PlayerShops            => Set<PlayerShop>();
    public DbSet<PlayerShopListing>      PlayerShopListings     => Set<PlayerShopListing>();
    public DbSet<PlayerShopTransaction>  PlayerShopTransactions => Set<PlayerShopTransaction>();
    public DbSet<AuctionListing>         AuctionListings        => Set<AuctionListing>();
    public DbSet<AuctionBid>             AuctionBids            => Set<AuctionBid>();
    public DbSet<MarketPrice>            MarketPrices           => Set<MarketPrice>();
    public DbSet<PriceHistory>           PriceHistory           => Set<PriceHistory>();
    public DbSet<SeasonalPriceModifier>  SeasonalPriceModifiers => Set<SeasonalPriceModifier>();
    public DbSet<RestaurantType>         RestaurantTypes        => Set<RestaurantType>();
    public DbSet<RestaurantApplication>  RestaurantApplications => Set<RestaurantApplication>();
    public DbSet<RestaurantVote>         RestaurantVotes        => Set<RestaurantVote>();
    public DbSet<Restaurant>             Restaurants            => Set<Restaurant>();
    public DbSet<RestaurantMenuItem>     RestaurantMenuItems    => Set<RestaurantMenuItem>();
    public DbSet<RestaurantOrder>        RestaurantOrders       => Set<RestaurantOrder>();
    public DbSet<MarketStall>            MarketStalls           => Set<MarketStall>();
    public DbSet<MarketStallRental>      MarketStallRentals     => Set<MarketStallRental>();
    public DbSet<MarketListing>          MarketListings         => Set<MarketListing>();

    // World - Phase 4
    public DbSet<PetSkill>              PetSkills              => Set<PetSkill>();
    public DbSet<PetCatchLog>           PetCatchLogs           => Set<PetCatchLog>();
    public DbSet<WildPetSpawn>          WildPetSpawns          => Set<WildPetSpawn>();
    public DbSet<FishSpecies>           FishSpecies            => Set<FishSpecies>();
    public DbSet<FishingLog>            FishingLogs            => Set<FishingLog>();
    public DbSet<Crop>                  Crops                  => Set<Crop>();
    public DbSet<FarmPlot>              FarmPlots              => Set<FarmPlot>();
    public DbSet<PlotCrop>              PlotCrops              => Set<PlotCrop>();
    public DbSet<PetRanch>              PetRanches             => Set<PetRanch>();
    public DbSet<RanchPet>              RanchPets              => Set<RanchPet>();
    public DbSet<Dungeon>               Dungeons               => Set<Dungeon>();
    public DbSet<DungeonRun>            DungeonRuns            => Set<DungeonRun>();
    public DbSet<DungeonRunMember>      DungeonRunMembers      => Set<DungeonRunMember>();
    public DbSet<WorldEventParticipant> WorldEventParticipants => Set<WorldEventParticipant>();
    public DbSet<Npc>                   Npcs                   => Set<Npc>();
    public DbSet<NpcAiProfile>          NpcAiProfiles          => Set<NpcAiProfile>();
    public DbSet<NpcSchedule>           NpcSchedules           => Set<NpcSchedule>();
    public DbSet<NpcCharacterRelation>  NpcCharacterRelations  => Set<NpcCharacterRelation>();
    public DbSet<Monster>               Monsters               => Set<Monster>();
    public DbSet<BattleLog>             BattleLogs             => Set<BattleLog>();

    // Content - Phase 5
    public DbSet<QuestObjective>              QuestObjectives              => Set<QuestObjective>();
    public DbSet<QuestReward>                 QuestRewards                 => Set<QuestReward>();
    public DbSet<QuestPrerequisite>           QuestPrerequisites           => Set<QuestPrerequisite>();
    public DbSet<CharacterQuestObjective>     CharacterQuestObjectives     => Set<CharacterQuestObjective>();
    public DbSet<AcademyExam>                 AcademyExams                 => Set<AcademyExam>();
    public DbSet<AcademyExamResult>           AcademyExamResults           => Set<AcademyExamResult>();
    public DbSet<AcademyTournament>           AcademyTournaments           => Set<AcademyTournament>();
    public DbSet<AcademyTournamentParticipant> AcademyTournamentParticipants => Set<AcademyTournamentParticipant>();
    public DbSet<DungeonFloor>                DungeonFloors                => Set<DungeonFloor>();
    public DbSet<DungeonCooldown>             DungeonCooldowns             => Set<DungeonCooldown>();
    public DbSet<StoryChapter>                StoryChapters                => Set<StoryChapter>();
    public DbSet<StoryNode>                   StoryNodes                   => Set<StoryNode>();
    public DbSet<CharacterStoryProgress>      CharacterStoryProgress       => Set<CharacterStoryProgress>();
    public DbSet<CharacterStoryEnding>        CharacterStoryEndings        => Set<CharacterStoryEnding>();
    public DbSet<Title>                       Titles                       => Set<Title>();
    public DbSet<CharacterTitle>              CharacterTitles              => Set<CharacterTitle>();
    public DbSet<Achievement>                 Achievements                 => Set<Achievement>();
    public DbSet<CharacterAchievement>        CharacterAchievements        => Set<CharacterAchievement>();
    public DbSet<CharacterReputation>         CharacterReputations         => Set<CharacterReputation>();
    public DbSet<CharacterCard>               CharacterCards               => Set<CharacterCard>();

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

        // ─── Phase 2: Social ─────────────────────────────────

        mb.Entity<FriendRequest>(e =>
        {
            e.HasIndex(f => new { f.FromId, f.ToId }).IsUnique();
            e.HasOne(f => f.From).WithMany()
             .HasForeignKey(f => f.FromId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.To).WithMany()
             .HasForeignKey(f => f.ToId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Relationship>(e =>
        {
            e.Property(r => r.RelType).HasConversion<string>();
            e.HasIndex(r => new { r.CharacterA, r.CharacterB, r.RelType }).IsUnique();
            e.HasOne(r => r.A).WithMany()
             .HasForeignKey(r => r.CharacterA).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.B).WithMany()
             .HasForeignKey(r => r.CharacterB).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Party>(e =>
        {
            e.HasOne(p => p.Leader).WithMany()
             .HasForeignKey(p => p.LeaderId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PartyMember>(e =>
        {
            e.HasIndex(pm => new { pm.PartyId, pm.CharacterId }).IsUnique();
            e.HasOne(pm => pm.Party).WithMany(p => p.Members)
             .HasForeignKey(pm => pm.PartyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pm => pm.Character).WithMany()
             .HasForeignKey(pm => pm.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PartyInvite>(e =>
        {
            e.HasOne(pi => pi.Party).WithMany()
             .HasForeignKey(pi => pi.PartyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pi => pi.Inviter).WithMany()
             .HasForeignKey(pi => pi.InviterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(pi => pi.Invitee).WithMany()
             .HasForeignKey(pi => pi.InviteeId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<ClanQuest>(e =>
        {
            e.HasOne(cq => cq.Clan).WithMany()
             .HasForeignKey(cq => cq.ClanId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ClanBoss>(e =>
        {
            e.HasOne(cb => cb.Clan).WithMany()
             .HasForeignKey(cb => cb.ClanId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ClanStorage>(e =>
        {
            e.HasOne(cs => cs.Clan).WithMany()
             .HasForeignKey(cs => cs.ClanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(cs => cs.Item).WithMany()
             .HasForeignKey(cs => cs.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CharacterBlock>(e =>
        {
            e.HasIndex(b => new { b.BlockerId, b.BlockedId }).IsUnique();
            e.HasOne(b => b.Blocker).WithMany()
             .HasForeignKey(b => b.BlockerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Blocked).WithMany()
             .HasForeignKey(b => b.BlockedId).OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Phase 3: Economy ────────────────────────────────

        mb.Entity<NpcShopItem>(e =>
        {
            e.HasOne(i => i.Shop).WithMany(s => s.Items)
             .HasForeignKey(i => i.ShopId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Item).WithMany()
             .HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<PlayerShop>(e =>
        {
            e.HasOne(s => s.Owner).WithMany()
             .HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PlayerShopListing>(e =>
        {
            e.HasIndex(l => new { l.ShopId, l.IsSold });
            e.HasOne(l => l.Shop).WithMany(s => s.Listings)
             .HasForeignKey(l => l.ShopId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Item).WithMany()
             .HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<AuctionListing>(e =>
        {
            e.HasIndex(a => new { a.Status, a.EndsAt });
            e.HasOne(a => a.Seller).WithMany()
             .HasForeignKey(a => a.SellerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Item).WithMany()
             .HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<AuctionBid>(e =>
        {
            e.HasIndex(b => new { b.AuctionId, b.IsWinning });
            e.HasOne(b => b.Auction).WithMany(a => a.Bids)
             .HasForeignKey(b => b.AuctionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Bidder).WithMany()
             .HasForeignKey(b => b.BidderId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MarketPrice>(e =>
        {
            e.HasIndex(m => m.ItemId).IsUnique();
            e.HasOne(m => m.Item).WithMany()
             .HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<PriceHistory>(e =>
        {
            e.HasIndex(h => new { h.ItemId, h.RecordedAt });
            e.HasOne(h => h.Item).WithMany()
             .HasForeignKey(h => h.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<RestaurantApplication>(e =>
        {
            e.HasOne(a => a.Applicant).WithMany()
             .HasForeignKey(a => a.ApplicantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Type).WithMany(t => t.Applications)
             .HasForeignKey(a => a.TypeId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<RestaurantVote>(e =>
        {
            e.HasIndex(v => new { v.ApplicationId, v.VoterId }).IsUnique();
            e.HasOne(v => v.Application).WithMany(a => a.Votes)
             .HasForeignKey(v => v.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.Voter).WithMany()
             .HasForeignKey(v => v.VoterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Restaurant>(e =>
        {
            e.HasOne(r => r.Owner).WithMany()
             .HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RestaurantApplication>().WithOne(a => a.Restaurant)
             .HasForeignKey<Restaurant>(r => r.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<RestaurantMenuItem>(e =>
        {
            e.HasOne(m => m.Restaurant).WithMany(r => r.MenuItems)
             .HasForeignKey(m => m.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RestaurantOrder>(e =>
        {
            e.HasOne(o => o.Restaurant).WithMany(r => r.Orders)
             .HasForeignKey(o => o.RestaurantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.MenuItem).WithMany()
             .HasForeignKey(o => o.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MarketStallRental>(e =>
        {
            e.HasIndex(r => new { r.StallId, r.IsActive });
            e.HasOne(r => r.Stall).WithOne(s => s.CurrentRental)
             .HasForeignKey<MarketStallRental>(r => r.StallId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Renter).WithMany()
             .HasForeignKey(r => r.RenterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<MarketListing>(e =>
        {
            e.HasIndex(l => new { l.ListingType, l.Price });
            e.HasOne(l => l.Rental).WithMany(r => r.Listings)
             .HasForeignKey(l => l.RentalId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Seller).WithMany()
             .HasForeignKey(l => l.SellerId).OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Phase 4: World ──────────────────────────────────

        mb.Entity<PetSkill>(e =>
        {
            e.Property(s => s.Element).HasConversion<string>();
            e.HasOne(s => s.Species).WithMany()
             .HasForeignKey(s => s.SpeciesId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<WildPetSpawn>(e =>
        {
            e.HasIndex(w => new { w.MapId, w.IsAlive });
            e.HasOne(w => w.Species).WithMany()
             .HasForeignKey(w => w.SpeciesId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FishSpecies>(e =>
            e.Property(f => f.Rarity).HasConversion<string>());

        mb.Entity<FishingLog>(e =>
        {
            e.HasIndex(f => new { f.CharacterId, f.FishedAt });
            e.HasOne(f => f.Character).WithMany()
             .HasForeignKey(f => f.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Fish).WithMany()
             .HasForeignKey(f => f.FishId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Crop>(e =>
        {
            e.HasOne(c => c.HarvestItem).WithMany()
             .HasForeignKey(c => c.HarvestItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.SeedItem).WithMany()
             .HasForeignKey(c => c.SeedItemId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FarmPlot>(e =>
        {
            e.HasOne(p => p.Owner).WithMany()
             .HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PlotCrop>(e =>
        {
            e.HasOne(pc => pc.Plot).WithOne(p => p.CurrentCrop)
             .HasForeignKey<PlotCrop>(pc => pc.PlotId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pc => pc.Crop).WithMany()
             .HasForeignKey(pc => pc.CropId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<PetRanch>(e =>
        {
            e.HasOne(r => r.Owner).WithMany()
             .HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RanchPet>(e =>
        {
            e.HasOne(rp => rp.Ranch).WithMany(r => r.Pets)
             .HasForeignKey(rp => rp.RanchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Pet).WithMany()
             .HasForeignKey(rp => rp.PetId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DungeonRun>(e =>
        {
            e.HasOne(r => r.Dungeon).WithMany(d => d.Runs)
             .HasForeignKey(r => r.DungeonId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<DungeonRunMember>(e =>
        {
            e.HasOne(m => m.Run).WithMany(r => r.Members)
             .HasForeignKey(m => m.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Character).WithMany()
             .HasForeignKey(m => m.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<WorldEventParticipant>(e =>
        {
            e.HasIndex(p => new { p.InstanceId, p.CharacterId }).IsUnique();
            e.HasOne(p => p.Instance).WithMany()
             .HasForeignKey(p => p.InstanceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Character).WithMany()
             .HasForeignKey(p => p.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Npc>(e => e.HasIndex(n => n.MapId));

        mb.Entity<NpcAiProfile>(e =>
        {
            e.HasOne(a => a.Npc).WithOne(n => n.AiProfile)
             .HasForeignKey<NpcAiProfile>(a => a.NpcId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<NpcSchedule>(e =>
        {
            e.HasOne(s => s.Npc).WithMany(n => n.Schedules)
             .HasForeignKey(s => s.NpcId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<NpcCharacterRelation>(e =>
        {
            e.HasIndex(r => new { r.NpcId, r.CharacterId }).IsUnique();
            e.HasOne(r => r.Npc).WithMany(n => n.Relations)
             .HasForeignKey(r => r.NpcId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Character).WithMany()
             .HasForeignKey(r => r.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Monster>(e =>
            e.Property(m => m.Element).HasConversion<string>());

        mb.Entity<BattleLog>(e =>
        {
            e.HasIndex(b => new { b.AttackerId, b.FoughtAt });
            e.HasOne(b => b.Attacker).WithMany()
             .HasForeignKey(b => b.AttackerId).OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Phase 5: Content ────────────────────────────────

        mb.Entity<QuestObjective>(e =>
        {
            e.HasOne(o => o.Quest).WithMany()
             .HasForeignKey(o => o.QuestId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<QuestReward>(e =>
        {
            e.HasOne(r => r.Quest).WithMany()
             .HasForeignKey(r => r.QuestId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<QuestPrerequisite>(e =>
        {
            e.HasOne(p => p.Quest).WithMany()
             .HasForeignKey(p => p.QuestId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CharacterQuestObjective>(e =>
        {
            e.HasOne(cqo => cqo.CharQuest).WithMany(cq => cq.Objectives)
             .HasForeignKey(cqo => cqo.CharQuestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(cqo => cqo.Objective).WithMany()
             .HasForeignKey(cqo => cqo.ObjectiveId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<AcademyExam>(e =>
        {
            e.HasOne(ex => ex.Academy).WithMany()
             .HasForeignKey(ex => ex.AcademyId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<AcademyExamResult>(e =>
        {
            e.HasIndex(r => new { r.CharacterId, r.ExamId, r.TakenAt });
            e.HasOne(r => r.Character).WithMany()
             .HasForeignKey(r => r.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Exam).WithMany(ex => ex.Results)
             .HasForeignKey(r => r.ExamId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<AcademyTournament>(e =>
        {
            e.HasOne(t => t.AcademyA).WithMany()
             .HasForeignKey(t => t.AcademyAId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AcademyB).WithMany()
             .HasForeignKey(t => t.AcademyBId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<AcademyTournamentParticipant>(e =>
        {
            e.HasOne(p => p.Tournament).WithMany()
             .HasForeignKey(p => p.TournamentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Character).WithMany()
             .HasForeignKey(p => p.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<DungeonFloor>(e =>
        {
            e.HasOne(f => f.Dungeon).WithMany()
             .HasForeignKey(f => f.DungeonId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DungeonCooldown>(e =>
        {
            e.HasIndex(cd => new { cd.CharacterId, cd.DungeonId });
            e.HasOne(cd => cd.Character).WithMany()
             .HasForeignKey(cd => cd.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(cd => cd.Dungeon).WithMany()
             .HasForeignKey(cd => cd.DungeonId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<StoryChapter>(e =>
        {
            e.HasOne(c => c.PrereqChapter).WithMany()
             .HasForeignKey(c => c.PrereqChapterId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<StoryNode>(e =>
        {
            e.HasOne(n => n.Chapter).WithMany(c => c.Nodes)
             .HasForeignKey(n => n.ChapterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.Npc).WithMany()
             .HasForeignKey(n => n.NpcId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CharacterStoryProgress>(e =>
        {
            e.HasIndex(p => new { p.CharacterId, p.ChapterId }).IsUnique();
            e.HasOne(p => p.Character).WithMany()
             .HasForeignKey(p => p.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Chapter).WithMany(c => c.Progress)
             .HasForeignKey(p => p.ChapterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.CurrentNode).WithMany()
             .HasForeignKey(p => p.CurrentNodeId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CharacterStoryEnding>(e =>
        {
            e.HasIndex(e2 => new { e2.CharacterId, e2.EndingId });
            e.HasOne(e2 => e2.Character).WithMany()
             .HasForeignKey(e2 => e2.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CharacterTitle>(e =>
        {
            e.HasIndex(ct => new { ct.CharacterId, ct.TitleId }).IsUnique();
            e.HasOne(ct => ct.Character).WithMany()
             .HasForeignKey(ct => ct.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ct => ct.Title).WithMany(t => t.CharacterTitles)
             .HasForeignKey(ct => ct.TitleId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CharacterAchievement>(e =>
        {
            e.HasIndex(ca => new { ca.CharacterId, ca.AchievementId }).IsUnique();
            e.HasOne(ca => ca.Character).WithMany()
             .HasForeignKey(ca => ca.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ca => ca.Achievement).WithMany()
             .HasForeignKey(ca => ca.AchievementId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CharacterReputation>(e =>
        {
            e.HasIndex(r => new { r.CharacterId, r.RegionId }).IsUnique();
            e.HasOne(r => r.Character).WithMany()
             .HasForeignKey(r => r.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Region).WithMany()
             .HasForeignKey(r => r.RegionId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CharacterCard>(e =>
        {
            e.HasIndex(cc => new { cc.CharacterId, cc.CardId }).IsUnique();
            e.HasOne(cc => cc.Character).WithMany()
             .HasForeignKey(cc => cc.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
