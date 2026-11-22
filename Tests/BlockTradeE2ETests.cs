using Godot;
using FileAccess = Godot.FileAccess;
using Timer = Godot.Timer;

namespace BlockTrade;

/// <summary>
/// End-to-end test for the full flow: main menu -> story -> trading, stage-up
/// notification, settings, paid activity, autosave, Continue and corrupt save.
///
/// Run with:
/// godot --headless --path . res://Tests/BlockTradeE2ETests.tscn
/// </summary>
public partial class BlockTradeE2ETests : Node
{
    private const string DefaultSavePath = BlockTradeSaveManager.DefaultSavePath;
    private int _passed;
    private int _failed;
    private string? _backup;

    public override async void _Ready()
    {
        try
        {
            GD.Print("==============================================");
            GD.Print(" BlockTrade end-to-end UI test suite");
            GD.Print("==============================================");

            _backup = FileAccess.FileExists(DefaultSavePath) ? ReadAllText(DefaultSavePath) : null;
            BlockTradeSaveManager.DeleteSave(DefaultSavePath);

            Node main = await StartMain();
            CanvasLayer menu = FindNamed<CanvasLayer>(main, "Menu")!;
            CanvasLayer story = FindNamed<CanvasLayer>(main, "Story")!;
            CanvasLayer ui = FindNamed<CanvasLayer>(main, "UI")!;
            Button newGame = FindNamed<Button>(main, "NewGameBtn")!;
            Button continueButton = FindNamed<Button>(main, "ContinueBtn")!;
            Button storyNext = FindNamed<Button>(main, "StoryNextBtn")!;
            Button storySkip = FindNamed<Button>(main, "StorySkipBtn")!;
            Timer tickTimer = main.GetNode<Timer>("TickTimer");
            BlockTradeSimulator simulator = main.GetNode<BlockTradeSimulator>("BlockTradeSimulator");

            Check(menu.Visible, "main menu is shown first");
            Check(!ui.Visible, "trading UI is hidden behind the menu");
            Check(continueButton.Disabled, "Continue is disabled with no save");
            Check(FindNamed<Button>(main, "SettingsBtn") is not null, "main menu has Settings");
            Check(Near(tickTimer.WaitTime, 120.0), "default market refresh is 120 seconds (2 minutes)");
            Check(tickTimer.IsStopped(), "timer is stopped while in the menu");

            newGame.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(story.Visible, "New Game opens the story");
            Check(!menu.Visible, "main menu is hidden during the story");
            Check(FindNamed<Control>(main, "StoryCard") is not null, "story card was built");

            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            storyNext.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Label storyPage = FindNamed<Label>(main, "StoryPageLabel")!;
            Check(storyPage.Text == "2 / 3", "story advances from page 1 to page 2");

            storySkip.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(ui.Visible, "skipping the story opens the trading UI");
            Check(!story.Visible, "story is hidden in the trading UI");
            Check(!tickTimer.IsStopped(), "timer starts when trading begins");
            Check(Near(tickTimer.WaitTime, 120.0), "timer uses the two-minute refresh interval");

            Control authOverlay = FindNamed<Control>(main, "AuthOverlay")!;
            LineEdit authPassword = FindNamed<LineEdit>(main, "AuthPasswordInput")!;
            LineEdit authConfirm = FindNamed<LineEdit>(main, "AuthConfirmInput")!;
            Button authConfirmButton = FindNamed<Button>(main, "AuthConfirmBtn")!;
            Check(authOverlay.Visible, "first entry requires setting a trading password");
            authPassword.Text = "123456";
            authConfirm.Text = "123456";
            authConfirmButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!authOverlay.Visible && simulator.HasTradingPassword,
                "trading password is saved before trading starts");

            CheckRequiredNodes(main);
            BlockTradeStockList stockList = FindNamed<BlockTradeStockList>(main, "StockList")!;
            BlockTradePriceChart chart = FindNamed<BlockTradePriceChart>(main, "PriceChart")!;
            LineEdit amount = FindNamed<LineEdit>(main, "AmountInput")!;
            Button buy = FindNamed<Button>(main, "BuyBtn")!;
            Button sell = FindNamed<Button>(main, "SellBtn")!;
            Button casinoButton = FindNamed<Button>(main, "CasinoBtn")!;
            Button settingsButton = FindNamed<Button>(main, "SettingsBtn")!;
            Label refreshLabel = FindNamed<Label>(main, "RefreshLabel")!;
            Label cashLabel = FindNamed<Label>(main, "CashLabel")!;
            Label stockAssetLabel = FindNamed<Label>(main, "StockAssetLabel")!;
            Label stageLabel = FindNamed<Label>(main, "LevelLabel")!;
            ProgressBar xpBar = FindNamed<ProgressBar>(main, "XpBar")!;
            BlockTradeUI tradingUi = FindNamed<BlockTradeUI>(main, "UI")!;

            Check(stockList.StockCount == 6, "stock list is populated with six rows");
            Check(chart.StockName == "铁锭", "chart starts on 铁锭");
            Check(chart.SeriesCount == 1, "chart shows only the selected item's line");
            Check(chart.SelectedPointCount == 1, "new game chart starts as one flat line");
            Check(Near(simulator.GetCash(), 0.0), "cash starts outside the stock market at zero");
            Check(Near(simulator.GetMarketCash(), 2_000.0), "market assets start at 2000");
            Check(Near(simulator.GetAsset(), 2_000.0), "asset label source includes market cash");
            Check(cashLabel.Text.Contains("$0.00"), "cash label starts at zero");
            Check(stockAssetLabel.Text.Contains("$2,000.00"), "asset label starts at 2000");
            Check(refreshLabel.Text.Contains("后刷新"), "refresh countdown label is visible");
            Check(stageLabel.Text.Contains("阶段 1"), "stage label shows stage 1");
            Check(casinoButton.Disabled, "minigames are locked at stage 1");
            Check(xpBar.Value == 0.0f, "xp bar starts empty");

            stockList.SelectStock("金锭", notify: true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(chart.StockName == "金锭", "selecting 金锭 updates the chart");
            Check(FindNamed<Label>(main, "StockNameLabel")!.Text == "金锭", "header follows stock selection");
            stockList.SelectStock("煤炭", notify: true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            tradingUi._UnhandledKeyInput(new InputEventKey { Keycode = Key.Key1, Pressed = true });
            Check(amount.Text == "1", "hotkey 1 sets quantity to 1");
            tradingUi._UnhandledKeyInput(new InputEventKey { Keycode = Key.Key2, Pressed = true });
            Check(amount.Text == "10", "hotkey 2 sets quantity to 10");
            tradingUi._UnhandledKeyInput(new InputEventKey { Keycode = Key.Key1, Pressed = true });

            tradingUi._Input(new InputEventKey
            {
                Keycode = Key.T,
                Pressed = true,
                CtrlPressed = true,
                ShiftPressed = true,
            });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            PanelContainer refreshEditor = FindNamed<PanelContainer>(main, "RefreshEditor")!;
            Check(refreshEditor.Visible, "Ctrl+Shift+T opens refresh-time editor");
            LineEdit refreshSecondsInput = FindNamed<LineEdit>(main, "RefreshSecondsInput")!;
            int intervalBefore = (int)simulator.RefreshIntervalSeconds;
            refreshSecondsInput.Text = "75";
            refreshSecondsInput.EmitSignal(LineEdit.SignalName.TextSubmitted, "75");
            Check((int)simulator.RefreshIntervalSeconds == 75,
                "typing exact seconds sets refresh time to 75");
            Check(Near(tickTimer.WaitTime, 75.0), "timer restarts at the exact second value");
            tradingUi._Input(new InputEventKey { Keycode = Key.Up, Pressed = true });
            Check((int)simulator.RefreshIntervalSeconds == 76,
                "Up arrow increases refresh time by one second");
            tradingUi._Input(new InputEventKey { Keycode = Key.Down, Pressed = true });
            Check((int)simulator.RefreshIntervalSeconds == 75,
                "Down arrow decreases refresh time by one second");
            refreshSecondsInput.Text = intervalBefore.ToString();
            refreshSecondsInput.EmitSignal(LineEdit.SignalName.TextSubmitted, intervalBefore.ToString());
            tradingUi._Input(new InputEventKey
            {
                Keycode = Key.T,
                Pressed = true,
                CtrlPressed = true,
                ShiftPressed = true,
            });
            Check(!refreshEditor.Visible, "Ctrl+Shift+T closes refresh-time editor");

            double timeBeforeTick = simulator.GetGameTime();
            tickTimer.Stop();
            tickTimer.WaitTime = 0.2;
            tickTimer.Start();
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            Check(simulator.GetGameTime() >= timeBeforeTick + 0.19, "TickTimer timeout advances the market");

            amount.Text = "5";
            buy.EmitSignal(BaseButton.SignalName.Pressed);
            Check(authOverlay.Visible, "buying opens password and captcha verification");
            authPassword.Text = "000000";
            LineEdit captchaInput = FindNamed<LineEdit>(main, "AuthCaptchaInput")!;
            BlockTradeCaptcha captcha = FindNamed<BlockTradeCaptcha>(main, "CaptchaImage")!;
            captchaInput.Text = captcha.Code;
            authConfirmButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(authOverlay.Visible, "wrong trading password keeps the verification dialog open");
            Check(!simulator.TryGetHolding("煤炭", out _, out _), "wrong trading password cannot buy stock");
            await ApproveBuy(main);
            Check(simulator.TryGetHolding("煤炭", out int shares, out _) && shares == 5,
                "UI BUY button buys 5 煤炭");
            Check(simulator.GetMarketCash() < 2_000.0, "UI BUY reduces market assets");
            Check(stockAssetLabel.Text.Contains(simulator.GetMarketCash().ToString("#,##0.00")),
                "asset label shows the reduced available balance after buying");
            Check(simulator.GetStockAsset() > 0.0 && stockAssetLabel.Text.StartsWith("资产 "),
                "asset label remains a distinct available-balance field");
            Check(simulator.Xp > 0, "a successful trade grants stage XP");
            Check(FileAccess.FileExists(DefaultSavePath), "UI BUY autosaves the game");

            double cashAfterBuy = simulator.GetMarketCash();
            amount.Text = "2";
            sell.EmitSignal(BaseButton.SignalName.Pressed);
            Check(simulator.TryGetHolding("煤炭", out shares, out _) && shares == 3,
                "UI SELL leaves 3 煤炭 shares after selling 2");
            Check(simulator.GetMarketCash() > cashAfterBuy, "UI SELL increases market assets");
            double cashAfterSell = simulator.GetMarketCash();

            amount.Text = "9999";
            sell.EmitSignal(BaseButton.SignalName.Pressed);
            Check(simulator.TryGetHolding("煤炭", out shares, out _) && shares == 3,
                "oversell UI order leaves holdings unchanged");

            simulator.AddExperience(simulator.XpToNextLevel);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(simulator.Level == 2, "adding XP reaches stage 2");
            Check(stockList.StockCount == 8, "stage 2 adds two more item rows");
            Check(!casinoButton.Disabled, "玩法 button unlocks at stage 2");
            Check(FindNamed<PanelContainer>(main, "LevelUpToast")!.Visible,
                "stage-up notification appears at top center");

            settingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CanvasLayer settings = FindNamed<CanvasLayer>(main, "Settings")!;
            Check(settings.Visible, "Settings overlay opens");
            OptionButton resolutionOption = FindNamed<OptionButton>(main, "ResolutionOption")!;
            Check(resolutionOption.ItemCount == 4, "settings exposes four resolution presets");
            resolutionOption.Selected = 1;
            resolutionOption.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(BlockTradeSettingsStore.WindowWidth == 1600 && BlockTradeSettingsStore.WindowHeight == 900,
                "selecting a resolution updates the settings store");
            resolutionOption.Selected = 0;
            resolutionOption.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(BlockTradeSettingsStore.WindowWidth == 1280 && BlockTradeSettingsStore.WindowHeight == 720,
                "resolution can be restored to 1280x720");
            Button settingsClose = FindNamed<Button>(main, "SettingsCloseBtn")!;
            settingsClose.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!settings.Visible, "Settings overlay closes");

            Check(Near(simulator.GetCash(), 0.0), "cash remains zero before transfer");
            Button transferButton = FindNamed<Button>(main, "TransferBtn")!;
            transferButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Control transferOverlay = FindNamed<Control>(main, "TransferOverlay")!;
            Check(transferOverlay.Visible, "transfer opens the cash/asset dialog");
            FindNamed<Button>(main, "ToCashBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            LineEdit transferInput = FindNamed<LineEdit>(main, "TransferAmountInput")!;
            transferInput.Text = "500";
            FindNamed<Button>(main, "TransferConfirmBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Near(simulator.GetCash(), 500.0), "assets transfer directly to cash");
            Check(FindLabelWithText(main, "+$500.00") is not null, "cash shows a red +500 delta");
            Check(FindLabelWithText(main, "-$500.00") is not null, "asset shows a green -500 delta");
            Check(!transferOverlay.Visible, "transfer dialog closes after withdrawal");

            double marketBeforeDeposit = simulator.GetMarketCash();
            transferButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            transferInput.Text = "200";
            FindNamed<Button>(main, "TransferConfirmBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Near(simulator.GetCash(), 300.0), "cash decreases when deposited into the market");
            Check(Near(simulator.GetMarketCash(), marketBeforeDeposit + 200.0),
                "cash converts back into market assets");

            casinoButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CanvasLayer casino = FindNamed<CanvasLayer>(main, "Casino")!;
            Check(casino.Visible, "玩法 screen opens at stage 2");
            Button scratchTab = FindNamed<Button>(main, "ScratchTabBtn")!;
            Button slotsTab = FindNamed<Button>(main, "SlotsTabBtn")!;
            Check(!scratchTab.Disabled, "scratch card is unlocked at stage 2");
            Check(slotsTab.Disabled, "slot machine remains locked at stage 2");

            Button scratchBuy = FindNamed<Button>(main, "ScratchBuyBtn")!;
            int xpBeforeScratch = simulator.Xp;
            double cashBeforeScratch = simulator.GetCash();
            scratchBuy.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(simulator.GetCash() <= cashBeforeScratch - 30.0 + 0.001,
                "scratch card charges its paid ticket");
            BlockTradeScratchCard scratchCard = FindNamed<BlockTradeScratchCard>(main, "ScratchCard")!;
            Check(scratchCard.IsActive, "scratch card starts a real scratch round");
            scratchCard.ScratchAllForTest();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(simulator.Xp >= xpBeforeScratch + 10, "scratch card grants stage XP");
            Button casinoClose = FindNamed<Button>(main, "CasinoCloseBtn")!;
            casinoClose.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!casino.Visible, "casino returns to the market");

            simulator.AddExperience(1000);
            simulator.AddCash(300);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(simulator.Level == BlockTradeSimulator.MaxLevel, "large XP gain reaches max stage");

            Button companyMarketButton = FindNamed<Button>(main, "CompanyMarketBtn")!;
            BlockTradeStockList companyList = FindNamed<BlockTradeStockList>(main, "StockList")!;
            Check(!companyMarketButton.Disabled, "company market unlocks at stage 4");
            Check(companyList.CompanyMode, "reaching stage 4 enters the company market automatically");
            Check(companyList.StockCount == 8, "company market shows eight company stocks");
            Check(simulator.GetPrice(companyList.SelectedStock) > 1_000.0,
                "company market trades large per-share amounts");

            casinoButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CanvasLayer casinoMax = FindNamed<CanvasLayer>(main, "Casino")!;
            Button slotsTabMax = FindNamed<Button>(main, "SlotsTabBtn")!;
            Check(casinoMax.Visible && !slotsTabMax.Disabled, "slot machine unlocks at stage 3");
            slotsTabMax.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            int xpBeforeSlot = simulator.Xp;
            Button slotSpin = FindNamed<Button>(main, "SlotSpinBtn")!;
            slotSpin.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            Check(simulator.Xp >= xpBeforeSlot + 15, "slot machine charges cash and grants XP");

            Button wheelTab = FindNamed<Button>(main, "WheelTabBtn")!;
            Check(!wheelTab.Disabled, "roulette unlocks at stage 4");
            wheelTab.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            int xpBeforeWheel = simulator.Xp;
            BlockTradeRouletteWheel wheel = FindNamed<BlockTradeRouletteWheel>(main, "RouletteWheel")!;
            float wheelRotationBefore = wheel.WheelRotation;
            Button wheelSpin = FindNamed<Button>(main, "WheelSpinBtn")!;
            wheelSpin.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            Check(Math.Abs(wheel.WheelRotation - wheelRotationBefore) > 0.1f,
                "wheel rotates under a fixed pointer instead of only changing highlight");
            Check(simulator.Xp >= xpBeforeWheel + 20, "roulette charges cash and grants XP");
            Button casinoCloseMax = FindNamed<Button>(main, "CasinoCloseBtn")!;
            casinoCloseMax.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            // Force a short remaining clock, then let the next successful trade
            // persist it. Continue must resume close to this remaining time.
            tickTimer.Stop();
            tickTimer.WaitTime = 45.0;
            tickTimer.Start();
            amount.Text = "1";
            buy.EmitSignal(BaseButton.SignalName.Pressed);
            await ApproveBuy(main);
            double cashBeforeRestart = simulator.GetCash();
            double marketBeforeRestart = simulator.GetMarketCash();
            simulator.TryGetHolding("煤炭", out shares, out _);
            int sharesBeforeRestart = shares;

            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Node restarted = await StartMain();
            CanvasLayer restartedMenu = FindNamed<CanvasLayer>(restarted, "Menu")!;
            Button continueRestarted = FindNamed<Button>(restarted, "ContinueBtn")!;
            Check(restartedMenu.Visible, "restart returns to the main menu");
            Check(!continueRestarted.Disabled, "Continue is enabled when a save exists");
            continueRestarted.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            BlockTradeSimulator restored = restarted.GetNode<BlockTradeSimulator>("BlockTradeSimulator");
            CanvasLayer restoredUi = FindNamed<CanvasLayer>(restarted, "UI")!;
            BlockTradePriceChart restoredChart = FindNamed<BlockTradePriceChart>(restarted, "PriceChart")!;
            BlockTradeStockList restoredList = FindNamed<BlockTradeStockList>(restarted, "StockList")!;
            Timer restoredTimer = restarted.GetNode<Timer>("TickTimer");
            Check(restoredUi.Visible, "Continue enters the trading UI directly");
            Check(restoredList.CompanyMode, "max-stage Continue re-enters the company market");
            Button restoredItemTab = FindNamed<Button>(restarted, "ItemMarketBtn")!;
            restoredItemTab.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!restoredList.CompanyMode, "item market tab returns to item stocks");
            Check(restoredChart.SelectedPointCount > 1, "Continue restores the price line history");
            Check(Near(restored.GetCash(), cashBeforeRestart), "restarted scene restores cash");
            Check(Near(restored.GetMarketCash(), marketBeforeRestart), "restarted scene restores market assets");
            Check(restored.TryGetHolding("煤炭", out int restoredShares, out _) && restoredShares == sharesBeforeRestart,
                "restarted scene restores 煤炭 shares");
            Check(restored.Level >= 2, "restarted scene restores stage");
            Check(restored.MarketRefreshRemaining > 40.0, "restarted scene preserves remaining refresh time");
            Check(restoredTimer.WaitTime > 40.0, "timer resumes the saved refresh countdown");

            Button loanButton = FindNamed<Button>(restarted, "LoanBtn")!;
            loanButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Control loanOverlay = FindNamed<Control>(restarted, "LoanOverlay")!;
            Check(loanOverlay.Visible, "loan dialog opens");
            LineEdit loanInput = FindNamed<LineEdit>(restarted, "LoanAmountInput")!;
            double loanCashBefore = restored.GetCash();
            loanInput.Text = "1000";
            FindNamed<Button>(restarted, "BorrowBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Near(restored.GetCash(), loanCashBefore + 1000.0) && Near(restored.LoanDebt, 1000.0),
                "borrow adds cash and records debt");
            loanInput.Text = "1000";
            FindNamed<Button>(restarted, "RepayBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Near(restored.GetCash(), loanCashBefore) && Near(restored.LoanDebt, 0.0),
                "repay removes debt and deducts cash");
            FindNamed<Button>(restarted, "LoanCloseBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            restarted.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            WriteAllText(DefaultSavePath, "{ broken save");
            Node fallback = await StartMain();
            Button continueBroken = FindNamed<Button>(fallback, "ContinueBtn")!;
            continueBroken.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            BlockTradeSimulator fresh = fallback.GetNode<BlockTradeSimulator>("BlockTradeSimulator");
            CanvasLayer fallbackStory = FindNamed<CanvasLayer>(fallback, "Story")!;
            Check(Near(fresh.GetCash(), 0.0), "corrupt save falls back to zero external cash");
            Check(Near(fresh.GetMarketCash(), 2_000.0), "corrupt save falls back to 2000 market assets");
            Check(fresh.GetHoldingCount() == 0, "corrupt save falls back to empty holdings");
            Check(fresh.GetUnlockedStockNames().Length == 6, "corrupt save still builds the market");
            Check(fallbackStory.Visible, "corrupt save restarts the story flow");

            Button fallbackSkip = FindNamed<Button>(fallback, "StorySkipBtn")!;
            fallbackSkip.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(FindNamed<CanvasLayer>(fallback, "UI")!.Visible, "fallback story reaches the market");
            fallback.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        catch (Exception exception)
        {
            _failed++;
            GD.PushError($"[E2E EXCEPTION] {exception}");
        }
        finally
        {
            RestoreDefaultSave();
        }

        GD.Print("----------------------------------------------");
        GD.Print($" RESULT: {_passed} passed, {_failed} failed");
        GD.Print("----------------------------------------------");
        if (_failed > 0)
        {
            GD.PushError("BLOCKTRADE UI TESTS FAILED");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ALL UI TESTS PASSED");
        GetTree().Quit(0);
    }

    private async Task<Node> StartMain()
    {
        var packed = GD.Load<PackedScene>("res://Scenes/Main.tscn");
        Node main = packed.Instantiate();
        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return main;
    }

    private void CheckRequiredNodes(Node main)
    {
        Check(FindNamed<Control>(main, "StockList") is BlockTradeStockList, "StockList is a custom stock list");
        Check(FindNamed<Control>(main, "PriceChart") is BlockTradePriceChart, "PriceChart is the custom chart");
        Check(FindNamed<Control>(main, "TradingPanel") is HBoxContainer, "TradingPanel is an HBoxContainer");
        Check(FindNamed<Control>(main, "AmountInput") is LineEdit, "AmountInput is a LineEdit");
        Check(FindNamed<Control>(main, "BuyBtn") is Button, "BuyBtn is a Button");
        Check(FindNamed<Control>(main, "SellBtn") is Button, "SellBtn is a Button");
        Check(FindNamed<Control>(main, "CashLabel") is Label, "CashLabel is a Label");
        Check(FindNamed<Control>(main, "SaveQuitBtn") is Button, "SaveQuitBtn is a Button");
        Check(FindNamed<Control>(main, "LevelLabel") is Label, "LevelLabel exists");
        Check(FindNamed<Control>(main, "XpBar") is ProgressBar, "XpBar exists");
        Check(FindNamed<Control>(main, "CasinoBtn") is Button, "CasinoBtn exists");
        Check(FindNamed<Control>(main, "SettingsBtn") is Button, "SettingsBtn exists");
        Check(main.GetNodeOrNull<Timer>("TickTimer") is not null, "TickTimer exists");
    }

    private async Task ApproveBuy(Node main)
    {
        LineEdit password = FindNamed<LineEdit>(main, "AuthPasswordInput")!;
        LineEdit captchaInput = FindNamed<LineEdit>(main, "AuthCaptchaInput")!;
        BlockTradeCaptcha captcha = FindNamed<BlockTradeCaptcha>(main, "CaptchaImage")!;
        Button confirm = FindNamed<Button>(main, "AuthConfirmBtn")!;
        password.Text = "123456";
        captchaInput.Text = captcha.Code;
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static Label? FindLabelWithText(Node root, string text)
    {
        if (root is Label label && label.Text == text)
        {
            return label;
        }

        foreach (Node child in root.GetChildren())
        {
            Label? found = FindLabelWithText(child, text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static T? FindNamed<T>(Node root, string name) where T : Node
    {
        if (root is T typed && root.Name == name)
        {
            return typed;
        }

        foreach (Node child in root.GetChildren())
        {
            T? found = FindNamed<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
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

    private void RestoreDefaultSave()
    {
        if (_backup is null)
        {
            BlockTradeSaveManager.DeleteSave(DefaultSavePath);
            return;
        }

        WriteAllText(DefaultSavePath, _backup);
    }

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
}
