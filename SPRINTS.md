# SPRINTS.md — TradeAI Development Plan

> Sprint-by-sprint build order. Each sprint has a clear goal, ordered tasks,
> acceptance criteria, and a definition of done.
> Always check this file before starting any session. Mark tasks [x] as completed.
> Never start Sprint N+1 until Sprint N Definition of Done is fully met.

---

## HOW TO USE THIS FILE

- Each sprint = 1 focused week of work
- Tasks are ordered — do them top to bottom, no skipping
- `[ ]` = not started, `[~]` = in progress, `[x]` = done
- After each sprint, update this file before closing the session
- Blockers go in the BLOCKERS section at the bottom

---

## PHASE 1 — FOUNDATION
> Goal: App launches, displays live candles on a chart, watchlist shows prices.

---

### Sprint 1 — Solution Scaffolding ✅ COMPLETE
**Goal:** Empty but correctly structured solution compiles and runs.

#### Tasks
- [x] 1.1 Create solution `TradeAI.sln` with projects:
  - `TradeAI.App` (WPF, net9.0-windows)
  - `TradeAI.Core` (Class Library, net8.0)
  - `TradeAI.Data` (Class Library, net8.0)
  - `TradeAI.UI` (WPF Class Library, net9.0-windows)
  - `TradeAI.Infrastructure` (Class Library, net8.0)
  - `TradeAI.Core.Tests` / `TradeAI.Data.Tests` (xUnit, net8.0)
- [x] 1.2 Add project references:
  - App → UI, Infrastructure
  - UI → Core, Data
  - Core → (none)
  - Data → Core
  - Infrastructure → Core
- [x] 1.3 Install NuGet packages:
  - `Microsoft.Web.WebView2` 1.0.3800.47 → App
  - `Microsoft.Data.Sqlite` 10.0.3 → Data
  - `Microsoft.Extensions.DependencyInjection` 10.0.3 → App
  - `Microsoft.Extensions.Hosting` 10.0.3 → App
  - `CommunityToolkit.Mvvm` 8.4.0 → UI, App
- [x] 1.4 Create `MainWindow.xaml` with 3-column layout:
  - Left: Watchlist panel (200px, resizable)
  - Center: Chart area (fill) with timeframe button bar
  - Right: Signal panel (280px, resizable) with chat override bar
  - Header bar + status bar rows
- [x] 1.5 Dark theme in `Resources/Styles/DarkTheme.xaml` — full colour palette,
       brush library, Button/TextBox/ScrollBar styles, TimeframeButtonStyle
- [x] 1.6 `AppSettings.cs` — DatabasePath, ActiveSymbol, ActiveTimeframe,
       window geometry, poll intervals, cache depth, feature limits
- [x] 1.7 DI container wired in `App.xaml.cs` via `IHost` / `IServiceCollection`
       — MainWindow resolved from DI, host lifecycle managed

#### Definition of Done ✅
- [x] Solution builds with 0 errors, 0 warnings
- [x] All 5 src projects + 2 test projects in solution
- [x] Project references match architecture diagram
- [x] Dark 3-panel layout defined in XAML

#### Notes
- dotnet template WPF projects default to net9.0-windows (not net8.0-windows)
- `LetterSpacing` and `Spacing` are WinUI properties — not valid in WPF; use Margin instead
- `StartupUri` removed from App.xaml — window resolved via DI host

---

### Sprint 2 — Database + Models ✅ COMPLETE
**Goal:** SQLite database initializes on launch with correct schema.

#### Tasks
- [x] 2.1 Create all model records in `TradeAI.Core/Models/`:
  - `Candle.cs` — symbol, timeframe, open_time (DateTimeOffset), OHLCV
  - `Signal.cs` — full schema per CLAUDE.md
  - `TradeZone.cs` — entry_low, entry_high, stop, target_low, target_high, ttl
  - `OverlayState.cs` — enum: None, Pending, Active, TargetHit, StopHit, Expired
  - `RiskProfile.cs` — max_risk_pct, stop_style, max_concurrent, drawdown_tolerance
  - `WatchlistItem.cs` — symbol, asset_type, position, is_active
- [x] 2.2 Create `AppDbContext.cs` in `TradeAI.Data/Database/`
  - Opens SQLite connection to path from AppSettings
  - Enables WAL mode on open: `PRAGMA journal_mode=WAL`
  - `InitializeAsync()` method runs CREATE TABLE IF NOT EXISTS for all 6 tables
