using Godot;

namespace BlockTrade;

/// <summary>
/// Paid stage-locked side activities: scratch card, slot machine and roulette.
/// Every activity spends player cash and can award cash and stage XP.
/// </summary>
public partial class BlockTradeCasino : CanvasLayer
{
    public event Action? Closed;

    private const int ScratchCost = 30;
    private const int SlotCost = 50;
    private const int WheelCost = 80;

    private static readonly string[] Symbols = { "铁锭", "金锭", "钻石", "红石", "绿宝石", "煤炭" };

    private BlockTradeSimulator? _simulator;
    private BlockTradeSoundManager? _sound;
    private Control _root = null!;
    private string _currentGame = "scratch";
    private bool _busy;

    private Label _cashLabel = null!;
    private Label _levelLabel = null!;
    private Button _scratchTab = null!;
    private Button _slotTab = null!;
    private Button _wheelTab = null!;
    private Label _gameTitle = null!;
    private Label _gameRules = null!;
    private Label _gameStatus = null!;
    private Control _scratchPanel = null!;
    private Control _slotPanel = null!;
    private Control _wheelPanel = null!;
    private Button _scratchBuy = null!;
    private BlockTradeScratchCard _scratchCard = null!;
    private readonly string[] _scratchSymbols = new string[9];
    private bool _scratchActive;
    private Button _slotSpin = null!;
    private readonly Label[] _reels = new Label[3];
    private Button _wheelSpin = null!;
    private BlockTradeRouletteWheel _wheel = null!;

