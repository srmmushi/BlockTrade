using Godot;
using System.Security.Cryptography;
using GodotDictionary = Godot.Collections.Dictionary;

namespace BlockTrade;

/// <summary>
/// The complete BlockTrade simulation: market, player wallet and holdings,
/// stage progression, paid side activities, trading rules and save/load.
///
/// The node does not write to disk in _Process. A Timer owned by Main calls
/// Tick() after the configured market-refresh interval.
/// </summary>
[GlobalClass]
public partial class BlockTradeSimulator : Node
{
    public const double StartingCash = 0.0;
    public const double StartingMarketCash = 2_000.0;
    public const double DefaultRefreshIntervalSeconds = 120.0;
    public const double InitialPriceCeiling = 500.0;
    public const double CompanyInitialPriceCeiling = 100_000.0;
    public const double ShortTermUpBias = 0.0022;
    public const double DownsideJumpChance = 0.12;
    public const double DownsideJump = 0.004;
    public const double MinPrice = 1.0;
    public const double MaxPrice = 1_000_000.0;
    public const double MaxLoan = 50_000_000.0;
    public const int MaxLevel = 4;
    public const int MaxPriceHistory = 60;

    // XP required to reach level 2, 3 and 4. Index 0 is unused.
    private static readonly int[] XpToReachLevel = { 0, 80, 220, 450 };

    private static readonly (string Name, double Price, double Volatility, double Drift, int UnlockLevel)[] InitialStocks =
    {
        ("铁锭", 120.00, 2.60, 0.020, 1),
        ("金锭", 280.00, 4.20, -0.010, 1),
        ("钻石", 500.00, 7.00, 0.030, 1),
        ("红石", 45.00, 1.40, 0.000, 1),
        ("绿宝石", 360.00, 5.50, 0.010, 1),
        ("煤炭", 18.00, 0.80, -0.020, 1),
        ("青金石", 90.00, 1.80, 0.005, 2),
        ("石英", 60.00, 1.20, 0.000, 2),
        ("黑曜石", 300.00, 4.80, 0.015, 3),
        ("下界合金", 480.00, 6.50, 0.025, 3),
    };

    // The level-4 "company market": real companies with very large per-share
    // prices, so a single order is a major transaction. Names and icons are
    // deliberately separate from the item market.
    private static readonly (string Name, double Price, double Volatility, double Drift, int UnlockLevel)[] CompanyStocks =
    {
        ("深岩控股", 1_600.00, 1.20, -0.010, 4),
        ("潮汐能源", 3_200.00, 2.00, 0.000, 4),
        ("苍穹科技", 8_000.00, 3.00, 0.010, 4),
        ("星链物流", 18_000.00, 4.00, 0.015, 4),
        ("极光资本", 25_000.00, 4.80, 0.018, 4),
        ("熔炉工业", 42_000.00, 5.50, 0.020, 4),
        ("黑曜集团", 55_000.00, 6.00, 0.022, 4),
        ("末地银行", 88_000.00, 7.00, 0.030, 4),
    };

    [Export]
    public string SimulatorLabel { get; set; } = "BlockTrade Market";

    [Export(PropertyHint.Range, "5,600,5")]
    public double RefreshIntervalSeconds { get; set; } = DefaultRefreshIntervalSeconds;

    public event Action? MarketTicked;
    public event Action? StateChanged;
    public event Action? MarketReset;
    public event Action<string>? TradeFailed;
    public event Action<int, string>? LevelUp;

    private readonly Dictionary<string, Stock> _stocks = new(StringComparer.Ordinal);
    private readonly List<string> _stockOrder = new();
    private readonly List<string> _companyOrder = new();
    private readonly List<string> _allStockOrder = new();
    private readonly HashSet<string> _companyNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Holding> _holdings = new(StringComparer.Ordinal);
    private Random _random = new();

    public double PlayerCash { get; private set; } = StartingCash;
    public double MarketCash { get; private set; } = StartingMarketCash;
    public string TradingPasswordHash { get; private set; } = string.Empty;
    public string TradingPasswordSalt { get; private set; } = string.Empty;
    public double LoanDebt { get; private set; }
    public bool HasTradingPassword => !string.IsNullOrEmpty(TradingPasswordHash) &&
                                      !string.IsNullOrEmpty(TradingPasswordSalt);
    public double GameTime { get; private set; }
    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public double MarketRefreshRemaining { get; set; } = DefaultRefreshIntervalSeconds;

    public bool IsMaxLevel => Level >= MaxLevel;

