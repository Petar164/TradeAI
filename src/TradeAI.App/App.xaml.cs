using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Models;
using TradeAI.Data.Database;
using TradeAI.Data.Database.Repositories;
using TradeAI.Infrastructure.AI;
using TradeAI.Infrastructure.MarketData;
using TradeAI.Infrastructure.RiskManagement;
using TradeAI.Infrastructure.Settings;
using TradeAI.Infrastructure.Signals;
using TradeAI.UI.ChartBridge;
using TradeAI.UI.ViewModels;

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

        // Initialize the database BEFORE starting hosted services so that
        // DataFeedManager.StartAsync (which writes candles immediately) finds
        // existing tables instead of throwing a "no such table" exception.
        await BootstrapDatabaseAsync();
        await _host.StartAsync();

        // Sprint 9 — Risk profile service loads from DB (falls back to Default if none set)
        var riskService = _host.Services.GetRequiredService<IRiskProfileService>();
        await riskService.LoadAsync();

        // Eagerly resolve singletons that subscribe to SignalBus in their constructors
        _host.Services.GetRequiredService<SignalAggregator>();
        _host.Services.GetRequiredService<OverlayStateMachine>();

        // Create MainWindow and register it as Application.MainWindow BEFORE showing
        // any dialog.  ShutdownMode="OnMainWindowClose" auto-assigns MainWindow to the
        // first window opened — if that is the wizard, closing it exits the app before
        // Show() is ever reached.  Assigning here prevents that.
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        Application.Current.MainWindow = mainWindow;

        // Show onboarding wizard on first launch
        var db = _host.Services.GetRequiredService<AppDbContext>();
        if (await db.GetSettingAsync("onboarding_complete") != "1")
        {
            var wizard = new RiskProfileWizard();
            if (wizard.ShowDialog() == true && wizard.Result is not null)
            {
                await riskService.SaveAsync(wizard.Result);
                await db.SetSettingAsync("onboarding_complete", "1");
            }
        }

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
        services.AddSingleton<ICandleRepository,        CandleRepository>();
        services.AddSingleton<ISignalRepository,        SignalRepository>();
        services.AddSingleton<IFeatureVectorRepository, FeatureVectorRepository>();
        services.AddSingleton<IWatchlistRepository,     WatchlistRepository>();
        services.AddSingleton<IRiskProfileRepository,   RiskProfileRepository>();
        // Forward write/read/store interfaces to existing singleton instances (no double-instantiation)
        services.AddSingleton<ICandleWriter>(sp   => (ICandleWriter)sp.GetRequiredService<ICandleRepository>());
        services.AddSingleton<IWatchlistReader>(sp => (IWatchlistReader)sp.GetRequiredService<IWatchlistRepository>());
        services.AddSingleton<ISignalStore>(sp    => (ISignalStore)sp.GetRequiredService<ISignalRepository>());
        services.AddSingleton<IFeatureVectorStore>(sp => (IFeatureVectorStore)sp.GetRequiredService<IFeatureVectorRepository>());

        // ── Market data feed
        services.AddSingleton<CandleCache>();
        services.AddSingleton<RateLimitScheduler>();
        services.AddSingleton<IMarketDataProvider, YahooFinanceProvider>();
        // Register DataFeedManager as singleton AND as a hosted service
        services.AddSingleton<DataFeedManager>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DataFeedManager>());
        // Forward ILiveCandleFeed to the same DataFeedManager singleton
        services.AddSingleton<ILiveCandleFeed>(sp => sp.GetRequiredService<DataFeedManager>());
        // Forward IActiveSymbolProvider to AppSettings singleton
        services.AddSingleton<IActiveSymbolProvider>(sp => sp.GetRequiredService<AppSettings>());

        // ── Chart (Sprint 4)
        services.AddSingleton<ChartBridgeService>();
        services.AddSingleton<ChartViewModel>();

        // ── UI (Transient so each resolve creates a fresh window instance)
        services.AddTransient<MainWindow>();

        // ── SignalBus (Sprint 5) — capture WPF SynchronizationContext after UI thread is ready
        services.AddSingleton<SignalBus>(_ =>
            new SignalBus(SynchronizationContext.Current));

        // ── Signal engine (Sprints 6 + 7 + 8)
        // IEnumerable<ISignalDetector> is auto-resolved by DI from all registered ISignalDetector singletons
        services.AddSingleton<ISignalDetector, TrendContinuationDetector>();
        services.AddSingleton<ISignalDetector, BreakoutRetestDetector>();
        services.AddSingleton<ISignalDetector, MeanReversionDetector>();
        services.AddSingleton<ISignalDetector, SupportResistanceBounceDetector>();
        // Sprint 8 — Similarity engine
        services.AddSingleton<FeatureVectorBuilder>();
        services.AddSingleton<ISimilarityEngine, SimilarityEngine>();
        services.AddSingleton<SignalAggregator>();
        services.AddSingleton<OverlayStateMachine>();

        // Sprint 9 — Risk profile system
        services.AddSingleton<IRiskProfileService, RiskProfileService>();

        // Sprint 10.5 — Ollama AI assistant
        services.AddSingleton<OllamaClient>(_ =>
            new OllamaClient(new HttpClient()));
        services.AddSingleton<IAIAssistant, TradeAIAssistant>();
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
