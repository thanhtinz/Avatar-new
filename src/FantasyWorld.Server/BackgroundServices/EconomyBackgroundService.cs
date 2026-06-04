// FantasyWorld.Server/BackgroundServices/EconomyBackgroundService.cs
using FantasyWorld.Server.Services.Economy;

namespace FantasyWorld.Server.BackgroundServices;

public class EconomyBackgroundService(
    IServiceProvider services,
    ILogger<EconomyBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("EconomyBackgroundService started");

        // Stagger các task để không hit DB cùng lúc
        var priceTask      = RunEveryAsync(TimeSpan.FromMinutes(5),   UpdatePricesAsync,      ct);
        var auctionTask    = RunEveryAsync(TimeSpan.FromMinutes(2),   ProcessAuctionsAsync,   ct);
        var restaurantTask = RunEveryAsync(TimeSpan.FromMinutes(10),  RestaurantTickAsync,    ct);
        var voteTask       = RunEveryAsync(TimeSpan.FromMinutes(30),  ProcessVotesAsync,      ct);

        await Task.WhenAll(priceTask, auctionTask, restaurantTask, voteTask);
    }

    private async Task RunEveryAsync(TimeSpan interval, Func<Task> action, CancellationToken ct)
    {
        // Delay ban đầu ngẫu nhiên để tránh thundering herd
        await Task.Delay(Random.Shared.Next(1000, 5000), ct);

        while (!ct.IsCancellationRequested)
        {
            try { await action(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Economy task failed: {Task}", action.Method.Name);
            }
            await Task.Delay(interval, ct);
        }
    }

    private async Task UpdatePricesAsync()
    {
        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMarketPriceService>();
        await svc.UpdateAllPricesAsync();
    }

    private async Task ProcessAuctionsAsync()
    {
        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAuctionService>();
        await svc.ProcessExpiredAsync();
    }

    private async Task RestaurantTickAsync()
    {
        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRestaurantService>();
        await svc.SpawnNpcOrdersAsync();
    }

    private async Task ProcessVotesAsync()
    {
        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IRestaurantService>();
        await svc.ProcessVotingAsync();
    }
}
