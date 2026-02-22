using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>Write-only candle persistence. Used by Infrastructure so it does not need to reference Data.</summary>
public interface ICandleWriter
{
    Task UpsertAsync(Candle candle);
    Task UpsertBatchAsync(IEnumerable<Candle> candles);
}