    public override void _Ready()
    {
        BuildInterface();
        Visible = false;
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Open(BlockTradeSimulator simulator, BlockTradeSoundManager sound)
    {
        Unbind();
        _simulator = simulator;
        _sound = sound;
        _simulator.StateChanged += OnStateChanged;
        Visible = true;
        _busy = false;
        if (_simulator.Level >= 4)
        {
            SelectGame("wheel");
        }
        else if (_simulator.Level >= 3)
        {
            SelectGame("slots");
        }
        else
        {
            SelectGame("scratch");
        }

        RefreshHeader();
        UpdateTabs();
        _scratchTab.GrabFocus();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Unbind();
        Closed?.Invoke();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Space)
        {
            switch (_currentGame)
            {
                case "scratch":
                    _scratchBuy.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                case "slots":
                    _slotSpin.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                case "wheel":
                    _wheelSpin.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void Unbind()
    {
        if (_simulator is not null)
        {
            _simulator.StateChanged -= OnStateChanged;
        }

        _simulator = null;
    }

    private void OnStateChanged()
    {
        RefreshHeader();
        UpdateTabs();
    }

    private void RefreshHeader()
    {
        if (_simulator is null)
        {
            return;
        }

        _cashLabel.Text = $"现金 ${_simulator.GetCash():#,##0.00}";
        _levelLabel.Text = _simulator.IsMaxLevel
            ? $"阶段 {_simulator.Level} / MAX"
            : $"阶段 {_simulator.Level} / XP {_simulator.XpToNextLevel}";
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        if (_simulator is null)
        {
            return;
        }

        _scratchTab.Disabled = _simulator.Level < 2;
        _slotTab.Disabled = _simulator.Level < 3;
        _wheelTab.Disabled = _simulator.Level < 4;

        _scratchTab.Text = _scratchTab.Disabled ? "刮刮乐\n阶段 2 解锁" : $"刮刮乐\n门票 ${ScratchCost}";
        _slotTab.Text = _slotTab.Disabled ? "老虎机\n阶段 3 解锁" : $"老虎机\n门票 ${SlotCost}";
        _wheelTab.Text = _wheelTab.Disabled ? "大转盘\n阶段 4 解锁" : $"大转盘\n门票 ${WheelCost}";

        if (_currentGame == "scratch" && _scratchTab.Disabled ||
            _currentGame == "slots" && _slotTab.Disabled ||
            _currentGame == "wheel" && _wheelTab.Disabled)
        {
            SelectGame(_scratchTab.Disabled ? _slotTab.Disabled ? "wheel" : "slots" : "scratch");
        }
    }

    private void SelectGame(string game)
    {
        _currentGame = game;
        _scratchPanel.Visible = game == "scratch";
        _slotPanel.Visible = game == "slots";
        _wheelPanel.Visible = game == "wheel";

        (_gameTitle.Text, _gameRules.Text) = game switch
        {
            "slots" => ("老虎机", $"支付 ${SlotCost}，转出三个相同方块赢得 $400；两个相同赢得 $50。"),
            "wheel" => ("大转盘", $"支付 ${WheelCost}，指针落在哪些倍率，就获得 门票 x 倍率。"),
            _ => ("刮刮乐", $"支付 ${ScratchCost}，长按鼠标左键刮开银色涂层；集齐 3 个相同方块赢得 $120。"),
        };

        _gameStatus.Text = "选择玩法，点击下方按钮开始";
        _gameStatus.AddThemeColorOverride("font_color", BlockTradeTheme.TextMuted);
    }

    private async void OnScratchBuy()
    {
        if (_simulator is null || _sound is null)
        {
            return;
        }

        if (_simulator.Level < 2)
        {
            ShowStatus("阶段不足 // 阶段 2 解锁刮刮乐", BlockTradeTheme.DownBright);
            return;
        }

        if (!_simulator.TrySpendCash(ScratchCost))
        {
            _sound.Play(BlockTradeSound.Error);
            ShowStatus("现金不足 // 需要 $30", BlockTradeTheme.DownBright);
            return;
        }

        ShowCashDelta(-ScratchCost);

        _scratchActive = true;
        for (int i = 0; i < 9; i++)
        {
            _scratchSymbols[i] = Symbols[Random.Shared.Next(Symbols.Length)];
        }

        _scratchCard.StartRound(_scratchSymbols);
        _sound.Play(BlockTradeSound.Scratch);
        ShowStatus("长按鼠标左键刮开银色涂层，寻找 3 个相同方块", BlockTradeTheme.Gold);
        RefreshHeader();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void OnScratchCompleted()
    {
        if (_simulator is null || _sound is null || !_scratchActive)
        {
            return;
        }

        _scratchActive = false;
        int best = _scratchSymbols.GroupBy(symbol => symbol).Max(group => group.Count());
        int payout = best >= 3 ? 120 : 0;
        if (payout > 0)
        {
            _simulator.AddCash(payout);
            ShowCashDelta(payout);
            _sound.Play(BlockTradeSound.Win);
            ShowStatus($"中奖！获得 ${payout}", BlockTradeTheme.UpBright);
        }
        else
        {
            _sound.Play(BlockTradeSound.Lose);
            ShowStatus("没有凑齐 3 个相同方块，再接再厉", BlockTradeTheme.DownBright);
        }

        _simulator.AddExperience(10);
        _simulator.SaveGame();
        RefreshHeader();
    }

    private async void OnSlotSpin()
    {
        if (_simulator is null || _sound is null)
        {
            return;
        }

        if (_simulator.Level < 3)
        {
            ShowStatus("阶段不足 // 阶段 3 解锁老虎机", BlockTradeTheme.DownBright);
            return;
        }

        if (_busy || !_simulator.TrySpendCash(SlotCost))
        {
            _sound.Play(BlockTradeSound.Error);
            ShowStatus("现金不足 // 需要 $50", BlockTradeTheme.DownBright);
            return;
        }

        ShowCashDelta(-SlotCost);

        _busy = true;
        _slotSpin.Disabled = true;
        _sound.Play(BlockTradeSound.Spin);
        ShowStatus("转动中...", BlockTradeTheme.Gold);
        for (int frame = 0; frame < 9; frame++)
        {
            SetReels(RandomSymbol(), RandomSymbol(), RandomSymbol());
            await ToSignal(GetTree().CreateTimer(0.06), SceneTreeTimer.SignalName.Timeout);
            if (!Visible)
            {
                _busy = false;
                return;
            }
        }

        string a = RandomSymbol();
        string b = RandomSymbol();
        string c = RandomSymbol();
        SetReels(a, b, c);
        int payout = a == b && b == c ? 400 : a == b || a == c || b == c ? 50 : 0;
        if (payout > 0)
        {
            _simulator.AddCash(payout);
            ShowCashDelta(payout);
            _sound.Play(BlockTradeSound.Win);
            ShowStatus($"中奖！获得 ${payout}", BlockTradeTheme.UpBright);
        }
        else
        {
            _sound.Play(BlockTradeSound.Lose);
            ShowStatus("三个方块没有成对，再来一次", BlockTradeTheme.DownBright);
        }

        _simulator.AddExperience(15);
        _simulator.SaveGame();
        RefreshHeader();
        _busy = false;
        _slotSpin.Disabled = false;
    }

    private async void OnWheelSpin()
    {
        if (_simulator is null || _sound is null)
        {
            return;
        }

        if (_simulator.Level < 4)
        {
            ShowStatus("阶段不足 // 阶段 4 解锁大转盘", BlockTradeTheme.DownBright);
            return;
        }

        if (_busy || !_simulator.TrySpendCash(WheelCost))
        {
            _sound.Play(BlockTradeSound.Error);
            ShowStatus("现金不足 // 需要 $80", BlockTradeTheme.DownBright);
            return;
        }

        ShowCashDelta(-WheelCost);

        _busy = true;
        _wheelSpin.Disabled = true;
        _sound.Play(BlockTradeSound.Spin);
        ShowStatus("大转盘转动中...", BlockTradeTheme.Gold);

        int finalIndex = Random.Shared.Next(8);
        float step = MathF.Tau / 8.0f;
        float startRotation = _wheel.WheelRotation;
        float desired = -finalIndex * step;
        float delta = (desired - startRotation) % MathF.Tau;
        if (delta <= 0.0f)
        {
            delta += MathF.Tau;
        }

        float targetRotation = startRotation + delta + MathF.Tau * 4.0f;
        _wheel.SetSelectedIndex(-1);
        const int frames = 28;
        for (int frame = 1; frame <= frames; frame++)
        {
            float t = frame / (float)frames;
            float eased = 1.0f - MathF.Pow(1.0f - t, 3.0f);
            _wheel.SetWheelRotation(Mathf.Lerp(startRotation, targetRotation, eased));
            await ToSignal(GetTree().CreateTimer(0.03), SceneTreeTimer.SignalName.Timeout);
            if (!Visible)
            {
                _busy = false;
                return;
            }
        }

        _wheel.SetWheelRotation(targetRotation);
        _wheel.SetSelectedIndex(finalIndex);
        int multiplier = _wheel.Multiplier(finalIndex);
        int payout = WheelCost * multiplier;
        if (payout > 0)
        {
            _simulator.AddCash(payout);
            ShowCashDelta(payout);
            _sound.Play(BlockTradeSound.Win);
            ShowStatus($"指针停在 {multiplier}x，获得 ${payout}", BlockTradeTheme.UpBright);
        }
        else
        {
            _sound.Play(BlockTradeSound.Lose);
            ShowStatus("指针停在 0x，下一次会更好", BlockTradeTheme.DownBright);
        }

        _simulator.AddExperience(20);
        _simulator.SaveGame();
        RefreshHeader();
        _busy = false;
        _wheelSpin.Disabled = false;
    }

    private void SetReels(string a, string b, string c)
    {
        _reels[0].Text = a;
        _reels[1].Text = b;
        _reels[2].Text = c;
    }

    private static string RandomSymbol() => Symbols[Random.Shared.Next(Symbols.Length)];

    private void ShowStatus(string message, Color color)
    {
        _gameStatus.Text = message;
        _gameStatus.AddThemeColorOverride("font_color", color);
    }

    private void ShowCashDelta(double amount)
    {
        if (_root is not null)
        {
            BlockTradeDeltaPopup.Show(_root, _cashLabel, amount);
        }
    }

    private void BuildInterface()
    {
        var root = new Control
        {
            Name = "CasinoRoot",
            Theme = BlockTradeTheme.CreateTheme(),
        };
        _root = root;
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new BlockTradeBackdrop { Name = "CasinoBackdrop" };
        root.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var margin = new MarginContainer { Name = "CasinoMargin" };
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        root.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 10);
        margin.AddChild(page);

        var top = new PanelContainer();
        BlockTradeTheme.ApplyPanel(top);
        var topMargin = new MarginContainer();
        topMargin.AddThemeConstantOverride("margin_left", 10);
        topMargin.AddThemeConstantOverride("margin_right", 10);
        topMargin.AddThemeConstantOverride("margin_top", 8);
        topMargin.AddThemeConstantOverride("margin_bottom", 8);
        top.AddChild(topMargin);
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 18);
        topMargin.AddChild(topRow);
        topRow.AddChild(MakeLabel("BLOCKTRADE // 玩法", 18, BlockTradeTheme.Gold));
        topRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _levelLabel = MakeLabel("阶段 1", 14, BlockTradeTheme.TextMuted);
        topRow.AddChild(_levelLabel);
        _cashLabel = MakeLabel("现金 $0.00", 14, BlockTradeTheme.Gold);
        topRow.AddChild(_cashLabel);
        var close = new Button
        {
            Name = "CasinoCloseBtn",
            Text = "返回市场",
            CustomMinimumSize = new Vector2(130, 38),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(close, BlockTradeButtonTone.Neutral);
        close.Pressed += Close;
        topRow.AddChild(close);
        page.AddChild(top);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        page.AddChild(body);

        var selector = new PanelContainer { CustomMinimumSize = new Vector2(250, 0) };
        BlockTradeTheme.ApplyPanel(selector);
        var selectorMargin = new MarginContainer();
        selectorMargin.AddThemeConstantOverride("margin_left", 10);
        selectorMargin.AddThemeConstantOverride("margin_right", 10);
        selectorMargin.AddThemeConstantOverride("margin_top", 10);
        selectorMargin.AddThemeConstantOverride("margin_bottom", 10);
        selector.AddChild(selectorMargin);
        var selectorColumn = new VBoxContainer();
        selectorColumn.AddThemeConstantOverride("separation", 8);
        selectorMargin.AddChild(selectorColumn);
        selectorColumn.AddChild(MakeLabel("玩法", 16, BlockTradeTheme.Text));
        selectorColumn.AddChild(Separator());
        _scratchTab = MakeGameTab("ScratchTabBtn", "刮刮乐", BlockTradeButtonTone.Green);
        _scratchTab.Pressed += () => SelectGame("scratch");
        selectorColumn.AddChild(_scratchTab);
        _slotTab = MakeGameTab("SlotsTabBtn", "老虎机", BlockTradeButtonTone.Gold);
        _slotTab.Pressed += () => SelectGame("slots");
        selectorColumn.AddChild(_slotTab);
        _wheelTab = MakeGameTab("WheelTabBtn", "大转盘", BlockTradeButtonTone.Red);
        _wheelTab.Pressed += () => SelectGame("wheel");
        selectorColumn.AddChild(_wheelTab);
        selectorColumn.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        selectorColumn.AddChild(MakeLabel("所有玩法都消耗现金。\n量力而行。", 12, BlockTradeTheme.TextDim));
        body.AddChild(selector);

        var content = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        BlockTradeTheme.ApplyPanel(content);
        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 16);
        contentMargin.AddThemeConstantOverride("margin_right", 16);
        contentMargin.AddThemeConstantOverride("margin_top", 14);
        contentMargin.AddThemeConstantOverride("margin_bottom", 14);
        content.AddChild(contentMargin);
        var contentColumn = new VBoxContainer();
        contentColumn.AddThemeConstantOverride("separation", 8);
        contentMargin.AddChild(contentColumn);
        _gameTitle = MakeLabel("刮刮乐", 24, BlockTradeTheme.Text);
        contentColumn.AddChild(_gameTitle);
        _gameRules = MakeLabel("", 14, BlockTradeTheme.TextMuted);
        _gameRules.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        contentColumn.AddChild(_gameRules);
        contentColumn.AddChild(Separator());
        contentColumn.AddChild(BuildScratchPanel());
        contentColumn.AddChild(BuildSlotPanel());
        contentColumn.AddChild(BuildWheelPanel());
        _gameStatus = MakeLabel("选择玩法，点击下方按钮开始", 14, BlockTradeTheme.TextMuted);
        contentColumn.AddChild(_gameStatus);
        body.AddChild(content);

        UpdateTabs();
    }

    private Control BuildScratchPanel()
    {
        _scratchPanel = new VBoxContainer { Name = "ScratchPanel" };
        var scratchColumn = (VBoxContainer)_scratchPanel;
        scratchColumn.AddThemeConstantOverride("separation", 8);

        _scratchCard = new BlockTradeScratchCard
        {
            Name = "ScratchCard",
            CustomMinimumSize = new Vector2(330, 210),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        _scratchCard.Completed += OnScratchCompleted;
        scratchColumn.AddChild(_scratchCard);

        _scratchBuy = new Button
        {
            Name = "ScratchBuyBtn",
            Text = $"购买刮刮乐（${ScratchCost}）",
            CustomMinimumSize = new Vector2(0, 48),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(_scratchBuy, BlockTradeButtonTone.Green);
        _scratchBuy.Pressed += OnScratchBuy;
        scratchColumn.AddChild(_scratchBuy);
        return _scratchPanel;
    }

    private Control BuildSlotPanel()
    {
        _slotPanel = new VBoxContainer { Name = "SlotsPanel", Visible = false };
        var column = (VBoxContainer)_slotPanel;
        column.AddThemeConstantOverride("separation", 10);

        var reels = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        reels.AddThemeConstantOverride("separation", 10);
        for (int i = 0; i < 3; i++)
        {
            var holder = new PanelContainer { CustomMinimumSize = new Vector2(150, 110) };
            BlockTradeTheme.ApplyPanel(holder, BlockTradeTheme.MakeBox(BlockTradeTheme.PanelDark, BlockTradeTheme.Gold, 2));
            var label = MakeLabel("？", 30, BlockTradeTheme.Gold);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            holder.AddChild(label);
            reels.AddChild(holder);
            _reels[i] = label;
        }
        column.AddChild(reels);

        _slotSpin = new Button
        {
            Name = "SlotSpinBtn",
            Text = $"转动老虎机（${SlotCost}）",
            CustomMinimumSize = new Vector2(0, 48),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(_slotSpin, BlockTradeButtonTone.Gold);
        _slotSpin.Pressed += OnSlotSpin;
        column.AddChild(_slotSpin);
        return _slotPanel;
    }

    private Control BuildWheelPanel()
    {
        _wheelPanel = new VBoxContainer { Name = "WheelPanel", Visible = false };
        var column = (VBoxContainer)_wheelPanel;
        column.AddThemeConstantOverride("separation", 8);

        _wheel = new BlockTradeRouletteWheel
        {
            Name = "RouletteWheel",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(280, 280),
        };
        column.AddChild(_wheel);

        _wheelSpin = new Button
        {
            Name = "WheelSpinBtn",
            Text = $"转动大转盘（${WheelCost}）",
            CustomMinimumSize = new Vector2(0, 48),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(_wheelSpin, BlockTradeButtonTone.Red);
        _wheelSpin.Pressed += OnWheelSpin;
        column.AddChild(_wheelSpin);
        return _wheelPanel;
    }

    private static Button MakeGameTab(string name, string label, BlockTradeButtonTone tone)
    {
        var button = new Button
        {
            Name = name,
            Text = label,
            CustomMinimumSize = new Vector2(0, 62),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(button, tone);
        button.AddThemeFontSizeOverride("font_size", 16);
        return button;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static ColorRect Separator()
    {
        return new ColorRect
        {
            Color = BlockTradeTheme.BorderDark,
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }
}