    public int XpToNextLevel => IsMaxLevel ? 0 : Math.Max(0, XpToReachLevel[Level] - Xp);

    public float LevelProgress
    {
        get
        {
            if (IsMaxLevel)
            {
                return 1.0f;
            }

            int start = XpToReachLevel[Level - 1];
            int end = XpToReachLevel[Level];
            return end <= start ? 1.0f : Math.Clamp((Xp - start) / (float)(end - start), 0.0f, 1.0f);
        }
    }

    public override void _Ready()
    {
        if (_stocks.Count == 0)
        {
            InitMarket();
        }
    }

    /// <summary>
    /// Starts a brand-new game. The optional seed exists so automated tests are
    /// deterministic; normal play uses a fresh random seed.
    /// </summary>
    public void InitMarket(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _stocks.Clear();
        _stockOrder.Clear();
        _companyOrder.Clear();
        _allStockOrder.Clear();
        _companyNames.Clear();
        _holdings.Clear();
        PlayerCash = StartingCash;
        MarketCash = StartingMarketCash;
        TradingPasswordHash = string.Empty;
        TradingPasswordSalt = string.Empty;
        LoanDebt = 0.0;
        GameTime = 0.0;
        Level = 1;
        Xp = 0;
        RefreshIntervalSeconds = DefaultRefreshIntervalSeconds;
        MarketRefreshRemaining = RefreshIntervalSeconds;

        foreach ((string name, double price, double volatility, double drift, int unlockLevel) in InitialStocks)
        {
            AddStock(new Stock
            {
                Name = name,
                Price = price,
                BasePrice = price,
                Volatility = volatility,
                Drift = drift,
                UnlockLevel = unlockLevel,
            }, company: false);
        }

        foreach ((string name, double price, double volatility, double drift, int unlockLevel) in CompanyStocks)
        {
            AddStock(new Stock
            {
                Name = name,
                Price = price,
                BasePrice = price,
                Volatility = volatility,
                Drift = drift,
                UnlockLevel = unlockLevel,
            }, company: true);
        }

        MarketReset?.Invoke();
        StateChanged?.Invoke();
        GD.Print($"[BlockTrade] New market initialized: {_stockOrder.Count} item stocks, " +
                 $"{_companyOrder.Count} company stocks, cash {PlayerCash:0.00}.");
    }

