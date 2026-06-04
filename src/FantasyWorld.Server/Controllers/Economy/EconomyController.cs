// FantasyWorld.Server/Controllers/Economy/EconomyController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FantasyWorld.Server.Services.Economy;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Controllers.Economy;

// ─── Shop Controller ─────────────────────────────────────────

[ApiController, Route("api/shops"), Authorize]
public class ShopController(IShopService shopSvc) : GameControllerBase
{
    [HttpGet("npc/{shopId}")]
    public async Task<IActionResult> GetNpcShop(int shopId) =>
        Ok(new { success = true, data = await shopSvc.GetNpcShopItemsAsync(shopId, CharId, Lang) });

    [HttpPost("npc/buy")]
    public async Task<IActionResult> BuyFromNpc([FromBody] BuyItemRequest req)
    {
        var (ok, msg, newGold) = await shopSvc.BuyFromNpcAsync(CharId, req.ShopItemId, req.Quantity, Lang);
        return ok ? Ok(new { success = true, message = msg, gold = newGold })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("npc/sell")]
    public async Task<IActionResult> SellToNpc([FromBody] SellItemRequest req)
    {
        var (ok, msg, earned) = await shopSvc.SellToNpcAsync(CharId, req.InventoryId, req.Quantity, Lang);
        return ok ? Ok(new { success = true, message = msg, goldEarned = earned })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("player/my")]
    public async Task<IActionResult> GetMyShop()
    {
        var shop = await shopSvc.GetMyShopAsync(CharId);
        return shop is null
            ? NotFound(new { success = false })
            : Ok(new { success = true, data = shop });
    }

    [HttpPost("player")]
    public async Task<IActionResult> CreateShop([FromBody] CreateShopRequest req)
    {
        var (ok, msg, shopId) = await shopSvc.CreatePlayerShopAsync(
            CharId, req.Name, req.ShopType, req.MapId, Lang);
        return ok ? StatusCode(201, new { success = true, shopId })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("player/map/{mapId}")]
    public async Task<IActionResult> GetShopsOnMap(int mapId) =>
        Ok(new { success = true, data = await shopSvc.GetShopsOnMapAsync(mapId) });

    [HttpGet("player/{shopId}/listings")]
    public async Task<IActionResult> GetListings(long shopId) =>
        Ok(new { success = true, data = await shopSvc.GetShopListingsAsync(shopId) });

