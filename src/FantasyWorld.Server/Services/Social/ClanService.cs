// FantasyWorld.Server/Services/Social/ClanService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Social;

public interface IClanService
{
    Task<(bool Ok, string Msg, int ClanId)> CreateAsync(long leaderId, CreateClanRequest req, string lang);
    Task<(bool Ok, string Msg)>  InviteAsync(long clanId, long inviterId, long targetId, string lang);
    Task<(bool Ok, string Msg)>  JoinAsync(long charId, int clanId, string lang);
    Task<(bool Ok, string Msg)>  LeaveAsync(long charId, string lang);
    Task<(bool Ok, string Msg)>  KickAsync(long leaderId, long targetId, string lang);
    Task<(bool Ok, string Msg)>  PromoteAsync(long leaderId, long targetId, string role, string lang);
    Task<(bool Ok, string Msg)>  DisbandAsync(long leaderId, string lang);
    Task<(bool Ok, string Msg)>  DepositGoldAsync(long charId, long amount, string lang);
    Task<(bool Ok, string Msg)>  WithdrawGoldAsync(long charId, long amount, string lang);
    Task<ClanDto?>               GetByIdAsync(int clanId);
    Task<List<ClanMemberDto>>    GetMembersAsync(int clanId);
    Task<int?>                   GetCharClanIdAsync(long charId);
    Task<List<ClanStorage>>      GetStorageAsync(int clanId);
    Task<(bool Ok, string Msg)>  DepositItemAsync(long charId, int itemId, int qty, string lang);
    Task<(bool Ok, string Msg)>  WithdrawItemAsync(long charId, int itemId, int qty, string lang);
    Task                         AddContributionAsync(long charId, int points);
}

public class ClanService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<ClanService> logger) : IClanService
{
    private const int CreateCost = 10_000; // gold để lập clan

