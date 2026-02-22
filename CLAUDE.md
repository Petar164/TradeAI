# CLAUDE.md — TradeAI Desktop App

> Master reference for all AI-assisted development sessions.
> This file defines architecture, constraints, patterns, and conventions.
> Every session should start by reading this file in full.

---

## PROJECT OVERVIEW

**TradeAI** is a Windows desktop application that visually augments stock and forex charts with AI-powered trade planning. It overlays probabilistic entry zones, stops, targets, and educational insights directly on a TradingView-style chart.

**This app does NOT execute trades. It is a prediction + planning tool.**

The goal: "TradingView on crack — focused purely on prediction and trade planning."

---

## TECH STACK

| Layer | Technology |
|---|---|
| Desktop Framework | WPF (.NET 8) |
| Chart Rendering | TradingView Lightweight Charts (via WebView2) |
| Local Database | SQLite (via Microsoft.Data.Sqlite) |
| Signal Engine | C# (core logic, no Python in v1) |
| Inter-component Messaging | SignalBus (custom event bus pattern) |
| Market Data | Free sources initially (Yahoo Finance / Alpha Vantage / Polygon free tier) |
| Packaging | MSIX / single-file publish |
| Future ML | ONNX Runtime (LightGBM/XGBoost export path) |

---

## SYSTEM ARCHITECTURE

```
┌─────────────────────────────────────────────────────────────────┐
│                        TradeAI Desktop                          │
│                                                                 │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────────┐   │
│  │  WPF Shell   │   │  WebView2    │   │   Signal Panel   │   │
│  │  (Layout,    │   │  (Chart Host │   │   (Overlays,     │   │
│  │   Nav, UX)   │   │   + Overlays)│   │    Prob Score)   │   │
│  └──────┬───────┘   └──────┬───────┘   └────────┬─────────┘   │
│         │                  │                     │             │
│         └──────────────────┼─────────────────────┘            │
│                            │                                   │
│                     ┌──────▼──────┐                           │
│                     │  SignalBus  │  (Event-driven backbone)  │
│                     └──────┬──────┘                           │
│                            │                                   │
│    ┌───────────────────────┼───────────────────────┐          │
│    │                       │                       │          │
│  ┌─▼──────────┐   ┌────────▼───────┐   ┌──────────▼──────┐  │
│  │ DataFeed   │   │ SignalEngine   │   │ SimilarityEngine│  │
│  │ Manager    │   │ (C# detection) │   │ (kNN + vectors) │  │
│  └─┬──────────┘   └────────┬───────┘   └──────────┬──────┘  │
│    │                       │                       │          │
│  ┌─▼──────────┐   ┌────────▼───────┐   ┌──────────▼──────┐  │
│  │ Rate-Limit │   │ OverlayState   │   │    SQLite DB    │  │
│  │ Scheduler  │   │ Machine        │   │  (candles,      │  │
│  │            │   │                │   │   vectors,      │  │
│  └────────────┘   └────────────────┘   │   signals)      │  │
│                                        └─────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## FOLDER STRUCTURE

```
TradeAI/
├── CLAUDE.md                          ← This file
├── TradeAI.sln
│
├── src/
│   ├── TradeAI.App/                   ← WPF entry point
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   └── Resources/
│   │       ├── Styles/
│   │       └── Fonts/
│   │
│   ├── TradeAI.Core/                  ← Business logic (no UI deps)
│   │   ├── Models/
│   │   │   ├── Candle.cs
│   │   │   ├── Signal.cs
│   │   │   ├── OverlayState.cs
│   │   │   ├── TradeZone.cs
│   │   │   ├── RiskProfile.cs
│   │   │   └── WatchlistItem.cs
│   │   │
│   │   ├── Signals/
│   │   │   ├── ISignalDetector.cs
│   │   │   ├── TrendContinuationDetector.cs
│   │   │   ├── BreakoutRetestDetector.cs
│   │   │   ├── MeanReversionDetector.cs
│   │   │   ├── SupportResistanceBounceDetector.cs
│   │   │   └── SignalAggregator.cs
│   │   │
│   │   ├── Probability/
│   │   │   ├── FeatureVectorBuilder.cs
│   │   │   ├── SimilarityEngine.cs
│   │   │   └── HistoricalOutcomeEvaluator.cs
│   │   │
│   │   ├── RiskManagement/
│   │   │   ├── RiskProfileService.cs
│   │   │   ├── StopPlacementCalculator.cs
│   │   │   └── PositionSizeCalculator.cs
│   │   │
│   │   ├── Overlay/
│   │   │   ├── OverlayStateMachine.cs
│   │   │   └── OverlayStateTransitions.cs
│   │   │
│   │   └── Messaging/
│   │       ├── SignalBus.cs
│   │       └── Events/
│   │           ├── CandleClosedEvent.cs
│   │           ├── IntraCandleUpdateEvent.cs
│   │           ├── SignalDetectedEvent.cs
│   │           ├── OverlayStateChangedEvent.cs
│   │           └── RiskProfileUpdatedEvent.cs
│   │
│   ├── TradeAI.Data/                  ← Data access layer
│   │   ├── Database/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   │       ├── CandleRepository.cs
│   │   │       ├── SignalRepository.cs
│   │   │       ├── FeatureVectorRepository.cs
│   │   │       └── WatchlistRepository.cs
│   │   │
│   │   └── MarketData/
│   │       ├── IMarketDataProvider.cs
│   │       ├── YahooFinanceProvider.cs
│   │       ├── AlphaVantageProvider.cs
│   │       ├── RateLimitScheduler.cs
│   │       └── CandleCache.cs
│   │
│   ├── TradeAI.UI/                    ← WPF Views + ViewModels
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── ChartViewModel.cs
│   │   │   ├── WatchlistViewModel.cs
│   │   │   ├── SignalPanelViewModel.cs
│   │   │   └── RiskProfileViewModel.cs
│   │   │
│   │   ├── Views/
│   │   │   ├── ChartView.xaml
│   │   │   ├── WatchlistView.xaml
│   │   │   ├── SignalPanelView.xaml
│   │   │   ├── RiskProfileWizard.xaml
│   │   │   └── EducationalOverlay.xaml
│   │   │
│   │   └── ChartBridge/
│   │       ├── ChartBridgeService.cs   ← C# ↔ WebView2 JS bridge
│   │       └── chart-host.html
│   │
│   └── TradeAI.Infrastructure/        ← Cross-cutting concerns
│       ├── Logging/
│       ├── Settings/
│       │   └── AppSettings.cs
│       ├── Licensing/
│       │   └── FeatureToggle.cs
│       └── Telemetry/
│
└── tests/
    ├── TradeAI.Core.Tests/
    └── TradeAI.Data.Tests/
