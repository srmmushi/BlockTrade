namespace BlockTrade;

/// <summary>
/// Plain C# snapshot of one stock. Kept free of Godot types so validation and
/// unit tests never depend on a live scene tree.
/// </summary>
public sealed class BlockTradeStockSnapshot
{
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public double BasePrice { get; set; }
    public double Volatility { get; set; }
    public double Drift { get; set; }
    public List<double> History { get; } = new();
}

/// <summary>Plain C# snapshot of one player holding.</summary>
public sealed class BlockTradeHoldingSnapshot
{
    public string Name { get; set; } = string.Empty;
    public int Shares { get; set; }
    public double AvgCost { get; set; }
}

/// <summary>Serializable state for version 5 of the BlockTrade save format.</summary>
public sealed class BlockTradeSaveData
{
    public int Version { get; set; } = BlockTradeSaveManager.CurrentVersion;
    public double PlayerCash { get; set; }
    public double MarketCash { get; set; }
    public double LoanDebt { get; set; }
    public string TradingPasswordHash { get; set; } = string.Empty;
    public string TradingPasswordSalt { get; set; } = string.Empty;
    public double GameTime { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public double RefreshIntervalSeconds { get; set; } = 120.0;
    public double RefreshRemainingSeconds { get; set; } = 120.0;
    public List<BlockTradeStockSnapshot> Stocks { get; } = new();
    public List<BlockTradeHoldingSnapshot> Holdings { get; } = new();
}
