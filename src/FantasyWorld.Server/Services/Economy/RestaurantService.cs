// FantasyWorld.Server/Services/Economy/RestaurantService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Economy;

public interface IRestaurantService
{
    Task<(bool Ok, string Msg, long AppId)> ApplyAsync(long charId, ApplyRestaurantRequest req, string lang);
    Task<(bool Ok, string Msg)>             VoteAsync(long charId, long applicationId, string lang);
    Task<(bool Ok, string Msg)>             AddMenuItemAsync(long ownerId, long restaurantId, int itemId, int price, int? dailyLimit, string lang);
    Task<(bool Ok, string Msg)>             RemoveMenuItemAsync(long ownerId, long menuItemId, string lang);
    Task<(bool Ok, string Msg)>             ToggleOpenAsync(long ownerId, long restaurantId, bool isOpen, string lang);
    Task<(bool Ok, string Msg, int Gold)>   OrderAsync(long charId, OrderRequest req, string lang);
    Task<List<RestaurantDto>>               GetOnMapAsync(int mapId);
    Task<List<RestaurantMenuItemDto>>       GetMenuAsync(long restaurantId);
    Task                                    ProcessVotingAsync();   // cron kiểm tra vote
    Task                                    SpawnNpcOrdersAsync();  // cron NPC ghé mua
}

