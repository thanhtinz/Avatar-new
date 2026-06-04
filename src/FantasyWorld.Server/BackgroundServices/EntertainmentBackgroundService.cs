// FantasyWorld.Server/BackgroundServices/EntertainmentBackgroundService.cs
using FantasyWorld.Server.Services.Entertainment;

namespace FantasyWorld.Server.BackgroundServices;

public class EntertainmentBackgroundService(
    IServiceProvider services,
    ILogger<EntertainmentBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("EntertainmentBackgroundService started");

        await Task.WhenAll(
            RunEveryAsync(TimeSpan.FromSeconds(5), ProcessRacesAsync,       ct, delay: 2000),
            RunEveryAsync(TimeSpan.FromMinutes(5), AudienceTickAsync,       ct, delay: 3000),
            RunEveryAsync(TimeSpan.FromMinutes(30), ProcessTournamentsAsync, ct, delay: 4000)
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
            { logger.LogError(ex, "Entertainment task failed: {Task}", action.Method.Name); }
            await Task.Delay(interval, ct);
        }
    }

    private async Task ProcessRacesAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMountRaceService>()
            .ProcessRacesAsync();
    }

    private async Task AudienceTickAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPerformanceService>()
            .AudienceTickAsync();
    }

    private async Task ProcessTournamentsAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAcademyService>()
            .ProcessTournamentsAsync();
    }
}
