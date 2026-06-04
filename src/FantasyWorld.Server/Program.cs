// FantasyWorld.Server/Program.cs
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using FantasyWorld.Server.BackgroundServices;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Hubs;
using FantasyWorld.Server.Services;

// ─── Serilog bootstrap ───────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/server-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var cfg = builder.Configuration;

    // ─── Database (EF Core + MySQL) ──────────────────────────
    var connStr = cfg.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string not found");

    builder.Services.AddDbContext<GameDbContext>(opt =>
        opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr), mySql =>
        {
            mySql.CommandTimeout(30);
            mySql.EnableRetryOnFailure(3);
        }));

    // ─── Authentication (JWT) ────────────────────────────────
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(cfg["Auth:JwtSecret"]!)),
                ValidateIssuer   = true,
                ValidIssuer      = "FantasyWorld",
                ValidateAudience = true,
                ValidAudience    = "FantasyWorldClient",
                ClockSkew        = TimeSpan.Zero,
            };

            // MagicOnion: lấy token từ header
            opt.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    ctx.Token = ctx.Request.Headers.Authorization
                        .FirstOrDefault()?.Replace("Bearer ", "");
                    if (string.IsNullOrEmpty(ctx.Token))
                        ctx.Token = ctx.Request.Headers["x-auth-token"].FirstOrDefault();
                    return Task.CompletedTask;
                },
            };
        });

    builder.Services.AddAuthorization();

    // ─── MagicOnion (gRPC StreamingHub cho Unity) ────────────
    builder.Services.AddGrpc();
    builder.Services.AddMagicOnion(opt =>
    {
        opt.IsReturnExceptionStackTraceInErrorDetail =
            builder.Environment.IsDevelopment();
    });

    // ─── Controllers (REST API) ──────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // ─── CORS ────────────────────────────────────────────────
    builder.Services.AddCors(opt =>
        opt.AddDefaultPolicy(p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));

    // ─── Game Services ───────────────────────────────────────
    builder.Services.AddSingleton<IGameStateService, GameStateService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ICharacterService, CharacterService>();
    builder.Services.AddSingleton<ILocalizationService, LocalizationService>();

    // ─── Background Services ─────────────────────────────────
    builder.Services.AddHostedService<WorldTimeBackgroundService>();
    builder.Services.AddHostedService<DailyResetService>();

    // ─── Rate Limiting ───────────────────────────────────────
    builder.Services.AddRateLimiter(opt =>
    {
        opt.AddFixedWindowLimiter("login", p =>
        {
            p.PermitLimit = 10;
            p.Window      = TimeSpan.FromMinutes(15);
        });
        opt.AddFixedWindowLimiter("api", p =>
        {
            p.PermitLimit = 100;
            p.Window      = TimeSpan.FromMinutes(15);
        });
    });

    // ─── Health check ────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<GameDbContext>();

    var app = builder.Build();

    // ─── Migrate database on startup ─────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migration completed");
    }

    // ─── Middleware pipeline ─────────────────────────────────
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // REST API
    app.MapControllers();

    // Health
    app.MapHealthChecks("/health");

    // MagicOnion (gRPC) — Unity client
    app.MapMagicOnionService();

    // Info endpoint
    app.MapGet("/", () => new
    {
        game    = "Fantasy World Game",
        version = "1.0.0",
        status  = "running",
        time    = DateTime.UtcNow,
    });

    Log.Information("🚀 Fantasy World Game Server starting...");
    Log.Information("   REST API  → http://0.0.0.0:{Port}", cfg["HTTP_PORT"] ?? "3000");
    Log.Information("   gRPC/MagicOnion → for Unity client");
    Log.Information("   J2ME TCP  → separate gateway on :7777");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server failed to start");
}
finally
{
    Log.CloseAndFlush();
}
