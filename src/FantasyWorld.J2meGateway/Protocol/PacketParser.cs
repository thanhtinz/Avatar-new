// FantasyWorld.J2meGateway/Protocol/PacketParser.cs
// Binary protocol: [2 bytes length][2 bytes opcode][N bytes payload]
using System.Buffers.Binary;
using System.Text;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.J2meGateway.Protocol;

public static class PacketParser
{
    public const int HeaderSize = 4; // 2 length + 2 opcode

    /// <summary>Đọc packet từ buffer — trả về (opcode, payload)</summary>
    public static bool TryReadPacket(ReadOnlySpan<byte> buffer,
        out PacketOpcode opcode, out ReadOnlySpan<byte> payload, out int consumed)
    {
        opcode  = PacketOpcode.Error;
        payload = default;
        consumed = 0;

        if (buffer.Length < HeaderSize) return false;

        var length = BinaryPrimitives.ReadUInt16BigEndian(buffer[..2]);
        if (buffer.Length < length) return false;

        opcode   = (PacketOpcode)BinaryPrimitives.ReadUInt16BigEndian(buffer[2..4]);
        payload  = buffer[HeaderSize..length];
        consumed = length;
        return true;
    }

    /// <summary>Tạo packet gửi xuống J2ME client</summary>
    public static byte[] BuildPacket(PacketOpcode opcode, ReadOnlySpan<byte> payload = default)
    {
        var totalLen = (ushort)(HeaderSize + payload.Length);
        var buf      = new byte[totalLen];

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), totalLen);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(2, 2), (ushort)opcode);
        payload.CopyTo(buf.AsSpan(HeaderSize));
        return buf;
    }

    // ─── Payload builders (viết tay, hiệu quả cho J2ME) ─────

    public static byte[] BuildLoginResponse(bool success, string message, string? token)
    {
        var msg   = Encoding.UTF8.GetBytes(message);
        var tok   = token is null ? [] : Encoding.UTF8.GetBytes(token);
        var buf   = new byte[1 + 2 + msg.Length + 2 + tok.Length];
        var span  = buf.AsSpan();
        span[0]   = success ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16BigEndian(span[1..], (ushort)msg.Length);
        msg.CopyTo(span[3..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[(3 + msg.Length)..], (ushort)tok.Length);
        tok.CopyTo(span[(5 + msg.Length)..]);
        return BuildPacket(PacketOpcode.LoginResponse, buf);
    }

    public static byte[] BuildPlayerMove(long charId, float x, float y, byte dir)
    {
        var buf  = new byte[8 + 4 + 4 + 1];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteInt64BigEndian(span[..8], charId);
        BinaryPrimitives.WriteSingleBigEndian(span[8..12], x);
        BinaryPrimitives.WriteSingleBigEndian(span[12..16], y);
        span[16] = dir;
        return BuildPacket(PacketOpcode.PlayerMove, buf);
    }

    public static byte[] BuildWorldTime(byte hour, byte minute, bool isDay,
                                         byte season, byte moonPhase)
    {
        var buf = new byte[] { hour, minute, isDay ? (byte)1 : (byte)0, season, moonPhase };
        return BuildPacket(PacketOpcode.WorldTime, buf);
    }

    public static byte[] BuildChatReceive(long senderId, string name,
                                           byte channel, string content)
    {
        var nameBytes    = Encoding.UTF8.GetBytes(name);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var buf          = new byte[8 + 1 + nameBytes.Length + 1 + 2 + contentBytes.Length];
        var span         = buf.AsSpan();
        BinaryPrimitives.WriteInt64BigEndian(span, senderId);
        span[8]  = (byte)nameBytes.Length;
        nameBytes.CopyTo(span[9..]);
        int off  = 9 + nameBytes.Length;
        span[off++] = channel;
        BinaryPrimitives.WriteUInt16BigEndian(span[off..], (ushort)contentBytes.Length);
        contentBytes.CopyTo(span[(off + 2)..]);
        return BuildPacket(PacketOpcode.ChatReceive, buf);
    }

    public static byte[] BuildError(string code, string message)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var msgBytes  = Encoding.UTF8.GetBytes(message);
        var buf       = new byte[1 + codeBytes.Length + 2 + msgBytes.Length];
        var span      = buf.AsSpan();
        span[0]       = (byte)codeBytes.Length;
        codeBytes.CopyTo(span[1..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[(1 + codeBytes.Length)..], (ushort)msgBytes.Length);
        msgBytes.CopyTo(span[(3 + codeBytes.Length)..]);
        return BuildPacket(PacketOpcode.Error, buf);
    }

    // ─── Payload readers ─────────────────────────────────────

    public static (string Username, string Password, string Lang)
        ReadLoginRequest(ReadOnlySpan<byte> p)
    {
        int off         = 0;
        var userLen     = p[off++];
        var user        = Encoding.UTF8.GetString(p.Slice(off, userLen)); off += userLen;
        var passLen     = p[off++];
        var pass        = Encoding.UTF8.GetString(p.Slice(off, passLen)); off += passLen;
        var langLen     = p[off++];
        var lang        = Encoding.UTF8.GetString(p.Slice(off, langLen));
        return (user, pass, lang);
    }

    public static (long CharId, float X, float Y, int MapId, byte Dir)
        ReadMove(ReadOnlySpan<byte> p)
    {
        var charId = BinaryPrimitives.ReadInt64BigEndian(p[..8]);
        var x      = BinaryPrimitives.ReadSingleBigEndian(p[8..12]);
        var y      = BinaryPrimitives.ReadSingleBigEndian(p[12..16]);
        var mapId  = BinaryPrimitives.ReadInt32BigEndian(p[16..20]);
        var dir    = p[20];
        return (charId, x, y, mapId, dir);
    }
}