    [HttpPost("player/listing")]
    public async Task<IActionResult> AddListing([FromBody] AddListingRequest req)
    {
        var (ok, msg) = await shopSvc.AddListingAsync(CharId, req.ItemId, req.Quantity, req.Price, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("player/listing/{listingId}/buy")]
    public async Task<IActionResult> BuyFromPlayer(long listingId, [FromBody] BuyListingRequest req)
    {
        var (ok, msg) = await shopSvc.BuyFromPlayerShopAsync(CharId, listingId, req.Quantity, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("player/listing/{listingId}")]
    public async Task<IActionResult> RemoveListing(long listingId)
    {
        var (ok, msg) = await shopSvc.RemoveListingAsync(CharId, listingId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPatch("player/toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleShopRequest req)
    {
        var (ok, _) = await shopSvc.ToggleShopAsync(CharId, req.IsOpen, Lang);
        return ok ? Ok(new { success = true }) : BadRequest();
    }
}

// ─── Auction Controller ──────────────────────────────────────

[ApiController, Route("api/auction"), Authorize]
public class AuctionController(IAuctionService auctionSvc) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetActive(
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(new { success = true, data = await auctionSvc.GetActiveAsync(type, page, pageSize) });

    [HttpGet("{auctionId}")]
    public async Task<IActionResult> GetById(long auctionId)
    {
        var a = await auctionSvc.GetByIdAsync(auctionId);
        return a is null ? NotFound() : Ok(new { success = true, data = a });
    }

    [HttpGet("{auctionId}/bids")]
    public async Task<IActionResult> GetBids(long auctionId) =>
        Ok(new { success = true, data = await auctionSvc.GetBidsAsync(auctionId) });

    [HttpGet("my")]
    public async Task<IActionResult> GetMy() =>
        Ok(new { success = true, data = await auctionSvc.GetMyListingsAsync(CharId) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAuctionRequest req)
    {
        var (ok, msg, id) = await auctionSvc.CreateAsync(CharId, req, Lang);
        return ok ? StatusCode(201, new { success = true, auctionId = id })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{auctionId}/bid")]
    public async Task<IActionResult> Bid(long auctionId, [FromBody] PlaceBidRequest req)
    {
        var (ok, msg) = await auctionSvc.PlaceBidAsync(CharId, auctionId, req.Amount, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{auctionId}/buyout")]
    public async Task<IActionResult> Buyout(long auctionId)
    {
        var (ok, msg) = await auctionSvc.BuyoutAsync(CharId, auctionId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("{auctionId}")]
    public async Task<IActionResult> Cancel(long auctionId)
    {
        var (ok, msg) = await auctionSvc.CancelAsync(CharId, auctionId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }
}

// ─── Market Controller ───────────────────────────────────────

[ApiController, Route("api/market")]
public class MarketController(IMarketPriceService marketSvc) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        Ok(new { success = true, data = await marketSvc.GetAllPricesAsync(page, pageSize) });

    [HttpGet("{itemId}")]
    public async Task<IActionResult> GetPrice(int itemId)
    {
        var p = await marketSvc.GetPriceAsync(itemId);
        return p is null ? NotFound() : Ok(new { success = true, data = p });
    }

    [HttpGet("{itemId}/history")]
    public async Task<IActionResult> GetHistory(int itemId, [FromQuery] int hours = 24)
    {
        var h = await marketSvc.GetHistoryAsync(itemId, hours);
        return Ok(new { success = true, data = h.Select(x => new
        {
            x.Price, x.Supply, x.Demand,
            RecordedAt = new DateTimeOffset(x.RecordedAt).ToUnixTimeMilliseconds()
        })});
    }
}

// ─── Restaurant Controller ───────────────────────────────────

[ApiController, Route("api/restaurants"), Authorize]
public class RestaurantController(IRestaurantService restSvc) : GameControllerBase
{
    [HttpGet("map/{mapId}")]
    public async Task<IActionResult> GetOnMap(int mapId) =>
        Ok(new { success = true, data = await restSvc.GetOnMapAsync(mapId) });

    [HttpGet("{restaurantId}/menu")]
    public async Task<IActionResult> GetMenu(long restaurantId) =>
        Ok(new { success = true, data = await restSvc.GetMenuAsync(restaurantId) });

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyRestaurantRequest req)
    {
        var (ok, msg, appId) = await restSvc.ApplyAsync(CharId, req, Lang);
        return ok ? StatusCode(201, new { success = true, applicationId = appId })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("applications/{appId}/vote")]
    public async Task<IActionResult> Vote(long appId)
    {
        var (ok, msg) = await restSvc.VoteAsync(CharId, appId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{restaurantId}/menu")]
    public async Task<IActionResult> AddMenuItem(long restaurantId, [FromBody] AddMenuRequest req)
    {
        var (ok, msg) = await restSvc.AddMenuItemAsync(
            CharId, restaurantId, req.ItemId, req.Price, req.DailyLimit, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpDelete("menu/{menuItemId}")]
    public async Task<IActionResult> RemoveMenuItem(long menuItemId)
    {
        var (ok, msg) = await restSvc.RemoveMenuItemAsync(CharId, menuItemId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{restaurantId}/toggle")]
    public async Task<IActionResult> Toggle(long restaurantId, [FromBody] ToggleShopRequest req)
    {
        var (ok, _) = await restSvc.ToggleOpenAsync(CharId, restaurantId, req.IsOpen, Lang);
        return ok ? Ok(new { success = true }) : BadRequest();
    }

    [HttpPost("order")]
    public async Task<IActionResult> Order([FromBody] OrderRequest req)
    {
        var (ok, msg, gold) = await restSvc.OrderAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true, message = msg, gold })
                  : BadRequest(new { success = false, message = msg });
    }
}

// ─── Shared base controller ──────────────────────────────────

public abstract class GameControllerBase : ControllerBase
{
    protected long   CharId => long.Parse(User.FindFirst("charId")?.Value ?? "0");
    protected string Lang   => Request.Headers["Accept-Language"]
        .FirstOrDefault()?.Split(',')[0].Split('-')[0] ?? "vi";
}

// ─── Request records ─────────────────────────────────────────

public record CreateShopRequest(string Name, string ShopType, int MapId);
public record AddListingRequest(int ItemId, int Quantity, int Price);
public record BuyListingRequest(int Quantity);
public record ToggleShopRequest(bool IsOpen);
public record AddMenuRequest(int ItemId, int Price, int? DailyLimit = null);
