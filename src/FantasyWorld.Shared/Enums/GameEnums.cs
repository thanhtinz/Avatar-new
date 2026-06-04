// FantasyWorld.Shared/Enums/GameEnums.cs
namespace FantasyWorld.Shared.Enums;

public enum Gender       { Male, Female, Other }
public enum ClientType   { Unity, J2me, Web, Admin }
public enum AccountRole  { Player, Gm, Admin }
public enum ChatChannel  { Map, World, Clan, Party, Academy, Faction, Private }
public enum ItemType     { Weapon, Armor, Accessory, Potion, Food, Material, PetFood, Card, Furniture, Key }
public enum ItemRarity   { Common, Uncommon, Rare, Epic, Legendary }
public enum PetRarity    { Common, Uncommon, Rare, Epic, Legendary }
public enum Element      { None, Fire, Ice, Light, Dark, Wind, Earth, Water }
public enum MapType      { Town, Field, Dungeon, Cave, Pyramid, AncientCity, Special }
public enum RelType      { Friend, BestFriend, Soulmate, Mentor, Student, Rival, Married }
public enum MoveDir      { Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight, Idle }
public enum FactionCode  { Empire, Elf, Beastkin, Demon }
public enum SeasonCode   { Spring, Summer, Autumn, Winter }

// Packet opcodes — dùng chung Client/Server/J2ME
public enum PacketOpcode : ushort
{
    // Auth
    LoginRequest        = 0x0001,
    LoginResponse       = 0x0002,
    LogoutRequest       = 0x0003,

    // Character
    SelectCharacter     = 0x0010,
    GameReady           = 0x0011,
    CharacterInfo       = 0x0012,

    // Movement
    Move                = 0x0020,
    PlayerMove          = 0x0021,
    PlayerEnter         = 0x0022,
    PlayerLeave         = 0x0023,
    MapChanged          = 0x0024,
    UsePortal           = 0x0025,
    TeleportResult      = 0x0026,

    // Chat
    ChatSend            = 0x0030,
    ChatReceive         = 0x0031,
    Emote               = 0x0032,
    SystemMessage       = 0x0033,

    // World
    WorldTime           = 0x0040,
    NewDay              = 0x0041,
    SeasonChange        = 0x0042,
    FullMoon            = 0x0043,
    WorldEvent          = 0x0044,

    // Battle
    BattleStart         = 0x0050,
    BattleAction        = 0x0051,
    BattleResult        = 0x0052,

    // Pet
    PetCatch            = 0x0060,
    PetCatchResult      = 0x0061,
    PetLevelUp          = 0x0062,

    // Error
    Error               = 0xFFFF,
}
