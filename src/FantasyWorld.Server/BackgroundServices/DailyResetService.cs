// FantasyWorld.Server/BackgroundServices/DailyResetService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;

namespace FantasyWorld.Server.BackgroundServices;

public class DailyResetService(
    IServiceProvider services,
    ILogger<DailyResetService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("DailyResetService started");

        while (!ct.IsCancellationRequested)
        {
            // Đợi đến midnight UTC
            var now        = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var delay       = nextMidnight - now;
            await Task.Delay(delay, ct);

            if (ct.IsCancellationRequested) break;

            await RunDailyResetAsync();
        }
    }

    private async Task RunDailyResetAsync()
    {
        logger.LogInformation("Running daily reset...");

        await Task.WhenAll(
            CleanExpiredSessionsAsync(),
            CleanExpiredBansAsync(),
            ResetRestaurantLimitsAsync(),
            UpdateServerStatsAsync(),
            CheckFestivalsAsync()
        );

        logger.LogInformation("Daily reset completed");
    }

    private async Task CleanExpiredSessionsAsync()
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var deleted = await db.Sessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync();
        if (deleted > 0)
            logger.LogInformation("Cleaned {Count} expired sessions", deleted);
    }

    private async Task CleanExpiredBansAsync()
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var unbanned = await db.Accounts
            .Where(a => a.IsBanned
                     && a.BanUntil.HasValue
                     && a.BanUntil < DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsBanned,   false)
                .SetProperty(a => a.BanReason,  (string?)null)
                .SetProperty(a => a.BanUntil,   (DateTime?)null));
        if (unbanned > 0)
            logger.LogInformation("Auto-unbanned {Count} accounts", unbanned);
    }

    private async Task ResetRestaurantLimitsAsync()
    {
        // Sẽ implement khi build Restaurant module (Phase 3)
        await Task.CompletedTask;
    }

    private async Task UpdateServerStatsAsync()
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var dau = await db.Characters
            .Where(c => c.LastOnline >= yesterday && c.LastOnline < yesterday.AddDays(1))
            .Select(c => c.AccountId)
            .Distinct()
            .CountAsync();

        logger.LogInformation("DAU yesterday: {Dau}", dau);
    }

    private async Task CheckFestivalsAsync()
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var now = DateTime.UtcNow;

        // Kích hoạt festival
        await db.Festivals
            .Where(f => f.StartDate <= now && f.EndDate >= now && !f.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, true));

        // Tắt festival hết hạn
        await db.Festivals
            .Where(f => f.EndDate < now && f.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false));
    }
}