    private static bool IsCompanyDefinition(string name)
    {
        foreach ((string companyName, _, _, _, _) in CompanyStocks)
        {
            if (companyName == name)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetDefinitionUnlockLevel(string name)
    {
        foreach ((string itemName, _, _, _, int unlockLevel) in InitialStocks)
        {
            if (itemName == name)
            {
                return unlockLevel;
            }
        }

        foreach ((string companyName, _, _, _, int unlockLevel) in CompanyStocks)
        {
            if (companyName == name)
            {
                return unlockLevel;
            }
        }

        return 1;
    }

    private void AddMissingItemStocks()
    {
        foreach ((string name, double price, double volatility, double drift, int unlockLevel) in InitialStocks)
        {
            if (_stocks.ContainsKey(name))
            {
                continue;
            }

            AddStock(new Stock
            {
                Name = name,
                Price = price,
                BasePrice = price,
                Volatility = volatility,
                Drift = drift,
                UnlockLevel = unlockLevel,
            }, company: false);
        }
    }

    private void AddMissingCompanyStocks()
    {
        foreach ((string name, double price, double volatility, double drift, int unlockLevel) in CompanyStocks)
        {
            if (_stocks.ContainsKey(name))
            {
                continue;
            }

            AddStock(new Stock
            {
                Name = name,
                Price = price,
                BasePrice = price,
                Volatility = volatility,
                Drift = drift,
                UnlockLevel = unlockLevel,
            }, company: true);
        }
    }

    private void AddStock(Stock stock, bool company)
    {
        _stocks[stock.Name] = stock;
        _allStockOrder.Add(stock.Name);
        if (company)
        {
            _companyOrder.Add(stock.Name);
            _companyNames.Add(stock.Name);
        }
        else
        {
            _stockOrder.Add(stock.Name);
        }

        if (stock.History.Count == 0)
        {
            stock.History.Add(stock.Price);
        }
    }

    /// <summary>
    /// Advances the market once per refresh. Every stock receives a small
    /// percentage-based random move; both the move size and the price ceiling
    /// grow slowly with game time, so the market becomes more valuable and more
    /// volatile over a long session. The ceiling starts at 500 and then grows.
    /// </summary>
    public void Tick(double delta)
    {
        if (!double.IsFinite(delta) || delta <= 0.0)
        {
            return;
        }

        GameTime += delta;
        double maturity = 1.0 + Math.Log(1.0 + GameTime / 600.0);
        foreach (string name in _allStockOrder)
        {
            Stock stock = _stocks[name];
            double ceiling = GetPriceCeiling(name);
            double riskFactor = 0.65 + stock.Volatility / 10.0;
            double growth = (ShortTermUpBias + Math.Max(0.0, stock.Drift) * 0.005) * maturity;
            double noise = RandomNormal() * 0.0055 * maturity * riskFactor;

            double average = RecentAverage(stock.History, 10);
            double stretch = average > 0.0 ? (stock.Price - average) / average : 0.0;
            double meanReversion = -stretch * 0.10;

            // Stocks that have climbed far above their original value become
            // increasingly prone to profit-taking, so high flyers also fall.
            double heightAboveBase = Math.Max(0.0, stock.Price / stock.BasePrice - 1.0);
            double profitTaking = heightAboveBase * 0.0015 * maturity;

            double change = growth + noise + meanReversion - profitTaking;
            if (_random.NextDouble() < DownsideJumpChance)
            {
                // Any stock, including a strong item or company, can drop in any
                // refresh. This is a sudden sell-off rather than a guaranteed rise.
                change -= DownsideJump * maturity * Math.Max(1.0, riskFactor);
            }
            double next = stock.Price * (1.0 + change);

            if (!double.IsFinite(next))
            {
                next = stock.BasePrice;
            }

            stock.Price = Math.Clamp(next, MinPrice, ceiling);
            stock.History.Add(stock.Price);
            if (stock.History.Count > MaxPriceHistory)
            {
                stock.History.RemoveAt(0);
            }
        }

        MarketTicked?.Invoke();
    }

    public string[] GetStockNames() => _stockOrder.ToArray();

    /// <summary>Items visible at the current stage.</summary>
    public string[] GetUnlockedStockNames()
    {
        return _stockOrder
            .Where(name => _stocks.TryGetValue(name, out Stock? stock) && stock.UnlockLevel <= Level)
            .ToArray();
    }

    public string[] GetCompanyNames() => _companyOrder.ToArray();

    public string[] GetAllStockNames() => _allStockOrder.ToArray();

    public bool IsCompanyStock(string name) => _companyNames.Contains(name);

    public bool IsStockUnlocked(string name)
    {
        return _stocks.TryGetValue(name, out Stock? stock) && stock.UnlockLevel <= Level;
    }

    public double GetPrice(string name)
    {
        return _stocks.TryGetValue(name, out Stock? stock) ? stock.Price : 0.0;
    }

    /// <summary>Read-only price history for charts; the last point is the live price.</summary>
    public IReadOnlyList<double> GetPriceHistory(string name)
    {
        return _stocks.TryGetValue(name, out Stock? stock)
            ? stock.History
            : Array.Empty<double>();
    }

    public double GetBasePrice(string name)
    {
        return _stocks.TryGetValue(name, out Stock? stock) ? stock.BasePrice : 0.0;
    }

    public double GetChangePercent(string name)
    {
        if (!_stocks.TryGetValue(name, out Stock? stock) ||
            stock.BasePrice <= 0.0 ||
            !double.IsFinite(stock.BasePrice))
        {
            return 0.0;
        }

        return (stock.Price - stock.BasePrice) / stock.BasePrice * 100.0;
    }

    public double GetCash() => PlayerCash;
    public double GetMarketCash() => MarketCash;

    /// <summary>
    /// The market's current price ceiling. It starts at 500 and grows slowly
    /// with game time, so early prices stay in the intended range while a long
    /// session can grow beyond it.
    /// </summary>
    public double GetPriceCeiling()
    {
        double hours = Math.Max(0.0, GameTime) / 3600.0;
        return Math.Min(MaxPrice, InitialPriceCeiling * Math.Exp(0.22 * hours));
    }

    /// <summary>Price ceiling for the selected market tier.</summary>
    public double GetPriceCeiling(string name)
    {
        double initial = IsCompanyStock(name) ? CompanyInitialPriceCeiling : InitialPriceCeiling;
        double hours = Math.Max(0.0, GameTime) / 3600.0;
        return Math.Min(MaxPrice, initial * Math.Exp(0.22 * hours));
    }

    public double GetGameTime() => GameTime;

    public bool TryGetHolding(string name, out int shares, out double avgCost)
    {
        if (_holdings.TryGetValue(name, out Holding? holding))
        {
            shares = holding.Shares;
            avgCost = holding.AvgCost;
            return true;
        }

        shares = 0;
        avgCost = 0.0;
        return false;
    }

    /// <summary>
    /// A Godot-friendly holdings view for scripts/UI:
    /// { "铁锭": { "shares": 1, "avg_cost": 120.0 }, ... }
    /// </summary>
    public GodotDictionary GetHoldings()
    {
        var result = new GodotDictionary();
        foreach ((string name, Holding holding) in _holdings)
        {
            var row = new GodotDictionary
            {
                ["shares"] = holding.Shares,
                ["avg_cost"] = holding.AvgCost,
            };
            result[name] = row;
        }

        return result;
    }

    public double GetAsset()
    {
        return MarketCash + GetStockAsset();
    }

    public double GetTotalAsset()
    {
        return PlayerCash + GetAsset();
    }

    /// <summary>Current market value of all stock holdings (cash excluded).</summary>
    public double GetStockAsset()
    {
        double value = 0.0;
        foreach ((string name, Holding holding) in _holdings)
        {
            if (_stocks.TryGetValue(name, out Stock? stock))
            {
                value += stock.Price * holding.Shares;
            }
        }

        return value;
    }

    /// <summary>
    /// Grants stage XP. A single call may cross multiple level thresholds; each
    /// level raises LevelUp with the feature unlocked at that stage.
    /// </summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Xp += amount;
        while (Level < MaxLevel && Xp >= XpToReachLevel[Level])
        {
            Level++;
            LevelUp?.Invoke(Level, GetUnlockText(Level));
        }

        StateChanged?.Invoke();
    }

    public static string GetUnlockText(int level) => level switch
    {
        2 => "刮刮乐 + 青金石 / 石英",
        3 => "老虎机 + 黑曜石 / 下界合金",
        4 => "公司股市 / 大转盘",
        _ => "新的阶段奖励",
    };

    public static int GetRequiredLevel(string activity) => activity switch
    {
        "scratch" => 2,
        "slots" => 3,
        "wheel" => 4,
        _ => 1,
    };

    /// <summary>Paid activities use this; it never allows the cash balance to go negative.</summary>
    public bool TrySpendCash(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0 || PlayerCash + 0.000001 < amount)
        {
            return false;
        }

        PlayerCash = Math.Max(0.0, PlayerCash - amount);
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Credits a minigame payout and emits a UI refresh.</summary>
    public void AddCash(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0)
        {
            return;
        }

        PlayerCash += amount;
        StateChanged?.Invoke();
    }

