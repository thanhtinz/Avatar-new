// FantasyWorld.Server/BackgroundServices/WorldTimeService.cs
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.BackgroundServices;

/// <summary>
/// Static accessor cho world time — dùng ở GameHub
/// </summary>
public static class WorldTimeService
{
    private static WorldTimeState _current = new(8, 0, true, SeasonCode.Spring, 0, 1);
    public static WorldTimeState Current => _current;
    internal static void Update(WorldTimeState s) => _current = s;
}

public record WorldTimeState(
    int        GameHour,
    int        GameMinute,
    bool       IsDay,
    SeasonCode Season,
    int        MoonPhase,
    int        DayCount
);

/// <summary>
/// BackgroundService chạy game world clock
/// </summary>
public class WorldTimeBackgroundService(
    IServiceProvider services,
    IConfiguration   config,
    ILogger<WorldTimeBackgroundService> logger)
    : BackgroundService
{
    private static readonly (SeasonCode Code, string Vi, string En)[] Seasons =
    [
        (SeasonCode.Spring, "Mùa Xuân", "Spring"),
        (SeasonCode.Summer, "Mùa Hạ",  "Summer"),
        (SeasonCode.Autumn, "Mùa Thu",  "Autumn"),
        (SeasonCode.Winter, "Mùa Đông", "Winter"),
    ];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var realMinutesPerDay = int.Parse(config["Game:RealMinutesPerDay"] ?? "60");
        var tickIntervalMs    = 60_000; // 1 phút thực

        // Ticks per game day
        double ticksPerDay = realMinutesPerDay;
        int tickCount      = 0;
        int moonPhase      = 0;
        int dayCount       = 1;
        var seasonIndex    = 0;

        logger.LogInformation("WorldTime started — {Min} real minutes per game day", realMinutesPerDay);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(tickIntervalMs, ct);
            tickCount++;

            // Giờ game
            var gameHour   = (int)(tickCount / (ticksPerDay / 24)) % 24;
            var gameMinute = (int)(tickCount % (ticksPerDay / 24) * (60.0 / (ticksPerDay / 24)));
            var isDay      = gameHour is >= 6 and < 20;

            // Ngày mới
            bool newDay = tickCount > 0 && tickCount % (int)ticksPerDay == 0;
            if (newDay)
            {
                dayCount++;
                moonPhase  = (moonPhase + 1) % 28;

                // Đổi mùa mỗi 30 ngày game
                var newSeasonIdx = ((dayCount - 1) / 30) % 4;
                var seasonChanged = newSeasonIdx != seasonIndex;
                seasonIndex = newSeasonIdx;

                await BroadcastAsync(hub =>
                {
                    hub.OnNewDay(dayCount, Seasons[seasonIndex].Code, moonPhase);
                    if (seasonChanged)
                        hub.OnSeasonChange(new SeasonChangeDto(
                            Seasons[seasonIndex].Code,
                            Seasons[seasonIndex].Vi,
                            Seasons[seasonIndex].En));
                    if (moonPhase == 14)
                        hub.OnFullMoon(dayCount);
                });

                logger.LogDebug("New day: {Day}, season: {Season}, moon: {Moon}",
                    dayCount, Seasons[seasonIndex].Code, moonPhase);
            }

            // Cập nhật static state (để GameHub đọc)
            WorldTimeService.Update(new WorldTimeState(
                gameHour, gameMinute, isDay,
                Seasons[seasonIndex].Code,
                moonPhase, dayCount));

            // Broadcast world time
            var wt = new WorldTimeDto(
                gameHour, gameMinute, isDay,
                Seasons[seasonIndex].Code,
                moonPhase,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await BroadcastAsync(hub => hub.OnWorldTime(wt));

            // Thông báo bình minh / hoàng hôn
            if (gameHour == 6 && gameMinute == 0)
                await BroadcastAsync(h => h.OnSystemMessage("day_start",
                    "☀️ Bình minh ló rạng tại thế giới Fantasy",
                    "☀️ Dawn breaks over the Fantasy World"));

            if (gameHour == 20 && gameMinute == 0)
                await BroadcastAsync(h => h.OnSystemMessage("night_start",
                    "🌙 Màn đêm buông xuống...",
                    "🌙 Darkness falls..."));
        }
    }

    private async Task BroadcastAsync(Action<Shared.Interfaces.IGameHubReceiver> action)
    {
        // Dùng MagicOnion IHubContext để broadcast
        try
        {
            using var scope = services.CreateScope();
            var hubContext  = scope.ServiceProvider
                .GetRequiredService<MagicOnion.Server.Hubs.IStreamingHubContext<
                    Shared.Interfaces.IGameHub,
                    Shared.Interfaces.IGameHubReceiver>>();
            // Broadcast toàn bộ clients
            action(hubContext.Group("world").CreateBroadcaster());
        }
        catch (Exception ex)
        {
            logger.LogDebug("Broadcast skipped: {Msg}", ex.Message);
        }
    }
}
