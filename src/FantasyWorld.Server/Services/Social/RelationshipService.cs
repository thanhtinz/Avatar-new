// FantasyWorld.Server/Services/Social/RelationshipService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Services.Social;

public interface IRelationshipService
{
    Task<(bool Ok, string Msg)> ProposeAsync(long fromId, long toId, RelType type, string lang);
    Task<(bool Ok, string Msg)> RemoveAsync(long charId, long partnerId, RelType type, string lang);
    Task<List<RelationshipDto>> GetAllAsync(long charId);
    Task                        AddIntimacyAsync(long charId, long partnerId, int amount);
    Task<RelationshipDto?>      GetAsync(long a, long b, RelType type);
}

public class RelationshipService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc) : IRelationshipService
{
    // Giới hạn số lượng mỗi loại quan hệ
    private static readonly Dictionary<RelType, int> MaxPerType = new()
    {
        [RelType.Friend]     = 200,
        [RelType.BestFriend] = 5,
        [RelType.Soulmate]   = 1,
        [RelType.Mentor]     = 1,
        [RelType.Student]    = 5,
        [RelType.Rival]      = 3,
        [RelType.Married]    = 1,
    };

    // ─── Propose / Form relationship ─────────────────────────
    public async Task<(bool, string)> ProposeAsync(
        long fromId, long toId, RelType type, string lang)
    {
        if (fromId == toId)
            return (false, loc.Get("error.bad_request", lang));

        var a = Math.Min(fromId, toId);
        var b = Math.Max(fromId, toId);

        // Kiểm tra đã tồn tại
        var exists = await db.Relationships.AnyAsync(r =>
            r.CharacterA == a && r.CharacterB == b && r.RelType == type);
        if (exists) return (false, loc.Get("error.bad_request", lang));

        // Kiểm tra giới hạn
        if (MaxPerType.TryGetValue(type, out var max))
        {
            var count = await db.Relationships.CountAsync(r =>
                (r.CharacterA == fromId || r.CharacterB == fromId) && r.RelType == type);
            if (count >= max)
                return (false, loc.Get("error.bad_request", lang));
        }

        // Điều kiện đặc biệt
        if (type == RelType.Married)
        {
            var alreadyMarried = await db.Relationships.AnyAsync(r =>
                (r.CharacterA == fromId || r.CharacterB == fromId)
                && r.RelType == RelType.Married);
            if (alreadyMarried)
                return (false, loc.Get("error.bad_request", lang));
        }

        db.Relationships.Add(new Relationship
        {
            CharacterA  = a,
            CharacterB  = b,
            RelType     = type,
            InitiatedBy = fromId,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Remove ──────────────────────────────────────────────
    public async Task<(bool, string)> RemoveAsync(
        long charId, long partnerId, RelType type, string lang)
    {
        var a = Math.Min(charId, partnerId);
        var b = Math.Max(charId, partnerId);

        var rel = await db.Relationships
            .FirstOrDefaultAsync(r => r.CharacterA == a
                && r.CharacterB == b && r.RelType == type);

        if (rel is null) return (false, loc.Get("error.not_found", lang));

        db.Relationships.Remove(rel);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get all relationships ───────────────────────────────
    public async Task<List<RelationshipDto>> GetAllAsync(long charId)
    {
        var rels = await db.Relationships
            .Where(r => r.CharacterA == charId || r.CharacterB == charId)
            .Include(r => r.A)
            .Include(r => r.B)
            .ToListAsync();

        return rels.Select(r =>
        {
            var partner = r.CharacterA == charId ? r.B : r.A;
            return new RelationshipDto(
                r.Id,
                partner.Id,
                partner.Name,
                r.RelType,
                r.Intimacy,
                state.IsOnline(partner.Id)
            );
        }).ToList();
    }

    // ─── Add intimacy ────────────────────────────────────────
    public async Task AddIntimacyAsync(long charId, long partnerId, int amount)
    {
        var a = Math.Min(charId, partnerId);
        var b = Math.Max(charId, partnerId);

        await db.Relationships
            .Where(r => r.CharacterA == a && r.CharacterB == b)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Intimacy, r => r.Intimacy + amount));
    }

    // ─── Get single ──────────────────────────────────────────
    public async Task<RelationshipDto?> GetAsync(long charA, long charB, RelType type)
    {
        var a = Math.Min(charA, charB);
        var b = Math.Max(charA, charB);

        var rel = await db.Relationships
            .Include(r => r.A).Include(r => r.B)
            .FirstOrDefaultAsync(r => r.CharacterA == a
                && r.CharacterB == b && r.RelType == type);

        if (rel is null) return null;

        var partner = rel.CharacterA == charA ? rel.B : rel.A;
        return new RelationshipDto(
            rel.Id, partner.Id, partner.Name,
            rel.RelType, rel.Intimacy, state.IsOnline(partner.Id));
    }
}