    /// <summary>Credits the stock account; used by tests and migration only.</summary>
    public void AddMarketCash(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0)
        {
            return;
        }

        MarketCash += amount;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Moves available stock-account money to external cash.
    /// </summary>
    public bool WithdrawToCash(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0 || MarketCash + 0.000001 < amount)
        {
            return false;
        }

        MarketCash = Math.Max(0.0, MarketCash - amount);
        PlayerCash += amount;
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Moves external cash into the stock account.</summary>
    public bool DepositToMarket(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0 || PlayerCash + 0.000001 < amount)
        {
            return false;
        }

        PlayerCash = Math.Max(0.0, PlayerCash - amount);
        MarketCash += amount;
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Borrows external cash. Total debt is capped at 50 million.</summary>
    public bool Borrow(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0 ||
            LoanDebt + amount > MaxLoan + 0.000001)
        {
            return false;
        }

        LoanDebt += amount;
        PlayerCash += amount;
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Repays debt from external cash only.</summary>
    public bool RepayLoan(double amount)
    {
        if (!double.IsFinite(amount) || amount <= 0.0 ||
            amount > LoanDebt + 0.000001 ||
            amount > PlayerCash + 0.000001)
        {
            return false;
        }

        LoanDebt = Math.Max(0.0, LoanDebt - amount);
        PlayerCash = Math.Max(0.0, PlayerCash - amount);
        StateChanged?.Invoke();
        return true;
    }

