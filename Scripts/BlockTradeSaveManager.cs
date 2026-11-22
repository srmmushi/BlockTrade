using Godot;
using FileAccess = Godot.FileAccess;
using GodotDictionary = Godot.Collections.Dictionary;
using GodotArray = Godot.Collections.Array;

namespace BlockTrade;

/// <summary>
/// Versioned JSON persistence for BlockTrade.
///
/// This deliberately uses FileAccess + Godot's Json instead of Resource files:
/// player-writable save data should never be loaded through ResourceLoader,
/// because .tres/.res can reference scripts and external resources.
/// </summary>
public static class BlockTradeSaveManager
{
    public const int CurrentVersion = 5;
    public const string DefaultSavePath = "user://blocktrade_save.json";
    public const int MaxStocks = 100;
    public const int MaxHoldings = 100;

    public static string GetSavePath() => DefaultSavePath;

    public static bool SaveFileExists(string path = DefaultSavePath)
    {
        return FileAccess.FileExists(path);
    }

    /// <summary>
    /// Validates the complete snapshot, creates the user directory, then writes
    /// and flushes one UTF-8 JSON file. Read validation is the recovery path if
    /// the process is killed during a write.
    /// </summary>
    public static bool Save(
        BlockTradeSimulator simulator,
        string path,
        out string error)
    {
        error = string.Empty;
        try
        {
            BlockTradeSaveData data = simulator.CaptureSaveData();
            if (!Validate(data, out error))
            {
                return false;
            }

            GodotDictionary root = ToGodotDictionary(data);
            string json = Json.Stringify(root, "  ", true, true);

            if (!EnsureDirectory(path, out error))
            {
                return false;
            }

            using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file is null)
            {
                error = $"OPEN_FOR_WRITE_FAILED: {FileAccess.GetOpenError()}";
                return false;
            }

            file.StoreString(json);
            file.Flush();

            if (file.GetError() != Error.Ok)
            {
                error = $"WRITE_FAILED: {file.GetError()}";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"SAVE_EXCEPTION: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Reads, parses and validates a save file. Never throws to the caller.
    /// The caller is responsible for resetting to a new game on failure.
    /// </summary>
    public static bool TryLoad(
        string path,
        out BlockTradeSaveData? data,
        out string error)
    {
        data = null;
        error = string.Empty;

        try
        {
            if (!FileAccess.FileExists(path))
            {
                error = $"SAVE_NOT_FOUND: {path}";
                return false;
            }

            using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                error = $"OPEN_FOR_READ_FAILED: {FileAccess.GetOpenError()}";
                return false;
            }

            if (file.GetLength() == 0)
            {
                error = "EMPTY_SAVE";
                return false;
            }

            string text = file.GetAsText();
            var json = new Json();
            Error parseError = json.Parse(text);
            if (parseError != Error.Ok)
            {
                error = $"JSON_PARSE_FAILED line {json.GetErrorLine()}: {json.GetErrorMessage()}";
                return false;
            }

            Variant parsed = json.Data;
            if (parsed.VariantType != Variant.Type.Dictionary)
            {
                error = "ROOT_NOT_DICTIONARY";
                return false;
            }

            GodotDictionary root = parsed.AsGodotDictionary();
            if (!FromGodotDictionary(root, out BlockTradeSaveData? loaded, out error))
            {
                return false;
            }

            if (!Validate(loaded!, out error))
            {
                return false;
            }

            data = loaded;
            return true;
        }
        catch (Exception exception)
        {
            error = $"LOAD_EXCEPTION: {exception.Message}";
            data = null;
            return false;
        }
    }

    public static bool DeleteSave(string path = DefaultSavePath)
    {
        try
        {
            if (!FileAccess.FileExists(path))
            {
                return true;
            }

            string absolutePath = ProjectSettings.GlobalizePath(path);
            Error result = DirAccess.RemoveAbsolute(absolutePath);
            return result == Error.Ok || result == Error.FileNotFound;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Full structural validation. A save with an unsupported version, a bad
    /// stock count, negative cash/shares, a duplicate name, or non-finite
    /// numbers is rejected as a whole.
    /// </summary>
    public static bool Validate(BlockTradeSaveData data, out string error)
    {
        error = string.Empty;
        if (data is null)
        {
            error = "SAVE_DATA_NULL";
            return false;
        }

        if (data.Version != CurrentVersion)
        {
            error = $"VERSION_MISMATCH: got {data.Version}, expected {CurrentVersion}";
            return false;
        }

        if (data.Level < 1 || data.Level > BlockTradeSimulator.MaxLevel)
        {
            error = $"INVALID_LEVEL: {data.Level}";
            return false;
        }

        if (data.Xp < 0)
        {
            error = $"INVALID_XP: {data.Xp}";
            return false;
        }

        if (!double.IsFinite(data.RefreshIntervalSeconds) ||
            data.RefreshIntervalSeconds < 5.0 ||
            data.RefreshIntervalSeconds > 600.0)
        {
            error = "INVALID_REFRESH_INTERVAL";
            return false;
        }

        if (!double.IsFinite(data.RefreshRemainingSeconds) ||
            data.RefreshRemainingSeconds < 0.0 ||
            data.RefreshRemainingSeconds > data.RefreshIntervalSeconds + 0.001)
        {
            error = "INVALID_REFRESH_REMAINING";
            return false;
        }

        if (!double.IsFinite(data.PlayerCash) || data.PlayerCash < 0.0)
        {
            error = "INVALID_PLAYER_CASH";
            return false;
        }

        if (!double.IsFinite(data.MarketCash))
        {
            error = "INVALID_MARKET_CASH";
            return false;
        }

        if (!double.IsFinite(data.LoanDebt) || data.LoanDebt < 0.0 ||
            data.LoanDebt > BlockTradeSimulator.MaxLoan + 0.000001)
        {
            error = "INVALID_LOAN_DEBT";
            return false;
        }

        if (string.IsNullOrEmpty(data.TradingPasswordHash) !=
            string.IsNullOrEmpty(data.TradingPasswordSalt))
        {
            error = "INCOMPLETE_TRADING_PASSWORD";
            return false;
        }

        if (!string.IsNullOrEmpty(data.TradingPasswordHash))
        {
            try
            {
                byte[] hash = Convert.FromBase64String(data.TradingPasswordHash);
                byte[] salt = Convert.FromBase64String(data.TradingPasswordSalt);
                if (hash.Length < 16 || salt.Length < 8)
                {
                    error = "INVALID_TRADING_PASSWORD";
                    return false;
                }
            }
            catch (FormatException)
            {
                error = "INVALID_TRADING_PASSWORD";
                return false;
            }
        }

        if (!double.IsFinite(data.GameTime) || data.GameTime < 0.0)
        {
            error = "INVALID_GAME_TIME";
            return false;
        }

        if (data.Stocks.Count > MaxStocks)
        {
            error = $"TOO_MANY_STOCKS: {data.Stocks.Count}";
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (BlockTradeStockSnapshot stock in data.Stocks)
        {
            if (string.IsNullOrWhiteSpace(stock.Name) || stock.Name.Length > 32)
            {
                error = "INVALID_STOCK_NAME";
                return false;
            }

            if (!names.Add(stock.Name))
            {
                error = $"DUPLICATE_STOCK_NAME: {stock.Name}";
                return false;
            }

            if (!double.IsFinite(stock.Price) ||
                stock.Price < BlockTradeSimulator.MinPrice ||
                stock.Price > BlockTradeSimulator.MaxPrice)
            {
                error = $"INVALID_STOCK_PRICE: {stock.Name}";
                return false;
            }

            if (!double.IsFinite(stock.BasePrice) ||
                stock.BasePrice < BlockTradeSimulator.MinPrice ||
                stock.BasePrice > BlockTradeSimulator.MaxPrice)
            {
                error = $"INVALID_STOCK_BASE_PRICE: {stock.Name}";
                return false;
            }

            if (!double.IsFinite(stock.Volatility) || stock.Volatility < 0.0)
            {
                error = $"INVALID_STOCK_VOLATILITY: {stock.Name}";
                return false;
            }

            if (!double.IsFinite(stock.Drift))
            {
                error = $"INVALID_STOCK_DRIFT: {stock.Name}";
                return false;
            }

            if (stock.History.Count > BlockTradeSimulator.MaxPriceHistory)
            {
                error = $"TOO_LONG_HISTORY: {stock.Name}";
                return false;
            }

            foreach (double point in stock.History)
            {
                if (!double.IsFinite(point) ||
                    point < BlockTradeSimulator.MinPrice ||
                    point > BlockTradeSimulator.MaxPrice)
                {
                    error = $"INVALID_HISTORY_POINT: {stock.Name}";
                    return false;
                }
            }
        }

        if (data.Holdings.Count > MaxHoldings)
        {
            error = $"TOO_MANY_HOLDINGS: {data.Holdings.Count}";
            return false;
        }

        var holdingNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (BlockTradeHoldingSnapshot holding in data.Holdings)
        {
            if (string.IsNullOrWhiteSpace(holding.Name) ||
                !names.Contains(holding.Name))
            {
                error = $"HOLDING_UNKNOWN_STOCK: {holding.Name}";
                return false;
            }

            if (!holdingNames.Add(holding.Name))
            {
                error = $"DUPLICATE_HOLDING: {holding.Name}";
                return false;
            }

            if (holding.Shares < 0)
            {
                error = $"INVALID_HOLDING_SHARES: {holding.Name}";
                return false;
            }

            if (!double.IsFinite(holding.AvgCost) || holding.AvgCost < 0.0)
            {
                error = $"INVALID_HOLDING_AVG_COST: {holding.Name}";
                return false;
            }
        }

        return true;
    }

    private static bool EnsureDirectory(string path, out string error)
    {
        error = string.Empty;
        int slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return true;
        }

        string directory = path[..(slash + 1)];
        string absoluteDirectory = ProjectSettings.GlobalizePath(directory);
        Error result = DirAccess.MakeDirRecursiveAbsolute(absoluteDirectory);
        if (result != Error.Ok && result != Error.AlreadyExists)
        {
            error = $"CREATE_SAVE_DIRECTORY_FAILED: {result} ({absoluteDirectory})";
            return false;
        }

        return true;
    }

    private static GodotDictionary ToGodotDictionary(BlockTradeSaveData data)
    {
        var root = new GodotDictionary
        {
            ["version"] = data.Version,
            ["player_cash"] = data.PlayerCash,
            ["market_cash"] = data.MarketCash,
            ["loan_debt"] = data.LoanDebt,
            ["trading_password_hash"] = data.TradingPasswordHash,
            ["trading_password_salt"] = data.TradingPasswordSalt,
            ["game_time"] = data.GameTime,
            ["level"] = data.Level,
            ["xp"] = data.Xp,
            ["refresh_interval"] = data.RefreshIntervalSeconds,
            ["refresh_remaining"] = data.RefreshRemainingSeconds,
        };

        var stocks = new GodotArray();
        foreach (BlockTradeStockSnapshot stock in data.Stocks)
        {
            var row = new GodotDictionary
            {
                ["name"] = stock.Name,
                ["price"] = stock.Price,
                ["base_price"] = stock.BasePrice,
                ["volatility"] = stock.Volatility,
                ["drift"] = stock.Drift,
            };
            var history = new GodotArray();
            foreach (double point in stock.History)
            {
                history.Add(point);
            }
            row["history"] = history;
            stocks.Add(row);
        }

        var holdings = new GodotArray();
        foreach (BlockTradeHoldingSnapshot holding in data.Holdings)
        {
            var row = new GodotDictionary
            {
                ["name"] = holding.Name,
                ["shares"] = holding.Shares,
                ["avg_cost"] = holding.AvgCost,
            };
            holdings.Add(row);
        }

        root["stocks"] = stocks;
        root["holdings"] = holdings;
        return root;
    }

    private static bool FromGodotDictionary(
        GodotDictionary root,
        out BlockTradeSaveData? data,
        out string error)
    {
        data = null;
        error = string.Empty;

        if (!TryGetInt(root, "version", out int version))
        {
            error = "MISSING_OR_INVALID_VERSION";
            return false;
        }

        if (version is not (1 or 2 or 3 or 4 or 5))
        {
            error = $"UNSUPPORTED_VERSION: {version}";
            return false;
        }

        bool legacy = version == 1;
        int level = 1;
        int xp = 0;
        double refreshInterval = 120.0;
        double refreshRemaining = 120.0;

        if (!legacy)
        {
            if (!TryGetInt(root, "level", out level) ||
                !TryGetInt(root, "xp", out xp) ||
                !TryGetDouble(root, "refresh_interval", out refreshInterval) ||
                !TryGetDouble(root, "refresh_remaining", out refreshRemaining))
            {
                error = "MALFORMED_V2_PROGRESS_FIELDS";
                return false;
            }
        }

        if (!TryGetDouble(root, "player_cash", out double cash))
        {
            error = "MISSING_OR_INVALID_PLAYER_CASH";
            return false;
        }

        double playerCash = cash;
        double marketCash = 0.0;
        if (version >= 4)
        {
            if (!TryGetDouble(root, "market_cash", out marketCash))
            {
                error = "MISSING_OR_INVALID_MARKET_CASH";
                return false;
            }
        }
        else
        {
            // Before v4 all cash was usable for stock trading. Preserve it as
            // market assets; the player can withdraw it to external cash later.
            marketCash = cash;
            playerCash = 0.0;
        }

        double loanDebt = 0.0;
        if (version >= 5 && !TryGetDouble(root, "loan_debt", out loanDebt))
        {
            error = "MISSING_OR_INVALID_LOAN_DEBT";
            return false;
        }

        string passwordHash = root.TryGetValue("trading_password_hash", out Variant hashVariant)
            ? hashVariant.AsString()
            : string.Empty;
        string passwordSalt = root.TryGetValue("trading_password_salt", out Variant saltVariant)
            ? saltVariant.AsString()
            : string.Empty;

        if (!TryGetDouble(root, "game_time", out double gameTime))
        {
            error = "MISSING_OR_INVALID_GAME_TIME";
            return false;
        }

        if (!root.TryGetValue("stocks", out Variant stocksVariant) ||
            stocksVariant.VariantType != Variant.Type.Array)
        {
            error = "MISSING_OR_INVALID_STOCKS";
            return false;
        }

        if (!root.TryGetValue("holdings", out Variant holdingsVariant) ||
            holdingsVariant.VariantType != Variant.Type.Array)
        {
            error = "MISSING_OR_INVALID_HOLDINGS";
            return false;
        }

        var loaded = new BlockTradeSaveData
        {
            Version = CurrentVersion,
            PlayerCash = playerCash,
            MarketCash = marketCash,
            LoanDebt = loanDebt,
            TradingPasswordHash = passwordHash,
            TradingPasswordSalt = passwordSalt,
            GameTime = gameTime,
            Level = level,
            Xp = xp,
            RefreshIntervalSeconds = refreshInterval,
            RefreshRemainingSeconds = refreshRemaining,
        };

        GodotArray stocks = stocksVariant.AsGodotArray();
        foreach (Variant element in stocks)
        {
            if (element.VariantType != Variant.Type.Dictionary)
            {
                error = "STOCK_ROW_NOT_DICTIONARY";
                return false;
            }

            GodotDictionary row = element.AsGodotDictionary();
            if (!TryGetString(row, "name", out string name) ||
                !TryGetDouble(row, "price", out double price) ||
                !TryGetDouble(row, "base_price", out double basePrice) ||
                !TryGetDouble(row, "volatility", out double volatility) ||
                !TryGetDouble(row, "drift", out double drift))
            {
                error = "MALFORMED_STOCK_ROW";
                return false;
            }

            var snapshot = new BlockTradeStockSnapshot
            {
                Name = MigrateCompanyName(legacy ? MigrateLegacyName(name) : name),
                Price = price,
                BasePrice = basePrice,
                Volatility = volatility,
                Drift = drift,
            };

            if (row.TryGetValue("history", out Variant historyVariant) &&
                historyVariant.VariantType == Variant.Type.Array)
            {
                GodotArray historyArray = historyVariant.AsGodotArray();
                foreach (Variant pointVariant in historyArray)
                {
                    if (pointVariant.VariantType is Variant.Type.Float or Variant.Type.Int)
                    {
                        double point = pointVariant.AsDouble();
                        if (double.IsFinite(point))
                        {
                            snapshot.History.Add(point);
                        }
                    }

                    if (snapshot.History.Count >= BlockTradeSimulator.MaxPriceHistory)
                    {
                        break;
                    }
                }
            }

            if (snapshot.History.Count == 0)
            {
                snapshot.History.Add(price);
            }

            loaded.Stocks.Add(snapshot);
        }

        GodotArray holdings = holdingsVariant.AsGodotArray();
        foreach (Variant element in holdings)
        {
            if (element.VariantType != Variant.Type.Dictionary)
            {
                error = "HOLDING_ROW_NOT_DICTIONARY";
                return false;
            }

            GodotDictionary row = element.AsGodotDictionary();
            if (!TryGetString(row, "name", out string name) ||
                !TryGetInt(row, "shares", out int shares) ||
                !TryGetDouble(row, "avg_cost", out double avgCost))
            {
                error = "MALFORMED_HOLDING_ROW";
                return false;
            }

            loaded.Holdings.Add(new BlockTradeHoldingSnapshot
            {
                Name = MigrateCompanyName(legacy ? MigrateLegacyName(name) : name),
                Shares = shares,
                AvgCost = avgCost,
            });
        }

        data = loaded;
        return true;
    }

    private static string MigrateCompanyName(string name) => name switch
    {
        "煤炭重工" => "深岩控股",
        "红石能源" => "潮汐能源",
        "铁脉集团" => "苍穹科技",
        "金辉银行" => "星链物流",
        "绿宝石控股" => "极光资本",
        "钻石科技" => "末地银行",
        _ => name,
    };

    private static string MigrateLegacyName(string name) => name switch
    {
        "IRON" => "铁锭",
        "GOLD" => "金锭",
        "DIAMOND" => "钻石",
        "REDSTONE" => "红石",
        "EMERALD" => "绿宝石",
        "COAL" => "煤炭",
        _ => name,
    };

    private static bool TryGetString(GodotDictionary dictionary, string key, out string value)
    {
        value = string.Empty;
        if (!dictionary.TryGetValue(key, out Variant variant) ||
            variant.VariantType != Variant.Type.String)
        {
            return false;
        }

        value = variant.AsString();
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetDouble(GodotDictionary dictionary, string key, out double value)
    {
        value = 0.0;
        if (!dictionary.TryGetValue(key, out Variant variant))
        {
            return false;
        }

        if (variant.VariantType is not (Variant.Type.Float or Variant.Type.Int))
        {
            return false;
        }

        value = variant.AsDouble();
        return double.IsFinite(value);
    }

    private static bool TryGetInt(GodotDictionary dictionary, string key, out int value)
    {
        value = 0;
        if (!dictionary.TryGetValue(key, out Variant variant))
        {
            return false;
        }

        if (variant.VariantType is not (Variant.Type.Float or Variant.Type.Int))
        {
            return false;
        }

        double number = variant.AsDouble();
        if (!double.IsFinite(number) ||
            Math.Abs(number - Math.Round(number)) > 0.000001 ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            return false;
        }

        value = (int)Math.Round(number);
        return true;
    }
}
