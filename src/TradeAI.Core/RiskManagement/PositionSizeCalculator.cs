namespace TradeAI.Core.RiskManagement;

/// <summary>
/// Calculates position size (units / shares) from account size, risk parameters,
/// and the entry / stop prices.
///
/// Formula:
///   risk_amount  = accountSize × (maxRiskPct / 100)
///   risk_per_unit = |entryPrice − stopPrice|
///   units         = floor(risk_amount / risk_per_unit)
/// </summary>
public static class PositionSizeCalculator
{
    /// <summary>
    /// Returns the whole number of units to trade so that total risk
    /// equals <paramref name="maxRiskPct"/>% of <paramref name="accountSize"/>.
    /// Returns 0 if the entry and stop prices are too close to compute safely.
    /// </summary>
    /// <param name="accountSize">Total account equity in the account currency.</param>
    /// <param name="maxRiskPct">Maximum percentage of account to risk (e.g. 1.0 = 1%).</param>
    /// <param name="entryPrice">Planned entry price.</param>
    /// <param name="stopPrice">Planned stop-loss price.</param>
    public static double Calculate(
        double accountSize, double maxRiskPct,
        double entryPrice,  double stopPrice)
    {
        double riskAmount  = accountSize * (maxRiskPct / 100.0);
        double riskPerUnit = Math.Abs(entryPrice - stopPrice);

        if (riskPerUnit < 1e-10) return 0;

        return Math.Floor(riskAmount / riskPerUnit);
    }
}
