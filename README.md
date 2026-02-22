# TradeAI

> AI-powered trade setup detection for Windows — overlays probabilistic entry zones, stops, and targets directly on live charts. Built for traders who want edge, not noise.

![Status](https://img.shields.io/badge/status-in%20development-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## What it does

TradeAI watches your watchlist in real-time and automatically detects high-probability trade setups across stocks and forex. When a setup is found, it draws a visual overlay directly on the chart — showing you exactly where to enter, where to place your stop, and where the target is — along with a probability score based on how similar setups have played out historically.

**This app does not execute trades.** It is a planning and education tool.

---

## Key Features

### Signal Detection
Detects 4 battle-tested trade setups automatically:

| Signal | Logic |
|--------|-------|
| **Trend Continuation** | EMA pullback in an established trend — high-probability continuation entries |
| **Breakout Retest** | Price breaks resistance, retests it, confirms — classic breakout entry |
| **Mean Reversion** | Bollinger Band breach + RSI extreme + exhaustion candle — fade the move |
| **S/R Bounce** | Price approaches a clustered support/resistance zone with volume confirmation |

### Probabilistic Overlays
Every signal comes with an overlay drawn on the chart:
- **Entry zone** — semi-transparent rectangle showing the optimal entry range
- **Stop line** — dashed red line at your invalidation point
- **Target zone** — green zone at the take-profit area
- **Probability badge** — `68% (7/10)` — how often this exact setup has worked historically

### Similarity Engine
Uses a **k-Nearest Neighbours** engine (K=10, 20-feature vectors) to score each new setup against thousands of historical patterns. The more data it accumulates, the smarter it gets. Designed to upgrade to ONNX/LightGBM post-v1.

### Overlay State Machine
Overlays are alive — they track price in real-time:
- **Pending** → waiting for entry
- **Active** → price entered the zone (entry zone pulses)
- **Target Hit** → green flash, outcome recorded
- **Stop Hit** → red flash, outcome recorded
- **Expired** → TTL elapsed, fades out

### Risk Profile System
Set your personal risk parameters once via an onboarding wizard:
- Max risk % per trade
- Stop style: Tight / Normal / Wide (ATR multiplier)
- Max concurrent trades
- Drawdown tolerance

The app adjusts every signal's stop and position size suggestion to match your profile. Override on-the-fly by typing in the chat bar: *"too risky"*, *"widen stops"*, *"more aggressive"*.

### Live Market Data
- Real-time OHLCV from Yahoo Finance
- Timeframes: 1m, 5m, 15m, 30m, 1h, 1d
- Watchlist: up to 40 symbols (stocks + forex)
- Rate-limit safe: token bucket scheduler + circuit breaker

---

## Screenshots

> Coming in Sprint 4 (chart rendering).

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | WPF (.NET 9, Windows) |
| Chart | TradingView Lightweight Charts (JS) via WebView2 |
| Data | SQLite with WAL mode |
| Market Data | Yahoo Finance API |
| DI / Hosting | Microsoft.Extensions.Hosting |
| MVVM | CommunityToolkit.Mvvm |
| ML (future) | ONNX Runtime / LightGBM |

### Architecture

```
TradeAI.App          ← Entry point, DI wiring, WPF host
TradeAI.UI           ← Views, ViewModels, chart bridge
TradeAI.Infrastructure ← Market data providers, cache, rate limiter, feed manager
TradeAI.Data         ← SQLite repositories
TradeAI.Core         ← Models, interfaces, signal detection logic
```

---

## Development Roadmap

| Sprint | Goal | Status |
|--------|------|--------|
| 1 | Solution scaffolding + dark UI shell | ✅ Complete |
| 2 | SQLite database + all models + repositories | ✅ Complete |
| 3 | Market data feed (Yahoo Finance, cache, rate limiter) | 🔄 In progress |
| 4 | TradingView chart in WebView2 + live candles | ⬜ Pending |
| 5 | SignalBus + EMA, ATR, RSI, BB, VWAP indicators | ⬜ Pending |
| 6 | Trend Continuation detector + overlay drawing | ⬜ Pending |
| 7 | All 4 signal detectors | ⬜ Pending |
| 8 | kNN similarity engine + probability scores | ⬜ Pending |
| 9 | Risk profile wizard + stop calculator | ⬜ Pending |
| 10 | Live watchlist panel + signal badges | ⬜ Pending |
| 11 | Animations + educational overlay cards | ⬜ Pending |
| 12 | Rate limit hardening + fallback providers | ⬜ Pending |
| 13 | Free / Paid tier feature gates | ⬜ Pending |
| 14 | Settings, persistence, crash handling | ⬜ Pending |
| 15 | Performance profiling (target: i5 / 8GB RAM) | ⬜ Pending |
| 16 | MSIX packaging + auto-updater | ⬜ Pending |

---

## Getting Started

### Prerequisites
- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed on Windows 11)

### Build & Run

```bash
git clone https://github.com/YOUR_USERNAME/TradeAI.git
cd TradeAI
dotnet build TradeAI.sln
dotnet run --project src/TradeAI.App
```

On first launch:
- Database is created at `%AppData%\TradeAI\tradeai.db`
- Default watchlist is seeded: AAPL, MSFT, TSLA, EUR/USD, GBP/USD

---

## Project Philosophy

Most trading tools are either too simple (just pretty charts) or too complex (full algorithmic execution suites). TradeAI sits in the middle — it does the pattern recognition and probability work automatically, then hands the decision back to the human trader. No black box, no auto-trading, no subscriptions to mystery signals. Just your chart, with an AI co-pilot pointing out setups you might have missed.

---

## License

MIT
