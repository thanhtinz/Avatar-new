// FantasyWorld.Server/Services/AuthService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, LoginResponseDto? Data)>
        RegisterAsync(string username, string password, string? email, string lang);

    Task<(bool Success, string Message, LoginResponseDto? Data)>
        LoginAsync(string username, string password, string lang,
                   ClientType clientType, string? ip, string? deviceInfo);

    Task LogoutAsync(string token);
    Task<string?> RefreshTokenAsync(string refreshToken, ClientType clientType);
    Task<bool> ValidateTokenAsync(string token);
}

public class AuthService(
    GameDbContext db,
    IConfiguration config,
    ILocalizationService loc,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly int _bcryptRounds = int.Parse(config["Auth:BcryptRounds"] ?? "12");

    // ─── Register ────────────────────────────────────────────
    public async Task<(bool, string, LoginResponseDto?)> RegisterAsync(
        string username, string password, string? email, string lang)
    {
        // Check duplicates
        var userLower = username.ToLower();
        var exists = await db.Accounts
            .Where(a => a.Username == userLower ||
                        (email != null && a.Email == email))
            .Select(a => new { a.Username, a.Email })
            .FirstOrDefaultAsync();

        if (exists != null)
        {
            if (exists.Username == userLower)
                return (false, loc.Get("auth.username_taken", lang), null);
            return (false, loc.Get("auth.email_taken", lang), null);
        }

        var account = new Account
        {
            Username     = userLower,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, _bcryptRounds),
            Email        = email,
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        logger.LogInformation("New account registered: {Username} (id={Id})", username, account.Id);
        return (true, loc.Get("auth.register_success", lang), null);
    }

    // ─── Login ───────────────────────────────────────────────
    public async Task<(bool, string, LoginResponseDto?)> LoginAsync(
        string username, string password, string lang,
        ClientType clientType, string? ip, string? deviceInfo)
    {
        var account = await db.Accounts
            .Include(a => a.Characters)
            .FirstOrDefaultAsync(a => a.Username == username.ToLower());

        if (account is null || !BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
            return (false, loc.Get("auth.invalid_credentials", lang), null);

        if (account.IsBanned)
        {
            var msg = loc.Get("auth.account_banned", lang,
                new { reason = account.BanReason ?? "" });
            return (false, msg, null);
        }

        // Update login info
        account.LastLogin = DateTime.UtcNow;
        account.LastIp    = ip;

        // Create tokens
        var accessToken  = GenerateAccessToken(account.Id, clientType);
        var refreshToken = GenerateRefreshToken(account.Id, clientType);

        var expiryDays = int.Parse(config["Auth:JwtExpiresDays"] ?? "7");
        var session = new Session
        {
            AccountId  = account.Id,
            Token      = accessToken,
            ClientType = clientType,
            IpAddress  = ip,
            DeviceInfo = deviceInfo,
            ExpiresAt  = DateTime.UtcNow.AddDays(expiryDays),
        };

        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var characters = account.Characters.Select(c => new CharacterSummaryDto(
            c.Id, c.Name, c.Level, c.Gender, c.MapId, c.PosX, c.PosY
        )).ToList();

        logger.LogInformation("Login: {Username} [{ClientType}] from {Ip}",
            account.Username, clientType, ip);

        return (true, loc.Get("auth.login_success", lang), new LoginResponseDto(
            true,
            loc.Get("auth.login_success", lang),
            accessToken,
            refreshToken,
            new AccountInfoDto(account.Id, account.Username, account.Role.ToString()),
            characters
        ));
    }

    // ─── Logout ──────────────────────────────────────────────
    public async Task LogoutAsync(string token)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Token == token);
        if (session is not null)
        {
            db.Sessions.Remove(session);
            await db.SaveChangesAsync();
        }
    }

    // ─── Refresh ─────────────────────────────────────────────
    public async Task<string?> RefreshTokenAsync(string refreshToken, ClientType clientType)
    {
        var accountId = ValidateRefreshToken(refreshToken);
        if (accountId is null) return null;

        var account = await db.Accounts.FindAsync(accountId.Value);
        if (account is null || account.IsBanned) return null;

        var newToken   = GenerateAccessToken(account.Id, clientType);
        var expiryDays = int.Parse(config["Auth:JwtExpiresDays"] ?? "7");

        db.Sessions.Add(new Session
        {
            AccountId  = account.Id,
            Token      = newToken,
            ClientType = clientType,
            ExpiresAt  = DateTime.UtcNow.AddDays(expiryDays),
        });
        await db.SaveChangesAsync();
        return newToken;
    }

    // ─── Validate token ──────────────────────────────────────
    public async Task<bool> ValidateTokenAsync(string token)
    {
        return await db.Sessions
            .AnyAsync(s => s.Token == token && s.ExpiresAt > DateTime.UtcNow);
    }

    // ─── JWT helpers ─────────────────────────────────────────
    private string GenerateAccessToken(long accountId, ClientType clientType)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:JwtSecret"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(int.Parse(config["Auth:JwtExpiresDays"] ?? "7"));

        var token = new JwtSecurityToken(
            issuer:   "FantasyWorld",
            audience: "FantasyWorldClient",
            claims:
            [
                new Claim("accountId",   accountId.ToString()),
                new Claim("clientType",  clientType.ToString()),
            ],
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken(long accountId, ClientType clientType)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:JwtRefreshSecret"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:   "FantasyWorld",
            audience: "FantasyWorldClient",
            claims:
            [
                new Claim("accountId",   accountId.ToString()),
                new Claim("clientType",  clientType.ToString()),
                new Claim("type",        "refresh"),
            ],
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private long? ValidateRefreshToken(string token)
    {
        try
        {
            var handler    = new JwtSecurityTokenHandler();
            var key        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:JwtRefreshSecret"]!));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = false,
                ValidateAudience         = false,
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            var id        = principal.FindFirst("accountId")?.Value;
            return id is null ? null : long.Parse(id);
        }
        catch { return null; }
    }
}