- [x] 2.3 Create all repositories with interfaces:
  - `ICandleRepository` / `CandleRepository`
    - `UpsertAsync(Candle)` — INSERT OR REPLACE
    - `GetRecentAsync(symbol, timeframe, count)` → `List<Candle>`
  - `ISignalRepository` / `SignalRepository`
    - `InsertAsync(Signal)`
    - `UpdateStateAsync(id, OverlayState, outcomeTime?)`
    - `GetActiveAsync(symbol)` → `List<Signal>`
  - `IFeatureVectorRepository` / `FeatureVectorRepository`
    - `InsertAsync(signalId, signalType, vectorJson, outcome?)`
    - `GetByTypeAsync(signalType)` → `List<(string vectorJson, int? outcome)>`
  - `IWatchlistRepository` / `WatchlistRepository`
    - `GetAllAsync()` → `List<WatchlistItem>`
    - `UpsertAsync(WatchlistItem)`
    - `RemoveAsync(symbol)`
- [x] 2.4 Register all repositories in DI container
- [x] 2.5 Seed watchlist with 5 default symbols on first launch:
  - AAPL, MSFT, EUR/USD, GBP/USD, TSLA
- [x] 2.6 Verify with SQLite browser that schema matches CLAUDE.md exactly

#### Definition of Done ✅
- [x] App launches, DB file created at configured path
- [x] All 6 tables exist with correct columns and indexes
- [x] Repositories can insert and query without exceptions

---

### Sprint 3 — Market Data Feed ✅ COMPLETE
**Goal:** App fetches real OHLCV data and stores it in the database.

#### Tasks
- [x] 3.1 Create `IMarketDataProvider.cs` + `ICandleWriter.cs` + `IWatchlistReader.cs` in Core/Interfaces/
       and `CandleEventArgs.cs` in Core/Events/. Updated ICandleRepository : ICandleWriter,
       IWatchlistRepository : IWatchlistReader to avoid Infrastructure → Data dependency.
- [x] 3.2 Implement `YahooFinanceProvider.cs` — HttpClient, Yahoo v8 API, JSON parsing with
       null/gap handling, FOREX conversion (EUR/USD → EURUSD=X), per-timeframe interval+range map
- [x] 3.3 Create `CandleCache.cs` — ConcurrentDictionary, 500-candle depth,
       Set / UpdateLastOrAppend / GetLast / TryGet (all lock-safe)
- [x] 3.4 Create `RateLimitScheduler.cs` — token bucket (10 tok, 1/s refill),
       3-failure circuit breaker (60s cooldown), half-open probe, per-request jitter
- [x] 3.5 Create `DataFeedManager.cs` (IHostedService) — active-symbol loop (5s),
       candle-close detection via OpenTime comparison, watchlist refresh loop (60s),
       SetActiveSymbol() with volatile reload flag
- [x] 3.6 Registered: CandleCache, RateLimitScheduler, YahooFinanceProvider, DataFeedManager
       (singleton + IHostedService). ICandleWriter/IWatchlistReader forwarded from repo singletons.
- [x] 3.7 Verified: 200 AAPL/5m candles on startup. All 5 watchlist symbols have rows in DB.

#### Definition of Done ✅
- [x] Log shows "Loaded 200 candles for AAPL/5m" on startup
- [x] Candles table has rows for all 5 watchlist symbols
- [x] Build: 0 errors, 0 warnings

---

### Sprint 4 — Chart Display ✅ COMPLETE
**Goal:** Live candles render on the TradingView chart inside WebView2.

