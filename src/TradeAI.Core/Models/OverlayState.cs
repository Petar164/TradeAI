namespace TradeAI.Core.Models;

public enum OverlayState
{
    None,
    Pending,
    Active,
    TargetHit,
    StopHit,
    Expired
}

public enum TradeDirection { Long, Short }
public enum StopStyle     { Tight, Normal, Wide }
public enum DrawdownTolerance { Low, Medium, High }
public enum AssetType     { Stock, Forex }