```

---

## DATA FLOW

```
[Market Data Provider]
        │
        ▼ raw OHLCV
[RateLimitScheduler]
        │
        ├─── Active symbol → intracandle tick (every ~5s)
        └─── Watchlist symbols → candle-close only
        │
        ▼
[CandleCache] ─── persists to SQLite via CandleRepository
        │
        ▼
[SignalBus] publishes: CandleClosedEvent / IntraCandleUpdateEvent
        │
        ├─── [SignalAggregator] ← runs all ISignalDetector implementations
        │         │
        │         ▼ SignalDetectedEvent
        │    [SimilarityEngine] ← builds feature vector, finds 10 kNN, computes probability
        │         │
        │         ▼ Signal with probability attached
        │    [OverlayStateMachine] ← manages: Pending → Active → Hit/Stopped/Expired
        │         │
        │         ▼ OverlayStateChangedEvent
        │    [ChartBridgeService] ← serializes overlay to JSON → postMessage to WebView2
        │         │
        │         ▼
        │    [chart-host.html] ← TradingView Lightweight Charts draws overlay
        │
        └─── [WatchlistViewModel] ← updates price/signal badges on watchlist items
```

---

## OVERLAY STATE MACHINE

```
States: NONE → PENDING → ACTIVE → [TARGET_HIT | STOP_HIT | EXPIRED]

NONE
  │  signal detected
  ▼
