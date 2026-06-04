// FantasyWorld.Server/Services/Gameplay/CitizenHotelStallService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;

namespace FantasyWorld.Server.Services.Gameplay;

// ─── Citizen Card ────────────────────────────────────────────

public interface ICitizenCardService
{
    Task<CitizenCardProfileDto?> GetAsync(long charId);
    Task<CitizenCardProfileDto?> GetByNameAsync(string charName);
    Task<(bool Ok, string Msg)>  UpdateAsync(long charId, string? bio, string? hobbies,
        string? favPetName, string? favRegion, string lang);
    Task                         RecordViewAsync(long viewerCharId, long targetCharId);
    Task<List<CitizenCardViewDto>> GetRecentViewersAsync(long charId, int count);
}

public class CitizenCardService(
    GameDbContext db, ILocalizationService loc,
    IGameStateService state) : ICitizenCardService
{
    public async Task<CitizenCardProfileDto?> GetAsync(long charId)
    {
        var char_ = await db.Characters
            .Include(c => c.FactionMemberships).ThenInclude(fm => fm.Faction)
            .Include(c => c.AcademyEnrollments).ThenInclude(ae => ae.Academy)
            .Include(c => c.ActiveTitle)
            .FirstOrDefaultAsync(c => c.Id == charId);
        if (char_ is null) return null;

        var card = await db.CitizenCards.FindAsync(charId)
            ?? new CitizenCard { CharacterId = charId };

        var fame = await db.CharacterFameStats.FindAsync(charId);

        return new CitizenCardProfileDto(
            char_.Id, char_.Name, char_.Level,
            char_.ActiveTitle?.Title.Name,
            char_.FactionMemberships.FirstOrDefault()?.Faction.Name,
            char_.AcademyEnrollments.FirstOrDefault()?.Academy.Name,
            card.Bio, card.Hobbies, card.FavPetName, card.FavRegion,
            fame?.FishCaught ?? 0, fame?.PetsOwned ?? 0,
            fame?.HouseScore ?? 0, fame?.FashionScore ?? 0,
            state.IsOnline(charId),
            char_.CreatedAt);
    }

    public async Task<CitizenCardProfileDto?> GetByNameAsync(string charName)
    {
        var char_ = await db.Characters.FirstOrDefaultAsync(c => c.Name == charName);
        return char_ is null ? null : await GetAsync(char_.Id);
    }

    public async Task<(bool, string)> UpdateAsync(
        long charId, string? bio, string? hobbies, string? favPetName, string? favRegion, string lang)
    {
        if (bio?.Length > 500) return (false, loc.Get("error.bad_request", lang));
        if (hobbies?.Length > 200) return (false, loc.Get("error.bad_request", lang));

        var card = await db.CitizenCards.FindAsync(charId);
        if (card is null)
        {
            card = new CitizenCard { CharacterId = charId };
            db.CitizenCards.Add(card);
        }

        if (bio       != null) card.Bio       = bio;
        if (hobbies   != null) card.Hobbies   = hobbies;
        if (favPetName != null) card.FavPetName = favPetName;
        if (favRegion != null) card.FavRegion  = favRegion;

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task RecordViewAsync(long viewerCharId, long targetCharId)
    {
        if (viewerCharId == targetCharId) return;

        db.CitizenCardViews.Add(new CitizenCardView
        {
            ViewerId   = viewerCharId,
            TargetId   = targetCharId,
            ViewedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<CitizenCardViewDto>> GetRecentViewersAsync(long charId, int count) =>
        await db.CitizenCardViews
            .Where(v => v.TargetId == charId)
            .Include(v => v.Viewer)
            .OrderByDescending(v => v.ViewedAt)
            .Take(count)
            .Select(v => new CitizenCardViewDto(
                v.ViewerId, v.Viewer.Name, v.Viewer.Level,
                new DateTimeOffset(v.ViewedAt).ToUnixTimeMilliseconds()))
            .ToListAsync();
}

public record CitizenCardProfileDto(
    long CharId, string Name, int Level,
    string? TitleName, string? FactionName, string? AcademyName,
    string? Bio, string? Hobbies, string? FavPetName, string? FavRegion,
    int FishCaught, int PetsOwned, int HouseScore, int FashionScore,
    bool IsOnline, DateTime JoinedAt);

public record CitizenCardViewDto(
    long ViewerId, string ViewerName, int ViewerLevel, long ViewedAtMs);

// ─── Hotel Service ───────────────────────────────────────────

public interface IHotelService
{
    Task<List<Hotel>>               GetOnMapAsync(int mapId);
    Task<Hotel?>                    GetByIdAsync(long hotelId);
    Task<List<HotelRoom>>           GetRoomsAsync(long hotelId);
    Task<(bool Ok, string Msg, long RentalId)> RentRoomAsync(long charId, long roomId, int days, string lang);
    Task<(bool Ok, string Msg)>     CheckoutAsync(long charId, long roomId, string lang);
    Task<(bool Ok, string Msg)>     DecorateRoomAsync(long charId, long roomId, string decorJson, string lang);
    Task<(bool Ok, string Msg)>     InviteToRoomAsync(long charId, long guestId, string lang);
}

public class HotelService(
    GameDbContext db, ILocalizationService loc,
    ILogger<HotelService> logger) : IHotelService
{
    public async Task<List<Hotel>> GetOnMapAsync(int mapId) =>
        await db.Hotels.Where(h => h.MapId == mapId && h.IsOpen).ToListAsync();

    public async Task<Hotel?> GetByIdAsync(long hotelId) =>
        await db.Hotels.Include(h => h.Rooms).FirstOrDefaultAsync(h => h.Id == hotelId);

    public async Task<List<HotelRoom>> GetRoomsAsync(long hotelId) =>
        await db.HotelRooms
            .Where(r => r.HotelId == hotelId)
            .Include(r => r.ActiveRental)
            .ToListAsync();

    public async Task<(bool, string, long)> RentRoomAsync(
        long charId, long roomId, int days, string lang)
    {
        days = Math.Clamp(days, 1, 30);

        var room = await db.HotelRooms
            .Include(r => r.ActiveRental)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room is null) return (false, loc.Get("error.not_found", lang), 0);

        if (room.ActiveRental is not null
            && room.ActiveRental.CheckoutAt > DateTime.UtcNow)
            return (false, loc.Get("hotel.room_occupied", lang), 0);

        var totalCost = room.PricePerDay * days;
        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < totalCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = totalCost, have = char_.Gold }));

        char_.Gold -= totalCost;

        // Pay hotel owner
        var hotel = await db.Hotels.FindAsync(room.HotelId)!;
        if (hotel!.OwnerId.HasValue)
            await db.Characters.Where(c => c.Id == hotel.OwnerId.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Gold, c => c.Gold + (long)(totalCost * 0.9)));

        var rental = new HotelRental
        {
            RoomId     = roomId,
            RenterId   = charId,
            CheckinAt  = DateTime.UtcNow,
            CheckoutAt = DateTime.UtcNow.AddDays(days),
            TotalCost  = totalCost,
        };
        db.HotelRentals.Add(rental);
        room.ActiveRental = rental;

        await db.SaveChangesAsync();

        logger.LogInformation("Hotel room rented: charId={C} room={R} days={D}",
            charId, roomId, days);
        return (true, "OK", rental.Id);
    }

    public async Task<(bool, string)> CheckoutAsync(long charId, long roomId, string lang)
    {
        var rental = await db.HotelRentals
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.RenterId == charId);
        if (rental is null) return (false, loc.Get("error.not_found", lang));

        rental.CheckoutAt = DateTime.UtcNow; // early checkout
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> DecorateRoomAsync(
        long charId, long roomId, string decorJson, string lang)
    {
        var rental = await db.HotelRentals
            .FirstOrDefaultAsync(r => r.RoomId == roomId
                && r.RenterId == charId && r.CheckoutAt > DateTime.UtcNow);
        if (rental is null) return (false, loc.Get("error.forbidden", lang));

        var room = await db.HotelRooms.FindAsync(roomId)!;
        room!.CustomDecorJson = decorJson;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> InviteToRoomAsync(
        long charId, long guestId, string lang)
    {
        // check renter has active room
        var rental = await db.HotelRentals
            .Include(r => r.Room).ThenInclude(r => r.Hotel)
            .FirstOrDefaultAsync(r => r.RenterId == charId
                && r.CheckoutAt > DateTime.UtcNow);
        if (rental is null) return (false, loc.Get("error.forbidden", lang));

        // Teleport guest to room map
        await db.Characters.Where(c => c.Id == guestId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.MapId, rental.Room.Hotel.MapId));

        await db.SaveChangesAsync();
        return (true, "OK");
    }
}

