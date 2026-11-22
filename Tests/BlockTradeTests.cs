using Godot;
using FileAccess = Godot.FileAccess;
using GodotDictionary = Godot.Collections.Dictionary;

namespace BlockTrade;

/// <summary>
/// Headless integration suite for the market, trading, stage progression,
/// paid activities, save format 2 and legacy migration.
///
/// Run with:
/// godot --headless --path . res://Tests/BlockTradeTests.tscn
///
/// A non-zero process exit means at least one check failed.
/// </summary>
public partial class BlockTradeTests : Node
{
    private const string TestSavePath = "user://blocktrade_test_save.json";
    private const string RestartSavePath = "user://blocktrade_restart_test.json";
    private const int DefaultSeed = 1337;

    private int _passed;
    private int _failed;

    public override void _Ready()
    {
        string[] userArguments = OS.GetCmdlineUserArgs();

        if (Array.IndexOf(userArguments, "--persist-write") >= 0)
        {
            RunPersistWrite();
            return;
        }

        if (Array.IndexOf(userArguments, "--persist-read") >= 0)
        {
            RunPersistRead();
            return;
        }

        try
        {
            GD.Print("==============================================");
            GD.Print(" BlockTrade automated test suite");
            GD.Print("==============================================");

            TestNewGame();
            TestMarketInitialization();
            TestBuyWeightedAverage();
            TestSellAndRemoveHolding();
            TestRejectedOrdersLeaveStateUnchanged();
            TestStageProgressionAndPaidCash();
            TestLoanAndNegativeAsset();
            TestCompanyMarket();
            TestTickBounds();
            TestDownTurns();
            TestSaveLoadRoundTrip();
            TestLegacySaveMigration();
            TestV3CashMigration();
            TestCorruptSaveFallsBack();
            TestWrongVersionFallsBack();
            TestUnknownHoldingFallsBack();
            TestZeroStockSaveBoundary();
            TestCrossProcessWriteThenRead();
        }
        catch (Exception exception)
        {
            _failed++;
            GD.PushError($"[CORE EXCEPTION] {exception}");
        }

        GD.Print("----------------------------------------------");
        GD.Print($" RESULT: {_passed} passed, {_failed} failed");
        GD.Print("----------------------------------------------");

        if (_failed > 0)
        {
            GD.PushError("BLOCKTRADE TESTS FAILED");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ALL TESTS PASSED");
        GetTree().Quit(0);
    }

    private void TestNewGame()
    {
        Section("new game");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);

        Check(Near(sim.GetCash(), 0.0), "external cash starts at zero");
        Check(Near(sim.GetMarketCash(), 2_000.0), "market assets start at 2000");
        Check(Near(sim.GetAsset(), 2_000.0), "asset equals market cash plus holdings");
        Check(!sim.HasTradingPassword, "new game has no trading password yet");
        Check(sim.SetTradingPassword("123456"), "six-digit trading password can be set");
        Check(sim.VerifyTradingPassword("123456"), "trading password verifies");
        Check(!sim.VerifyTradingPassword("654321"), "wrong trading password is rejected");
        Check(!sim.SetTradingPassword("abc"), "invalid trading password is rejected");
        Check(sim.WithdrawToCash(200.0), "asset can transfer out to cash");
        Check(Near(sim.GetCash(), 200.0) && Near(sim.GetMarketCash(), 1_800.0),
            "withdrawal moves market assets into cash");
        Check(sim.DepositToMarket(150.0), "cash can transfer back into market assets");
        Check(Near(sim.GetCash(), 50.0) && Near(sim.GetMarketCash(), 1_950.0),
            "deposit moves cash into market assets");
        Check(Near(sim.GetStockAsset(), 0.0), "stock asset starts at zero");
        Check(sim.GetHoldingCount() == 0, "holdings start empty");
        Check(sim.GetGameTime() == 0.0, "game time starts at zero");
        Check(sim.GetStockNames().Length == 10, "ten item definitions exist");
        Check(sim.GetUnlockedStockNames().Length == 6, "six items are unlocked at stage 1");
        Check(sim.GetCompanyNames().Length == 8, "eight companies exist");
        Check(Near(sim.RefreshIntervalSeconds, 120.0), "default refresh interval is two minutes");
        Check(Near(sim.MarketRefreshRemaining, 120.0), "default remaining refresh is two minutes");
        Check(Near(sim.GetPriceCeiling(), 500.0), "initial price ceiling is 500");
        Check(sim.Level == 1, "stage starts at 1");
        Check(sim.Xp == 0, "xp starts at zero");
        Check(BlockTradeSimulator.GetRequiredLevel("scratch") == 2, "scratch card unlocks at stage 2");
        Check(BlockTradeSimulator.GetRequiredLevel("slots") == 3, "slot machine unlocks at stage 3");
        Check(BlockTradeSimulator.GetRequiredLevel("wheel") == 4, "roulette unlocks at stage 4");
    }

