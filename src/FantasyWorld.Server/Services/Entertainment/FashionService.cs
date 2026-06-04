// FantasyWorld.Server/Services/Entertainment/FashionService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Entertainment;

public interface IFashionService
{
    Task<List<FashionItemDto>>      GetShopItemsAsync(long charId, string? slot);
    Task<(bool Ok, string Msg)>     BuyItemAsync(long charId, int itemId, bool useDiamond, string lang);
    Task<List<FashionItemDto>>      GetWardrobeAsync(long charId);
    Task<OutfitDto?>                GetOutfitAsync(long charId);
    Task<(bool Ok, string Msg)>     WearAsync(long charId, WearRequest req, string lang);
    Task<(bool Ok, string Msg)>     SavePresetAsync(long charId, SavePresetRequest req, string lang);
    Task<(bool Ok, string Msg)>     LoadPresetAsync(long charId, int slotNumber, string lang);
    Task<List<OutfitPreset>>        GetPresetsAsync(long charId);
    Task<(bool Ok, string Msg, long DesignId)> SubmitDesignAsync(long charId, SubmitDesignRequest req, string lang);
    Task<(bool Ok, string Msg)>     ReviewDesignAsync(long reviewerId, long designId, bool approve, string? note, string lang);
    Task<List<FashionDesignDto>>    GetApprovedDesignsAsync(string? slot, int page);
    Task<(bool Ok, string Msg)>     BuyDesignAsync(long charId, long designId, string lang);
}

public class FashionService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<FashionService> logger) : IFashionService
{
    private const double DesignerRoyaltyRate = 0.15; // 15% hoa hồng

    // ─── Shop ────────────────────────────────────────────────
    public async Task<List<FashionItemDto>> GetShopItemsAsync(long charId, string? slot)
    {
        var owned = await db.WardrobeItems
            .Where(w => w.CharacterId == charId)
            .Select(w => w.FashionItemId)
            .ToListAsync();

        var query = db.FashionItems
            .Where(f => f.Source != "designer");

        if (slot is not null) query = query.Where(f => f.Slot == slot);

        return await query
            .Select(f => new FashionItemDto(
                f.Id, f.Name, f.Slot, f.Rarity, f.Source,
                f.PriceGold, f.PriceDiamond,
                f.Dyeable, f.HasEffect,
                owned.Contains(f.Id)))
            .ToListAsync();
    }

