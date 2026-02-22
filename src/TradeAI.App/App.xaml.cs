using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradeAI.Core.Models;
using TradeAI.Data.Database;
using TradeAI.Data.Database.Repositories;
using TradeAI.Infrastructure.Settings;

namespace TradeAI.App;

public partial class App : Application
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(RegisterServices)
            .Build();

        await _host.StartAsync();
        await BootstrapDatabaseAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }

    // ── Service Registration ─────────────────────────────────────────────────

    private static void RegisterServices(IServiceCollection services)
    {
        // ── Settings
        services.AddSingleton<AppSettings>();

        // ── Database context (factory: bridges AppSettings → AppDbContext)
        services.AddSingleton<AppDbContext>(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            return new AppDbContext(settings.DatabasePath);
        });

        // ── Repositories
        services.AddSingleton<ICandleRepository,       CandleRepository>();
        services.AddSingleton<ISignalRepository,       SignalRepository>();
        services.AddSingleton<IFeatureVectorRepository, FeatureVectorRepository>();
        services.AddSingleton<IWatchlistRepository,    WatchlistRepository>();

        // ── UI
        services.AddTransient<MainWindow>();

        // Sprint 3: DataFeedManager, MarketDataProviders registered here
        // Sprint 4: ChartBridgeService registered here
        // Sprint 5: SignalBus registered here
        // Sprint 6+: Signal detectors, OverlayStateMachine registered here
    }

    // ── Startup bootstrap ────────────────────────────────────────────────────

    private async Task BootstrapDatabaseAsync()
    {
        var db = _host.Services.GetRequiredService<AppDbContext>();
        await db.InitializeAsync();
        await SeedDefaultWatchlistAsync();
    }

    private static readonly WatchlistItem[] DefaultWatchlist =
    [
        new(0, "AAPL",    "STOCK", 0, true),
        new(0, "MSFT",    "STOCK", 1, true),
        new(0, "TSLA",    "STOCK", 2, true),
        new(0, "EUR/USD", "FOREX", 3, true),
        new(0, "GBP/USD", "FOREX", 4, true),
    ];

    private async Task SeedDefaultWatchlistAsync()
    {
        var repo     = _host.Services.GetRequiredService<IWatchlistRepository>();
        var db       = _host.Services.GetRequiredService<AppDbContext>();
        var seeded   = await db.GetSettingAsync("watchlist_seeded");

        if (seeded == "1") return;

        foreach (var item in DefaultWatchlist)
            await repo.UpsertAsync(item);

        await db.SetSettingAsync("watchlist_seeded", "1");
    }
}
