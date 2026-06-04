// FantasyWorld.Server/Services/Entertainment/CasinoService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Entertainment;

public interface ICasinoService
{
    Task<List<CasinoGameDto>>  GetGamesAsync();
    Task<CasinoResultDto>      PlayAsync(long charId, CasinoBetRequest req, string lang);
    Task<List<CasinoLog>>      GetHistoryAsync(long charId, int count);
    Task<(int Won, int Lost, int NetGold)> GetStatsAsync(long charId);
}

public class CasinoService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<CasinoService> logger) : ICasinoService
{
    private const int DailyBetLimit  = 1_000_000;
    private const int DailyLossLimit =   500_000;

    public async Task<List<CasinoGameDto>> GetGamesAsync() =>
        await db.CasinoGames
            .Where(g => g.IsActive)
            .Select(g => new CasinoGameDto(g.Id, g.Name, g.GameType, g.MinBet, g.MaxBet))
            .ToListAsync();

    // ─── Play ────────────────────────────────────────────────
    public async Task<CasinoResultDto> PlayAsync(
        long charId, CasinoBetRequest req, string lang)
    {
        var game = await db.CasinoGames.FindAsync(req.GameId);
        if (game is null || !game.IsActive)
            throw new InvalidOperationException(loc.Get("error.not_found", lang));

        var amount = Math.Clamp(req.Amount, game.MinBet, game.MaxBet);

        // Kiểm tra daily limit
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = await db.CasinoDailyLimits
            .FirstOrDefaultAsync(l => l.CharacterId == charId && l.Date == today);

        if (limit is not null)
        {
            if (limit.TotalBetToday + amount > DailyBetLimit)
                throw new InvalidOperationException(loc.Get("error.bad_request", lang));
        }

        // Kiểm tra gold
        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < amount)
            throw new InvalidOperationException(
                loc.Get("character.insufficient_gold", lang, new { need = amount, have = char_.Gold }));

        // Trừ gold trước
        char_.Gold -= amount;

        // Chơi game
        var rng = new Random();
        var (won, payout, detail) = game.GameType switch
        {
            "slots"     => PlaySlots(amount, game.HouseEdge, rng),
            "roulette"  => PlayRoulette(amount, game.HouseEdge, req.ExtraJson, rng),
            "card_flip" => PlayCardFlip(amount, game.HouseEdge, rng),
            "wheel"     => PlayWheel(amount, game.HouseEdge, rng),
            "dice"      => PlayDice(amount, game.HouseEdge, req.ExtraJson, rng),
            _           => PlaySlots(amount, game.HouseEdge, rng),
        };

        // Cộng thắng
        if (payout > 0) char_.Gold += payout;

        var netChange = payout - amount;

        // Cập nhật daily limit
        if (limit is null)
        {
            limit = new CasinoDailyLimit
            {
                CharacterId = charId,
                Date        = today,
            };
            db.CasinoDailyLimits.Add(limit);
        }
        limit.TotalBetToday  += amount;
        limit.TotalLossToday += Math.Max(0, amount - payout);

        // Ghi log
        db.CasinoLogs.Add(new CasinoLog
        {
            CharacterId = charId,
            GameId      = req.GameId,
            BetAmount   = amount,
            ResultJson  = detail,
            Payout      = payout,
            NetChange   = netChange,
        });

        await db.SaveChangesAsync();

        logger.LogDebug("Casino: charId={Char} game={Game} bet={Bet} payout={Pay} net={Net}",
            charId, game.GameType, amount, payout, netChange);

