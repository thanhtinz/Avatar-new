# Fantasy World Game — C# Server

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 |
| Web Framework | ASP.NET Core 8 |
| Unity Socket | MagicOnion (gRPC StreamingHub) |
| J2ME Socket | TCP Server (binary packet) |
| Database ORM | EF Core 8 + Pomelo (MySQL) |
| Auth | JWT Bearer + BCrypt |
| Logging | Serilog |
| Background Jobs | IHostedService |

---

## Cấu trúc project

```
FantasyWorld/
├── FantasyWorld.sln
└── src/
    ├── FantasyWorld.Shared/          ← Dùng chung Unity + Server
    │   ├── DTOs/GameDtos.cs          ← CharacterDto, MoveDto, ChatDto...
    │   ├── Enums/GameEnums.cs        ← PacketOpcode, Gender, Element...
    │   └── Interfaces/IGameHub.cs    ← Interface Unity gọi server
    │
    ├── FantasyWorld.Server/          ← Main game server
    │   ├── Program.cs                ← Entry point, DI setup
    │   ├── appsettings.json
    │   ├── Controllers/
    │   │   └── AuthController.cs     ← POST /api/auth/login, register...
    │   ├── Hubs/
    │   │   └── GameHub.cs            ← MagicOnion hub cho Unity
    │   ├── Services/
    │   │   ├── AuthService.cs        ← Login, register, JWT
    │   │   ├── CharacterService.cs   ← CRUD, EXP, gold, life stats
    │   │   ├── GameStateService.cs   ← In-memory: players, maps, clans
    │   │   └── LocalizationService.cs ← VI/EN strings
    │   ├── Data/
    │   │   ├── GameDbContext.cs      ← EF Core DbContext
    │   │   └── Entities/Entities.cs  ← Tất cả EF entities
    │   └── BackgroundServices/
    │       ├── WorldTimeService.cs   ← Day/night cycle, seasons
    │       └── DailyResetService.cs  ← Cleanup, festival check
    │
    └── FantasyWorld.J2meGateway/     ← TCP bridge cho J2ME
        ├── Program.cs
        ├── Protocol/PacketParser.cs  ← Binary packet encode/decode
        └── Handlers/TcpServer.cs    ← TCP server, forward đến REST
```

---

## Chạy trên máy local

### Prerequisites
```bash
dotnet --version   # cần >= 8.0
mysql --version    # MySQL 8.0+
```

### 1. Clone và setup
```bash
cd FantasyWorld

# Copy config
cp src/FantasyWorld.Server/appsettings.json \
   src/FantasyWorld.Server/appsettings.Development.json

# Sửa connection string
# "Server=localhost;Database=fantasy_game;User=root;Password=YOUR_PASSWORD"
```

### 2. Tạo database
```bash
# Chạy các file SQL từ fantasy-game-db/
mysql -u root -p < ../fantasy-game-db/schema.sql
mysql -u root -p < ../fantasy-game-db/schema_part2.sql
mysql -u root -p < ../fantasy-game-db/schema_part3.sql
mysql -u root -p < ../fantasy-game-db/seed.sql
mysql -u root -p < ../fantasy-game-db/seed_part2.sql
mysql -u root -p < ../fantasy-game-db/seed_part3.sql
```

### 3. EF Core Migration (lần đầu)
```bash
cd src/FantasyWorld.Server
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Restore packages
```bash
dotnet restore FantasyWorld.sln
```

### 5. Chạy server
```bash
# Terminal 1: Game Server
cd src/FantasyWorld.Server
dotnet run

# Terminal 2: J2ME Gateway
cd src/FantasyWorld.J2meGateway
dotnet run
```

---

## API Endpoints

### Auth
```
POST /api/auth/register   { username, password, email? }
POST /api/auth/login      { username, password }
POST /api/auth/logout     (Authorization: Bearer TOKEN)
POST /api/auth/refresh    { refreshToken }
GET  /api/auth/me         (Authorization: Bearer TOKEN)
```

### Health
```
GET  /health
GET  /
```

---

## Unity Client Setup

Trong Unity project, thêm package MagicOnion.Client và dùng Shared project:

```csharp
// Unity: kết nối đến server
var channel = GrpcChannel.ForAddress("https://your-server:5001");
var client  = MagicOnionClient.Create<IGameHub>(channel);

// Login qua REST trước, lấy token
// Sau đó connect hub với token trong header

// Gọi method
await client.SelectCharacterAsync(charId);
await client.MoveAsync(new MoveDto(x, y, mapId, MoveDir.Right));
await client.SendChatAsync(new ChatSendDto(ChatChannel.Map, "Hello!"));

// Nhận event từ server (implement IGameHubReceiver)
void OnPlayerMove(PlayerMoveDto data)     => MoveCharacter(data.CharId, data.X, data.Y);
void OnChatReceive(ChatReceiveDto data)   => ShowChat(data.SenderName, data.Content);
void OnWorldTime(WorldTimeDto data)       => UpdateTimeUI(data.GameHour, data.IsDay);
void OnLevelUp(int level, string vi, string en) => ShowLevelUpEffect(level);
```

---

## J2ME Client Packet Format

```
[2 bytes: total_length][2 bytes: opcode][N bytes: payload]

Ví dụ Login Request:
[00 0F]          ← length = 15
[00 01]          ← opcode = 0x0001 (LoginRequest)
[05]             ← username length = 5
[61 64 6D 69 6E] ← "admin"
[06]             ← password length = 6
[31 32 33 34 35 36] ← "123456"
[02]             ← lang length = 2
[76 69]          ← "vi"
```

---

## Phase Tiếp Theo

```
Phase 2 — Social
  ├── ClanService.cs
  ├── RelationshipService.cs
  ├── FriendService.cs
  └── PartyService.cs

Phase 3 — Economy
  ├── ShopService.cs
  ├── MarketService.cs (dynamic pricing)
  ├── AuctionService.cs
  └── RestaurantService.cs (voting)

Phase 4 — World Systems
  ├── PetService.cs
  ├── FishingService.cs
  ├── FarmService.cs
  └── WorldEventService.cs

Phase 5 — Content
  ├── QuestService.cs
  ├── AcademyService.cs
  ├── DungeonService.cs
  └── StoryService.cs (branching)
```
