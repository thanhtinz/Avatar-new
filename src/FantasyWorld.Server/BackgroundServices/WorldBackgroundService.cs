// FantasyWorld.Server/BackgroundServices/WorldBackgroundService.cs
using FantasyWorld.Server.Services.World;

namespace FantasyWorld.Server.BackgroundServices;

public class WorldBackgroundService(
    IServiceProvider services,
    ILogger<WorldBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("WorldBackgroundService started");

        await Task.WhenAll(
            RunEveryAsync(TimeSpan.FromMinutes(5),  SpawnWildPetsAsync,       ct, delay: 2000),
            RunEveryAsync(TimeSpan.FromMinutes(10), DecayPetHungerAsync,      ct, delay: 3000),
            RunEveryAsync(TimeSpan.FromMinutes(1),  GrowCropsAsync,           ct, delay: 1000),
            RunEveryAsync(TimeSpan.FromHours(1),    TriggerWorldEventsAsync,  ct, delay: 5000),
            RunEveryAsync(TimeSpan.FromMinutes(2),  ProcessExpiredEventsAsync,ct, delay: 4000),
            RunEveryAsync(TimeSpan.FromMinutes(1),  TickNpcSchedulesAsync,    ct, delay: 500)
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
            { logger.LogError(ex, "World task failed: {Task}", action.Method.Name); }
            await Task.Delay(interval, ct);
        }
    }

    private async Task SpawnWildPetsAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPetService>()
            .SpawnWildPetsAsync();
    }

    private async Task DecayPetHungerAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPetService>()
            .DecayPetHungerAsync();
    }

    private async Task GrowCropsAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFarmService>()
            .GrowCropsAsync();
    }

    private async Task TriggerWorldEventsAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWorldEventService>()
            .TriggerRandomEventsAsync();
    }

    private async Task ProcessExpiredEventsAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWorldEventService>()
            .ProcessExpiredInstancesAsync();
    }

    private async Task TickNpcSchedulesAsync()
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<INpcAiService>()
            .TickNpcSchedulesAsync();
    }
}