    // ─── Create ──────────────────────────────────────────────
    public async Task<(bool, string, int)> CreateAsync(
        long leaderId, CreateClanRequest req, string lang)
    {
        // Kiểm tra đã có clan
        if (await GetCharClanIdAsync(leaderId) is not null)
            return (false, loc.Get("error.bad_request", lang), 0);

        // Kiểm tra tên unique
        if (await db.Clans.AnyAsync(c => c.Name == req.Name))
            return (false, loc.Get("clan.created", lang), 0);

        // Kiểm tra gold
        var char_ = await db.Characters.FindAsync(leaderId)
            ?? throw new InvalidOperationException();
        if (char_.Gold < CreateCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = CreateCost, have = char_.Gold }), 0);

        // Trừ gold
        char_.Gold -= CreateCost;

        var clan = new Clan
        {
            Name        = req.Name.Trim(),
            LeaderId    = leaderId,
            Description = req.Description,
            Emblem      = req.Emblem,
        };
        db.Clans.Add(clan);
        await db.SaveChangesAsync();

        // Thêm leader vào member
        db.ClanMembers.Add(new ClanMember
        {
            ClanId      = clan.Id,
            CharacterId = leaderId,
            Role        = "leader",
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Clan created: {Name} (id={Id}) by charId={Leader}",
            clan.Name, clan.Id, leaderId);

        return (true, loc.Get("clan.created", lang, new { name = clan.Name }), clan.Id);
    }

    // ─── Invite ──────────────────────────────────────────────
    public async Task<(bool, string)> InviteAsync(
        long clanId, long inviterId, long targetId, string lang)
    {
        var inviterClan = await GetCharClanIdAsync(inviterId);
        if (inviterClan != clanId)
            return (false, loc.Get("error.forbidden", lang));

        if (await GetCharClanIdAsync(targetId) is not null)
            return (false, loc.Get("error.bad_request", lang));

        var clan = await db.Clans.FindAsync((int)clanId);
        if (clan is null) return (false, loc.Get("error.not_found", lang));

        var count = await db.ClanMembers.CountAsync(m => m.ClanId == clanId);
        if (count >= clan.MaxMembers)
            return (false, loc.Get("clan.full", lang));

        return (true, "INVITE_SENT");
    }

    // ─── Join ────────────────────────────────────────────────
    public async Task<(bool, string)> JoinAsync(long charId, int clanId, string lang)
    {
        if (await GetCharClanIdAsync(charId) is not null)
            return (false, loc.Get("error.bad_request", lang));

        var clan = await db.Clans.FindAsync(clanId);
        if (clan is null) return (false, loc.Get("error.not_found", lang));

        var count = await db.ClanMembers.CountAsync(m => m.ClanId == clanId);
        if (count >= clan.MaxMembers)
            return (false, loc.Get("clan.full", lang));

        db.ClanMembers.Add(new ClanMember
        {
            ClanId      = clanId,
            CharacterId = charId,
            Role        = "member",
        });
        await db.SaveChangesAsync();

        return (true, loc.Get("clan.joined", lang, new { name = clan.Name }));
    }

    // ─── Leave ───────────────────────────────────────────────
    public async Task<(bool, string)> LeaveAsync(long charId, string lang)
    {
        var member = await db.ClanMembers
            .Include(m => m.Clan)
            .FirstOrDefaultAsync(m => m.CharacterId == charId);

        if (member is null)
            return (false, loc.Get("clan.not_member", lang));

        if (member.Role == "leader")
        {
            // Leader phải chuyển quyền trước hoặc giải tán
            var count = await db.ClanMembers.CountAsync(m => m.ClanId == member.ClanId);
            if (count > 1)
                return (false, loc.Get("error.bad_request", lang));

            // Tự giải tán nếu còn 1 mình
            return await DisbandAsync(charId, lang);
        }

        db.ClanMembers.Remove(member);
        await db.SaveChangesAsync();
        return (true, loc.Get("clan.left", lang));
    }

    // ─── Kick ────────────────────────────────────────────────
    public async Task<(bool, string)> KickAsync(long leaderId, long targetId, string lang)
    {
        var leaderMember = await db.ClanMembers
            .FirstOrDefaultAsync(m => m.CharacterId == leaderId
                && (m.Role == "leader" || m.Role == "elder"));
        if (leaderMember is null)
            return (false, loc.Get("error.forbidden", lang));

        var target = await db.ClanMembers
            .FirstOrDefaultAsync(m => m.CharacterId == targetId
                && m.ClanId == leaderMember.ClanId);
        if (target is null)
            return (false, loc.Get("error.not_found", lang));

        if (target.Role == "leader")
            return (false, loc.Get("error.forbidden", lang));

        db.ClanMembers.Remove(target);
        await db.SaveChangesAsync();
        return (true, loc.Get("clan.kicked", lang));
    }

    // ─── Promote ─────────────────────────────────────────────
    public async Task<(bool, string)> PromoteAsync(
        long leaderId, long targetId, string role, string lang)
    {
        var leader = await db.ClanMembers
            .FirstOrDefaultAsync(m => m.CharacterId == leaderId && m.Role == "leader");
        if (leader is null)
            return (false, loc.Get("error.forbidden", lang));

        var target = await db.ClanMembers
            .FirstOrDefaultAsync(m => m.CharacterId == targetId
                && m.ClanId == leader.ClanId);
        if (target is null)
            return (false, loc.Get("error.not_found", lang));

        // Chuyển leader
        if (role == "leader")
        {
            leader.Role = "elder";
            target.Role = "leader";

            var clan = await db.Clans.FindAsync(leader.ClanId);
            if (clan is not null) clan.LeaderId = targetId;
        }
        else
        {
            target.Role = role;
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Disband ─────────────────────────────────────────────
    public async Task<(bool, string)> DisbandAsync(long leaderId, string lang)
    {
        var leader = await db.ClanMembers
            .FirstOrDefaultAsync(m => m.CharacterId == leaderId && m.Role == "leader");
        if (leader is null)
            return (false, loc.Get("error.forbidden", lang));

        var clan = await db.Clans.FindAsync(leader.ClanId);
        if (clan is null)
            return (false, loc.Get("error.not_found", lang));

        db.Clans.Remove(clan);
        await db.SaveChangesAsync();

        logger.LogInformation("Clan disbanded: {Name} (id={Id})", clan.Name, clan.Id);
        return (true, "OK");
    }

    // ─── Deposit gold ────────────────────────────────────────
    public async Task<(bool, string)> DepositGoldAsync(long charId, long amount, string lang)
    {
        if (amount <= 0) return (false, loc.Get("error.bad_request", lang));

        var clanId = await GetCharClanIdAsync(charId);
        if (clanId is null) return (false, loc.Get("clan.not_member", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < amount)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = amount, have = char_.Gold }));

        char_.Gold -= amount;

        var clan = await db.Clans.FindAsync(clanId.Value)!;
        clan!.Gold += amount;

        await AddContributionAsync(charId, (int)Math.Min(amount / 100, int.MaxValue));
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Withdraw gold ───────────────────────────────────────
    public async Task<(bool, string)> WithdrawGoldAsync(long charId, long amount, string lang)
    {
        var member = await db.ClanMembers
            .Include(m => m.Clan)
            .FirstOrDefaultAsync(m => m.CharacterId == charId);

        if (member is null) return (false, loc.Get("clan.not_member", lang));
        if (member.Role == "member") return (false, loc.Get("error.forbidden", lang));
        if (member.Clan.Gold < amount)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = amount, have = member.Clan.Gold }));

        member.Clan.Gold -= amount;

        var char_ = await db.Characters.FindAsync(charId)!;
        char_!.Gold += amount;

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Deposit item ────────────────────────────────────────
    public async Task<(bool, string)> DepositItemAsync(
        long charId, int itemId, int qty, string lang)
    {
        var clanId = await GetCharClanIdAsync(charId);
        if (clanId is null) return (false, loc.Get("clan.not_member", lang));

        var inInv = await db.Inventories
            .Where(i => i.CharacterId == charId && i.ItemId == itemId)
            .SumAsync(i => i.Quantity);
        if (inInv < qty)
            return (false, loc.Get("inventory.item_not_found", lang));

        // Trừ khỏi inventory
        var rows = await db.Inventories
            .Where(i => i.CharacterId == charId && i.ItemId == itemId)
            .OrderBy(i => i.Id)
            .ToListAsync();

        int remaining = qty;
        foreach (var row in rows)
        {
            if (remaining <= 0) break;
            if (row.Quantity <= remaining) { remaining -= row.Quantity; db.Inventories.Remove(row); }
            else { row.Quantity -= remaining; remaining = 0; }
        }

        // Thêm vào clan storage
        var existing = await db.ClanStorages
            .FirstOrDefaultAsync(s => s.ClanId == clanId && s.ItemId == itemId);
        if (existing is not null) existing.Quantity += qty;
        else db.ClanStorages.Add(new ClanStorage
        {
            ClanId      = clanId.Value,
            ItemId      = itemId,
            Quantity    = qty,
            DepositedBy = charId,
        });

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Withdraw item ───────────────────────────────────────
    public async Task<(bool, string)> WithdrawItemAsync(
        long charId, int itemId, int qty, string lang)
    {
        var member = await db.ClanMembers
            .FirstOrDefaultAsync(m => m.CharacterId == charId);
        if (member is null) return (false, loc.Get("clan.not_member", lang));

        var storage = await db.ClanStorages
            .FirstOrDefaultAsync(s => s.ClanId == member.ClanId && s.ItemId == itemId);
        if (storage is null || storage.Quantity < qty)
            return (false, loc.Get("inventory.item_not_found", lang));

        storage.Quantity -= qty;
        if (storage.Quantity == 0) db.ClanStorages.Remove(storage);

        // Thêm vào inventory
        var inv = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId && i.ItemId == itemId);
        if (inv is not null) inv.Quantity += qty;
        else
        {
            var maxSlot = await db.Inventories
                .Where(i => i.CharacterId == charId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new InventoryItem
            {
                CharacterId = charId,
                ItemId      = itemId,
                Quantity    = qty,
                Slot        = maxSlot + 1,
            });
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get clan ────────────────────────────────────────────
    public async Task<ClanDto?> GetByIdAsync(int clanId)
    {
        var clan = await db.Clans
            .Include(c => c.Members).ThenInclude(m => m.Character)
            .FirstOrDefaultAsync(c => c.Id == clanId);

        if (clan is null) return null;

        var leader = clan.Members.FirstOrDefault(m => m.Role == "leader");
        var online = clan.Members.Count(m => state.IsOnline(m.CharacterId));

        return new ClanDto(
            clan.Id, clan.Name, clan.Emblem, clan.Description,
            clan.Level, clan.Gold,
            clan.Members.Count, clan.MaxMembers,
            clan.LeaderId, leader?.Character.Name ?? "",
            online);
    }

    // ─── Get members ─────────────────────────────────────────
    public async Task<List<ClanMemberDto>> GetMembersAsync(int clanId)
    {
        return await db.ClanMembers
            .Where(m => m.ClanId == clanId)
            .Include(m => m.Character)
            .OrderBy(m => m.Role == "leader" ? 0 : m.Role == "elder" ? 1 : 2)
            .ThenBy(m => m.Character.Name)
            .Select(m => new ClanMemberDto(
                m.CharacterId,
                m.Character.Name,
                m.Character.Level,
                m.Role,
                m.Contribution,
                state.IsOnline(m.CharacterId),
                new DateTimeOffset(m.JoinedAt).ToUnixTimeMilliseconds()))
            .ToListAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────
    public async Task<int?> GetCharClanIdAsync(long charId)
    {
        var row = await db.ClanMembers
            .Where(m => m.CharacterId == charId)
            .Select(m => (int?)m.ClanId)
            .FirstOrDefaultAsync();
        return row;
    }

    public async Task<List<ClanStorage>> GetStorageAsync(int clanId) =>
        await db.ClanStorages
            .Include(s => s.Item)
            .Where(s => s.ClanId == clanId)
            .ToListAsync();

    public async Task AddContributionAsync(long charId, int points)
    {
        await db.ClanMembers
            .Where(m => m.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Contribution, m => m.Contribution + points));
    }
}