#### Tasks
- [x] 4.1 `chart-host.html` as EmbeddedResource in TradeAI.UI — TradingView v3.8.0 from CDN,
       dark theme (#0B0E17), `window.bridge.loadCandles()` + `updateLastCandle()`,
       ResizeObserver for container resize, CHART_READY postMessage on load
- [x] 4.2 `ChartBridgeService.cs` — EnsureCoreWebView2Async, NavigateToString from manifest
       resource, TaskCompletionSource for CHART_READY, LoadCandlesAsync + UpdateLastCandleAsync
       via ExecuteScriptAsync (30s init timeout)
- [x] 4.3 `ChartView.xaml` UserControl — WebView2 fills center panel.
       Loaded event → `vm.InitializeChartAsync(WebView)`
- [x] 4.4 `ChartViewModel.cs` — ILiveCandleFeed events → Dispatcher.BeginInvoke →
       UpdateLastCandleAsync. ChangeTimeframeAsync reloads 200 candles from DB.
- [x] 4.5 Timeframe buttons call `DataFeedManager.SetActiveSymbol` + `ChartViewModel.ChangeTimeframeAsync`
- [x] 4.6 `chart.timeScale().fitContent()` called inside `bridge.loadCandles()` in JS
- [x] New Core interfaces: ILiveCandleFeed, IActiveSymbolProvider.
       AppSettings : IActiveSymbolProvider. DataFeedManager : ILiveCandleFeed.

#### Definition of Done ✅
- [x] App launches and shows live AAPL/5m candlestick chart (200 candles)
- [x] Candles update in real-time via IntraCandleUpdated events
- [x] Timeframe buttons reload chart with correct data from DB
- [x] Build: 0 errors, 0 warnings

---

## PHASE 2 — SIGNAL ENGINE
> Goal: First signal type detected and drawn on chart as overlay.

---

### Sprint 5 — Event Bus + Indicator Helpers
**Goal:** SignalBus operational, technical indicator library available.

#### Tasks
- [ ] 5.1 Implement `SignalBus.cs` in `TradeAI.Core/Messaging/`:
  - Generic `Subscribe<T>(Action<T> handler)` with weak reference
  - `Publish<T>(T event)` — iterates live subscribers
  - `Unsubscribe<T>(Action<T> handler)`
  - Thread-safe — use `ConcurrentDictionary`
  - UI thread dispatch: accept optional `SynchronizationContext`
- [ ] 5.2 Define all event types in `TradeAI.Core/Messaging/Events/`:
  - `CandleClosedEvent` — symbol, timeframe, Candle
  - `IntraCandleUpdateEvent` — symbol, timeframe, partial Candle
  - `SignalDetectedEvent` — Signal
  - `OverlayStateChangedEvent` — signalId, old state, new state, Signal
  - `RiskProfileUpdatedEvent` — RiskProfile
  - `ActiveSymbolChangedEvent` — symbol, timeframe
- [ ] 5.3 Replace all direct method calls between DataFeedManager and ViewModels with SignalBus events
- [ ] 5.4 Create `Indicators.cs` static class in `TradeAI.Core/`:
  - `EMA(IList<double> closes, int period)` → `double[]`
  - `ATR(IList<Candle> candles, int period)` → `double[]`
  - `RSI(IList<double> closes, int period)` → `double[]`
  - `BollingerBands(IList<double> closes, int period, double stdDev)` → `(upper, middle, lower)[]`
  - `VWAP(IList<Candle> candles)` → `double[]` (resets per session)
  - All functions: return NaN for insufficient data, never throw
- [ ] 5.5 Unit test all indicators against known values (at least 3 test cases each)

#### Definition of Done
- SignalBus routes events between DataFeedManager, ViewModels, and Signal Engine
- All indicator functions return correct values verified by unit tests
- No direct cross-layer method calls remain

---

### Sprint 6 — Trend Continuation Detector + Overlay Drawing
**Goal:** Trend Continuation signals detected and drawn on chart.

#### Tasks
- [ ] 6.1 Create `ISignalDetector.cs`:
  ```csharp
  Task<Signal?> DetectAsync(IList<Candle> candles, string symbol, string timeframe)
  ```
- [ ] 6.2 Implement `TrendContinuationDetector.cs` per CLAUDE.md pseudocode:
  - EMA20 > EMA50 for trend
  - 38–62% pullback to EMA20
  - Trigger candle closes back above EMA20
  - Outputs: entry zone, stop (pullback_low - ATR*0.5), target (swing_high + extension)
  - Return `null` if no setup found
- [ ] 6.3 Create `SignalAggregator.cs`:
  - Holds list of `ISignalDetector` implementations
  - `RunAllAsync(candles, symbol, timeframe)` → first non-null Signal
  - Called on every `CandleClosedEvent`
  - Publishes `SignalDetectedEvent` if signal found
- [ ] 6.4 Extend `chart-host.html` with overlay drawing functions:
  - `bridge.drawSignal(signal)`:
    - Arrow marker on trigger candle
    - Entry zone: semi-transparent rectangle (blue, 20% opacity) spanning TTL candles
    - Stop line: red dashed horizontal line
    - Target zone: semi-transparent green rectangle
    - Probability badge: small label near arrow
  - `bridge.updateOverlayState(id, state, rMultiple)`:
    - ACTIVE: entry zone pulses (subtle animation)
    - TARGET_HIT: green glow flash then fade to outline
    - STOP_HIT: red glow flash then fade
    - EXPIRED: fade to 10% opacity
  - `bridge.clearExpiredOverlays()`: removes EXPIRED overlays after 5s
- [ ] 6.5 Extend `ChartBridgeService.cs`:
  - `DrawSignalAsync(Signal)` → serializes signal to JSON → calls `bridge.drawSignal()`
  - `UpdateOverlayStateAsync(id, state, rMultiple)` → calls `bridge.updateOverlayState()`
- [ ] 6.6 Implement `OverlayStateMachine.cs`:
  - Tracks all active overlays in `Dictionary<int, SignalOverlay>`
  - On `IntraCandleUpdateEvent`: check each PENDING/ACTIVE overlay:
    - PENDING: if price in entry zone → transition to ACTIVE
    - ACTIVE: if price hits target → TARGET_HIT; if price hits stop → STOP_HIT
    - Any: if ttl_candles elapsed → EXPIRED
  - On state change: publish `OverlayStateChangedEvent`
  - Subscribe to `OverlayStateChangedEvent` in `ChartViewModel` → call bridge update
- [ ] 6.7 Wire full flow: CandleClose → Detect → Draw → State transitions → Visual update

#### Definition of Done
- On AAPL 5m chart, Trend Continuation signals appear within first 10 minutes
- Entry zone rectangle visible on chart with correct price range
- Stop line and target zone visible
- State transitions correctly change overlay visuals

---

## PHASE 3 — ALL SIGNALS + PROBABILITY ENGINE
> Goal: All 4 signal types working. Probability score shown on every signal.

---

### Sprint 7 — Remaining Signal Detectors
**Goal:** All 4 signal types detected and producing overlays.

#### Tasks
- [ ] 7.1 Implement `BreakoutRetestDetector.cs` per CLAUDE.md pseudocode:
  - Identify resistance zones (swing highs touched 2+ times in last 50 candles)
  - Detect breakout candle (strong close > 70% of range above resistance)
  - Detect retest (pullback within ATR*0.3 of broken resistance)
  - Confirmation close back above
- [ ] 7.2 Implement `MeanReversionDetector.cs`:
  - BB upper/lower breach with RSI extreme
  - Exhaustion candle pattern (small body, large wick)
  - Volume declining on extension
- [ ] 7.3 Implement `SupportResistanceBounceDetector.cs`:
  - Build S/R zones from last 100 candles (cluster swing points within ATR*0.3)
  - Detect approach to zone with low volume
  - Detect bounce candle with volume spike
- [ ] 7.4 Register all 4 detectors in `SignalAggregator` via DI
- [ ] 7.5 Add `signal_type` visual distinction on chart:
  - Trend Continuation: blue arrow
  - Breakout Retest: orange arrow
  - Mean Reversion: purple arrow
  - S/R Bounce: yellow arrow
- [ ] 7.6 Prevent duplicate signals: if active signal exists for same symbol+direction, skip
- [ ] 7.7 Test each detector with 1 week of historical data — verify at least 1 signal fires

#### Definition of Done
- All 4 detectors fire signals on historical data review
- No duplicate overlays for same direction on same symbol
- Each signal type has distinct visual color

---

### Sprint 8 — Similarity Engine + Probability Score
**Goal:** Every signal shows a probability score based on historical similarity.

#### Tasks
- [ ] 8.1 Implement `FeatureVectorBuilder.cs`:
  - `Build(IList<Candle> candles)` → `FeatureVector`
  - Compute all 20 features per CLAUDE.md spec
  - Return as `float[]` for distance calculations
  - Normalize: track running min/max per feature in SQLite `settings` table
- [ ] 8.2 Implement `SimilarityEngine.cs`:
  - `ComputeProbabilityAsync(FeatureVector current, string signalType)`:
    1. Load all vectors for signalType from `FeatureVectorRepository`
    2. If fewer than 10 samples → return `null` (not enough data)
    3. Compute Euclidean distance to each stored vector
    4. Sort, take top 10
    5. Count outcomes: hit=1, stop=0, skip expired/null
    6. Return `(probability: float, sampleCount: int, hitRate: float)`
  - `StoreVectorAsync(Signal signal, FeatureVector vector)` — stores with null outcome
  - `RecordOutcomeAsync(signalId, outcome)` — updates outcome when state resolves
- [ ] 8.3 Integrate into signal pipeline:
  - After `SignalAggregator` produces a Signal → run `SimilarityEngine.ComputeProbabilityAsync()`
  - Attach result to Signal before publishing `SignalDetectedEvent`
  - Store feature vector in DB via `SimilarityEngine.StoreVectorAsync()`
- [ ] 8.4 On overlay state resolving (TARGET_HIT / STOP_HIT):
  - Call `SimilarityEngine.RecordOutcomeAsync(signalId, outcome)`
- [ ] 8.5 Display probability on chart overlay:
  - Show badge near arrow: "68% (7/10)" in white text on dark pill background
  - If null (insufficient data): show "—" badge
- [ ] 8.6 Display probability in Signal Panel (right panel):
  - Large percentage display
  - Small bar chart showing 10 neighbor outcomes (green/red squares)

#### Definition of Done
- Probability score appears on all signals after 10+ historical outcomes exist
- Feature vectors stored in DB on each signal detection
- Outcomes recorded when overlays resolve
- After running for 1 session, second session shows improving probability scores

---

## PHASE 4 — RISK PROFILE + UX POLISH
> Goal: Personalized risk system. Animations. Educational overlays.

---

### Sprint 9 — Risk Profile System
**Goal:** User completes onboarding questionnaire. Signals adapt to risk settings.

#### Tasks
- [ ] 9.1 Build `RiskProfileWizard.xaml` — 4-step modal dialog:
  - Step 1: % risk slider (0.5%, 1%, 2%, custom input)
  - Step 2: Stop style radio buttons (TIGHT / NORMAL / WIDE + description of each)
  - Step 3: Concurrent trades stepper (1–5)
  - Step 4: Drawdown tolerance (LOW / MEDIUM / HIGH with explanations)
  - Progress indicator at top
  - "Next" / "Back" / "Finish" buttons
- [ ] 9.2 Show wizard on first launch (detect via `settings` table key `onboarding_complete`)
- [ ] 9.3 Implement `StopPlacementCalculator.cs`:
  - TIGHT: `stop = swing_point - ATR * 0.5`
  - NORMAL: `stop = swing_point - ATR * 1.0`
  - WIDE: `stop = swing_point - ATR * 1.5`
  - Applied when signal detectors calculate their stops
- [ ] 9.4 Implement `PositionSizeCalculator.cs`:
  - `Calculate(accountSize, maxRiskPct, entryPrice, stopPrice)` → units
  - Display suggested position size on Signal Panel (not for execution, display only)
- [ ] 9.5 Create `ChatOverrideBar.xaml` — thin input bar at bottom of Signal Panel:
  - Text input + send button
  - Recognizes: "too risky", "more aggressive", "reset", "tighten stops", "widen stops"
  - Calls `RiskProfileService.ApplyOverride(command)`
- [ ] 9.6 Implement `RiskProfileService.cs`:
  - `ApplyOverride(string naturalLanguageCommand)` — basic keyword matching
  - Updates in-memory risk profile, publishes `RiskProfileUpdatedEvent`
  - Recalculates active pending signal stops on the fly
  - Shows confirmation message in chat bar

#### Definition of Done
- First launch shows wizard, settings saved to DB
- Stop distance visibly changes between TIGHT/NORMAL/WIDE modes
- Chat bar "too risky" command updates active signal overlays
- Position size shown in Signal Panel

---

### Sprint 10 — Watchlist Panel + Signal Badges
**Goal:** Watchlist panel shows live prices, signal indicators, and allows symbol switching.

#### Tasks
- [ ] 10.1 Build `WatchlistView.xaml`:
  - Scrollable list (max 40 items, virtualized)
  - Each row: symbol, asset type icon, last price, % change (color coded), signal badge
  - Signal badge: colored dot if active signal (matches signal type color)
  - Active symbol: highlighted row with accent border
- [ ] 10.2 Implement `WatchlistViewModel.cs`:
  - Loads watchlist from `WatchlistRepository`
  - Subscribes to `CandleClosedEvent` → updates price and % change
  - Subscribes to `SignalDetectedEvent` → sets badge on matching symbol
  - Subscribes to `OverlayStateChangedEvent` → clears/updates badge
  - `SelectSymbolCommand` → publishes `ActiveSymbolChangedEvent`
- [ ] 10.3 Add symbol search/add dialog:
  - Search box with typeahead
  - Add to watchlist → `WatchlistRepository.UpsertAsync()`
  - Enforce 40-symbol cap (10 for free tier via `FeatureToggle`)
- [ ] 10.4 Add drag-to-reorder on watchlist rows (update `position` field in DB)
- [ ] 10.5 Subscribe to `ActiveSymbolChangedEvent` in `ChartViewModel`:
  - Load fresh candles from cache
  - Clear existing overlays
  - Reload any existing active signals for new symbol from `SignalRepository`

#### Definition of Done
- Clicking a watchlist row switches chart to that symbol
- Price and % change update on each candle close
- Signal badges appear on rows when signals are active
- Adding/removing symbols persists across app restarts

---

### Sprint 11 — Animations + Educational Overlays
**Goal:** Polished animations. Educational cards explain signals to the user.

#### Tasks
- [ ] 11.1 Entry zone pulse animation (JS/CSS in chart-host.html):
  - When state = ACTIVE: entry zone rectangle border animates opacity 100%→40%→100% every 1.5s
  - Stop on TARGET_HIT or STOP_HIT
- [ ] 11.2 Outcome flash animations:
  - TARGET_HIT: green glow expands from target zone, fades over 2s
  - STOP_HIT: red pulse from stop line, fades over 2s
  - Both play once, then overlay fades to 30% opacity
- [ ] 11.3 Arrow appearance animation:
  - Arrow draws in from below (translate + fade, 300ms ease-out)
- [ ] 11.4 WPF panel animations:
  - Signal Panel slides in from right when signal detected (300ms ease-out)
  - Probability score counts up from 0 to final value (500ms)
- [ ] 11.5 Create `EducationalOverlay.xaml` — card shown in Signal Panel:
  - Signal type name + icon
  - Plain-English explanation of why this setup was detected (generated from signal properties)
  - "What to watch for" checklist (entry filled / stop nearby / target in sight)
  - R:R ratio displayed prominently
  - On outcome: "What happened" summary with lesson text
- [ ] 11.6 Write educational text templates for each signal type + outcome combination:
  - 4 signal types × 3 outcomes (hit / stop / expired) = 12 text templates
  - Templates use signal data substitution (e.g., "Price pulled back {pullback_pct}% to the EMA...")

#### Definition of Done
- All animations play smoothly at 60fps (no jank)
- Educational card appears for every signal detection
- Outcome card shows on every resolution
- No animation runs indefinitely or blocks interaction

---

## PHASE 5 — RATE LIMITING + MONETIZATION
> Goal: App is rate-limit safe. Free and paid tiers enforced correctly.

---

### Sprint 12 — Rate Limit Hardening
**Goal:** App never hits provider rate limits. Gracefully handles failures.

#### Tasks
- [ ] 12.1 Harden `RateLimitScheduler.cs`:
  - Implement proper token bucket with `SemaphoreSlim`
  - Per-provider configuration: Yahoo (12/min), Alpha Vantage (25/day)
  - Jitter: add `Random.Shared.Next(0, 500)` ms delay per request
  - Backoff: 429 → wait `Retry-After` header seconds or 60s default
  - Circuit breaker state: `Closed → Open (3 failures) → HalfOpen (after 60s) → Closed`
- [ ] 12.2 Add fallback logic in `DataFeedManager`:
  - If provider fails → serve from `CandleCache` (stale data)
  - Show "Data delayed" indicator in UI header when using stale data
- [ ] 12.3 Add watchlist batching in `DataFeedManager`:
  - Never fetch all 40 symbols simultaneously
  - Batch of 5, 200ms between batches
  - Candle-close detection: per-symbol timer that resets on each close
- [ ] 12.4 Add request logging to SQLite:
  - `provider_requests` table: provider, timestamp, status, latency_ms
  - Used to detect if approaching limits
- [ ] 12.5 Add `AlphaVantageProvider.cs` as secondary fallback:
  - Historical data only (not realtime)
  - Track daily usage, disable when 25-call limit reached

#### Definition of Done
- App runs for 4 hours without hitting a rate limit error
- "Data delayed" label appears when cache is stale
- Circuit breaker recovers automatically after 60s

---

### Sprint 13 — Feature Toggles + Free Tier
**Goal:** Free and paid tiers correctly gate features.

#### Tasks
- [ ] 13.1 Implement `LicenseManager.cs`:
  - Reads license key from `settings` table
  - Validates against local hash (offline validation for v1)
  - Exposes `IsValid` bool and `Tier` enum (Free / Paid)
- [ ] 13.2 Implement `FeatureToggle.cs` per CLAUDE.md spec:
  - All capability checks centralized here
  - No feature logic in ViewModels — only `FeatureToggle.CanXxx` checks
- [ ] 13.3 Enforce free tier restrictions:
  - `WatchlistRepository`: cap inserts at 10 if `FeatureToggle.IsWatchlistLimited`
  - `SignalAggregator`: set `ttl_candles = 2` if `FeatureToggle.IsSignalTtlFixed`
  - `SimilarityEngine`: return null probability if `FeatureToggle.IsSimilarityLocked`
  - Show upgrade prompt (non-blocking) when free user hits a limit
- [ ] 13.4 Create `UpgradePromptView.xaml` — subtle banner/modal:
  - Appears when free user tries to use paid feature
  - Shows what paid tier unlocks
  - "Dismiss" and "Learn More" buttons (Learn More opens placeholder URL)
- [ ] 13.5 Add "Enter License Key" dialog in settings
- [ ] 13.6 Test both tiers: verify free restrictions work, verify paid unlocks work

#### Definition of Done
- Free user cannot add 11th watchlist symbol (upgrade prompt appears)
- Free user sees "—" probability score with lock icon
- Paid user with valid license key has all restrictions removed
- No feature-gate bypass possible from UI

---

## PHASE 6 — POLISH + DISTRIBUTION
> Goal: App is packaged, auto-updates, and is ready for real users.

---

### Sprint 14 — Settings, Persistence, Stability
**Goal:** All user settings persist. App recovers gracefully from errors.

#### Tasks
- [ ] 14.1 Build `SettingsView.xaml`:
  - Active data provider selection (Yahoo / Alpha Vantage)
  - Theme toggle (dark only for v1 — button disabled but visible)
  - License key input
  - Risk profile "Edit" button (re-opens wizard)
  - Data storage path display
  - "Clear Cache" button
- [ ] 14.2 Persist all UI state to `settings` table:
  - Last active symbol + timeframe
  - Window size and position
  - Panel widths
- [ ] 14.3 Restore UI state on launch (last symbol, last timeframe, window geometry)
- [ ] 14.4 Add global exception handler in `App.xaml.cs`:
  - `Application.DispatcherUnhandledException` → log to file + show friendly error dialog
  - Never crash silently
- [ ] 14.5 Add structured logging (Microsoft.Extensions.Logging → file sink):
  - Log level: Info for signal detections, Warning for rate limits, Error for exceptions
  - Log file: `%AppData%\TradeAI\logs\tradeai-YYYY-MM-DD.log`
- [ ] 14.6 Add startup health check:
  - Verify DB accessible
  - Verify WebView2 runtime installed (show install prompt if not)
  - Verify internet connectivity (warn if offline)

#### Definition of Done
- App restores to last state on relaunch
- Crash logs written to file
- Friendly error message shown on any unhandled exception (no crash dialog)

---

### Sprint 15 — Performance Profiling
**Goal:** App runs smoothly on low-spec hardware (i5, 8GB RAM target).

#### Tasks
- [ ] 15.1 Profile signal detection: verify all 4 detectors complete in < 50ms
- [ ] 15.2 Profile DB queries: verify all queries complete in < 20ms (add indexes if needed)
- [ ] 15.3 Profile WebView2 rendering: verify overlay redraws < 16ms (60fps cap)
- [ ] 15.4 Implement debounce on `IntraCandleUpdateEvent`: max 1 dispatch per 100ms
- [ ] 15.5 Implement virtualization on WatchlistView (VirtualizingStackPanel already in XAML)
- [ ] 15.6 Prune candle cache: verify enforced at 500 candles per symbol/timeframe
- [ ] 15.7 Prune feature_vectors table: archive rows older than 6 months to `feature_vectors_archive`
- [ ] 15.8 Memory profiling: verify no memory leak after 4h runtime

#### Definition of Done
- App uses < 400MB RAM after 4h runtime
- UI remains responsive during signal detection
- No memory growth trend over time

---

### Sprint 16 — Packaging + Distribution
**Goal:** App installable by end user with one click.

#### Tasks
- [ ] 16.1 Configure MSIX packaging in Visual Studio
- [ ] 16.2 Bundle WebView2 runtime (Evergreen bootstrapper or fixed version)
- [ ] 16.3 Create installer that:
  - Installs app to `%LocalAppData%\TradeAI`
  - Creates Start Menu shortcut
  - Creates Desktop shortcut (opt-in)
  - Creates DB directory at `%AppData%\TradeAI`
- [ ] 16.4 Implement auto-updater:
  - Check `version.json` at update URL on launch
  - If newer version → show update prompt
  - Download + install MSIX silently
- [ ] 16.5 Code-sign the MSIX (self-signed for v1, valid cert for public release)
- [ ] 16.6 Final smoke test checklist:
  - [ ] Fresh install on clean Windows 11 VM
  - [ ] First launch wizard completes
  - [ ] Chart displays live data within 30s
  - [ ] Signal detected within 10 minutes
  - [ ] Overlay animates correctly
  - [ ] App uninstalls cleanly (no orphaned files)

#### Definition of Done
- MSIX installs and runs on clean Windows 11 VM
- All smoke test items checked
- Installer < 150MB

---

## ONNX ML UPGRADE SPRINT (Post-v1)

### Sprint 17 — ONNX Model Integration (Future)
**Goal:** Replace kNN similarity with trained LightGBM model.

#### Tasks
- [ ] 17.1 Export accumulated feature vectors + outcomes from SQLite to CSV
- [ ] 17.2 Train LightGBM model in Python (offline, separate repo)
- [ ] 17.3 Export trained model to ONNX format
- [ ] 17.4 Add `Microsoft.ML.OnnxRuntime` NuGet package
- [ ] 17.5 Implement `OnnxSimilarityEngine.cs` implementing same `ISimilarityEngine` interface
- [ ] 17.6 Load `.onnx` model file from app directory
- [ ] 17.7 Swap DI registration from `SimilarityEngine` to `OnnxSimilarityEngine`
- [ ] 17.8 Validate: ONNX predictions within 10% of kNN baseline on test set

#### Definition of Done
- ONNX model loads without error
- Probability scores produced within 10ms (vs potentially slower kNN)
- A/B test confirms ONNX accuracy >= kNN accuracy

---

## CURRENT STATUS

```
Active Sprint: Sprint 8 — Similarity Engine + Probability Score
Last Completed Sprint: Sprint 7 — Remaining Signal Detectors ✅ (2026-02-23)
Blocked: No blockers
```

---

## BLOCKERS

> Add blockers here as they arise. Format: [Date] Description — Owner

*(none yet)*

---

## NOTES + DECISIONS LOG

> Append architectural decisions made during development.

- [2026-02-22] CLAUDE.md and SPRINTS.md created.
- [2026-02-22] Sprint 1 complete. Solution scaffolded, dark theme, DI, AppSettings all done. Build: 0 errors.
  - WPF templates default to net9.0-windows. Use Margin not Spacing (WinUI only). Remove StartupUri when using DI host.
- [2026-02-22] Sprint 2 complete. All 6 models, AppDbContext (WAL), 4 repositories, DI registration, watchlist seed. Build: 0 errors.
  - AppDbContext needs path as string (not AppSettings reference) to avoid Data → Infrastructure circular dep. Use DI factory lambda in App.xaml.cs.
  - Microsoft.Data.Sqlite doesn't support multiple statements per command — loop over string[] of CREATE TABLE statements.
  - DB verified: %AppData%\TradeAI\tradeai.db, 6 tables, 5 seeded watchlist rows, watchlist_seeded=1.
- [2026-02-22] Sprint 4 complete. TradingView chart in WebView2, ChartBridgeService, ChartViewModel, live candle updates. Build: 0 errors.
  - chart-host.html as EmbeddedResource (not Content) — guaranteed to ship inside the DLL, no file path issues.
  - NavigateToString works with CDN scripts (HTTPS absolute URLs load fine from any origin).
  - WebView2 ExecuteScriptAsync is thread-safe; use Dispatcher.BeginInvoke for UpdateLastCandleAsync calls from background threads.
  - ILiveCandleFeed + IActiveSymbolProvider in Core keep UI → Core dependency clean (no UI → Infrastructure).
- [2026-02-22] Sprint 3 complete. YahooFinanceProvider, CandleCache, RateLimitScheduler, DataFeedManager all built. Build: 0 errors.
  - ICandleRepository : ICandleWriter and IWatchlistRepository : IWatchlistReader — keeps Infrastructure → Core only (no Infrastructure → Data dep).
  - DataFeedManager registered as both singleton and IHostedService via forwarding lambda so it can be resolved directly from DI by ViewModels.
  - Candle-close detection: compare partial.OpenTime to last cached OpenTime — simple and reliable.
  - Infrastructure.csproj uses Microsoft.Extensions.Hosting.Abstractions 8.0.1 (net8.0 target); App uses 10.0.3 — runtime resolves to highest.
- [2026-02-23] Sprint 5 complete. SignalBus (WeakReference pub/sub, SynchronizationContext dispatch), 6 event types, Indicators static class (EMA/ATR/RSI/BollingerBands/VWAP), 24 unit tests all passing. Build: 0 errors.
  - Candle record uses PascalCase named params — test helpers must use Open:/High:/Low:/Close:/Volume:.
  - Store delegate fields as strong references in subscribers to keep SignalBus WeakReferences alive.
- [2026-02-23] Sprint 6 complete. TrendContinuationDetector, SignalAggregator, OverlayStateMachine, chart overlay drawing (HTML div zones + LightweightCharts markers). Build: 0 errors.
  - ISignalStore in Core avoids Infrastructure → Data dep. ISignalRepository : ISignalStore.
  - Chart overlays: position:relative on #chart container, absolute-positioned divs, reposition on timeScale scroll/scale events.
  - Eager DI resolution of SignalAggregator and OverlayStateMachine at startup so they subscribe to SignalBus before first CandleClosedEvent.
- [2026-02-23] Sprint 7 complete. BreakoutRetestDetector, MeanReversionDetector, SupportResistanceBounceDetector. All 4 detectors registered via DI. Build: 0 errors.
  - Duplicate signal prevention in SignalAggregator: HashSet of active (symbol, direction) pairs, cleared on terminal OverlayStateChangedEvent.
  - Per-type arrow colours: TrendContinuation=blue, BreakoutRetest=orange, MeanReversion=purple, SRBounce=yellow.
  - signalType field added to ChartBridgeService DrawSignalAsync serialization so JS can apply per-type colours.
  - Yahoo Finance v8 API now requires crumb+cookie auth (2024+): updated YahooFinanceProvider with CookieContainer and crumb fetch from query2.finance.yahoo.com/v1/test/getcrumb.
  - LightweightCharts bundled as EmbeddedResource (lightweight-charts.standalone.production.js, 157KB) — no CDN dependency, works offline.
  - HistoricalDataReadyEvent added: DataFeedManager publishes after initial candle load; ChartViewModel subscribes and reloads chart. Fixes race condition where chart queries DB before data arrives.
