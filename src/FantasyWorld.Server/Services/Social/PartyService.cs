// FantasyWorld.Server/Services/Social/PartyService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Social;

public interface IPartyService
{
    Task<(bool Ok, string Msg, long PartyId)> CreateAsync(long leaderId, int maxMembers, string lang);
    Task<(bool Ok, string Msg)> InviteAsync(long partyId, long inviterId, long targetId, string lang);
    Task<(bool Ok, string Msg)> AcceptInviteAsync(long inviteId, long charId, string lang);
    Task<(bool Ok, string Msg)> DeclineInviteAsync(long inviteId, long charId, string lang);
    Task<(bool Ok, string Msg)> LeaveAsync(long charId, string lang);
    Task<(bool Ok, string Msg)> KickAsync(long leaderId, long targetId, string lang);
    Task<(bool Ok, string Msg)> DisbandAsync(long leaderId, string lang);
    Task<PartyDto?>             GetPartyAsync(long partyId);
    Task<long?>                 GetCharPartyIdAsync(long charId);
    Task<bool>                  IsInPartyAsync(long charId);
}

public class PartyService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<PartyService> logger) : IPartyService
{
    // ─── Create ──────────────────────────────────────────────
    public async Task<(bool, string, long)> CreateAsync(
        long leaderId, int maxMembers, string lang)
    {
        if (await IsInPartyAsync(leaderId))
            return (false, loc.Get("error.bad_request", lang), 0);

        maxMembers = Math.Clamp(maxMembers, 2, 8);

        var party = new Party
        {
            LeaderId   = leaderId,
            MaxMembers = maxMembers,
            Status     = "forming",
        };
        db.Parties.Add(party);
        await db.SaveChangesAsync();

        db.PartyMembers.Add(new PartyMember
        {
            PartyId     = party.Id,
            CharacterId = leaderId,
            Role        = "leader",
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Party created id={Id} leader={Leader}", party.Id, leaderId);
        return (true, "OK", party.Id);
    }

    // ─── Invite ──────────────────────────────────────────────
    public async Task<(bool, string)> InviteAsync(
        long partyId, long inviterId, long targetId, string lang)
    {
        var party = await db.Parties
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == partyId && p.Status == "forming");

        if (party is null) return (false, loc.Get("error.not_found", lang));

        // Chỉ leader/member trong party mới invite
        if (!party.Members.Any(m => m.CharacterId == inviterId))
            return (false, loc.Get("error.forbidden", lang));

        if (party.Members.Count >= party.MaxMembers)
            return (false, loc.Get("error.bad_request", lang));

        if (await IsInPartyAsync(targetId))
            return (false, loc.Get("error.bad_request", lang));

        // Kiểm tra đã có invite pending chưa
        var exists = await db.PartyInvites.AnyAsync(i =>
            i.PartyId == partyId && i.InviteeId == targetId && i.Status == "pending"
            && i.ExpiresAt > DateTime.UtcNow);
        if (exists) return (false, loc.Get("error.bad_request", lang));

        db.PartyInvites.Add(new PartyInvite
        {
            PartyId   = partyId,
            InviterId = inviterId,
            InviteeId = targetId,
            Status    = "pending",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        });
        await db.SaveChangesAsync();

        return (true, "INVITE_SENT");
    }

    // ─── Accept invite ───────────────────────────────────────
    public async Task<(bool, string)> AcceptInviteAsync(long inviteId, long charId, string lang)
    {
        var invite = await db.PartyInvites
            .Include(i => i.Party).ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(i => i.Id == inviteId
                && i.InviteeId == charId
                && i.Status == "pending"
                && i.ExpiresAt > DateTime.UtcNow);

        if (invite is null)
            return (false, loc.Get("error.not_found", lang));

        if (invite.Party.Members.Count >= invite.Party.MaxMembers)
            return (false, loc.Get("error.bad_request", lang));

        if (await IsInPartyAsync(charId))
            return (false, loc.Get("error.bad_request", lang));

        invite.Status = "accepted";
        db.PartyMembers.Add(new PartyMember
        {
            PartyId     = invite.PartyId,
            CharacterId = charId,
            Role        = "member",
        });
        await db.SaveChangesAsync();

        return (true, "OK");
    }

    // ─── Decline invite ──────────────────────────────────────
    public async Task<(bool, string)> DeclineInviteAsync(long inviteId, long charId, string lang)
    {
        var invite = await db.PartyInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId
                && i.InviteeId == charId && i.Status == "pending");

        if (invite is null) return (false, loc.Get("error.not_found", lang));

        invite.Status = "rejected";
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Leave ───────────────────────────────────────────────
    public async Task<(bool, string)> LeaveAsync(long charId, string lang)
    {
        var member = await db.PartyMembers
            .Include(m => m.Party).ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(m => m.CharacterId == charId
                && m.Party.Status != "disbanded");

        if (member is null) return (false, loc.Get("error.bad_request", lang));

        var party = member.Party;

        if (member.Role == "leader")
        {
            // Chuyển leader cho người tiếp theo
            var next = party.Members
                .Where(m => m.CharacterId != charId)
                .OrderBy(m => m.JoinedAt)
                .FirstOrDefault();

            if (next is null)
                return await DisbandAsync(charId, lang);

            next.Role     = "leader";
            party.LeaderId = next.CharacterId;
        }

        db.PartyMembers.Remove(member);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Kick ────────────────────────────────────────────────
    public async Task<(bool, string)> KickAsync(long leaderId, long targetId, string lang)
    {
        var leaderMember = await db.PartyMembers
            .Include(m => m.Party)
            .FirstOrDefaultAsync(m => m.CharacterId == leaderId && m.Role == "leader");

        if (leaderMember is null)
            return (false, loc.Get("error.forbidden", lang));

        var target = await db.PartyMembers
            .FirstOrDefaultAsync(m => m.CharacterId == targetId
                && m.PartyId == leaderMember.PartyId);

        if (target is null) return (false, loc.Get("error.not_found", lang));
        if (target.Role == "leader") return (false, loc.Get("error.forbidden", lang));

        db.PartyMembers.Remove(target);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Disband ─────────────────────────────────────────────
    public async Task<(bool, string)> DisbandAsync(long leaderId, string lang)
    {
        var party = await db.Parties
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.LeaderId == leaderId
                && p.Status != "disbanded");

        if (party is null) return (false, loc.Get("error.not_found", lang));

        party.Status = "disbanded";
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get party ───────────────────────────────────────────
    public async Task<PartyDto?> GetPartyAsync(long partyId)
    {
        var party = await db.Parties
            .Include(p => p.Leader)
            .Include(p => p.Members).ThenInclude(m => m.Character)
            .FirstOrDefaultAsync(p => p.Id == partyId);

        if (party is null) return null;

        var members = party.Members.Select(m =>
        {
            var c = m.Character;
            return new PartyMemberDto(
                c.Id, c.Name, c.Level,
                c.Hp, c.HpMax, c.Mp, c.MpMax,
                state.IsOnline(c.Id),
                m.Role);
        }).ToList();

        return new PartyDto(
            party.Id, party.LeaderId, party.Leader.Name,
            party.Status, party.MaxMembers, members);
    }

    // ─── Helpers ─────────────────────────────────────────────
    public async Task<long?> GetCharPartyIdAsync(long charId)
    {
        return await db.PartyMembers
            .Where(m => m.CharacterId == charId
                && m.Party.Status != "disbanded"
                && m.Party.Status != "completed")
            .Select(m => (long?)m.PartyId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsInPartyAsync(long charId) =>
        await GetCharPartyIdAsync(charId) is not null;
}