    public bool SetTradingPassword(string password)
    {
        if (password.Length != 6 || !password.All(char.IsDigit))
        {
            return false;
        }

        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000,
            HashAlgorithmName.SHA256, 32);
        TradingPasswordSalt = Convert.ToBase64String(salt);
        TradingPasswordHash = Convert.ToBase64String(hash);
        StateChanged?.Invoke();
        return true;
    }

    public bool VerifyTradingPassword(string password)
    {
        if (!HasTradingPassword || password.Length != 6 || !password.All(char.IsDigit))
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(TradingPasswordSalt);
            byte[] expected = Convert.FromBase64String(TradingPasswordHash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000,
                HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Buys a whole number of shares. Invalid operations return false and leave
    /// cash and holdings untouched.
    /// </summary>
    public bool Buy(string name, int shares)
    {
        if (string.IsNullOrWhiteSpace(name) || shares <= 0)
        {
            RejectTrade("INVALID_ORDER");
            return false;
        }

        if (!_stocks.TryGetValue(name, out Stock? stock))
        {
            RejectTrade("UNKNOWN_STOCK");
            return false;
        }

        if (!IsStockUnlocked(name))
        {
            RejectTrade("LOCKED_STOCK");
            return false;
        }

        double cost = stock.Price * shares;
        if (!double.IsFinite(cost) || cost <= 0.0)
        {
            RejectTrade("INVALID_ORDER");
            return false;
        }

        // The market may overdraw the available asset balance; this is how
        // negative assets are represented. Loans are managed separately.
        MarketCash -= cost;
        if (MarketCash < 0.0 && MarketCash > -0.000001)
        {
            MarketCash = 0.0;
        }

        if (_holdings.TryGetValue(name, out Holding? holding))
        {
            int oldShares = holding.Shares;
            holding.AvgCost = (holding.AvgCost * oldShares + stock.Price * shares) / (oldShares + shares);
            holding.Shares = oldShares + shares;
        }
        else
        {
            _holdings.Add(name, new Holding
            {
                Shares = shares,
                AvgCost = stock.Price,
            });
        }

        AddExperience(Math.Clamp(25 + (int)Math.Floor(cost / 100.0), 25, 100));
        return true;
    }

    /// <summary>
    /// Sells a whole number of shares. Holdings are removed at exactly zero.
    /// Invalid operations return false and leave all state untouched.
    /// </summary>
    public bool Sell(string name, int shares)
    {
        if (string.IsNullOrWhiteSpace(name) || shares <= 0)
        {
            RejectTrade("INVALID_ORDER");
            return false;
        }

        if (!_stocks.TryGetValue(name, out Stock? stock))
        {
            RejectTrade("UNKNOWN_STOCK");
            return false;
        }

        if (!IsStockUnlocked(name))
        {
            RejectTrade("LOCKED_STOCK");
            return false;
        }

        if (!_holdings.TryGetValue(name, out Holding? holding) || holding.Shares < shares)
        {
            RejectTrade("NOT_ENOUGH_SHARES");
            return false;
        }

        MarketCash += stock.Price * shares;
        holding.Shares -= shares;
        if (holding.Shares == 0)
        {
            _holdings.Remove(name);
        }

        AddExperience(Math.Clamp(25 + (int)Math.Floor(stock.Price * shares / 100.0), 25, 100));
        return true;
    }

    public bool SaveGame(string path = BlockTradeSaveManager.DefaultSavePath)
    {
        bool saved = BlockTradeSaveManager.Save(this, path, out string error);
        if (saved)
        {
            GD.Print($"[BlockTrade] Saved to {path}");
        }
        else
        {
            GD.PushError($"[BlockTrade] Save failed: {error}");
        }

        return saved;
    }

    /// <summary>
    /// Loads a save. Any missing file, parse failure, or validation failure
    /// resets to a clean new game and returns false; it never crashes.
    /// </summary>
    public bool LoadGame(string path = BlockTradeSaveManager.DefaultSavePath)
    {
        if (!BlockTradeSaveManager.TryLoad(path, out BlockTradeSaveData? data, out string error) ||
            data is null)
        {
            GD.PushWarning($"[BlockTrade] Load failed, starting a new game: {error}");
            InitMarket();
            return false;
        }

        try
        {
            ApplySaveData(data);
            GD.Print($"[BlockTrade] Loaded {path}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[BlockTrade] Apply failed, starting a new game: {exception.Message}");
            InitMarket();
            return false;
        }
    }

    /// <summary>Captures a plain snapshot for the save manager and tests.</summary>
    public BlockTradeSaveData CaptureSaveData()
    {
        var data = new BlockTradeSaveData
        {
            Version = BlockTradeSaveManager.CurrentVersion,
            PlayerCash = PlayerCash,
            MarketCash = MarketCash,
            LoanDebt = LoanDebt,
            TradingPasswordHash = TradingPasswordHash,
            TradingPasswordSalt = TradingPasswordSalt,
            GameTime = GameTime,
            Level = Level,
            Xp = Xp,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            RefreshRemainingSeconds = Math.Clamp(MarketRefreshRemaining, 0.0, RefreshIntervalSeconds),
        };

        foreach (string name in _allStockOrder)
        {
            Stock stock = _stocks[name];
            data.Stocks.Add(new BlockTradeStockSnapshot
            {
                Name = stock.Name,
                Price = stock.Price,
                BasePrice = stock.BasePrice,
                Volatility = stock.Volatility,
                Drift = stock.Drift,
            });
            data.Stocks[^1].History.AddRange(stock.History);
        }

        foreach ((string name, Holding holding) in _holdings)
        {
            data.Holdings.Add(new BlockTradeHoldingSnapshot
            {
                Name = name,
                Shares = holding.Shares,
                AvgCost = holding.AvgCost,
            });
        }

        return data;
    }

    public int GetStockCount() => _stocks.Count;

    public int GetHoldingCount() => _holdings.Count;

    private void ApplySaveData(BlockTradeSaveData data)
    {
        _stocks.Clear();
        _stockOrder.Clear();
        _companyOrder.Clear();
        _allStockOrder.Clear();
        _companyNames.Clear();
        _holdings.Clear();

        foreach (BlockTradeStockSnapshot snapshot in data.Stocks)
        {
            var stock = new Stock
            {
                Name = snapshot.Name,
                Price = snapshot.Price,
                BasePrice = snapshot.BasePrice,
                Volatility = snapshot.Volatility,
                Drift = snapshot.Drift,
                UnlockLevel = GetDefinitionUnlockLevel(snapshot.Name),
            };
            foreach (double point in snapshot.History)
            {
                stock.History.Add(point);
                if (stock.History.Count >= MaxPriceHistory)
                {
                    break;
                }
            }

            AddStock(stock, IsCompanyDefinition(snapshot.Name));
        }

        // Old v3 saves contain only the six item stocks. Add the company tier
        // when a real market was loaded, but keep an intentional zero-stock save empty.
        if (data.Stocks.Count > 0)
        {
            AddMissingItemStocks();
            AddMissingCompanyStocks();
        }

        foreach (BlockTradeHoldingSnapshot snapshot in data.Holdings)
        {
            if (snapshot.Shares <= 0)
            {
                continue;
            }

            _holdings[snapshot.Name] = new Holding
            {
                Shares = snapshot.Shares,
                AvgCost = snapshot.AvgCost,
            };
        }

        PlayerCash = data.PlayerCash;
        MarketCash = data.MarketCash;
        LoanDebt = data.LoanDebt;
        TradingPasswordHash = data.TradingPasswordHash;
        TradingPasswordSalt = data.TradingPasswordSalt;
        GameTime = data.GameTime;
        Level = Math.Clamp(data.Level, 1, MaxLevel);
        Xp = Math.Max(0, data.Xp);
        RefreshIntervalSeconds = Math.Clamp(data.RefreshIntervalSeconds, 5.0, 600.0);
        MarketRefreshRemaining = Math.Clamp(data.RefreshRemainingSeconds, 0.0, RefreshIntervalSeconds);

        MarketReset?.Invoke();
        StateChanged?.Invoke();
    }

    private double RandomNormal()
    {
        // Box-Muller. u1 is offset from zero to keep Log() finite.
        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static double RecentAverage(IReadOnlyList<double> history, int maxPoints)
    {
        if (history.Count == 0)
        {
            return 0.0;
        }

        int start = Math.Max(0, history.Count - maxPoints);
        double sum = 0.0;
        for (int i = start; i < history.Count; i++)
        {
            sum += history[i];
        }

        return sum / (history.Count - start);
    }

    private void RejectTrade(string reason)
    {
        TradeFailed?.Invoke(reason);
    }

    /// <summary>
    /// Internal quote data. The setters are internal so only the simulator and
    /// save layer can mutate live market state.
    /// </summary>
    public sealed class Stock
    {
        public string Name { get; internal set; } = string.Empty;
        public double Price { get; internal set; }
        public double BasePrice { get; internal set; }
        public double Volatility { get; internal set; }
        public double Drift { get; internal set; }
        public int UnlockLevel { get; internal set; } = 1;
        public List<double> History { get; } = new();
    }
}

/// <summary>One player position. Always a whole number of shares.</summary>
public sealed class Holding
{
    public int Shares { get; internal set; }
    public double AvgCost { get; internal set; }
}
