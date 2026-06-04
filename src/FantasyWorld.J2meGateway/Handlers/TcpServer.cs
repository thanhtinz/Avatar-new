// FantasyWorld.J2meGateway/Handlers/TcpServer.cs
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FantasyWorld.J2meGateway.Protocol;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.J2meGateway.Handlers;

/// <summary>
/// TCP server lắng nghe kết nối từ J2ME client
/// Bridge: binary TCP ↔ HTTP REST + JSON
/// </summary>
public class J2meTcpServer(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<J2meTcpServer> logger)
    : BackgroundService
{
    private readonly int _port = int.Parse(config["J2me:TcpPort"] ?? "7777");
    private readonly string _gameServerUrl = config["J2me:GameServerUrl"] ?? "http://localhost:3000";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        logger.LogInformation("J2ME TCP Gateway listening on :{Port}", _port);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "J2ME accept error");
            }
        }

        listener.Stop();
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        var endpoint = tcp.Client.RemoteEndPoint?.ToString() ?? "unknown";
        logger.LogInformation("J2ME client connected: {Endpoint}", endpoint);

        var stream       = tcp.GetStream();
        var recvBuf      = new byte[4096];
        var parseBuffer  = new List<byte>();
        string? token    = null;
        long charId      = 0;
        int mapId        = 1;

        try
        {
            while (!ct.IsCancellationRequested && tcp.Connected)
            {
                var bytesRead = await stream.ReadAsync(recvBuf, ct);
                if (bytesRead == 0) break;

                parseBuffer.AddRange(recvBuf.AsSpan(0, bytesRead).ToArray());

                // Xử lý từng packet trong buffer
                while (true)
                {
                    var span = parseBuffer.ToArray().AsSpan();
                    if (!PacketParser.TryReadPacket(span, out var opcode, out var payload, out var consumed))
                        break;

                    parseBuffer.RemoveRange(0, consumed);

                    // Dispatch packet
                    var response = await DispatchAsync(opcode, payload, endpoint,
                        ref token, ref charId, ref mapId);

                    if (response is not null)
                        await stream.WriteAsync(response, ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("J2ME client error [{Endpoint}]: {Msg}", endpoint, ex.Message);
        }
        finally
        {
            tcp.Close();
            logger.LogInformation("J2ME client disconnected: {Endpoint}", endpoint);
        }
    }

    private async Task<byte[]?> DispatchAsync(
        PacketOpcode opcode, ReadOnlySpan<byte> payload, string endpoint,
        ref string? token, ref long charId, ref int mapId)
    {
        var http = httpFactory.CreateClient("GameServer");

        try
        {
            switch (opcode)
            {
                case PacketOpcode.LoginRequest:
                {
                    var (username, password, lang) = PacketParser.ReadLoginRequest(payload);
                    var body = JsonSerializer.Serialize(new { username, password });
                    var res  = await http.PostAsync("/api/auth/login",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (!res.IsSuccessStatusCode)
                    {
                        var msg = doc.RootElement.GetProperty("message").GetString() ?? "Error";
                        return PacketParser.BuildError("LOGIN_FAILED", msg);
                    }

                    token = doc.RootElement
                        .GetProperty("data").GetProperty("accessToken").GetString();

                    var message = doc.RootElement.GetProperty("message").GetString() ?? "OK";
                    return PacketParser.BuildLoginResponse(true, message, token);
                }

                case PacketOpcode.SelectCharacter:
                {
                    if (token is null) return PacketParser.BuildError("NOT_AUTH", "Login first");

                    charId = BitConverter.ToInt64(payload.ToArray(), 0);
                    // Validasi karakter via REST
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var res = await http.GetAsync($"/api/characters/{charId}");
                    if (!res.IsSuccessStatusCode)
                        return PacketParser.BuildError("SELECT_FAILED", "Character not found");

                    return null; // GameReady sẽ build phức tạp hơn — Phase 2
                }

                case PacketOpcode.Move:
                {
                    if (charId == 0) return null;
                    var (_, x, y, newMapId, dir) = PacketParser.ReadMove(payload);

                    // Forward movement đến game server qua REST (hoặc SignalR)
                    // J2ME dùng HTTP polling thay vì persistent connection
                    mapId = newMapId > 0 ? newMapId : mapId;

                    // Build broadcast packet
                    return PacketParser.BuildPlayerMove(charId, x, y, dir);
                }

                case PacketOpcode.ChatSend:
                {
                    if (charId == 0) return null;
                    // Parse chat packet
                    var channel = payload[0];
                    var msgLen  = (payload[1] << 8) | payload[2];
                    var content = Encoding.UTF8.GetString(payload.Slice(3, msgLen));

                    // Forward qua REST
                    if (token is not null)
                    {
                        http.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        var body = JsonSerializer.Serialize(new { channel, content });
                        await http.PostAsync("/api/chat",
                            new StringContent(body, Encoding.UTF8, "application/json"));
                    }
                    return null;
                }

                case PacketOpcode.LogoutRequest:
                {
                    if (token is not null)
                    {
                        http.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        await http.PostAsync("/api/auth/logout", null);
                        token  = null;
                        charId = 0;
                    }
                    return null;
                }

                default:
                    logger.LogDebug("Unknown opcode: 0x{Opcode:X4}", (ushort)opcode);
                    return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dispatch error opcode=0x{Opcode:X4}", (ushort)opcode);
            return PacketParser.BuildError("SERVER_ERROR", "Internal server error");
        }
    }
}
