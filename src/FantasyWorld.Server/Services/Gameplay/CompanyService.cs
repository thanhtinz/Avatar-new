// FantasyWorld.Server/Services/Gameplay/CompanyService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;

namespace FantasyWorld.Server.Services.Gameplay;

public interface ICompanyService
{
    Task<(bool Ok, string Msg, long CompanyId)> CreateAsync(long ownerId, int typeId, string name, string? desc, int mapId, string lang);
    Task<(bool Ok, string Msg)>  HireAsync(long ownerId, long charId, int salary, string lang);
    Task<(bool Ok, string Msg)>  FireAsync(long ownerId, long employeeCharId, string lang);
    Task<(bool Ok, string Msg)>  SetSalaryAsync(long ownerId, long employeeCharId, int salary, string lang);
    Task<(bool Ok, string Msg)>  DepositAsync(long ownerId, long amount, string lang);
    Task<List<Company>>          GetMyCompaniesAsync(long charId);
    Task<Company?>               GetByIdAsync(long companyId);
    Task                         PaySalariesAsync(); // cron daily
}

public class CompanyService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<CompanyService> logger) : ICompanyService
{
    private const int CreateCost = 50_000;

    public async Task<(bool, string, long)> CreateAsync(
        long ownerId, int typeId, string name, string? desc, int mapId, string lang)
    {
        var type = await db.CompanyTypes.FindAsync(typeId);
        if (type is null) return (false, loc.Get("error.not_found", lang), 0);

        var char_ = await db.Characters.FindAsync(ownerId)!;
        if (char_!.Gold < CreateCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = CreateCost, have = char_.Gold }), 0);

        if (await db.Companies.AnyAsync(c => c.Name == name.Trim()))
            return (false, loc.Get("error.bad_request", lang), 0);

        char_.Gold -= CreateCost;
        var company = new Company
        {
            OwnerId     = ownerId, TypeId = typeId,
            Name        = name.Trim(), Description = desc,
            MapId       = mapId,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // Owner is employee too
        db.CompanyEmployees.Add(new CompanyEmployee
        {
            CompanyId = company.Id, CharacterId = ownerId,
            Role = "owner", Salary = 0,
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Company created: {Name} by charId={Owner}", name, ownerId);
        return (true, "OK", company.Id);
    }

    public async Task<(bool, string)> HireAsync(
        long ownerId, long charId, int salary, string lang)
    {
        var company = await db.Companies
            .Include(c => c.Employees)
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId);
        if (company is null) return (false, loc.Get("error.forbidden", lang));

        var type = await db.CompanyTypes.FindAsync(company.TypeId)!;
        if (company.Employees.Count >= type!.MaxEmployees)
            return (false, loc.Get("error.bad_request", lang));

        if (company.Employees.Any(e => e.CharacterId == charId))
            return (false, loc.Get("error.bad_request", lang));

        db.CompanyEmployees.Add(new CompanyEmployee
        {
            CompanyId = company.Id, CharacterId = charId,
            Role = "employee", Salary = salary,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> FireAsync(long ownerId, long employeeCharId, string lang)
    {
        var emp = await db.CompanyEmployees
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Company.OwnerId == ownerId
                && e.CharacterId == employeeCharId && e.Role != "owner");
        if (emp is null) return (false, loc.Get("error.not_found", lang));

        db.CompanyEmployees.Remove(emp);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> SetSalaryAsync(
        long ownerId, long employeeCharId, int salary, string lang)
    {
        var emp = await db.CompanyEmployees
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Company.OwnerId == ownerId
                && e.CharacterId == employeeCharId);
        if (emp is null) return (false, loc.Get("error.not_found", lang));

        emp.Salary = salary;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> DepositAsync(long ownerId, long amount, string lang)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.OwnerId == ownerId);
        if (company is null) return (false, loc.Get("error.not_found", lang));

        var char_ = await db.Characters.FindAsync(ownerId)!;
        if (char_!.Gold < amount)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = amount, have = char_.Gold }));

        char_.Gold    -= amount;
        company.Balance += amount;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<Company>> GetMyCompaniesAsync(long charId) =>
        await db.Companies
            .Include(c => c.Employees)
            .Where(c => c.OwnerId == charId ||
                c.Employees.Any(e => e.CharacterId == charId))
            .ToListAsync();

    public async Task<Company?> GetByIdAsync(long companyId) =>
        await db.Companies
            .Include(c => c.Employees).ThenInclude(e => e.Character)
            .FirstOrDefaultAsync(c => c.Id == companyId);

    // ─── Pay salaries (cron daily) ───────────────────────────
    public async Task PaySalariesAsync()
    {
        var companies = await db.Companies
            .Include(c => c.Employees)
            .Where(c => c.IsOpen)
            .ToListAsync();

        foreach (var company in companies)
        {
            var totalSalary = company.Employees
                .Where(e => e.Role != "owner")
                .Sum(e => e.Salary);

            if (company.Balance < totalSalary)
            {
                logger.LogWarning("Company {Name} cannot pay salaries!", company.Name);
                continue;
            }

            company.Balance -= totalSalary;

            foreach (var emp in company.Employees.Where(e => e.Role != "owner" && e.Salary > 0))
            {
                await db.Characters.Where(c => c.Id == emp.CharacterId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Gold, c => c.Gold + emp.Salary));

                // Log transaction
                db.CompanyTransactions.Add(new CompanyTransaction
                {
                    CompanyId   = company.Id,
                    TxType      = "salary",
                    Amount      = -emp.Salary,
                    Description = $"Lương nhân viên charId={emp.CharacterId}",
                });
                emp.LastPaid = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Salaries paid for {Count} companies", companies.Count);
    }
}

