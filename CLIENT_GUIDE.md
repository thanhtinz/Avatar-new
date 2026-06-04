# Fantasy World Game — Client Integration Guide

## Unity Client Setup

### 1. Install packages (Unity Package Manager)

```
MagicOnion.Client     → https://github.com/Cysharp/MagicOnion
Newtonsoft.Json       → com.unity.nuget.newtonsoft-json
TextMeshPro           → built-in Unity package
```

### 2. Scene hierarchy

```
[Scene: Persistent]
  └── NetworkManager       ← Scripts/Network/NetworkManager.cs
  └── GameManager          ← Scripts/Game/GameManager.cs
  └── NotificationUI       ← Scripts/UI/GameUI.cs (NotificationUI)

[Scene: Login]
  └── LoginCanvas
        └── LoginPanel     ← LoginUI.cs

[Scene: CharacterSelect]
  └── CharSelectManager    ← load characters from API

[Scene: Game]
  └── MainCanvas
  │     ├── HudUI          ← HudUI.cs
  │     ├── ChatUI         ← ChatUI.cs
  │     └── NotificationUI
  └── PlayerPrefab         ← PlayerController.cs
  └── RemotePlayers        ← spawned by GameManager
```

### 3. NetworkManager Inspector

```
Server Url:      https://your-server.com
Api Base Url:    https://your-server.com/api
Reconnect Delay: 3
Max Reconnects:  5
Log Packets:     true (dev), false (prod)
```

### 4. Quick start code

```csharp
// Example: LoginScene.cs
async void Start()
{
    // Try auto-login
    if (await NetworkManager.Instance.RestoreSessionAsync())
    {
        SceneManager.LoadScene("CharacterSelect");
        return;
    }
    // Show login UI
}

// Example: CharacterSelect.cs
async void OnSelectCharacter(long charId)
{
    bool ok = await NetworkManager.Instance.ConnectAndSelectCharAsync(charId);
    if (ok) SceneManager.LoadScene("Game");
}

// Example: GameScene.cs
void Start()
{
    GameManager.Instance.OnNotification   += ShowToast;
    GameManager.Instance.OnPlayerSpawned  += SpawnPlayerPrefab;
    GameManager.Instance.OnPlayerDespawned += DespawnPlayer;
}

// Move player
void Update()
{
    if (Input.GetMouseButtonDown(0))
        _ = GameManager.Instance.MoveAsync(targetX, targetZ);
}

// Chat
void SendChatMessage(string text)
    => _ = GameManager.Instance.SendChatAsync(ChatChannel.Map, text);
```

### 5. API calls

```csharp
// Buy item from NPC shop
var result = await NetworkManager.Instance.Api
    .PostAsync<object>(ApiEndpoints.NpcBuy,
        new { ShopItemId = 1, Quantity = 5 });

// Cast fishing
var fish = await NetworkManager.Instance.Api
    .PostAsync<FishingResultDto>(ApiEndpoints.FishCast(mapId), null);

if (fish.Data?.Result == "caught")
    ShowFishResult(fish.Data.FishName, fish.Data.Weight);

// Accept quest
await NetworkManager.Instance.Api
    .PostAsync<object>(ApiEndpoints.QuestAccept,
        new { QuestId = questId });
```

---

## J2ME Client Protocol

### Packet format

```
[2 bytes: total_length]  big-endian uint16
[2 bytes: opcode]        big-endian uint16  (see PacketOpcode enum)
[N bytes: payload]       variable

String encoding: [2B length][UTF-8 bytes]
Numbers:         big-endian
```

### Example J2ME connection (pseudocode)

```java
// MIDlet
SocketConnection conn = (SocketConnection)
    Connector.open("socket://your-server.com:7777");

DataOutputStream out = conn.openDataOutputStream();
DataInputStream  in  = conn.openDataInputStream();

// 1. Login
byte[] loginPkt = buildLoginPacket("username", "password", "vi");
out.write(loginPkt);
out.flush();

// 2. Read response
short totalLen = in.readShort();
short opcode   = in.readShort();
// parse payload based on opcode...

// 3. Move packet (in game loop)
byte[] movePkt = buildMovePacket(x, y, mapId, dir);
out.write(movePkt);
out.flush();
```

### Build Login Packet (Java)

```java
byte[] buildLoginPacket(String user, String pass, String lang) {
    ByteArrayOutputStream payload = new ByteArrayOutputStream();
    DataOutputStream dout = new DataOutputStream(payload);

    byte[] uBytes = user.getBytes("UTF-8");
    dout.writeShort(uBytes.length);  dout.write(uBytes);

    byte[] pBytes = pass.getBytes("UTF-8");
    dout.writeShort(pBytes.length);  dout.write(pBytes);

    byte[] lBytes = lang.getBytes("UTF-8");
    dout.writeShort(lBytes.length);  dout.write(lBytes);

    byte[] pl = payload.toByteArray();
    ByteArrayOutputStream pkt = new ByteArrayOutputStream();
    DataOutputStream d2 = new DataOutputStream(pkt);
    d2.writeShort(4 + pl.length); // total length
    d2.writeShort(0x0001);        // opcode LOGIN_REQUEST
    d2.write(pl);
    return pkt.toByteArray();
}
```

---

## Deployment

### Quick deploy (Docker)

```bash
git clone https://github.com/thanhtinz/Avatar-new
cd Avatar-new

# First time setup
bash deploy/scripts/setup.sh

# Edit secrets
nano .env

# Start everything
docker compose -f deploy/docker-compose.yml up -d

# View logs
docker compose -f deploy/docker-compose.yml logs -f game_server

# Backup DB
bash deploy/scripts/backup.sh
```

### Environment variables (.env)

```env
MYSQL_ROOT_PASSWORD=<strong_password>
MYSQL_PASSWORD=<strong_password>
REDIS_PASSWORD=<strong_password>
JWT_SECRET=<64_char_random_string>
JWT_REFRESH_SECRET=<64_char_random_string>
```

### Endpoints after deploy

```
REST API:     https://your-domain.com/api
gRPC (Unity): https://your-domain.com/grpc
J2ME TCP:     your-domain.com:7777
Health:       https://your-domain.com/health
```

---

## GM Dashboard

### Endpoints (require GM/Admin JWT)

```
GET  /api/admin/stats                    Server statistics
GET  /api/admin/players?search=name      Player list
POST /api/admin/players/{id}/ban         Ban player
POST /api/admin/players/{id}/unban       Unban player
POST /api/admin/players/{charId}/give-gold  Give gold
POST /api/admin/players/{charId}/give-item  Give item
POST /api/admin/events/{eventId}/trigger    Trigger world event
GET  /api/admin/economy                  Economy overview
GET  /api/admin/fashion/pending          Fashion review queue
POST /api/admin/restaurants/applications/{id}/approve
POST /api/admin/broadcast                Server announcement
DELETE /api/admin/sessions/{accountId}   Force logout
```
