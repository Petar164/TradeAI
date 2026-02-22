using TradeAI.Core.Events;

namespace TradeAI.Core.Interfaces;

/// <summary>
/// Published by DataFeedManager. ViewModels subscribe to these events
/// to drive chart updates. Replaced by SignalBus in Sprint 5.
/// </summary>
public interface ILiveCandleFeed
{
    /// <summary>Raised every ~5 s with the current (partial) candle for the active symbol.</summary>
    event EventHandler<CandleEventArgs>? IntraCandleUpdated;

    /// <summary>Raised when a candle closes (new candle started) for any polled symbol.</summary>
    event EventHandler<CandleEventArgs>? CandleClosed;
}