PENDING
  │  Entry zone drawn. TTL countdown begins.
  │
  ├── price enters entry_low..entry_high → ACTIVE
  │       │
  │       ├── price hits target_low..target_high → TARGET_HIT (green glow)
  │       ├── price hits stop_price → STOP_HIT (red glow)
  │       └── TTL exhausted → EXPIRED (fade out)
  │
  └── TTL exhausted before entry → EXPIRED (fade out)

ACTIVE state tracks:
  - current R-multiple (live)
  - max favorable excursion
  - time in trade (candles)

On TARGET_HIT:
  - Log outcome to SQLite (used by SimilarityEngine for future probability)
  - Show educational summary card

On STOP_HIT:
  - Log outcome
  - Show what went wrong (educational)

On EXPIRED:
  - Log as "no-fill"
```

---

## SIGNAL DETECTION — PSEUDOCODE

### Shared Signal Output Schema
```
Signal {
  symbol: string
  timeframe: string
  direction: LONG | SHORT
  entry_low: decimal
  entry_high: decimal
  stop_price: decimal
  target_low: decimal
  target_high: decimal
  ttl_candles: int           // free=2, paid=configurable
  confidence_pct: float      // from SimilarityEngine
  similarity_sample_count: int
  historical_hit_rate_pct: float
  signal_type: string
  detected_at_candle_index: int
  detected_at_price: decimal
}
```

### 1. Trend Continuation
```
INPUTS: candles[-20..-1], EMA(20), EMA(50), ATR(14)

DETECT:
  trend = bullish if EMA20 > EMA50 AND last 5 closes > EMA20
  pullback = last 2-3 candles retraced 38%–62% toward EMA20
  trigger = current candle body closes back above EMA20

OUTPUTS:
  entry_low  = trigger_candle.low
  entry_high = trigger_candle.high
  stop       = pullback_low - ATR * 0.5
  target     = last_swing_high + (swing_high - swing_low) * 0.618
  ttl        = 3 candles
```

### 2. Breakout + Retest
```
INPUTS: candles[-50..-1], resistance_zones[], ATR(14)

DETECT:
  breakout_candle = strong close (>0.7 of range) above resistance
  retest = subsequent candle(s) pull back to broken resistance ± ATR*0.3
  confirmation = candle closes back above resistance from below

OUTPUTS:
  entry_low  = resistance_level
  entry_high = resistance_level + ATR * 0.4
  stop       = resistance_level - ATR * 0.8
  target     = resistance_level + (breakout_height * 1.5)
  ttl        = 2-4 candles
```

### 3. Mean Reversion
```
INPUTS: candles[-30..-1], VWAP, Bollinger Bands(20,2), RSI(14)

DETECT:
  overextended = price > BB_upper AND RSI > 72 (short) OR price < BB_lower AND RSI < 28 (long)
  exhaustion_candle = small body + large wick in direction of extension
  volume_decline = current_volume < avg_volume_5 * 0.7

OUTPUTS:
  entry_low  = exhaustion_candle.close - ATR*0.2
  entry_high = exhaustion_candle.close + ATR*0.2
  stop       = exhaustion_candle.high + ATR*0.3 (for short)
  target     = VWAP or BB_middle
  ttl        = 2 candles
```

### 4. Support/Resistance Bounce
```
INPUTS: candles[-100..-1], identified S/R levels, ATR(14)

DETECT:
  sr_zone = price within ATR*0.5 of major S/R level (3+ touches in history)
  approach = candle closes within zone with low volume
  bounce_candle = next candle: strong rejection wick + close away from zone
  volume_spike = bounce_candle volume > avg_volume_10 * 1.2

OUTPUTS:
  entry_low  = sr_level + ATR*0.1
  entry_high = sr_level + ATR*0.5
  stop       = sr_level - ATR*0.7
  target     = next S/R level in direction
  ttl        = 3 candles
