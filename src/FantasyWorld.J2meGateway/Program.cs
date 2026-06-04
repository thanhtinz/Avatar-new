// FantasyWorld.J2meGateway/Program.cs
using Serilog;
using FantasyWorld.J2meGateway.Handlers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/j2me-gateway-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((ctx, services) =>
    {
        services.AddHttpClient("GameServer", c =>
        {
            c.BaseAddress = new Uri(
                ctx.Configuration["J2me:GameServerUrl"] ?? "http://localhost:3000");
            c.DefaultRequestHeaders.Add("X-Client-Type", "j2me");
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<J2meTcpServer>();
    });

var host = builder.Build();
Log.Information("🔌 J2ME TCP Gateway starting on port 7777...");
await host.RunAsync();