    // ─── Buy item ────────────────────────────────────────────
    public async Task<(bool, string)> BuyItemAsync(
        long charId, int itemId, bool useDiamond, string lang)
    {
        var item = await db.FashionItems.FindAsync(itemId);
        if (item is null) return (false, loc.Get("error.not_found", lang));

        var alreadyOwned = await db.WardrobeItems
            .AnyAsync(w => w.CharacterId == charId && w.FashionItemId == itemId);
        if (alreadyOwned) return (false, loc.Get("error.bad_request", lang));

        var char_ = await db.Characters.FindAsync(charId)!;

        if (useDiamond)
        {
            if (item.PriceDiamond <= 0) return (false, loc.Get("error.bad_request", lang));
            if (char_!.Diamond < item.PriceDiamond)
                return (false, loc.Get("character.insufficient_diamond", lang));
            char_.Diamond -= item.PriceDiamond;
        }
        else
        {
            if (item.PriceGold <= 0) return (false, loc.Get("error.bad_request", lang));
            if (char_!.Gold < item.PriceGold)
                return (false, loc.Get("character.insufficient_gold", lang,
                    new { need = item.PriceGold, have = char_.Gold }));
            char_.Gold -= item.PriceGold;
        }

        db.WardrobeItems.Add(new WardrobeItem
        {
            CharacterId   = charId,
            FashionItemId = itemId,
        });

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Wardrobe ────────────────────────────────────────────
    public async Task<List<FashionItemDto>> GetWardrobeAsync(long charId)
    {
        return await db.WardrobeItems
            .Where(w => w.CharacterId == charId)
            .Include(w => w.FashionItem)
            .Select(w => new FashionItemDto(
                w.FashionItemId, w.FashionItem.Name,
                w.FashionItem.Slot, w.FashionItem.Rarity,
                w.FashionItem.Source, w.FashionItem.PriceGold,
                w.FashionItem.PriceDiamond, w.FashionItem.Dyeable,
                w.FashionItem.HasEffect, true))
            .ToListAsync();
    }

    // ─── Get outfit ──────────────────────────────────────────
    public async Task<OutfitDto?> GetOutfitAsync(long charId)
    {
        var outfit = await db.CharacterOutfits.FindAsync(charId);
        if (outfit is null) return null;

        return new OutfitDto(
            outfit.TopId, outfit.BottomId, outfit.ShoesId,
            outfit.HatId, outfit.GlassesId, outfit.CapeId,
            outfit.WingsId, outfit.AuraId,
            outfit.DyeJson, outfit.EffectJson);
    }

    // ─── Wear ────────────────────────────────────────────────
    public async Task<(bool, string)> WearAsync(
        long charId, WearRequest req, string lang)
    {
        // Kiểm tra có item trong wardrobe
        if (req.FashionItemId.HasValue)
        {
            var owned = await db.WardrobeItems
                .AnyAsync(w => w.CharacterId == charId
                    && w.FashionItemId == req.FashionItemId);
            if (!owned) return (false, loc.Get("error.forbidden", lang));
        }

        var outfit = await db.CharacterOutfits.FindAsync(charId);
        if (outfit is null)
        {
            outfit = new CharacterOutfit { CharacterId = charId };
            db.CharacterOutfits.Add(outfit);
        }

        // Apply slot
        switch (req.Slot.ToLower())
        {
            case "top":     outfit.TopId     = req.FashionItemId; break;
            case "bottom":  outfit.BottomId  = req.FashionItemId; break;
            case "shoes":   outfit.ShoesId   = req.FashionItemId; break;
            case "hat":     outfit.HatId     = req.FashionItemId; break;
            case "glasses": outfit.GlassesId = req.FashionItemId; break;
            case "cape":    outfit.CapeId    = req.FashionItemId; break;
            case "wings":   outfit.WingsId   = req.FashionItemId; break;
            case "aura":    outfit.AuraId    = req.FashionItemId; break;
        }

        // Cập nhật dye
        if (req.DyeColorHex is not null && req.FashionItemId.HasValue)
        {
            var dye = outfit.DyeJson is null
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(outfit.DyeJson) ?? [];
            dye[req.Slot] = req.DyeColorHex;
            outfit.DyeJson = JsonSerializer.Serialize(dye);

            // Cập nhật wardrobe item dye
            var wi = await db.WardrobeItems
                .FirstOrDefaultAsync(w => w.CharacterId == charId
                    && w.FashionItemId == req.FashionItemId);
            if (wi is not null) wi.DyeColorHex = req.DyeColorHex;
        }

        // Cập nhật fashion score
        await db.CharacterFameStats
            .Where(f => f.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.FashionScore, f => f.FashionScore + 1));

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Save preset ─────────────────────────────────────────
    public async Task<(bool, string)> SavePresetAsync(
        long charId, SavePresetRequest req, string lang)
    {
        var outfit = await db.CharacterOutfits.FindAsync(charId);
        if (outfit is null) return (false, loc.Get("error.not_found", lang));

        var preset = await db.OutfitPresets
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.SlotNumber == req.SlotNumber);

        var outfitJson = JsonSerializer.Serialize(outfit);

        if (preset is null)
        {
            db.OutfitPresets.Add(new OutfitPreset
            {
                CharacterId = charId,
                PresetName  = req.PresetName,
                SlotNumber  = req.SlotNumber,
                OutfitJson  = outfitJson,
            });
        }
        else
        {
            preset.PresetName = req.PresetName;
            preset.OutfitJson = outfitJson;
            preset.SavedAt    = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Load preset ─────────────────────────────────────────
    public async Task<(bool, string)> LoadPresetAsync(
        long charId, int slotNumber, string lang)
    {
        var preset = await db.OutfitPresets
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.SlotNumber == slotNumber);

        if (preset is null) return (false, loc.Get("error.not_found", lang));

        var saved = JsonSerializer.Deserialize<CharacterOutfit>(preset.OutfitJson);
        if (saved is null) return (false, loc.Get("error.server_error", lang));

        var outfit = await db.CharacterOutfits.FindAsync(charId);
        if (outfit is null)
        {
            outfit = new CharacterOutfit { CharacterId = charId };
            db.CharacterOutfits.Add(outfit);
        }

        outfit.TopId     = saved.TopId;
        outfit.BottomId  = saved.BottomId;
        outfit.ShoesId   = saved.ShoesId;
        outfit.HatId     = saved.HatId;
        outfit.GlassesId = saved.GlassesId;
        outfit.CapeId    = saved.CapeId;
        outfit.WingsId   = saved.WingsId;
        outfit.AuraId    = saved.AuraId;
        outfit.DyeJson   = saved.DyeJson;
        outfit.EffectJson = saved.EffectJson;

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<OutfitPreset>> GetPresetsAsync(long charId) =>
        await db.OutfitPresets
            .Where(p => p.CharacterId == charId)
            .OrderBy(p => p.SlotNumber)
            .ToListAsync();

    // ─── Submit design ───────────────────────────────────────
    public async Task<(bool, string, long)> SubmitDesignAsync(
        long charId, SubmitDesignRequest req, string lang)
    {
        var design = new FashionDesign
        {
            DesignerId   = charId,
            Name         = req.Name.Trim(),
            Slot         = req.Slot,
            DesignData   = req.DesignData,
            PriceGold    = req.PriceGold,
            PriceDiamond = req.PriceDiamond,
            Status       = "pending_review",
            SubmittedAt  = DateTime.UtcNow,
        };
        db.FashionDesigns.Add(design);
        await db.SaveChangesAsync();

        logger.LogInformation("Fashion design submitted: charId={Char} name={Name}",
            charId, req.Name);

        return (true, "OK", design.Id);
    }

    // ─── Review (GM) ─────────────────────────────────────────
    public async Task<(bool, string)> ReviewDesignAsync(
        long reviewerId, long designId, bool approve, string? note, string lang)
    {
        var design = await db.FashionDesigns.FindAsync(designId);
        if (design is null) return (false, loc.Get("error.not_found", lang));

        design.Status       = approve ? "approved" : "rejected";
        design.ReviewerNote = note;
        if (approve) design.ApprovedAt = DateTime.UtcNow;

        if (approve)
        {
            // Tạo fashion item từ design
            db.FashionItems.Add(new FashionItem
            {
                Name       = design.Name,
                Slot       = design.Slot,
                Source     = "designer",
                DesignerId = design.DesignerId,
                PriceGold  = design.PriceGold,
                PriceDiamond = design.PriceDiamond,
                Dyeable    = true,
            });
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get approved designs ────────────────────────────────
    public async Task<List<FashionDesignDto>> GetApprovedDesignsAsync(
        string? slot, int page)
    {
        var query = db.FashionDesigns
            .Where(d => d.Status == "approved")
            .Include(d => d.Designer)
            .AsQueryable();

        if (slot is not null) query = query.Where(d => d.Slot == slot);

        return await query
            .OrderByDescending(d => d.SalesCount)
            .Skip((page - 1) * 20)
            .Take(20)
            .Select(d => new FashionDesignDto(
                d.Id, d.Name, d.Slot,
                d.Designer.Name,
                d.PriceGold, d.PriceDiamond,
                d.SalesCount, d.Status,
                d.ThumbnailUrl))
            .ToListAsync();
    }

    // ─── Buy design ──────────────────────────────────────────
    public async Task<(bool, string)> BuyDesignAsync(
        long charId, long designId, string lang)
    {
        var design = await db.FashionDesigns
            .Include(d => d.Designer)
            .FirstOrDefaultAsync(d => d.Id == designId && d.Status == "approved");

        if (design is null) return (false, loc.Get("error.not_found", lang));

        var alreadyBought = await db.FashionDesignPurchases
            .AnyAsync(p => p.BuyerId == charId && p.DesignId == designId);
        if (alreadyBought) return (false, loc.Get("error.bad_request", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < design.PriceGold)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = design.PriceGold, have = char_.Gold }));

        char_.Gold -= design.PriceGold;

        // Hoa hồng cho designer
        var royalty = (int)(design.PriceGold * DesignerRoyaltyRate);
        await db.Characters.Where(c => c.Id == design.DesignerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + royalty));

        design.SalesCount++;

        // Tìm fashion item tương ứng và add vào wardrobe
        var fashionItem = await db.FashionItems
            .FirstOrDefaultAsync(f => f.DesignerId == design.DesignerId
                && f.Name == design.Name && f.Slot == design.Slot);

        if (fashionItem is not null)
        {
            db.WardrobeItems.Add(new WardrobeItem
            {
                CharacterId   = charId,
                FashionItemId = fashionItem.Id,
            });
        }

        db.FashionDesignPurchases.Add(new FashionDesignPurchase
        {
            DesignId  = designId,
            BuyerId   = charId,
            PricePaid = design.PriceGold,
        });

        await db.SaveChangesAsync();
        return (true, "OK");
    }
}
