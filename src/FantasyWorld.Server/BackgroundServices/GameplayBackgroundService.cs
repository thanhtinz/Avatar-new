// FantasyWorld.Server/BackgroundServices/GameplayBackgroundService.cs
using FantasyWorld.Server.Services.Gameplay;

namespace FantasyWorld.Server.BackgroundServices;

public class GameplayBackgroundService(
    IServiceProvider services,
    ILogger<GameplayBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("GameplayBackgroundService started");

        await Task.WhenAll(
            RunEveryAsync(TimeSpan.FromHours(24),  PaySalariesAsync,          ct, delay: 2_000),
            RunEveryAsync(TimeSpan.FromHours(1),   FestivalTickAsync,         ct, delay: 4_000),
            RunEveryAsync(TimeSpan.FromHours(24),  LeaderboardSnapshotAsync,  ct, delay: 6_000),
            RunEveryAsync(TimeSpan.FromHours(1),   FactionWarTickAsync,       ct, delay: 3_000),
            RunEveryAsync(TimeSpan.FromMinutes(10),ExpireStallsAsync,         ct, delay: 1_000),
            RunEveryAsync(TimeSpan.FromHours(1),   SeasonTickAsync,           ct, delay: 5_000)
        );
    }

    private async Task RunEveryAsync(TimeSpan interval, Func<Task> action,
        CancellationToken ct, int delay = 0)
    {
        await Task.Delay(delay, ct);
        while (!ct.IsCancellationRequested)
        {
            try   { await action(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogError(ex, "Gameplay task failed: {Task}", action.Method.Name); }
            await Task.Delay(interval, ct);
        }
    }

    private async Task PaySalariesAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICompanyService>()
            .PaySalariesAsync();
    }

    private async Task FestivalTickAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFestivalService>()
            .TickAsync();
    }

    private async Task LeaderboardSnapshotAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ILeaderboardService>()
            .SnapshotAsync();
    }

    private async Task FactionWarTickAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFactionService>()
            .ProcessWarAsync();
    }

    private async Task ExpireStallsAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMarketStallService>()
            .ExpireRentalsAsync();
    }

    private async Task SeasonTickAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISeasonService>()
            .TickAsync();
    }
}