```

---

## SIMILARITY ENGINE DESIGN

### Feature Vector (per setup snapshot)
```
FeatureVector {
  // Returns (multi-window)
  return_1c: float    // 1-candle return
  return_3c: float
  return_5c: float
  return_10c: float

  // Volatility
  atr_14_normalized: float   // ATR / price
  bb_width_normalized: float

  // Trend context
  ema20_slope: float         // (EMA20_now - EMA20_5ago) / price
  ema50_slope: float
  price_vs_ema20: float      // (price - EMA20) / ATR

  // Candle structure (last 3 candles)
  body_ratio_1: float        // body / total_range
  wick_upper_ratio_1: float
  wick_lower_ratio_1: float
  body_ratio_2: float
  body_ratio_3: float

  // Volume
  volume_ratio_5: float      // current_vol / avg_vol_5
  volume_ratio_20: float

  // Price context
  distance_from_vwap: float  // (price - VWAP) / ATR
  rsi_14: float              // normalized 0..1
}
```

### kNN Similarity Query
```
ALGORITHM:
1. Build FeatureVector for current setup (20 floats)
2. Normalize all features to [0,1] using stored min/max
3. Query SQLite for all stored vectors of same signal_type
4. Compute Euclidean distance to each stored vector
5. Take 10 nearest neighbors (K=10)
6. For each neighbor: look up outcome (TARGET_HIT=1, STOP_HIT=0, EXPIRED=skip)
7. probability = hits / (hits + stops)
8. Return probability, K used, actual sample count

STORAGE:
  - Store vector as JSON blob in SQLite (fast enough for <100k rows)
  - Add index on signal_type
  - Upgrade path: move to FAISS or dedicated vector column when >1M rows
```

---

## DATABASE SCHEMA

```sql
-- Candle data
CREATE TABLE candles (
  id          INTEGER PRIMARY KEY,
  symbol      TEXT NOT NULL,
  timeframe   TEXT NOT NULL,
  open_time   INTEGER NOT NULL,   -- Unix ms
  open        REAL NOT NULL,
  high        REAL NOT NULL,
  low         REAL NOT NULL,
  close       REAL NOT NULL,
  volume      REAL NOT NULL,
  UNIQUE(symbol, timeframe, open_time)
);
CREATE INDEX idx_candles_symbol_tf ON candles(symbol, timeframe, open_time DESC);

-- Detected signals
CREATE TABLE signals (
  id                        INTEGER PRIMARY KEY,
  symbol                    TEXT NOT NULL,
  timeframe                 TEXT NOT NULL,
  signal_type               TEXT NOT NULL,
  direction                 TEXT NOT NULL,       -- LONG / SHORT
  detected_at_candle_time   INTEGER NOT NULL,
  entry_low                 REAL NOT NULL,
  entry_high                REAL NOT NULL,
  stop_price                REAL NOT NULL,
  target_low                REAL NOT NULL,
  target_high               REAL NOT NULL,
  ttl_candles               INTEGER NOT NULL,
  confidence_pct            REAL,
  similarity_sample_count   INTEGER,
  historical_hit_rate_pct   REAL,
  state                     TEXT NOT NULL,       -- PENDING/ACTIVE/TARGET_HIT/STOP_HIT/EXPIRED
  outcome_time              INTEGER,
  feature_vector_json       TEXT                 -- serialized FeatureVector
);

-- Feature vectors for kNN lookup
CREATE TABLE feature_vectors (
  id             INTEGER PRIMARY KEY,
  signal_id      INTEGER REFERENCES signals(id),
  signal_type    TEXT NOT NULL,
  vector_json    TEXT NOT NULL,
  outcome        INTEGER,       -- 1=target hit, 0=stop hit, NULL=pending/expired
  created_at     INTEGER NOT NULL
);
CREATE INDEX idx_fv_signal_type ON feature_vectors(signal_type, outcome);

-- Watchlist
CREATE TABLE watchlist (
  id        INTEGER PRIMARY KEY,
  symbol    TEXT NOT NULL UNIQUE,
  asset_type TEXT NOT NULL,     -- STOCK / FOREX
  position  INTEGER NOT NULL,
  is_active INTEGER NOT NULL DEFAULT 1
);

-- Risk profile
CREATE TABLE risk_profiles (
  id                    INTEGER PRIMARY KEY,
  created_at            INTEGER NOT NULL,
  max_risk_pct          REAL NOT NULL,
  stop_style            TEXT NOT NULL,    -- TIGHT / NORMAL / WIDE
  max_concurrent_trades INTEGER NOT NULL,
  drawdown_tolerance    TEXT NOT NULL,    -- LOW / MEDIUM / HIGH
  is_active             INTEGER NOT NULL DEFAULT 0
);