// ─── RealEstate + House Services ─────────────────────────────

public interface IRealEstateService
{
    Task<List<LandPlot>>             GetAvailableAsync(int? zoneId);
    Task<LandPlot?>                  GetByIdAsync(int plotId);
    Task<(bool Ok, string Msg)>      BuyFromSystemAsync(long charId, int plotId, string lang);
    Task<(bool Ok, string Msg)>      ListForSaleAsync(long charId, int plotId, int price, string lang);
    Task<(bool Ok, string Msg)>      BuyFromPlayerAsync(long buyerId, int plotId, string lang);
    Task<(bool Ok, string Msg)>      CancelListingAsync(long charId, int plotId, string lang);
    Task<List<LandPlot>>             GetMyPlotsAsync(long charId);
    Task<(bool Ok, string Msg)>      BuildHouseAsync(long charId, int plotId, string name, string lang);
    Task<(bool Ok, string Msg)>      UpdateHouseLayoutAsync(long charId, long houseId, string layoutJson, string lang);
    Task<(bool Ok, string Msg)>      RateHouseAsync(long raterCharId, long houseId, int stars, string? comment, string lang);
    Task<List<House>>                GetTopHousesAsync(int count);
}

public class RealEstateService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<RealEstateService> logger) : IRealEstateService
{
    public async Task<List<LandPlot>> GetAvailableAsync(int? zoneId)
    {
        var q = db.LandPlots.Include(p => p.LandZone).AsQueryable();
        if (zoneId.HasValue) q = q.Where(p => p.LandZoneId == zoneId.Value);
        return await q.Where(p => p.CurrentOwner == null || p.IsForSale).ToListAsync();
    }

    public async Task<LandPlot?> GetByIdAsync(int plotId) =>
        await db.LandPlots.Include(p => p.LandZone).FirstOrDefaultAsync(p => p.Id == plotId);

