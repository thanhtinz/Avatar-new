// FantasyWorld.J2meGateway/Protocol/FullPacketParser.cs
// Full binary protocol — ALL opcodes for J2ME client
using System.Buffers.Binary;
using System.Text;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.J2meGateway.Protocol;

/// <summary>
/// Complete J2ME binary protocol
/// Format: [2B length][2B opcode][N bytes payload]
/// Strings: [2B length][UTF-8 bytes]
/// All integers: big-endian
/// </summary>
public static class FullPacketParser
{
    public const int HEADER = 4;

    // ─── Frame ───────────────────────────────────────────────

    public static bool TryRead(ReadOnlySpan<byte> buf,
        out PacketOpcode op, out ReadOnlySpan<byte> payload, out int consumed)
    {
        op = PacketOpcode.Error; payload = default; consumed = 0;
        if (buf.Length < HEADER) return false;

        var len = BinaryPrimitives.ReadUInt16BigEndian(buf);
        if (buf.Length < len) return false;

        op       = (PacketOpcode)BinaryPrimitives.ReadUInt16BigEndian(buf[2..]);
        payload  = buf[HEADER..len];
        consumed = len;
        return true;
    }

    public static byte[] Build(PacketOpcode op, ReadOnlySpan<byte> payload = default)
    {
        var total = (ushort)(HEADER + payload.Length);
        var buf   = new byte[total];
        BinaryPrimitives.WriteUInt16BigEndian(buf,       total);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(2), (ushort)op);
        payload.CopyTo(buf.AsSpan(HEADER));
        return buf;
    }

    // ─── Writers ─────────────────────────────────────────────

    public static byte[] WriteLoginResponse(bool ok, string msg, string? token, long accountId)
    {
        var w = new PacketWriter();
        w.WriteBool(ok);
        w.WriteString(msg);
        w.WriteString(token ?? "");
        w.WriteInt64(accountId);
        return Build(PacketOpcode.LoginResponse, w.ToSpan());
    }

    public static byte[] WriteGameReady(long charId, int mapId, float x, float y,
        int hp, int hpMax, long gold, int level)
    {
        var w = new PacketWriter();
        w.WriteInt64(charId);
        w.WriteInt32(mapId);
        w.WriteFloat(x); w.WriteFloat(y);
        w.WriteInt32(hp); w.WriteInt32(hpMax);
        w.WriteInt64(gold);
        w.WriteInt32(level);
        return Build(PacketOpcode.GameReady, w.ToSpan());
    }

    public static byte[] WritePlayerMove(long charId, float x, float y, byte dir)
    {
        var w = new PacketWriter();
        w.WriteInt64(charId);
        w.WriteFloat(x); w.WriteFloat(y);
        w.WriteByte(dir);
        return Build(PacketOpcode.PlayerMove, w.ToSpan());
    }

    public static byte[] WritePlayerEnter(long charId, string name, int level,
        float x, float y, byte gender)
    {
        var w = new PacketWriter();
        w.WriteInt64(charId);
        w.WriteString(name);
        w.WriteInt32(level);
        w.WriteFloat(x); w.WriteFloat(y);
        w.WriteByte(gender);
        return Build(PacketOpcode.PlayerEnter, w.ToSpan());
    }

    public static byte[] WritePlayerLeave(long charId)
    {
        var w = new PacketWriter();
        w.WriteInt64(charId);
        return Build(PacketOpcode.PlayerLeave, w.ToSpan());
    }

    public static byte[] WriteChatReceive(long senderId, string name,
        byte channel, string content, int level)
    {
        var w = new PacketWriter();
        w.WriteInt64(senderId);
        w.WriteString(name);
        w.WriteInt32(level);
        w.WriteByte(channel);
        w.WriteString(content);
        return Build(PacketOpcode.ChatReceive, w.ToSpan());
    }

    public static byte[] WriteWorldTime(byte hour, byte minute, bool isDay,
        byte season, byte moonPhase)
    {
        return Build(PacketOpcode.WorldTime,
            new byte[] { hour, minute, isDay ? (byte)1 : (byte)0, season, moonPhase });
    }

    public static byte[] WriteSystemMessage(string msgVi, string msgEn)
    {
        var w = new PacketWriter();
        w.WriteString(msgVi);
        w.WriteString(msgEn);
        return Build(PacketOpcode.SystemMessage, w.ToSpan());
    }

    public static byte[] WriteLevelUp(int newLevel)
    {
        var w = new PacketWriter();
        w.WriteInt32(newLevel);
        return Build(PacketOpcode.PlayerMove, w.ToSpan()); // reuse opcode slot
    }

    public static byte[] WriteGoldChanged(long newGold, long delta)
    {
        var w = new PacketWriter();
        w.WriteInt64(newGold);
        w.WriteInt64(delta);
        return Build(PacketOpcode.ChatReceive, w.ToSpan()); // custom opcode in real impl
    }

    public static byte[] WriteHpChanged(int hp, int hpMax)
    {
        var w = new PacketWriter();
        w.WriteInt32(hp); w.WriteInt32(hpMax);
        return Build(PacketOpcode.BattleResult, w.ToSpan());
    }

    public static byte[] WriteMapChanged(int mapId, string mapName, float x, float y, bool pvp)
    {
        var w = new PacketWriter();
        w.WriteInt32(mapId);
        w.WriteString(mapName);
        w.WriteFloat(x); w.WriteFloat(y);
        w.WriteBool(pvp);
        return Build(PacketOpcode.MapChanged, w.ToSpan());
    }

    public static byte[] WriteWorldEvent(string name, string descVi, long endsAtMs)
    {
        var w = new PacketWriter();
        w.WriteString(name);
        w.WriteString(descVi);
        w.WriteInt64(endsAtMs);
        return Build(PacketOpcode.WorldEvent, w.ToSpan());
    }

    public static byte[] WritePetCatchResult(bool success, string petName,
        byte rarity, bool isShiny)
    {
        var w = new PacketWriter();
        w.WriteBool(success);
        w.WriteString(petName);
        w.WriteByte(rarity);
        w.WriteBool(isShiny);
        return Build(PacketOpcode.PetCatchResult, w.ToSpan());
    }

    public static byte[] WriteBattleStart(long defId, string defName,
        int defHp, int defLevel)
    {
        var w = new PacketWriter();
        w.WriteInt64(defId);
        w.WriteString(defName);
        w.WriteInt32(defHp);
        w.WriteInt32(defLevel);
        return Build(PacketOpcode.BattleStart, w.ToSpan());
    }

    public static byte[] WriteBattleResult(string outcome, int expGain, int goldGain)
    {
        var w = new PacketWriter();
        w.WriteString(outcome);
        w.WriteInt32(expGain);
        w.WriteInt32(goldGain);
        return Build(PacketOpcode.BattleResult, w.ToSpan());
    }

    public static byte[] WriteError(string code, string message)
    {
        var w = new PacketWriter();
        w.WriteString(code);
        w.WriteString(message);
        return Build(PacketOpcode.Error, w.ToSpan());
    }

    // ─── Readers ─────────────────────────────────────────────

    public static (string User, string Pass, string Lang)
        ReadLoginRequest(ReadOnlySpan<byte> p)
    {
        var r = new PacketReader(p);
        return (r.ReadString(), r.ReadString(), r.ReadString());
    }

    public static (float X, float Y, int MapId, byte Dir)
        ReadMove(ReadOnlySpan<byte> p)
    {
        var r = new PacketReader(p);
        return (r.ReadFloat(), r.ReadFloat(), r.ReadInt32(), r.ReadByte());
    }

    public static (byte Channel, string Content, long? TargetId)
        ReadChatSend(ReadOnlySpan<byte> p)
    {
        var r   = new PacketReader(p);
        var ch  = r.ReadByte();
        var txt = r.ReadString();
        long? target = r.Remaining >= 8 ? r.ReadInt64() : null;
        return (ch, txt, target);
    }

    public static (int PortalId,) ReadUsePortal(ReadOnlySpan<byte> p)
    {
        var r = new PacketReader(p);
        return (r.ReadInt32(),);
    }

    public static (long SpawnId, bool HasBait, int BaitItemId)
        ReadCatchPet(ReadOnlySpan<byte> p)
    {
        var r    = new PacketReader(p);
        var id   = r.ReadInt64();
        var bait = r.Remaining > 0 && r.ReadBool();
        var itemId = bait && r.Remaining >= 4 ? r.ReadInt32() : 0;
        return (id, bait, itemId);
    }
}