    private void TestMarketInitialization()
    {
        Section("market initialization");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);
        string[] expected = { "铁锭", "金锭", "钻石", "红石", "绿宝石", "煤炭" };
        string[] actual = sim.GetUnlockedStockNames();

        Check(actual.SequenceEqual(expected), "stock names are Chinese");
        foreach (string name in actual)
        {
            double price = sim.GetPrice(name);
            Check(price is >= 10.0 and <= 500.0, $"{name} initial price is 10..500 ({price:0.00})");
            Check(Near(price, sim.GetBasePrice(name)), $"{name} BasePrice equals Price");
        }
    }

    private void TestBuyWeightedAverage()
    {
        Section("buying and weighted average cost");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);
        double firstPrice = sim.GetPrice("煤炭");
        double startingMarketCash = sim.GetMarketCash();
        double startingCash = sim.GetCash();

        bool firstBuy = sim.Buy("煤炭", 5);
        Check(firstBuy, "buy 煤炭 x5 succeeds");
        Check(Near(sim.GetMarketCash(), startingMarketCash - firstPrice * 5.0),
            "market assets are reduced by exact cost");
        Check(Near(sim.GetCash(), startingCash), "external cash is not used for stock purchases");
        Check(sim.TryGetHolding("煤炭", out int shares, out double avg), "holding exists");
        Check(shares == 5, "holding has 5 shares");
        Check(Near(avg, firstPrice), "first average cost equals fill price");
        Check(sim.Xp == 25, "first trade grants stage XP");

        sim.Tick(0.5);
        double secondPrice = sim.GetPrice("煤炭");
        bool secondBuy = sim.Buy("煤炭", 2);
        double expectedAvg = (firstPrice * 5.0 + secondPrice * 2.0) / 7.0;

        Check(secondBuy, "second buy 煤炭 x2 succeeds");
        Check(sim.TryGetHolding("煤炭", out shares, out avg), "holding still exists");
        Check(shares == 7, "holding has 7 shares");
        Check(Near(avg, expectedAvg), $"weighted average is {expectedAvg:0.0000}");
        Check(sim.Xp == 50, "second trade adds XP");

        GodotDictionary holdings = sim.GetHoldings();
        Check(holdings.ContainsKey("煤炭"), "GetHoldings contains 煤炭");
        if (holdings.ContainsKey("煤炭"))
        {
            GodotDictionary coal = holdings["煤炭"].AsGodotDictionary();
            Check(coal["shares"].AsInt32() == 7, "GetHoldings reports 7 shares");
        }

        Check(Near(sim.GetTotalAsset(), sim.GetCash() + sim.GetMarketCash() + sim.GetPrice("煤炭") * 7.0),
            "total asset equals cash plus market cash and market value");
        Check(Near(sim.GetStockAsset(), sim.GetPrice("煤炭") * 7.0),
            "stock asset equals the holding's current market value");
    }

    private void TestSellAndRemoveHolding()
    {
        Section("selling");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);
        double price = sim.GetPrice("煤炭");
        sim.Buy("煤炭", 5);

        double beforeSellMarketCash = sim.GetMarketCash();
        bool sell = sim.Sell("煤炭", 2);
        Check(sell, "sell 煤炭 x2 succeeds");
        Check(Near(sim.GetMarketCash(), beforeSellMarketCash + price * 2.0),
            "market assets increase by sale proceeds");
        Check(sim.TryGetHolding("煤炭", out int shares, out _) && shares == 3, "3 shares remain");

        bool sellRest = sim.Sell("煤炭", 3);
        Check(sellRest, "sell remaining 3 succeeds");
        Check(!sim.TryGetHolding("煤炭", out _, out _), "holding is removed at zero shares");
        Check(sim.GetHoldingCount() == 0, "holdings dictionary is empty");
        Check(Near(sim.GetMarketCash(), 2_000.0), "round trip restores starting market assets");
        Check(sim.Xp == 75, "all three trades granted XP");
    }

    private void TestRejectedOrdersLeaveStateUnchanged()
    {
        Section("rejected orders");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);
        sim.Buy("煤炭", 5);
        double marketCashBefore = sim.GetMarketCash();
        int sharesBefore = sim.TryGetHolding("煤炭", out int s, out _) ? s : 0;
        int eventCount = 0;
        sim.TradeFailed += _ => eventCount++;

        Check(!sim.Buy("青金石", 1), "stage-locked item returns false");
        Check(Near(sim.GetMarketCash(), marketCashBefore), "locked item does not change market assets");
        Check(!sim.TryGetHolding("青金石", out _, out _), "locked item does not change holdings");

        Check(!sim.Sell("煤炭", sharesBefore + 1), "oversell returns false");
        Check(Near(sim.GetMarketCash(), marketCashBefore), "oversell does not change market assets");
        Check(sim.TryGetHolding("煤炭", out int afterShares, out _) && afterShares == sharesBefore,
            "oversell does not change holdings");

        Check(!sim.Buy("煤炭", 0), "zero-share buy returns false");
        Check(!sim.Buy("煤炭", -5), "negative-share buy returns false");
        Check(!sim.Sell("煤炭", 0), "zero-share sell returns false");
        Check(!sim.Sell("煤炭", -5), "negative-share sell returns false");
        Check(!sim.Buy("IRON", 1), "legacy English stock is not accepted");
        Check(!sim.Sell("IRON", 1), "legacy English sell is not accepted");
        Check(eventCount == 8, "every rejected order emits one failure event");
        Check(Near(sim.GetMarketCash(), marketCashBefore), "all invalid orders leave market assets unchanged");
    }

    private void TestStageProgressionAndPaidCash()
    {
        Section("stage progression and paid activities");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);
        var unlocks = new List<string>();
        sim.LevelUp += (level, unlock) => unlocks.Add($"{level}:{unlock}");

        sim.AddExperience(79);
        Check(sim.Level == 1, "79 XP is not enough for stage 2");

        sim.AddExperience(1);
        Check(sim.Level == 2, "80 XP reaches stage 2");
        Check(unlocks.Count == 1 && unlocks[0] == "2:刮刮乐 + 青金石 / 石英", "stage 2 announces 刮刮乐 and items");
        Check(sim.GetUnlockedStockNames().Length == 8, "stage 2 unlocks two more items");

        sim.AddExperience(sim.XpToNextLevel);
        Check(sim.Level == 3, "stage 3 reached");
        Check(unlocks.Count == 2 && unlocks[1] == "3:老虎机 + 黑曜石 / 下界合金", "stage 3 announces 老虎机 and items");
        Check(sim.GetUnlockedStockNames().Length == 10, "stage 3 unlocks the final two items");

        sim.AddExperience(sim.XpToNextLevel);
        Check(sim.Level == 4, "stage 4 reached");
        Check(sim.IsMaxLevel, "stage 4 is max level");
        Check(unlocks.Count == 3 && unlocks[2] == "4:公司股市 / 大转盘", "stage 4 announces 公司股市 and 大转盘");
        Check(Near(sim.LevelProgress, 1.0), "max level XP bar is full");

        sim.AddCash(100);
        double cash = sim.GetCash();
        Check(sim.TrySpendCash(30), "paid activity charges 30");
        Check(Near(sim.GetCash(), cash - 30), "cash is reduced by activity cost");
        Check(!sim.TrySpendCash(1_000_000), "activity cannot overdraw cash");
        sim.AddCash(50);
        Check(Near(sim.GetCash(), cash - 30 + 50), "activity payout is credited");
    }

    private void TestLoanAndNegativeAsset()
    {
        Section("loan and negative asset");
        BlockTradeSimulator sim = NewSimulator(55);
        Check(sim.Borrow(BlockTradeSimulator.MaxLoan), "borrow up to 50 million");
        Check(Near(sim.GetCash(), BlockTradeSimulator.MaxLoan), "loan proceeds enter cash");
        Check(Near(sim.LoanDebt, BlockTradeSimulator.MaxLoan), "loan debt records principal");
        Check(!sim.Borrow(1.0), "loan cannot exceed the 50 million cap");
        Check(sim.RepayLoan(1_000_000.0), "loan repayment succeeds");
        Check(Near(sim.LoanDebt, BlockTradeSimulator.MaxLoan - 1_000_000.0), "repayment reduces debt");

        BlockTradeSimulator overdraw = NewSimulator(56);
        double marketBefore = overdraw.GetMarketCash();
        Check(overdraw.Buy("煤炭", 5_000), "buying with insufficient assets is allowed");
        Check(overdraw.GetMarketCash() < marketBefore, "market asset is deducted");
        Check(overdraw.GetMarketCash() < 0.0, "market asset can become negative");
        Check(overdraw.TryGetHolding("煤炭", out int shares, out _) && shares == 5_000,
            "negative-asset purchase still records shares");
    }

    private void TestCompanyMarket()
    {
        Section("company market");
        BlockTradeSimulator sim = NewSimulator(77);
        string[] companies = sim.GetCompanyNames();
        Check(companies.Length == 8, "eight company stocks exist");
        Check(sim.GetAllStockNames().Length == 18, "item and company markets are both present");
        Check(companies.All(name => sim.GetPrice(name) > 1_000.0), "company shares have large prices");
        Check(Near(sim.GetPriceCeiling("深岩控股"), 100_000.0), "company price ceiling starts at 100000");
        Check(Near(sim.GetPriceCeiling("煤炭"), 500.0), "item price ceiling is unchanged");

        sim.AddExperience(450);
        Check(sim.IsMaxLevel, "company market requires the max stage");
        sim.AddMarketCash(200_000.0);
        double price = sim.GetPrice("深岩控股");
        double cashBefore = sim.GetMarketCash();
        double assetBefore = sim.GetStockAsset();
        Check(sim.Buy("深岩控股", 1), "buy one company share succeeds");
        Check(Near(sim.GetMarketCash(), cashBefore - price), "company trade deducts a large exact amount");
        Check(Near(sim.GetStockAsset(), assetBefore + price), "company holding is included in stock asset");
        Check(sim.TryGetHolding("深岩控股", out int shares, out double avg) && shares == 1 && Near(avg, price),
            "company holding is recorded");
        sim.Tick(120.0);
        Check(sim.GetPriceHistory("深岩控股").Count == 2, "company chart history advances");
    }

    private void TestTickBounds()
    {
        Section("market ticks and bounds");
        BlockTradeSimulator sim = NewSimulator(DefaultSeed);
        int tickEvents = 0;
        sim.MarketTicked += () => tickEvents++;

        double initialIron = sim.GetPrice("铁锭");
        for (int i = 0; i < 200; i++)
        {
            sim.Tick(0.5);
        }

        Check(tickEvents == 200, "200 ticks emit 200 market events");
        Check(Near(sim.GetGameTime(), 100.0), "game time advances by 100 seconds");
        Check(!Near(sim.GetPrice("铁锭"), initialIron), "prices actually move");

        bool allInBounds = true;
        double ceiling = sim.GetPriceCeiling();
        foreach (string name in sim.GetStockNames())
        {
            double price = sim.GetPrice(name);
            if (!double.IsFinite(price) ||
                price < BlockTradeSimulator.MinPrice ||
                price > ceiling + 0.000001)
            {
                allInBounds = false;
            }
        }
        Check(allInBounds, "all prices stay inside 1.0..dynamic ceiling after 200 ticks");
        Check(sim.GetAllStockNames().All(name =>
                double.IsFinite(sim.GetPrice(name)) &&
                sim.GetPrice(name) >= BlockTradeSimulator.MinPrice &&
                sim.GetPrice(name) <= sim.GetPriceCeiling(name) + 0.000001),
            "company prices also stay inside their own ceiling");

        BlockTradeSimulator stress = NewSimulator(5);
        for (int i = 0; i < 2000; i++)
        {
            stress.Tick(0.5);
        }
        Check(stress.GetStockNames().Select(stress.GetPrice)
                .All(price => double.IsFinite(price) &&
                              price >= BlockTradeSimulator.MinPrice &&
                              price <= stress.GetPriceCeiling() + 0.000001),
            "all prices stay in bounds after a 2000-tick stress run");

        BlockTradeSimulator twoMinute = NewSimulator(71);
        twoMinute.Tick(120.0);
        Check(Near(twoMinute.GetGameTime(), 120.0), "a two-minute refresh advances game time by 120 seconds");
        Check(twoMinute.GetPriceCeiling() > 500.0, "price ceiling grows after a refresh");
        Check(twoMinute.GetStockNames().Select(twoMinute.GetPrice)
                .All(price => double.IsFinite(price) &&
                              price >= BlockTradeSimulator.MinPrice &&
                              price <= twoMinute.GetPriceCeiling() + 0.000001),
            "a two-minute refresh stays under the grown ceiling");

        BlockTradeSimulator growth = NewSimulator(88);
        double initialCeiling = growth.GetPriceCeiling();
        for (int i = 0; i < 120; i++)
        {
            growth.Tick(120.0);
        }
        Check(growth.GetPriceCeiling() > initialCeiling, "long session raises the market ceiling");
        Check(growth.GetStockNames().Select(growth.GetPrice)
                .All(price => price <= growth.GetPriceCeiling() + 0.000001),
            "long session prices respect the dynamic ceiling");
    }

    private void TestDownTurns()
    {
        Section("downturns");
        BlockTradeSimulator sim = NewSimulator(2025);
        string[] names = sim.GetAllStockNames();
        var lastPrice = names.ToDictionary(name => name, sim.GetPrice);
        var downMoves = names.ToDictionary(name => name, _ => 0);

        for (int i = 0; i < 240; i++)
        {
            sim.Tick(120.0);
            foreach (string name in names)
            {
                double price = sim.GetPrice(name);
                if (price < lastPrice[name])
                {
                    downMoves[name]++;
                }

                lastPrice[name] = price;
            }
        }

        Check(downMoves.Values.All(count => count > 0),
            "every stock has down refreshes instead of only rising");
        Check(downMoves.Values.Min() >= 10,
            "each stock has repeated down refreshes over a long session");
    }

    private void TestSaveLoadRoundTrip()
    {
        Section("save/load round trip");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
        BlockTradeSimulator original = NewSimulator(42);
        original.Buy("煤炭", 6);
        original.Tick(0.5);
        original.Buy("红石", 2);
        original.AddExperience(100);
        original.SetTradingPassword("123456");
        original.Borrow(500.0);
        original.WithdrawToCash(100.0);
        original.MarketRefreshRemaining = 37.5;

        double expectedCash = original.GetCash();
        double expectedMarketCash = original.GetMarketCash();
        double expectedLoanDebt = original.LoanDebt;
        double expectedTime = original.GetGameTime();
        double expectedCoal = original.GetPrice("煤炭");
        double expectedRedstone = original.GetPrice("红石");
        int expectedLevel = original.Level;
        int expectedXp = original.Xp;
        original.TryGetHolding("煤炭", out int expectedCoalShares, out double expectedCoalAvg);

        bool saved = original.SaveGame(TestSavePath);
        Check(saved, "SaveGame writes the test save");
        Check(FileAccess.FileExists(TestSavePath), "save file exists at user://");

        string json = ReadAllText(TestSavePath);
        Check(json.Contains("\"version\": 5"), "save JSON contains version 5");
        Check(json.Contains("\"market_cash\""), "save JSON contains market cash");
        Check(json.Contains("\"loan_debt\""), "save JSON contains loan debt");
        Check(json.Contains("\"trading_password_hash\""), "save JSON contains password hash");
        Check(json.Contains("\"history\""), "save JSON contains history");
        Check(json.Contains("\"level\""), "save JSON contains level");
        Check(json.Contains("\"xp\""), "save JSON contains xp");
        Check(json.Contains("\"refresh_interval\""), "save JSON contains refresh_interval");
        Check(json.Contains("\"refresh_remaining\""), "save JSON contains refresh_remaining");
        Check(json.Contains("煤炭"), "save JSON uses Chinese stock names");
        Check(json.Contains("\"stocks\""), "save JSON contains stocks");
        Check(json.Contains("\"holdings\""), "save JSON contains holdings");

        BlockTradeSimulator restored = NewSimulator(99);
        bool loaded = restored.LoadGame(TestSavePath);
        Check(loaded, "LoadGame accepts a valid v5 save");
        Check(Near(restored.GetCash(), expectedCash), "cash restored exactly");
        Check(Near(restored.GetMarketCash(), expectedMarketCash), "market assets restored exactly");
        Check(Near(restored.LoanDebt, expectedLoanDebt), "loan debt restored exactly");
        Check(restored.VerifyTradingPassword("123456"), "trading password restored");
        Check(Near(restored.GetAsset(), expectedMarketCash + expectedCoal * 6 + expectedRedstone * 2),
            "asset restored from market cash and holdings");
        Check(Near(restored.GetGameTime(), expectedTime), "game time restored exactly");
        Check(Near(restored.GetPrice("煤炭"), expectedCoal), "煤炭 price restored exactly");
        Check(Near(restored.GetPrice("红石"), expectedRedstone), "红石 price restored exactly");
        Check(restored.TryGetHolding("煤炭", out int coalShares, out double coalAvg), "煤炭 holding restored");
        Check(coalShares == expectedCoalShares, "煤炭 share count restored");
        Check(Near(coalAvg, expectedCoalAvg), "煤炭 average cost restored");
        Check(restored.Level == expectedLevel, "stage restored");
        Check(restored.Xp == expectedXp, "xp restored");
        Check(Near(restored.RefreshIntervalSeconds, 120.0), "refresh interval restored");
        Check(Near(restored.MarketRefreshRemaining, 37.5), "remaining refresh countdown restored");
        IReadOnlyList<double> restoredHistory = restored.GetPriceHistory("煤炭");
        IReadOnlyList<double> originalHistory = original.GetPriceHistory("煤炭");
        Check(restoredHistory.Count == originalHistory.Count, "price history length restored");
        Check(restoredHistory.Count >= 2, "save contains more than the initial chart point");
        Check(Near(restoredHistory[^1], expectedCoal), "last history point is the saved price");
        Check(restored.GetStockNames().SequenceEqual(original.GetStockNames()), "stock list restored in order");
        Check(restored.GetCompanyNames().SequenceEqual(original.GetCompanyNames()), "company list restored in order");
        Check(restored.GetPriceHistory("深岩控股").Count == original.GetPriceHistory("深岩控股").Count,
            "company price history restored");

        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestLegacySaveMigration()
    {
        Section("version 1 migration");
        BlockTradeSimulator source = NewSimulator(12);
        source.Buy("煤炭", 4);
        source.SaveGame(TestSavePath);

        GodotDictionary root = ReadSaveDictionary(TestSavePath);
        root["version"] = 1;
        root.Remove("level");
        root.Remove("xp");
        root.Remove("refresh_interval");
        root.Remove("refresh_remaining");

        Godot.Collections.Array stocks = root["stocks"].AsGodotArray();
        for (int i = 0; i < stocks.Count; i++)
        {
            GodotDictionary row = stocks[i].AsGodotDictionary();
            row["name"] = LegacyName(row["name"].AsString());
            row.Remove("history");
        }
        root["stocks"] = stocks;

        Godot.Collections.Array holdings = root["holdings"].AsGodotArray();
        for (int i = 0; i < holdings.Count; i++)
        {
            GodotDictionary row = holdings[i].AsGodotDictionary();
            row["name"] = LegacyName(row["name"].AsString());
        }
        root["holdings"] = holdings;
        WriteSaveDictionary(TestSavePath, root);

        BlockTradeSimulator restored = NewSimulator(13);
        bool loaded = restored.LoadGame(TestSavePath);
        Check(loaded, "a version 1 save still loads");
        Check(restored.GetUnlockedStockNames().SequenceEqual(new[] { "铁锭", "金锭", "钻石", "红石", "绿宝石", "煤炭" }),
            "legacy English names migrate to Chinese");
        Check(restored.TryGetHolding("煤炭", out int shares, out _) && shares == 4,
            "legacy holding migrates to Chinese");
        Check(restored.Level == 1, "legacy save defaults to stage 1");
        Check(Near(restored.RefreshIntervalSeconds, 120.0), "legacy save defaults to 2-minute refresh");
        Check(restored.GetPriceHistory("煤炭").Count == 1,
            "legacy save without history starts from one price point");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestV3CashMigration()
    {
        Section("version 3 cash migration");
        BlockTradeSimulator source = NewSimulator(21);
        source.SaveGame(TestSavePath);

        GodotDictionary root = ReadSaveDictionary(TestSavePath);
        root["version"] = 3;
        root.Remove("market_cash");
        root.Remove("trading_password_hash");
        root.Remove("trading_password_salt");
        root["player_cash"] = 1234.5;
        WriteSaveDictionary(TestSavePath, root);

        BlockTradeSimulator restored = NewSimulator(22);
        bool loaded = restored.LoadGame(TestSavePath);
        Check(loaded, "a version 3 save still loads");
        Check(Near(restored.GetCash(), 0.0), "v3 cash migrates out of the external wallet");
        Check(Near(restored.GetMarketCash(), 1234.5), "v3 cash becomes market assets");
        Check(!restored.HasTradingPassword, "v3 save has no trading password yet");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestCorruptSaveFallsBack()
    {
        Section("corrupt save fallback");
        WriteAllText(TestSavePath, "{ this is not valid json !!!");
        BlockTradeSimulator sim = NewSimulator(77);
        sim.Buy("煤炭", 1);

        bool loaded = sim.LoadGame(TestSavePath);
        Check(!loaded, "corrupt JSON returns false");
        Check(Near(sim.GetCash(), 0.0), "corrupt save resets external cash to zero");
        Check(Near(sim.GetMarketCash(), 2_000.0), "corrupt save resets market assets to 2000");
        Check(sim.GetHoldingCount() == 0, "corrupt save resets holdings");
        Check(sim.GetUnlockedStockNames().Length == 6, "corrupt save rebuilds six initial item stocks");
        Check(sim.GetCompanyNames().Length == 8, "corrupt save rebuilds eight companies");
        Check(sim.GetGameTime() == 0.0, "corrupt save resets game time");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestWrongVersionFallsBack()
    {
        Section("wrong save version fallback");
        BlockTradeSimulator source = NewSimulator(21);
        source.Buy("煤炭", 2);
        source.SaveGame(TestSavePath);

        GodotDictionary root = ReadSaveDictionary(TestSavePath);
        root["version"] = 999;
        WriteSaveDictionary(TestSavePath, root);

        BlockTradeSimulator sim = NewSimulator(22);
        bool loaded = sim.LoadGame(TestSavePath);
        Check(!loaded, "unsupported version returns false");
        Check(Near(sim.GetMarketCash(), 2_000.0), "unsupported version starts new game");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestUnknownHoldingFallsBack()
    {
        Section("unknown holding fallback");
        BlockTradeSimulator source = NewSimulator(31);
        source.SaveGame(TestSavePath);

        GodotDictionary root = ReadSaveDictionary(TestSavePath);
        Godot.Collections.Array holdings = root["holdings"].AsGodotArray();
        var bogus = new GodotDictionary
        {
            ["name"] = "未知股票",
            ["shares"] = 5,
            ["avg_cost"] = 100.0,
        };
        holdings.Add(bogus);
        root["holdings"] = holdings;
        WriteSaveDictionary(TestSavePath, root);

        BlockTradeSimulator sim = NewSimulator(32);
        bool loaded = sim.LoadGame(TestSavePath);
        Check(!loaded, "holding of unknown stock returns false");
        Check(Near(sim.GetMarketCash(), 2_000.0), "invalid holdings start new game");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestZeroStockSaveBoundary()
    {
        Section("zero-stock save boundary");
        BlockTradeSimulator source = NewSimulator(61);
        source.SaveGame(TestSavePath);

        GodotDictionary root = ReadSaveDictionary(TestSavePath);
        root["stocks"] = new Godot.Collections.Array();
        root["holdings"] = new Godot.Collections.Array();
        root["player_cash"] = 1234.5;
        WriteSaveDictionary(TestSavePath, root);

        BlockTradeSimulator sim = NewSimulator(62);
        bool loaded = sim.LoadGame(TestSavePath);
        Check(loaded, "stock count 0 is accepted as specified");
        Check(sim.GetStockNames().Length == 0, "loaded save can contain zero stocks");
        Check(sim.GetCompanyNames().Length == 0, "zero-stock save does not invent companies");
        sim.Tick(0.5);
        Check(Near(sim.GetCash(), 1234.5), "tick does not reset a zero-stock save");
        Check(Near(sim.GetGameTime(), 0.5), "tick still advances game time");
        Check(sim.GetStockNames().Length == 0, "zero-stock market stays empty after a tick");
        BlockTradeSaveManager.DeleteSave(TestSavePath);
    }

    private void TestCrossProcessWriteThenRead()
    {
        Section("restart persistence helpers");
        BlockTradeSaveManager.DeleteSave(RestartSavePath);
        GD.Print("  Cross-process check is executed by two separate Godot runs:");
        GD.Print("    --persist-write then --persist-read");
        Check(!FileAccess.FileExists(RestartSavePath), "restart test save starts absent");
    }

    private void RunPersistWrite()
    {
        GD.Print("BLOCKTRADE_PERSIST_WRITE_START");
        BlockTradeSaveManager.DeleteSave(RestartSavePath);
        BlockTradeSimulator sim = NewSimulator(4242);
        sim.Buy("煤炭", 5);
        sim.Buy("红石", 2);
        sim.AddExperience(100);
        for (int i = 0; i < 20; i++)
        {
            sim.Tick(0.5);
        }
        sim.MarketRefreshRemaining = 44.25;

        bool saved = sim.SaveGame(RestartSavePath);
        GD.Print($"BLOCKTRADE_PERSIST_WRITE_RESULT saved={saved} cash={sim.GetCash():R} level={sim.Level} time={sim.GetGameTime():R}");
        GetTree().Quit(saved ? 0 : 1);
    }

    private void RunPersistRead()
    {
        GD.Print("BLOCKTRADE_PERSIST_READ_START");

        BlockTradeSimulator reference = NewSimulator(4242);
        reference.Buy("煤炭", 5);
        reference.Buy("红石", 2);
        reference.AddExperience(100);
        for (int i = 0; i < 20; i++)
        {
            reference.Tick(0.5);
        }
        reference.MarketRefreshRemaining = 44.25;

        BlockTradeSimulator restored = NewSimulator(999);
        bool loaded = restored.LoadGame(RestartSavePath);
        Check(loaded, "restart: save loads in a new process");
        Check(Near(restored.GetCash(), reference.GetCash()), "restart: cash matches");
        Check(Near(restored.GetGameTime(), reference.GetGameTime()), "restart: game time matches");
        Check(restored.Level == reference.Level, "restart: stage matches");
        Check(restored.Xp == reference.Xp, "restart: xp matches");
        Check(Near(restored.MarketRefreshRemaining, 44.25), "restart: refresh countdown matches");
        Check(restored.GetStockNames().SequenceEqual(reference.GetStockNames()), "restart: stock names match");
        Check(restored.GetHoldingCount() == reference.GetHoldingCount(), "restart: holdings match");
        IReadOnlyList<double> restoredHistory = restored.GetPriceHistory("煤炭");
        IReadOnlyList<double> referenceHistory = reference.GetPriceHistory("煤炭");
        Check(restoredHistory.Count == referenceHistory.Count &&
              restoredHistory.Zip(referenceHistory).All(pair => Near(pair.First, pair.Second)),
            "restart: price history matches");
        Check(restored.TryGetHolding("煤炭", out int coalShares, out double coalAvg), "restart: 煤炭 exists");
        Check(coalShares == 5, "restart: 煤炭 has 5 shares");
        reference.TryGetHolding("煤炭", out _, out double referenceAvg);
        Check(Near(coalAvg, referenceAvg), "restart: 煤炭 average cost matches");

        bool pricesMatch = reference.GetStockNames()
            .All(name => Near(restored.GetPrice(name), reference.GetPrice(name)));
        Check(pricesMatch, "restart: every stock price matches");

        BlockTradeSaveManager.DeleteSave(RestartSavePath);
        GD.Print($"BLOCKTRADE_PERSIST_READ_RESULT passed={_failed == 0} ({_passed} checks)");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private BlockTradeSimulator NewSimulator(int seed)
    {
        var simulator = new BlockTradeSimulator { Name = $"TestSimulator_{seed}_{_passed}_{_failed}" };
        AddChild(simulator);
        simulator.InitMarket(seed);
        return simulator;
    }

    private void Section(string name)
    {
        GD.Print($"-- {name} --");
    }

    private void Check(bool condition, string label)
    {
        if (condition)
        {
            _passed++;
            GD.Print($"  [PASS] {label}");
        }
        else
        {
            _failed++;
            GD.PushError($"  [FAIL] {label}");
        }
    }

    private static bool Near(double a, double b, double epsilon = 0.000001)
    {
        return Math.Abs(a - b) <= epsilon;
    }

    private static string LegacyName(string chinese) => chinese switch
    {
        "铁锭" => "IRON",
        "金锭" => "GOLD",
        "钻石" => "DIAMOND",
        "红石" => "REDSTONE",
        "绿宝石" => "EMERALD",
        "煤炭" => "COAL",
        _ => chinese,
    };

    private static string ReadAllText(string path)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText() ?? string.Empty;
    }

    private static void WriteAllText(string path, string text)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }

    private static GodotDictionary ReadSaveDictionary(string path)
    {
        var json = new Json();
        Error error = json.Parse(ReadAllText(path));
        if (error != Error.Ok)
        {
            throw new InvalidOperationException($"Test save cannot be parsed: {json.GetErrorMessage()}");
        }

        return json.Data.AsGodotDictionary();
    }

    private static void WriteSaveDictionary(string path, GodotDictionary dictionary)
    {
        WriteAllText(path, Json.Stringify(dictionary, "  ", true, true));
    }
}