    public async Task<(bool, string)> BuyFromSystemAsync(long charId, int plotId, string lang)
    {
        var plot = await db.LandPlots.FindAsync(plotId);
        if (plot is null || plot.CurrentOwner.HasValue)
            return (false, loc.Get("error.not_found", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < plot.BasePrice)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = plot.BasePrice, have = char_.Gold }));

        char_.Gold        -= plot.BasePrice;
        plot.CurrentOwner  = charId;
        plot.PurchasePrice = plot.BasePrice;
        plot.PurchasedAt   = DateTime.UtcNow;
        plot.IsForSale     = false;

        db.LandTransferHistories.Add(new LandTransferHistory
        {
            PlotId = plotId, ToOwner = charId, Price = plot.BasePrice
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Plot {Id} bought by charId={Char}", plotId, charId);
        return (true, "OK");
    }

    public async Task<(bool, string)> ListForSaleAsync(
        long charId, int plotId, int price, string lang)
    {
        var plot = await db.LandPlots.FindAsync(plotId);
        if (plot?.CurrentOwner != charId) return (false, loc.Get("error.forbidden", lang));
        if (price <= 0) return (false, loc.Get("error.bad_request", lang));

        plot.IsForSale     = true;
        plot.ListingPrice  = price;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> BuyFromPlayerAsync(long buyerId, int plotId, string lang)
    {
        var plot = await db.LandPlots.FindAsync(plotId);
        if (plot is null || !plot.IsForSale || !plot.ListingPrice.HasValue)
            return (false, loc.Get("error.not_found", lang));

        if (plot.CurrentOwner == buyerId) return (false, loc.Get("error.bad_request", lang));

        var buyer = await db.Characters.FindAsync(buyerId)!;
        if (buyer!.Gold < plot.ListingPrice.Value)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = plot.ListingPrice.Value, have = buyer.Gold }));

        buyer.Gold -= plot.ListingPrice.Value;

        // Pay seller (5% fee)
        var fee      = (int)(plot.ListingPrice.Value * 0.05);
        var sellerNet = plot.ListingPrice.Value - fee;
        if (plot.CurrentOwner.HasValue)
            await db.Characters.Where(c => c.Id == plot.CurrentOwner.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Gold, c => c.Gold + sellerNet));

        db.LandTransferHistories.Add(new LandTransferHistory
        {
            PlotId      = plotId,
            FromOwner   = plot.CurrentOwner,
            ToOwner     = buyerId,
            Price       = plot.ListingPrice.Value,
            TransferredAt = DateTime.UtcNow,
        });

        plot.CurrentOwner  = buyerId;
        plot.PurchasePrice = plot.ListingPrice.Value;
        plot.PurchasedAt   = DateTime.UtcNow;
        plot.IsForSale     = false;
        plot.ListingPrice  = null;

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> CancelListingAsync(long charId, int plotId, string lang)
    {
        var plot = await db.LandPlots.FindAsync(plotId);
        if (plot?.CurrentOwner != charId) return (false, loc.Get("error.forbidden", lang));

        plot.IsForSale    = false;
        plot.ListingPrice = null;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<LandPlot>> GetMyPlotsAsync(long charId) =>
        await db.LandPlots
            .Include(p => p.LandZone)
            .Where(p => p.CurrentOwner == charId)
            .ToListAsync();

    public async Task<(bool, string)> BuildHouseAsync(
        long charId, int plotId, string name, string lang)
    {
        var plot = await db.LandPlots.FindAsync(plotId);
        if (plot?.CurrentOwner != charId) return (false, loc.Get("error.forbidden", lang));

        var existing = await db.Houses.AnyAsync(h => h.PlotId == plotId);
        if (existing) return (false, loc.Get("error.bad_request", lang));

        db.Houses.Add(new House
        {
            PlotId   = plotId,
            OwnerId  = charId,
            Name     = name.Trim(),
            Level    = 1,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> UpdateHouseLayoutAsync(
        long charId, long houseId, string layoutJson, string lang)
    {
        var house = await db.Houses
            .FirstOrDefaultAsync(h => h.Id == houseId && h.OwnerId == charId);
        if (house is null) return (false, loc.Get("error.forbidden", lang));

        house.LayoutJson = layoutJson;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> RateHouseAsync(
        long raterCharId, long houseId, int stars, string? comment, string lang)
    {
        var house = await db.Houses.FindAsync(houseId);
        if (house is null) return (false, loc.Get("error.not_found", lang));
        if (house.OwnerId == raterCharId) return (false, loc.Get("error.bad_request", lang));
        if (!house.IsPublic) return (false, loc.Get("error.forbidden", lang));

        stars = Math.Clamp(stars, 1, 5);

        var existing = await db.HouseRatings
            .FirstOrDefaultAsync(r => r.HouseId == houseId && r.RaterId == raterCharId);
        if (existing is not null)
        {
            existing.Stars   = stars;
            existing.Comment = comment;
            existing.RatedAt = DateTime.UtcNow;
        }
        else
        {
            db.HouseRatings.Add(new HouseRating
            {
                HouseId = houseId, RaterId = raterCharId,
                Stars = stars, Comment = comment,
            });
        }

        // Recalc house score
        var avgStars = await db.HouseRatings
            .Where(r => r.HouseId == houseId)
            .AverageAsync(r => (double)r.Stars);
        house.Score = (int)(avgStars * 20); // 5*20=100 max

        // Update fame stats
        await db.CharacterFameStats
            .Where(f => f.CharacterId == house.OwnerId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.HouseScore, house.Score));

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<House>> GetTopHousesAsync(int count) =>
        await db.Houses
            .Where(h => h.IsPublic)
            .OrderByDescending(h => h.Score)
            .Take(count)
            .Include(h => h.Owner)
            .ToListAsync();
}