public class RestaurantService(
    GameDbContext           db,
    ILocalizationService    loc,
    ILogger<RestaurantService> logger) : IRestaurantService
{
    private const int VoteThreshold   = 10;  // cần ít nhất N vote để được duyệt
    private const int VotingDays      = 7;   // thời gian voting
    private const int MaxNpcOrdersDay = 20;  // NPC mua tối đa N lần/ngày/quán

    // ─── Apply ───────────────────────────────────────────────
    public async Task<(bool, string, long)> ApplyAsync(
        long charId, ApplyRestaurantRequest req, string lang)
    {
        // Kiểm tra chưa có đơn pending
        var hasPending = await db.RestaurantApplications
            .AnyAsync(a => a.ApplicantId == charId && a.Status == "voting");
        if (hasPending)
            return (false, loc.Get("error.bad_request", lang), 0);

        // Kiểm tra server còn chỗ không
        var type = await db.RestaurantTypes.FindAsync(req.TypeId);
        if (type is null) return (false, loc.Get("error.not_found", lang), 0);

        var currentCount = await db.Restaurants.CountAsync(r =>
            r.TypeId == req.TypeId);
        if (currentCount >= type.MaxPerServer)
            return (false, loc.Get("error.bad_request", lang), 0);

        var app = new RestaurantApplication
        {
            ApplicantId = charId,
            TypeId      = req.TypeId,
            Name        = req.Name.Trim(),
            Description = req.Description,
            MapId       = req.MapId,
            Status      = "voting",
            VoteEndsAt  = DateTime.UtcNow.AddDays(VotingDays),
        };
        db.RestaurantApplications.Add(app);
        await db.SaveChangesAsync();

        return (true, "OK", app.Id);
    }

    // ─── Vote ────────────────────────────────────────────────
    public async Task<(bool, string)> VoteAsync(
        long charId, long applicationId, string lang)
    {
        var app = await db.RestaurantApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId
                && a.Status == "voting"
                && a.VoteEndsAt > DateTime.UtcNow);
        if (app is null) return (false, loc.Get("error.not_found", lang));

        // Không tự vote cho mình
        if (app.ApplicantId == charId)
            return (false, loc.Get("error.forbidden", lang));

        // Đã vote chưa?
        var voted = await db.RestaurantVotes
            .AnyAsync(v => v.ApplicationId == applicationId && v.VoterId == charId);
        if (voted) return (false, loc.Get("error.bad_request", lang));

        db.RestaurantVotes.Add(new RestaurantVote
        {
            ApplicationId = applicationId,
            VoterId       = charId,
        });
        app.VoteCount++;
        await db.SaveChangesAsync();

        return (true, "OK");
    }

    // ─── Add menu item ───────────────────────────────────────
    public async Task<(bool, string)> AddMenuItemAsync(
        long ownerId, long restaurantId, int itemId, int price, int? dailyLimit, string lang)
    {
        var rest = await db.Restaurants
            .FirstOrDefaultAsync(r => r.Id == restaurantId && r.OwnerId == ownerId);
        if (rest is null) return (false, loc.Get("error.forbidden", lang));

        var item = await db.Items.FindAsync(itemId);
        if (item is null || item.ItemType.ToString().ToLower() != "food")
            return (false, loc.Get("error.bad_request", lang));

        var exists = await db.RestaurantMenuItems
            .AnyAsync(m => m.RestaurantId == restaurantId && m.ItemId == itemId);
        if (exists) return (false, loc.Get("error.bad_request", lang));

        db.RestaurantMenuItems.Add(new RestaurantMenuItem
        {
            RestaurantId = restaurantId,
            ItemId       = itemId,
            Price        = price,
            DailyLimit   = dailyLimit,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Remove menu item ────────────────────────────────────
    public async Task<(bool, string)> RemoveMenuItemAsync(
        long ownerId, long menuItemId, string lang)
    {
        var item = await db.RestaurantMenuItems
            .Include(m => m.Restaurant)
            .FirstOrDefaultAsync(m => m.Id == menuItemId
                && m.Restaurant.OwnerId == ownerId);
        if (item is null) return (false, loc.Get("error.not_found", lang));

        db.RestaurantMenuItems.Remove(item);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Toggle open ─────────────────────────────────────────
    public async Task<(bool, string)> ToggleOpenAsync(
        long ownerId, long restaurantId, bool isOpen, string lang)
    {
        var rest = await db.Restaurants
            .FirstOrDefaultAsync(r => r.Id == restaurantId && r.OwnerId == ownerId);
        if (rest is null) return (false, loc.Get("error.forbidden", lang));

        rest.IsOpen = isOpen;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Order ───────────────────────────────────────────────
    public async Task<(bool, string, int)> OrderAsync(
        long charId, OrderRequest req, string lang)
    {
        var menuItem = await db.RestaurantMenuItems
            .Include(m => m.Restaurant)
            .Include(m => m.Item)
            .FirstOrDefaultAsync(m => m.Id == req.MenuItemId
                && m.RestaurantId == req.RestaurantId
                && m.IsAvailable
                && m.Restaurant.IsOpen);

        if (menuItem is null)
            return (false, loc.Get("shop.out_of_stock", lang), 0);

        // Daily limit
        if (menuItem.DailyLimit.HasValue &&
            menuItem.SoldToday + req.Quantity > menuItem.DailyLimit.Value)
            return (false, loc.Get("shop.out_of_stock", lang), 0);

        var totalCost = menuItem.Price * req.Quantity;

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < totalCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = totalCost, have = char_.Gold }), 0);

        // Trừ gold
        char_.Gold -= totalCost;
        menuItem.SoldToday += req.Quantity;

        // Cộng gold cho owner (trừ 5% phí)
        var fee      = (int)(totalCost * 0.05);
        var ownerNet = totalCost - fee;
        await db.Characters
            .Where(c => c.Id == menuItem.Restaurant.OwnerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + ownerNet));

        menuItem.Restaurant.TotalRevenue += totalCost;
        menuItem.Restaurant.Reputation   += 1;

        // Thêm food vào inventory player
        await AddToInventoryAsync(charId, menuItem.ItemId, req.Quantity, 99);

        // Ghi order
        db.RestaurantOrders.Add(new RestaurantOrder
        {
            RestaurantId = req.RestaurantId,
            CustomerId   = charId,
            MenuItemId   = req.MenuItemId,
            Quantity     = req.Quantity,
            TotalPrice   = totalCost,
            OrderType    = "player",
            Status       = "served",
        });

        await db.SaveChangesAsync();

        return (true, loc.Get("shop.buy_success", lang, new
        {
            item  = menuItem.Item.Name,
            qty   = req.Quantity,
            price = totalCost
        }), (int)char_.Gold);
    }

    // ─── Get on map ──────────────────────────────────────────
    public async Task<List<RestaurantDto>> GetOnMapAsync(int mapId) =>
        await db.Restaurants
            .Where(r => r.MapId == mapId && r.IsOpen)
            .Include(r => r.Owner)
            .Include(r => r.MenuItems)
            .Select(r => new RestaurantDto(
                r.Id, r.Name,
                r.TypeId.ToString(),
                r.Owner.Name,
                r.Level, r.Reputation,
                r.IsOpen,
                r.MenuItems.Count(m => m.IsAvailable)))
            .ToListAsync();

    public async Task<List<RestaurantMenuItemDto>> GetMenuAsync(long restaurantId) =>
        await db.RestaurantMenuItems
            .Where(m => m.RestaurantId == restaurantId)
            .Include(m => m.Item)
            .Select(m => new RestaurantMenuItemDto(
                m.Id, m.ItemId, m.Item.Name,
                m.Price, m.IsAvailable,
                m.DailyLimit, m.SoldToday))
            .ToListAsync();

    // ─── Process voting (cron) ───────────────────────────────
    public async Task ProcessVotingAsync()
    {
        // Duyệt đơn đủ vote
        var readyApps = await db.RestaurantApplications
            .Include(a => a.Type)
            .Where(a => a.Status == "voting"
                && a.VoteCount >= VoteThreshold
                && a.VoteEndsAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var app in readyApps)
        {
            // Kiểm tra server còn chỗ
            var count = await db.Restaurants.CountAsync(r => r.TypeId == app.TypeId);
            if (count >= app.Type.MaxPerServer) { app.Status = "rejected"; continue; }

            app.Status    = "approved";
            app.ApprovedAt = DateTime.UtcNow;

            db.Restaurants.Add(new Restaurant
            {
                ApplicationId = app.Id,
                OwnerId       = app.ApplicantId,
                TypeId        = app.TypeId,
                Name          = app.Name,
                MapId         = app.MapId,
            });
            logger.LogInformation("Restaurant approved: {Name} (appId={Id})", app.Name, app.Id);
        }

        // Từ chối đơn hết hạn không đủ vote
        await db.RestaurantApplications
            .Where(a => a.Status == "voting" && a.VoteEndsAt <= DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "rejected"));

        await db.SaveChangesAsync();
    }

    // ─── NPC orders (cron) ───────────────────────────────────
    public async Task SpawnNpcOrdersAsync()
    {
        var restaurants = await db.Restaurants
            .Where(r => r.IsOpen)
            .Include(r => r.MenuItems.Where(m => m.IsAvailable))
            .ToListAsync();

        var rng = new Random();
        foreach (var rest in restaurants)
        {
            if (!rest.MenuItems.Any()) continue;

            // Random 1-5 NPC orders per restaurant per tick
            var npcOrders = rng.Next(1, 6);
            var todayOrders = await db.RestaurantOrders
                .CountAsync(o => o.RestaurantId == rest.Id
                    && o.OrderType == "npc"
                    && o.OrderedAt.Date == DateTime.UtcNow.Date);

            if (todayOrders >= MaxNpcOrdersDay) continue;

            var toAdd = Math.Min(npcOrders, MaxNpcOrdersDay - todayOrders);
            for (int i = 0; i < toAdd; i++)
            {
                var menu = rest.MenuItems[rng.Next(rest.MenuItems.Count)];
                var qty  = rng.Next(1, 3);

                if (menu.DailyLimit.HasValue &&
                    menu.SoldToday + qty > menu.DailyLimit.Value) continue;

                menu.SoldToday += qty;
                rest.TotalRevenue += (long)(menu.Price * qty);
                rest.Reputation   += 1;

                // Cộng gold owner
                var npcRevenue = menu.Price * qty;
                await db.Characters
                    .Where(c => c.Id == rest.OwnerId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Gold, c => c.Gold + npcRevenue));

                db.RestaurantOrders.Add(new RestaurantOrder
                {
                    RestaurantId = rest.Id,
                    MenuItemId   = menu.Id,
                    Quantity     = qty,
                    TotalPrice   = npcRevenue,
                    OrderType    = "npc",
                    Status       = "served",
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task AddToInventoryAsync(long charId, int itemId, int qty, int maxStack)
    {
        var existing = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == itemId && i.Quantity < maxStack);
        if (existing is not null) { existing.Quantity += qty; return; }

        var maxSlot = await db.Inventories
            .Where(i => i.CharacterId == charId)
            .MaxAsync(i => (int?)i.Slot) ?? -1;
        db.Inventories.Add(new InventoryItem
        {
            CharacterId = charId, ItemId = itemId,
            Quantity = qty, Slot = maxSlot + 1,
        });
    }
}