        return new CasinoResultDto(won, payout, netChange, detail, char_.Gold);
    }

    public async Task<List<CasinoLog>> GetHistoryAsync(long charId, int count) =>
        await db.CasinoLogs
            .Where(l => l.CharacterId == charId)
            .OrderByDescending(l => l.PlayedAt)
            .Take(Math.Min(count, 100))
            .ToListAsync();

    public async Task<(int, int, int)> GetStatsAsync(long charId)
    {
        var logs = await db.CasinoLogs
            .Where(l => l.CharacterId == charId)
            .ToListAsync();

        return (
            logs.Count(l => l.NetChange > 0),
            logs.Count(l => l.NetChange <= 0),
            logs.Sum(l => l.NetChange)
        );
    }

    // ─── Game implementations ─────────────────────────────────

    private static (bool Won, int Payout, string Detail) PlaySlots(
        int amount, float houseEdge, Random rng)
    {
        // 3 reels, 6 symbols
        var symbols = new[] { "🍒", "🍋", "🍊", "⭐", "💎", "🎰" };
        var reel1   = rng.Next(symbols.Length);
        var reel2   = rng.Next(symbols.Length);
        var reel3   = rng.Next(symbols.Length);

        var detail = $"{symbols[reel1]}{symbols[reel2]}{symbols[reel3]}";

        int multiplier = 0;
        if (reel1 == reel2 && reel2 == reel3)
        {
            multiplier = reel1 switch
            {
                5 => 50,   // 🎰🎰🎰 jackpot
                4 => 20,   // 💎💎💎
                3 => 10,   // ⭐⭐⭐
                _ => 5,
            };
        }
        else if (reel1 == reel2 || reel2 == reel3 || reel1 == reel3)
            multiplier = 2;

        var payout = (int)(amount * multiplier * (1 - houseEdge));
        return (payout > 0, payout, detail);
    }

    private static (bool Won, int Payout, string Detail) PlayRoulette(
        int amount, float houseEdge, string? extraJson, Random rng)
    {
        var spin   = rng.Next(37); // 0-36
        var color  = spin == 0 ? "green" : spin % 2 == 0 ? "red" : "black";
        var detail = $"Ball: {spin} ({color})";

        // Parse bet type từ extraJson
        string betType = "red";
        int betNum = -1;
        if (extraJson is not null)
        {
            var bet = JsonSerializer.Deserialize<Dictionary<string, string>>(extraJson) ?? [];
            betType = bet.GetValueOrDefault("type", "red");
            int.TryParse(bet.GetValueOrDefault("number", "-1"), out betNum);
        }

        bool won = betType switch
        {
            "number" => spin == betNum,
            "red"    => color == "red",
            "black"  => color == "black",
            "even"   => spin != 0 && spin % 2 == 0,
            "odd"    => spin % 2 == 1,
            _        => false,
        };

        int multiplier = betType == "number" ? 35 : 2;
        var payout = won ? (int)(amount * multiplier * (1 - houseEdge)) : 0;
        return (won, payout, detail);
    }

    private static (bool Won, int Payout, string Detail) PlayCardFlip(
        int amount, float houseEdge, Random rng)
    {
        var cards  = new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        var card   = cards[rng.Next(cards.Length)];
        var playerCard = cards[rng.Next(cards.Length)];
        bool won   = string.Compare(card, playerCard) > 0;
        var detail = $"House: {card} vs Player: {playerCard}";
        var payout = won ? (int)(amount * 1.95 * (1 - houseEdge)) : 0;
        return (won, payout, detail);
    }

    private static (bool Won, int Payout, string Detail) PlayWheel(
        int amount, float houseEdge, Random rng)
    {
        // Wheel segments: [2x, 3x, 5x, 0x, 0x, 0x, 1x, 1x, 10x, 0x]
        var segments   = new[] { 2, 3, 5, 0, 0, 0, 1, 1, 10, 0 };
        var landed     = rng.Next(segments.Length);
        var multiplier = segments[landed];
        bool won       = multiplier > 1;
        var detail     = $"Wheel landed on {multiplier}x";
        var payout     = (int)(amount * multiplier * (1 - houseEdge));
        return (won, payout, detail);
    }

    private static (bool Won, int Payout, string Detail) PlayDice(
        int amount, float houseEdge, string? extraJson, Random rng)
    {
        int d1 = rng.Next(1, 7), d2 = rng.Next(1, 7);
        var total   = d1 + d2;
        var detail  = $"🎲{d1} + 🎲{d2} = {total}";

        string guess = "high";
        if (extraJson is not null)
        {
            var bet = JsonSerializer.Deserialize<Dictionary<string, string>>(extraJson) ?? [];
            guess = bet.GetValueOrDefault("guess", "high");
        }

        bool won = guess switch
        {
            "high" => total >= 8,
            "low"  => total <= 6,
            "seven"=> total == 7,
            _      => false,
        };

        int mult   = guess == "seven" ? 4 : 2;
        var payout = won ? (int)(amount * mult * (1 - houseEdge)) : 0;
        return (won, payout, detail);
    }
}