-- App settings
CREATE TABLE settings (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
```

---

## RATE LIMIT STRATEGY

```
POLLING ARCHITECTURE:

Active Symbol (intracandle):
  - Poll every 5 seconds
  - Only for the symbol currently displayed on chart
  - Fetch latest partial candle only
  - Max: 12 calls/min for 1 symbol

Watchlist (candle-close):
  - Queue all 40 symbols
  - Process in batches of 5
  - Fire only when a new candle closes (timeframe-aware timer)
  - 1m TF: max 40 updates/min → batch over 60s
  - 5m TF: 40 updates spread over 300s → very comfortable
  - Jitter: ±500ms random delay per batch to avoid thundering herd

Provider limits (free tiers):
  - Yahoo Finance (yfinance): ~2000 req/hour, no key needed
  - Alpha Vantage free: 25 req/day (use for fallback/historical only)
  - Polygon.io free: 5 req/min realtime

Rate limit enforcement (RateLimitScheduler):
  - Token bucket per provider
  - Automatic backoff on 429
  - Circuit breaker after 3 consecutive failures
  - Fallback to cached last-known values
```

---

## MONETIZATION FEATURE TOGGLES

```csharp
// src/TradeAI.Infrastructure/Licensing/FeatureToggle.cs

public static class FeatureToggle
{
    // FREE TIER
    public static bool IsWatchlistLimited  => !IsPaid;   // max 10 symbols free
    public static bool IsSignalTtlFixed    => !IsPaid;   // TTL locked to 2 candles
    public static bool IsHistoryLimited    => !IsPaid;   // 30 days history only
    public static bool IsSimilarityLocked  => !IsPaid;   // probability hidden free

    // PAID TIER
    public static bool CanCustomizeTtl           => IsPaid;
    public static bool CanAccessAllTimeframes    => IsPaid;
    public static bool CanExportSignals          => IsPaid;
    public static bool HasFullWatchlist          => IsPaid;  // up to 40 symbols
    public static bool HasFullSimilarityScore    => IsPaid;
    public static bool HasRiskProfileAI          => IsPaid;  // chat override
    public static bool HasEducationalOverlays    => IsPaid;

    // Read from license file / server validation
    private static bool IsPaid => LicenseManager.Current?.IsValid ?? false;
}
```

---

## RISK PROFILE SYSTEM

### First-Launch Questionnaire Flow
```
Step 1: "What % of your account are you comfortable risking per trade?"
  → Options: 0.5% / 1% / 2% / Custom
  → Stored as: max_risk_pct

Step 2: "How tight should stops be?"
  → TIGHT: stop = ATR * 0.5 multiplier
  → NORMAL: stop = ATR * 1.0 multiplier
  → WIDE: stop = ATR * 1.5 multiplier
  → Stored as: stop_style

Step 3: "Max simultaneous open signal overlays?"
  → 1 / 2 / 3 / 5
  → Stored as: max_concurrent_trades

Step 4: "How much drawdown can you stomach before you want fewer signals?"
  → LOW (<5%) / MEDIUM (5-15%) / HIGH (>15%)
  → Adjusts: signal confidence threshold filter
```

### Chat-Style Override
```
User: "this feels too risky"
  → Widen stop by one tier (TIGHT→NORMAL or NORMAL→WIDE)
  → Reduce position size by 25%
  → Show updated R:R on overlay

User: "I'm feeling aggressive today"
  → Tighten stop one tier
  → Allow higher TTL
  → Show warning: "Higher risk mode active"

User: "reset to defaults"
  → Restore from saved risk_profile
```

---

## DEVELOPMENT ROADMAP

> Full sprint-by-sprint plan lives in **SPRINTS.md** — always check that file before starting any session.
> Never start a new sprint until the previous sprint's Definition of Done is fully met.

| Phase | Sprints | Goal |
|---|---|---|
| 1 — Foundation | 1–4 | App launches, live candles on chart |
| 2 — Signal Engine | 5–6 | First signal detected and drawn on chart |
| 3 — All Signals + Probability | 7–8 | All 4 signals + kNN probability score |
| 4 — Risk Profile + UX Polish | 9–11 | Personalized risk, animations, educational overlays |
| 5 — Rate Limiting + Monetization | 12–13 | Rate-safe, free/paid tiers enforced |
| 6 — Polish + Distribution | 14–16 | MSIX packaging, auto-update, smoke tested |
| Post-v1 | 17 | ONNX ML model replaces kNN |

---

## SIGNALBUS PATTERN

```csharp
// Usage example — publish
SignalBus.Publish(new SignalDetectedEvent { Signal = mySignal });

// Usage example — subscribe (in ViewModel constructor)
SignalBus.Subscribe<SignalDetectedEvent>(OnSignalDetected);

// Implementation: weak references, dispatcher-aware for UI thread
```

All cross-component communication MUST go through SignalBus.
Direct method calls between layers are only allowed within the same project (e.g. Core → Data).

---

## CHART BRIDGE PROTOCOL

WebView2 bridge uses postMessage in both directions.

```
C# → JS (commands):
  { type: "DRAW_SIGNAL", payload: Signal }
  { type: "UPDATE_OVERLAY_STATE", payload: { id, state, r_multiple } }
  { type: "CLEAR_EXPIRED" }
  { type: "SET_SYMBOL", payload: { symbol, timeframe } }
  { type: "LOAD_CANDLES", payload: Candle[] }

JS → C# (events):
  { type: "CHART_READY" }
  { type: "CROSSHAIR_MOVE", payload: { price, time } }
  { type: "SYMBOL_CLICK", payload: { symbol } }
```

---

## PERFORMANCE & SCALING

- SQLite WAL mode enabled — concurrent reads during writes
- Candle data pruned: keep last 500 candles per symbol/timeframe in memory cache
- Feature vector table capped at 500k rows; archive older rows to cold storage
- WebView2: overlay redraws batched — max 1 redraw per 100ms (debounced)
- WPF UI: all ViewModel updates via `Application.Current.Dispatcher.InvokeAsync`
- Signal detection: runs on background thread pool, never on UI thread
- Max 40 watchlist symbols: enforce hard cap in WatchlistRepository

---

## FUTURE ML UPGRADE PATH

```
v1: kNN similarity (cosine/Euclidean) — pure C#, no external deps
v2: Train LightGBM on accumulated outcome data (Python script, offline)
     → Export model to ONNX format
     → Load via Microsoft.ML.OnnxRuntime in C#
     → Drop-in replacement for SimilarityEngine.ComputeProbability()
v3: Online learning — retrain ONNX model weekly with new outcomes
v4: Per-user model (cloud sync of feature vectors + outcomes)
```

---

## CODING CONVENTIONS

- **MVVM strictly enforced** — no code-behind logic in .xaml.cs except wiring
- **No magic numbers** — all ATR multipliers, thresholds in named constants
- **Interfaces for all services** — ISignalDetector, IMarketDataProvider, etc.
- **Immutable signal records** — Signal is a C# record, not mutable class
- **Async all the way** — all data fetch and DB calls are async/await
- **No global state** — use DI (Microsoft.Extensions.DependencyInjection)
- **Test signal detectors independently** — pure functions, no DB dependency
- **Never block the UI thread** — all heavy work on Task.Run or background service

---

## SUPPORTED ASSETS

- **Stocks**: US equities (NYSE/NASDAQ)
- **Forex**: Major pairs (EUR/USD, GBP/USD, USD/JPY, etc.)
- **Timeframes with signal logic**: 1m, 3m, 5m, 15m, 30m
- **Viewable timeframes**: All (1m through Daily) — signals only generated sub-1h
- **Watchlist max**: 40 symbols (paid), 10 symbols (free)

---

## KEY CONSTRAINTS — NEVER VIOLATE

1. App NEVER sends orders to any broker or exchange
2. Rate limits NEVER exceeded — circuit breaker must be in place before any new provider
3. OverlayStateMachine is the ONLY place that changes overlay state
4. SignalBus is the ONLY inter-layer communication channel
5. FeatureToggle is checked at the point of capability use, not at UI layer only
6. All market data access goes through CandleCache — never raw provider calls from ViewModels
