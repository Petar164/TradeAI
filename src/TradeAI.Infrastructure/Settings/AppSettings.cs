using System.IO;
using TradeAI.Core.Interfaces;

namespace TradeAI.Infrastructure.Settings;

public class AppSettings : IActiveSymbolProvider
{
    // Database
    public string DatabasePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradeAI",
            "tradeai.db");

    // Active chart state
    public string ActiveSymbol    { get; set; } = "AAPL";
    public string ActiveTimeframe { get; set; } = "5m";

    // Window geometry (persisted on close)
    public double WindowLeft   { get; set; } = double.NaN;
    public double WindowTop    { get; set; } = double.NaN;
    public double WindowWidth  { get; set; } = 1600;
    public double WindowHeight { get; set; } = 900;

    // Data provider
    public string ActiveProvider { get; set; } = "Yahoo";

    // Polling intervals (ms)
    public int IntraCandlePollMs   { get; set; } = 5_000;
    public int WatchlistBatchDelayMs { get; set; } = 200;

    // Feature limits
    public int FreeWatchlistLimit  { get; set; } = 10;
    public int PaidWatchlistLimit  { get; set; } = 40;
    public int FreeSignalTtl       { get; set; } = 2;

    // Candle cache depth per symbol/timeframe
    public int CandleCacheDepth { get; set; } = 500;

    // Ollama AI assistant
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel   { get; set; } = "llama3.2";
}