// ─── Binary Writer ───────────────────────────────────────────

public class PacketWriter
{
    private readonly List<byte> _buf = new();

    public void WriteByte(byte v)   => _buf.Add(v);
    public void WriteBool(bool v)   => _buf.Add(v ? (byte)1 : (byte)0);

    public void WriteInt16(short v)
    {
        _buf.Add((byte)(v >> 8));
        _buf.Add((byte)(v & 0xFF));
    }

    public void WriteInt32(int v)
    {
        _buf.Add((byte)(v >> 24));
        _buf.Add((byte)(v >> 16));
        _buf.Add((byte)(v >> 8));
        _buf.Add((byte)(v & 0xFF));
    }

    public void WriteInt64(long v)
    {
        for (int i = 7; i >= 0; i--)
            _buf.Add((byte)(v >> (i * 8)));
    }

    public void WriteFloat(float v)
    {
        var bits = BitConverter.SingleToUInt32Bits(v);
        WriteInt32((int)bits);
    }

    public void WriteString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s ?? "");
        WriteInt16((short)bytes.Length);
        _buf.AddRange(bytes);
    }

    public ReadOnlySpan<byte> ToSpan() => _buf.ToArray().AsSpan();
}

// ─── Binary Reader ───────────────────────────────────────────

public class PacketReader
{
    private readonly ReadOnlySpan<byte> _buf;
    private int _pos;

    public int Remaining => _buf.Length - _pos;

    public PacketReader(ReadOnlySpan<byte> buf) { _buf = buf; _pos = 0; }

    public byte  ReadByte()  => _buf[_pos++];
    public bool  ReadBool()  => _buf[_pos++] != 0;

    public short ReadInt16()
    {
        var v = (short)((_buf[_pos] << 8) | _buf[_pos + 1]);
        _pos += 2; return v;
    }

    public int ReadInt32()
    {
        var v = (_buf[_pos] << 24) | (_buf[_pos+1] << 16) |
                (_buf[_pos+2] << 8) |  _buf[_pos+3];
        _pos += 4; return v;
    }

    public long ReadInt64()
    {
        long v = 0;
        for (int i = 0; i < 8; i++) v = (v << 8) | _buf[_pos++];
        return v;
    }

    public float ReadFloat()
    {
        var bits = (uint)ReadInt32();
        return BitConverter.UInt32BitsToSingle(bits);
    }

    public string ReadString()
    {
        var len = (ushort)((_buf[_pos] << 8) | _buf[_pos + 1]);
        _pos += 2;
        var s = Encoding.UTF8.GetString(_buf.Slice(_pos, len));
        _pos += len;
        return s;
    }
}
