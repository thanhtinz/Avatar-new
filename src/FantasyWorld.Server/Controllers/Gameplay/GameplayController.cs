// FantasyWorld.Server/Controllers/Gameplay/GameplayController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FantasyWorld.Server.Controllers.Economy;
using FantasyWorld.Server.Services.Gameplay;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Controllers.Gameplay;

// ─── Faction ─────────────────────────────────────────────────
[ApiController, Route("api/factions"), Authorize]
public class FactionController(IFactionService svc) : GameControllerBase
{
    [HttpGet]                  public async Task<IActionResult> GetAll()               => Ok(new { success = true, data = await svc.GetAllAsync() });
    [HttpGet("my")]            public async Task<IActionResult> GetMy()                => Ok(new { success = true, data = await svc.GetMyFactionAsync(CharId) });
    [HttpPost("{id}/join")]    public async Task<IActionResult> Join(int id)            { var (ok, msg) = await svc.JoinAsync(CharId, id, Lang); return ok ? Ok(new{success=true,message=msg}) : BadRequest(new{success=false,message=msg}); }
    [HttpPost("leave")]        public async Task<IActionResult> Leave()                 { var (ok, msg) = await svc.LeaveAsync(CharId, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpGet("{id}/quests")]   public async Task<IActionResult> GetQuests(int id)       => Ok(new { success = true, data = await svc.GetQuestsAsync(id, CharId) });
    [HttpPost("quests/{id}/accept")] public async Task<IActionResult> AcceptQuest(int id) { var (ok, msg) = await svc.AcceptFactionQuestAsync(CharId, id, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpPost("quests/{id}/complete")] public async Task<IActionResult> CompleteQuest(int id) { var (ok, msg) = await svc.CompleteFactionQuestAsync(CharId, id, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpGet("{id}/shop")]     public async Task<IActionResult> GetShop(int id)         => Ok(new { success = true, data = await svc.GetShopAsync(id, CharId) });
    [HttpPost("shop/{id}/buy")]public async Task<IActionResult> Buy(int id)             { var (ok, msg) = await svc.BuyFromFactionShopAsync(CharId, id, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpGet("war")]           public async Task<IActionResult> GetWar()               => Ok(new { success = true, data = await svc.GetActiveWarAsync() });
    [HttpPost("war/{id}/contribute")] public async Task<IActionResult> Contribute(int id, [FromBody] ContributeReq req) { var (ok, msg) = await svc.ContributeToWarAsync(CharId, id, req.Amount, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
}

// ─── Company ─────────────────────────────────────────────────
[ApiController, Route("api/companies"), Authorize]
public class CompanyController(ICompanyService svc) : GameControllerBase
{
    [HttpGet("my")]           public async Task<IActionResult> GetMy()                       => Ok(new { success = true, data = await svc.GetMyCompaniesAsync(CharId) });
    [HttpGet("{id}")]         public async Task<IActionResult> GetById(long id)               => Ok(new { success = true, data = await svc.GetByIdAsync(id) });
    [HttpPost]                public async Task<IActionResult> Create([FromBody] CreateCompanyReq req) { var (ok, msg, cid) = await svc.CreateAsync(CharId, req.TypeId, req.Name, req.Description, req.MapId, Lang); return ok ? StatusCode(201, new{success=true,companyId=cid}) : BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/hire")]   public async Task<IActionResult> Hire(long id, [FromBody] HireReq req) { var (ok, msg) = await svc.HireAsync(CharId, req.CharId, req.Salary, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/fire/{charId}")] public async Task<IActionResult> Fire(long id, long charId) { var (ok, msg) = await svc.FireAsync(CharId, charId, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpPatch("{id}/salary/{charId}")] public async Task<IActionResult> SetSalary(long id, long charId, [FromBody] SalaryReq req) { var (ok, msg) = await svc.SetSalaryAsync(CharId, charId, req.Salary, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/deposit")] public async Task<IActionResult> Deposit(long id, [FromBody] AmountReq req) { var (ok, msg) = await svc.DepositAsync(CharId, req.Amount, Lang); return ok ? Ok(new{success=true}) : BadRequest(new{success=false,message=msg}); }
}

// ─── Real Estate ─────────────────────────────────────────────
[ApiController, Route("api/realestate"), Authorize]
public class RealEstateController(IRealEstateService svc) : GameControllerBase
{
    [HttpGet("available")]               public async Task<IActionResult> GetAvailable([FromQuery] int? zone)    => Ok(new{success=true,data=await svc.GetAvailableAsync(zone)});
    [HttpGet("my")]                      public async Task<IActionResult> GetMy()                               => Ok(new{success=true,data=await svc.GetMyPlotsAsync(CharId)});
    [HttpPost("{plotId}/buy")]           public async Task<IActionResult> BuySystem(int plotId)                 { var (ok,msg)=await svc.BuyFromSystemAsync(CharId,plotId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{plotId}/list")]          public async Task<IActionResult> List(int plotId,[FromBody]ListReq req) { var (ok,msg)=await svc.ListForSaleAsync(CharId,plotId,req.Price,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{plotId}/buy-player")]    public async Task<IActionResult> BuyPlayer(int plotId)                 { var (ok,msg)=await svc.BuyFromPlayerAsync(CharId,plotId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{plotId}/cancel")]        public async Task<IActionResult> Cancel(int plotId)                    { var (ok,msg)=await svc.CancelListingAsync(CharId,plotId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{plotId}/build")]         public async Task<IActionResult> Build(int plotId,[FromBody]BuildReq req) { var (ok,msg)=await svc.BuildHouseAsync(CharId,plotId,req.Name,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPatch("houses/{houseId}/layout")] public async Task<IActionResult> Layout(long houseId,[FromBody]LayoutReq req) { var (ok,msg)=await svc.UpdateHouseLayoutAsync(CharId,houseId,req.LayoutJson,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("houses/{houseId}/rate")]  public async Task<IActionResult> Rate(long houseId,[FromBody]RateHouseReq req) { var (ok,msg)=await svc.RateHouseAsync(CharId,houseId,req.Stars,req.Comment,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("houses/top")]              public async Task<IActionResult> TopHouses([FromQuery]int count=10)    => Ok(new{success=true,data=await svc.GetTopHousesAsync(count)});
}

// ─── Museum ──────────────────────────────────────────────────
[ApiController, Route("api/museums"), Authorize]
public class MuseumController(IMuseumService svc) : GameControllerBase
{
    [HttpGet("map/{mapId}")]          public async Task<IActionResult> Get(int mapId)                        => Ok(new{success=true,data=await svc.GetServerMuseumAsync(mapId)});
    [HttpGet("{id}/exhibits")]        public async Task<IActionResult> GetExhibits(int id,[FromQuery]string? cat) => Ok(new{success=true,data=await svc.GetExhibitsAsync(id,cat)});
    [HttpPost("{id}/donate")]         public async Task<IActionResult> Donate(int id,[FromBody]DonateItemReq req) { var (ok,msg)=await svc.DonateAsync(CharId,id,req.ItemId,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("my-donations")]         public async Task<IActionResult> MyDonations()                         => Ok(new{success=true,data=await svc.GetMyDonationsAsync(CharId)});
}

// ─── Travel ──────────────────────────────────────────────────
[ApiController, Route("api/travel"), Authorize]
public class TravelController(ITravelService svc) : GameControllerBase
{
    [HttpGet("landmarks")]            public async Task<IActionResult> GetLandmarks([FromQuery]int? mapId)    => Ok(new{success=true,data=await svc.GetLandmarksAsync(mapId)});
    [HttpPost("landmarks/{id}/visit")] public async Task<IActionResult> Visit(int id)                        { var (ok,msg)=await svc.VisitLandmarkAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("passport")]             public async Task<IActionResult> GetPassport()                          => Ok(new{success=true,data=await svc.GetPassportAsync(CharId)});
    [HttpGet("stamps")]               public async Task<IActionResult> GetStamps()                            => Ok(new{success=true,data=await svc.GetMyStampsAsync(CharId)});
    [HttpGet("specialties/{regionId}")] public async Task<IActionResult> GetSpecialties(int regionId)        => Ok(new{success=true,data=await svc.GetSpecialtiesAsync(regionId)});
}

// ─── News ────────────────────────────────────────────────────
[ApiController, Route("api/news"), Authorize]
public class NewsController(INewsService svc) : GameControllerBase
{
    [HttpGet]                         public async Task<IActionResult> GetBulletin([FromQuery]int page=1) => Ok(new{success=true,data=await svc.GetBulletinAsync(page)});
    [HttpGet("my")]                   public async Task<IActionResult> GetMy()                           => Ok(new{success=true,data=await svc.GetMyArticlesAsync(CharId)});
    [HttpPost]                        public async Task<IActionResult> Publish([FromBody]PublishNewsReq req) { var (ok,msg,id)=await svc.PublishAsync(CharId,req.Title,req.Content,Lang); return ok?StatusCode(201,new{success=true,articleId=id}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/like")]           public async Task<IActionResult> Like(long id)                     { var (ok,_)=await svc.LikeAsync(CharId,id,Lang); return ok?Ok(new{success=true}):NotFound(); }
    [HttpPost("{id}/pin"), Authorize] public async Task<IActionResult> Pin(long id,[FromBody]PinReq req) { if(!IsGm)return Forbid(); var (ok,_)=await svc.PinAsync(CharId,id,req.Pin,Lang); return ok?Ok(new{success=true}):NotFound(); }
    [HttpDelete("{id}")]              public async Task<IActionResult> Delete(long id)                   { var (ok,msg)=await svc.DeleteAsync(CharId,id,Lang); return ok?Ok(new{success=true}):NotFound(new{success=false,message=msg}); }
}

// ─── Treasure Hunt ───────────────────────────────────────────
[ApiController, Route("api/treasure"), Authorize]
public class TreasureHuntController(IServerTreasureHuntService svc) : GameControllerBase
{
    [HttpGet]                          public async Task<IActionResult> GetActive()                           => Ok(new{success=true,data=await svc.GetActiveAsync()});
    [HttpGet("{id}")]                  public async Task<IActionResult> GetById(int id)                       => Ok(new{success=true,data=await svc.GetByIdAsync(id)});
    [HttpPost, Authorize]              public async Task<IActionResult> Create([FromBody]CreateHuntReq req)   { if(!IsGm)return Forbid(); var (ok,msg,id)=await svc.CreateHuntAsync(CharId,req.Title,req.Clue1,req.Clue2,req.Clue3,req.MapId,req.X,req.Y,req.GoldReward,Lang); return ok?StatusCode(201,new{success=true,huntId=id}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/submit")]          public async Task<IActionResult> Submit(int id,[FromBody]SubmitLocationRequest req) { var (ok,msg)=await svc.SubmitLocationAsync(CharId,id,req.X,req.Y,Lang); return Ok(new{success=ok,message=msg}); }
    [HttpPost("{id}/claim")]           public async Task<IActionResult> Claim(int id)                         { var (ok,msg)=await svc.ClaimTreasureAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
}

// ─── Territory ───────────────────────────────────────────────
[ApiController, Route("api/territory"), Authorize]
public class TerritoryController(IGuildTerritoryService svc) : GameControllerBase
{
    [HttpGet]                          public async Task<IActionResult> GetAll()                                  => Ok(new{success=true,data=await svc.GetAllAsync()});
    [HttpGet("clan/{clanId}")]         public async Task<IActionResult> GetByClan(int clanId)                     => Ok(new{success=true,data=await svc.GetByClanAsync(clanId)});
    [HttpPost("found")]                public async Task<IActionResult> Found([FromBody]FoundTerritoryReq req)     { var (ok,msg)=await svc.FoundTerritoryAsync(CharId,req.ClanId,req.Name,req.MapId,Lang); return ok?StatusCode(201,new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{clanId}/upgrade")]     public async Task<IActionResult> Upgrade(int clanId)                       { var (ok,msg)=await svc.UpgradeAsync(CharId,clanId,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{clanId}/build")]       public async Task<IActionResult> Build(int clanId,[FromBody]BuildingReq req) { var (ok,msg)=await svc.BuildAsync(CharId,clanId,req.BuildingType,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{clanId}/collect-tax")] public async Task<IActionResult> CollectTax(int clanId)                   { var (ok,msg)=await svc.CollectTaxAsync(CharId,clanId,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
}

// ─── Profession + Deity ──────────────────────────────────────
[ApiController, Route("api/professions")]
public class ProfessionController(IRareProfessionService rareSvc, ICivilProfessionService civilSvc) : GameControllerBase
{
    [HttpGet("rare")]                      public async Task<IActionResult> GetRare()                                    => Ok(new{success=true,data=await rareSvc.GetAllAsync()});
    [HttpPost("rare/{id}/apply"), Authorize] public async Task<IActionResult> Apply(int id)                              { var (ok,msg)=await rareSvc.ApplyAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("rare/{id}/abandon"), Authorize] public async Task<IActionResult> Abandon(int id)                          { var (ok,msg)=await rareSvc.AbandonAsync(CharId,id,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("civil")]                     public async Task<IActionResult> GetCivil()                                   => Ok(new{success=true,data=await civilSvc.GetAllAsync()});
    [HttpPost("civil/{id}/unlock"), Authorize] public async Task<IActionResult> Unlock(int id)                           { var (ok,msg)=await civilSvc.UnlockAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("civil/{id}/practice"), Authorize] public async Task<IActionResult> Practice(int id)                       { var (ok,msg)=await civilSvc.PracticeAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("civil/{id}/create-work"), Authorize] public async Task<IActionResult> CreateWork(int id,[FromBody]CreateWorkReq req) { var (ok,msg)=await civilSvc.CreateWorkAsync(CharId,id,req.Title,req.Content,req.Price,Lang); return ok?StatusCode(201,new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("works/{type}")]              public async Task<IActionResult> GetWorks(string type,[FromQuery]int page=1)   => Ok(new{success=true,data=await civilSvc.GetWorksAsync(type,page)});
    [HttpPost("works/{workId}/buy"), Authorize] public async Task<IActionResult> BuyWork(long workId)                   { var (ok,msg)=await civilSvc.BuyWorkAsync(CharId,workId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
}

[ApiController, Route("api/deity"), Authorize]
public class DeityController(IDeityService svc) : GameControllerBase
{
    [HttpGet]                    public async Task<IActionResult> GetAll()               => Ok(new{success=true,data=await svc.GetAllAsync()});
    [HttpGet("my")]              public async Task<IActionResult> GetMy()                => Ok(new{success=true,data=await svc.GetMyDeityAsync(CharId)});
    [HttpPost("{id}/choose")]    public async Task<IActionResult> Choose(int id)         { var (ok,msg)=await svc.ChooseDeityAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("pray")]           public async Task<IActionResult> Pray()                 { var (ok,msg)=await svc.PrayAsync(CharId,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("offer")]          public async Task<IActionResult> Offer([FromBody]OfferReq req) { var (ok,msg)=await svc.OfferAsync(CharId,req.ItemId,req.Qty,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
}

// ─── Leaderboard ─────────────────────────────────────────────
[ApiController, Route("api/leaderboard")]
public class LeaderboardController(ILeaderboardService svc) : GameControllerBase
{
    [HttpGet("{category}")]   public async Task<IActionResult> Get(string category,[FromQuery]int top=50) => Ok(new{success=true,data=await svc.GetAsync(category,top)});
    [HttpGet("categories")]   public IActionResult GetCategories() => Ok(new{success=true,data=LeaderboardService.Categories});
}

// ─── Endgame Realm ───────────────────────────────────────────
[ApiController, Route("api/realms"), Authorize]
public class EndgameRealmController(IEndgameRealmService svc) : GameControllerBase
{
    [HttpGet]                       public async Task<IActionResult> GetAll()                   => Ok(new{success=true,data=await svc.GetAllAsync()});
    [HttpGet("unlocked")]           public async Task<IActionResult> GetUnlocked()              => Ok(new{success=true,data=await svc.GetUnlockedAsync(CharId)});
    [HttpPost("{id}/unlock")]       public async Task<IActionResult> Unlock(int id)             { var (ok,msg)=await svc.UnlockAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/enter")]        public async Task<IActionResult> Enter(int id)              { var (ok,msg)=await svc.EnterAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
}

// ─── Season ──────────────────────────────────────────────────
[ApiController, Route("api/seasons")]
public class SeasonController(ISeasonService svc) : GameControllerBase
{
    [HttpGet("current")]  public async Task<IActionResult> GetCurrent() => Ok(new{success=true,data=await svc.GetCurrentAsync()});
    [HttpGet]             public async Task<IActionResult> GetAll()     => Ok(new{success=true,data=await svc.GetAllAsync()});
}

// ─── Festival ────────────────────────────────────────────────
[ApiController, Route("api/festivals"), Authorize]
public class FestivalController(IFestivalService svc) : GameControllerBase
{
    [HttpGet]                          public async Task<IActionResult> GetAll()                             => Ok(new{success=true,data=await svc.GetAllAsync()});
    [HttpGet("active")]                public async Task<IActionResult> GetActive()                          => Ok(new{success=true,data=await svc.GetActiveAsync()});
    [HttpGet("{id}/tasks")]            public async Task<IActionResult> GetTasks(int id)                     => Ok(new{success=true,data=await svc.GetTasksAsync(id,CharId)});
    [HttpPost("{id}/join")]            public async Task<IActionResult> Join(int id)                         { var (ok,msg)=await svc.ParticipateAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/tasks/{taskId}/complete")] public async Task<IActionResult> CompleteTask(int id,int taskId) { var (ok,msg)=await svc.CompleteTaskAsync(CharId,id,taskId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/claim")]           public async Task<IActionResult> Claim(int id)                        { var (ok,msg)=await svc.ClaimRewardAsync(CharId,id,Lang); return ok?Ok(new{success=true,message=msg}):BadRequest(new{success=false,message=msg}); }
}

// ─── UGC ─────────────────────────────────────────────────────
[ApiController, Route("api/ugc"), Authorize]
public class UgcController(IUgcService svc) : GameControllerBase
{
    [HttpGet("{type}")]              public async Task<IActionResult> GetTop(string type,[FromQuery]int page=1) => Ok(new{success=true,data=await svc.GetTopAsync(type,page)});
    [HttpGet("my")]                  public async Task<IActionResult> GetMine()                               => Ok(new{success=true,data=await svc.GetMineAsync(CharId)});
    [HttpPost]                       public async Task<IActionResult> Create([FromBody]CreateUgcReq req)       { var (ok,msg,id)=await svc.CreateAsync(CharId,req.ContentType,req.Title,req.DataJson,Lang); return ok?StatusCode(201,new{success=true,contentId=id}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/publish")]       public async Task<IActionResult> Publish(long id)                        { var (ok,msg)=await svc.PublishAsync(CharId,id,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{id}/visit")]         public async Task<IActionResult> Visit(long id)                          { var (ok,_)=await svc.VisitAsync(CharId,id,Lang); return Ok(new{success=true}); }
    [HttpPost("{id}/rate")]          public async Task<IActionResult> Rate(long id,[FromBody]RateUgcReq req)   { var (ok,msg)=await svc.RateAsync(CharId,id,req.Stars,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpDelete("{id}")]             public async Task<IActionResult> Delete(long id)                         { var (ok,msg)=await svc.DeleteAsync(CharId,id,Lang); return ok?Ok(new{success=true}):NotFound(new{success=false,message=msg}); }
}

// ─── Citizen Card ────────────────────────────────────────────
[ApiController, Route("api/citizen-card"), Authorize]
public class CitizenCardController(ICitizenCardService svc) : GameControllerBase
{
    [HttpGet("my")]              public async Task<IActionResult> GetMy()                      => Ok(new{success=true,data=await svc.GetAsync(CharId)});
    [HttpGet("{charId}")]        public async Task<IActionResult> GetById(long charId)         { await svc.RecordViewAsync(CharId,charId); return Ok(new{success=true,data=await svc.GetAsync(charId)}); }
    [HttpGet("by-name/{name}")]  public async Task<IActionResult> ByName(string name)          => Ok(new{success=true,data=await svc.GetByNameAsync(name)});
    [HttpPatch]                  public async Task<IActionResult> Update([FromBody]UpdateCardReq req) { var (ok,msg)=await svc.UpdateAsync(CharId,req.Bio,req.Hobbies,req.FavPetName,req.FavRegion,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("my/viewers")]      public async Task<IActionResult> GetViewers([FromQuery]int count=20) => Ok(new{success=true,data=await svc.GetRecentViewersAsync(CharId,count)});
}

// ─── Hotel ───────────────────────────────────────────────────
[ApiController, Route("api/hotels"), Authorize]
public class HotelController(IHotelService svc) : GameControllerBase
{
    [HttpGet("map/{mapId}")]    public async Task<IActionResult> GetOnMap(int mapId)              => Ok(new{success=true,data=await svc.GetOnMapAsync(mapId)});
    [HttpGet("{id}/rooms")]     public async Task<IActionResult> GetRooms(long id)               => Ok(new{success=true,data=await svc.GetRoomsAsync(id)});
    [HttpPost("rent")]          public async Task<IActionResult> Rent([FromBody]RentRoomRequest req) { var (ok,msg,id)=await svc.RentRoomAsync(CharId,req.RoomId,req.Days,Lang); return ok?Ok(new{success=true,rentalId=id}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{roomId}/checkout")] public async Task<IActionResult> Checkout(long roomId)       { var (ok,msg)=await svc.CheckoutAsync(CharId,roomId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPatch("{roomId}/decorate")] public async Task<IActionResult> Decorate(long roomId,[FromBody]DecorateReq req) { var (ok,msg)=await svc.DecorateRoomAsync(CharId,roomId,req.DecorJson,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("{roomId}/invite/{guestId}")] public async Task<IActionResult> Invite(long roomId,long guestId) { var (ok,msg)=await svc.InviteToRoomAsync(CharId,guestId,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
}

// ─── Market Stall ────────────────────────────────────────────
[ApiController, Route("api/stalls"), Authorize]
public class MarketStallController(IMarketStallService svc) : GameControllerBase
{
    [HttpGet("map/{mapId}")]      public async Task<IActionResult> GetOnMap(int mapId)                => Ok(new{success=true,data=await svc.GetOnMapAsync(mapId)});
    [HttpPost("rent")]            public async Task<IActionResult> Rent([FromBody]RentStallReq req)    { var (ok,msg,id)=await svc.RentStallAsync(CharId,req,Lang); return ok?Ok(new{success=true,rentalId=id}):BadRequest(new{success=false,message=msg}); }
    [HttpGet("{rentalId}/listings")] public async Task<IActionResult> GetListings(long rentalId)      => Ok(new{success=true,data=await svc.GetListingsAsync(rentalId)});
    [HttpPost("{rentalId}/listings")] public async Task<IActionResult> AddListing(long rentalId,[FromBody]CreateStallListingReq req) { var (ok,msg)=await svc.CreateListingAsync(CharId,rentalId,req,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpPost("buy/{listingId}")]  public async Task<IActionResult> Buy(long listingId,[FromBody]BuyQtyReq req) { var (ok,msg)=await svc.BuyFromStallAsync(CharId,listingId,req.Qty,Lang); return ok?Ok(new{success=true}):BadRequest(new{success=false,message=msg}); }
    [HttpDelete("listings/{listingId}")] public async Task<IActionResult> Remove(long listingId)     { var (ok,msg)=await svc.RemoveListingAsync(CharId,listingId,Lang); return ok?Ok(new{success=true}):NotFound(new{success=false,message=msg}); }
}

// ─── Request records ─────────────────────────────────────────
public record ContributeReq(int Amount);
public record CreateCompanyReq(int TypeId, string Name, string? Description, int MapId);
public record HireReq(long CharId, int Salary);
public record SalaryReq(int Salary);
public record AmountReq(long Amount);
public record ListReq(int Price);
public record BuildReq(string Name);
public record LayoutReq(string LayoutJson);
public record RateHouseReq(int Stars, string? Comment = null);
public record DonateItemReq(int ItemId);
public record PublishNewsReq(string Title, string Content);
public record PinReq(bool Pin);
public record CreateHuntReq(string Title, string Clue1, string Clue2, string Clue3, int MapId, float X, float Y, int GoldReward);
public record FoundTerritoryReq(int ClanId, string Name, int MapId);
public record BuildingReq(string BuildingType);
public record CreateWorkReq(string Title, string Content, int Price);
public record OfferReq(int ItemId, int Qty);
public record CreateUgcReq(string ContentType, string Title, string DataJson);
public record RateUgcReq(int Stars);
public record UpdateCardReq(string? Bio, string? Hobbies, string? FavPetName, string? FavRegion);
public record DecorateReq(string DecorJson);
public record BuyQtyReq(int Qty);