// ─── Market Stall Service ────────────────────────────────────

public interface IMarketStallService
{
    Task<List<MarketStall>>          GetOnMapAsync(int mapId);
    Task<(bool Ok, string Msg, long RentalId)> RentStallAsync(long charId, RentStallReq req, string lang);
    Task<(bool Ok, string Msg)>      CreateListingAsync(long charId, long rentalId, CreateStallListingReq req, string lang);
    Task<(bool Ok, string Msg)>      BuyFromStallAsync(long buyerId, long listingId, int qty, string lang);
    Task<(bool Ok, string Msg)>      RemoveListingAsync(long charId, long listingId, string lang);
    Task<List<MarketListing>>        GetListingsAsync(long rentalId);
    Task                             ExpireRentalsAsync(); // cron
}

public class MarketStallService(
    GameDbContext db, ILocalizationService loc,
    ILogger<MarketStallService> logger) : IMarketStallService
{
    public async Task<List<MarketStall>> GetOnMapAsync(int mapId) =>
        await db.MarketStalls
            .Include(s => s.CurrentRental).ThenInclude(r => r!.Renter)
            .Where(s => s.MapId == mapId)
            .ToListAsync();

    public async Task<(bool, string, long)> RentStallAsync(
        long charId, RentStallReq req, string lang)
    {
        var stall = await db.MarketStalls
            .Include(s => s.CurrentRental)
            .FirstOrDefaultAsync(s => s.Id == req.StallId);
        if (stall is null) return (false, loc.Get("error.not_found", lang), 0);

        if (stall.CurrentRental is not null
            && stall.CurrentRental.RentedUntil > DateTime.UtcNow
            && stall.CurrentRental.IsActive)
            return (false, loc.Get("error.bad_request", lang), 0);

        var days      = Math.Clamp(req.Days, 1, 30);
        var totalCost = stall.CurrentRental?.RentPriceDay ?? 50 * days;

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < totalCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = totalCost, have = char_.Gold }));

        char_.Gold -= totalCost;

        var rental = new MarketStallRental
        {
            StallId      = req.StallId,
            RenterId     = charId,
            RentedFrom   = DateTime.UtcNow,
            RentedUntil  = DateTime.UtcNow.AddDays(days),
            RentPriceDay = stall.CurrentRental?.RentPriceDay ?? 50,
            IsActive     = true,
        };
        db.MarketStallRentals.Add(rental);
        await db.SaveChangesAsync();

        logger.LogInformation("Stall rented: charId={C} stall={S} days={D}", charId, req.StallId, days);
        return (true, "OK", rental.Id);
    }

    public async Task<(bool, string)> CreateListingAsync(
        long charId, long rentalId, CreateStallListingReq req, string lang)
    {
        var rental = await db.MarketStallRentals
            .FirstOrDefaultAsync(r => r.Id == rentalId
                && r.RenterId == charId && r.IsActive && r.RentedUntil > DateTime.UtcNow);
        if (rental is null) return (false, loc.Get("error.forbidden", lang));

        if (req.ItemId.HasValue)
        {
            var total = await db.Inventories
                .Where(i => i.CharacterId == charId && i.ItemId == req.ItemId)
                .SumAsync(i => i.Quantity);
            if (total < req.Quantity) return (false, loc.Get("inventory.item_not_found", lang));

            // Remove from inventory
            var rows = await db.Inventories
                .Where(i => i.CharacterId == charId && i.ItemId == req.ItemId)
                .OrderBy(i => i.Id).ToListAsync();
            int rem = req.Quantity;
            foreach (var row in rows)
            {
                if (rem <= 0) break;
                var take = Math.Min(row.Quantity, rem);
                row.Quantity -= take; rem -= take;
                if (row.Quantity == 0) db.Inventories.Remove(row);
            }
        }

        db.MarketListings.Add(new MarketListing
        {
            RentalId    = rentalId,
            SellerId    = charId,
            ListingType = req.ListingType,
            ItemId      = req.ItemId,
            PetId       = req.PetId,
            Quantity    = req.Quantity,
            Price       = req.Price,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> BuyFromStallAsync(
        long buyerId, long listingId, int qty, string lang)
    {
        var listing = await db.MarketListings
            .Include(l => l.Rental)
            .Include(l => l.Item)
            .FirstOrDefaultAsync(l => l.Id == listingId
                && !l.IsSold && l.Rental.IsActive && l.Rental.RentedUntil > DateTime.UtcNow);
        if (listing is null) return (false, loc.Get("shop.out_of_stock", lang));
        if (listing.SellerId == buyerId) return (false, loc.Get("error.bad_request", lang));
        if (listing.Quantity < qty) return (false, loc.Get("shop.out_of_stock", lang));

        var total = listing.Price * qty;
        var buyer = await db.Characters.FindAsync(buyerId)!;
        if (buyer!.Gold < total)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = total, have = buyer.Gold }));

        buyer.Gold -= total;
        var fee = (int)(total * 0.05);
        await db.Characters.Where(c => c.Id == listing.SellerId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Gold, c => c.Gold + total - fee));

        listing.Quantity -= qty;
        if (listing.Quantity == 0) { listing.IsSold = true; listing.SoldAt = DateTime.UtcNow; }

        // Give item to buyer
        if (listing.ItemId.HasValue)
        {
            var maxSlot = await db.Inventories.Where(i => i.CharacterId == buyerId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new InventoryItem
            {
                CharacterId = buyerId, ItemId = listing.ItemId.Value,
                Quantity = qty, Slot = maxSlot + 1,
            });
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> RemoveListingAsync(
        long charId, long listingId, string lang)
    {
        var listing = await db.MarketListings
            .FirstOrDefaultAsync(l => l.Id == listingId && l.SellerId == charId && !l.IsSold);
        if (listing is null) return (false, loc.Get("error.not_found", lang));

        // Return item to inventory
        if (listing.ItemId.HasValue)
        {
            var maxSlot = await db.Inventories.Where(i => i.CharacterId == charId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new InventoryItem
            {
                CharacterId = charId, ItemId = listing.ItemId.Value,
                Quantity = listing.Quantity, Slot = maxSlot + 1,
            });
        }
        db.MarketListings.Remove(listing);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<MarketListing>> GetListingsAsync(long rentalId) =>
        await db.MarketListings.Where(l => l.RentalId == rentalId && !l.IsSold)
            .Include(l => l.Item)
            .ToListAsync();

    public async Task ExpireRentalsAsync()
    {
        var expired = await db.MarketStallRentals
            .Where(r => r.IsActive && r.RentedUntil < DateTime.UtcNow)
            .Include(r => r.Listings.Where(l => !l.IsSold))
            .ToListAsync();

        foreach (var rental in expired)
        {
            rental.IsActive = false;
            foreach (var listing in rental.Listings)
            {
                if (listing.ItemId.HasValue)
                {
                    var maxSlot = await db.Inventories.Where(i => i.CharacterId == rental.RenterId)
                        .MaxAsync(i => (int?)i.Slot) ?? -1;
                    db.Inventories.Add(new InventoryItem
                    {
                        CharacterId = rental.RenterId, ItemId = listing.ItemId.Value,
                        Quantity = listing.Quantity, Slot = maxSlot + 1,
                    });
                }
            }
        }

        if (expired.Count > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Expired {N} stall rentals", expired.Count);
        }
    }
}

public record RentStallReq(int StallId, int Days);
public record CreateStallListingReq(string ListingType, int? ItemId, long? PetId, int Quantity, int Price);
