using System.Globalization;
using Godot;
using Timer = Godot.Timer;

namespace BlockTrade;

/// <summary>
/// The trading floor. Simplified compared with the earlier dashboard: one
/// market column, one detail column, stage XP in the top bar and paid minigames
/// behind one button. Every visible stock name is Chinese.
/// </summary>
public partial class BlockTradeUI : CanvasLayer
{
    public event Action? CasinoRequested;
    public event Action? SettingsRequested;

    private BlockTradeSimulator? _simulator;
    private Timer? _tickTimer;
    private BlockTradeSoundManager? _sound;
    private string _selectedStock = "铁锭";
    private int _lastCountdownSecond = -1;

    private Control _root = null!;
    private BlockTradeStockList _stockList = null!;
    private BlockTradePriceChart _priceChart = null!;
    private BlockTradeStockIcon _stockIcon = null!;
    private ItemList _holdingsList = null!;
    private LineEdit _amountInput = null!;
    private Button _buyButton = null!;
    private Button _sellButton = null!;
    private Button _casinoButton = null!;
    private Button _settingsButton = null!;
    private Button _saveQuitButton = null!;
    private Label _cashLabel = null!;
    private Label _stockAssetLabel = null!;
    private Label _holdingAssetLabel = null!;
    private Label _assetLabel = null!;
    private Label _levelLabel = null!;
    private Label _xpLabel = null!;
    private ProgressBar _xpBar = null!;
    private Label _refreshLabel = null!;
    private Label _stockNameLabel = null!;
    private Label _priceLabel = null!;
    private Label _changeLabel = null!;
    private Label _costLabel = null!;
    private Label _holdingLabel = null!;
    private Label _holdingCountLabel = null!;
    private Label _statusLabel = null!;
    private Button _itemMarketButton = null!;
    private Button _companyMarketButton = null!;
    private bool _companyMode;
    private bool _companyAutoEntered;
    private Control _transferOverlay = null!;
    private LineEdit _transferAmountInput = null!;
    private Label _transferHint = null!;
    private bool _transferToMarket = true;
    private Button _toMarketButton = null!;
    private Button _toCashButton = null!;
    private Button _transferConfirmButton = null!;
    private Control _loanOverlay = null!;
    private Label _loanDebtLabel = null!;
    private LineEdit _loanAmountInput = null!;
    private Label _loanMessage = null!;
    private Control _authOverlay = null!;
    private Label _authTitle = null!;
    private Label _authSubtitle = null!;
    private LineEdit _authPassword = null!;
    private VBoxContainer _authConfirmBox = null!;
    private LineEdit _authConfirm = null!;
    private HBoxContainer _authCaptchaRow = null!;
    private BlockTradeCaptcha _authCaptcha = null!;
    private LineEdit _authCaptchaInput = null!;
    private Label _authError = null!;
    private Button _authCancel = null!;
    private bool _authSetupMode;
    private int _pendingShares;
    private double _lastAssetValue;
    private PanelContainer _refreshEditor = null!;
    private Label _refreshValueLabel = null!;
    private LineEdit _refreshSecondsInput = null!;

    public override void _Ready()
    {
        BuildInterface();
        Visible = false;
        GetWindow().SizeChanged += UpdateResponsiveLayout;
        UpdateResponsiveLayout();
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _tickTimer is null || _simulator is null)
        {
            return;
        }

        _simulator.MarketRefreshRemaining = _tickTimer.TimeLeft;
        UpdateCountdown(Math.Max(0, (int)Math.Ceiling(_tickTimer.TimeLeft)));
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || _simulator is null || IsOverlayOpen() ||
            @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.CtrlPressed && key.ShiftPressed && key.Keycode == Key.T)
        {
            _refreshEditor.Visible = !_refreshEditor.Visible;
            if (_refreshEditor.Visible)
            {
                UpdateRefreshEditor();
                _refreshSecondsInput.GrabFocus();
                _refreshSecondsInput.SelectAll();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_refreshEditor.Visible)
        {
            return;
        }

        if (key.Keycode == Key.Up)
        {
            AdjustRefreshInterval(key.ShiftPressed ? 10 : 1);
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Down)
        {
            AdjustRefreshInterval(key.ShiftPressed ? -10 : -1);
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Escape)
        {
            _refreshEditor.Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || _simulator is null || _authOverlay.Visible || _transferOverlay.Visible ||
            _loanOverlay.Visible ||
            @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.B:
                OnBuyPressed();
                break;
            case Key.S:
                OnSellPressed();
                break;
            case Key.Key1:
                SetAmount(1);
                break;
            case Key.Key2:
                SetAmount(10);
                break;
            case Key.Key3:
                OnAllPressed();
                break;
            case Key.Tab:
                CycleStock();
                break;
            case Key.C:
                OnCasinoPressed();
                break;
            case Key.F5:
                OnManualSave();
                break;
            case Key.Escape:
                SettingsRequested?.Invoke();
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    public void Bind(BlockTradeSimulator simulator, Timer tickTimer, BlockTradeSoundManager sound)
    {
        if (!ReferenceEquals(_simulator, simulator))
        {
            Unbind();
            _simulator = simulator;
            _simulator.MarketTicked += OnMarketTicked;
            _simulator.StateChanged += OnStateChanged;
            _simulator.MarketReset += OnMarketReset;
            _simulator.TradeFailed += OnTradeFailed;
            _stockList.Bind(simulator);
            _priceChart.Bind(simulator);
        }

        _tickTimer = tickTimer;
        _sound = sound;
        _companyMode = false;
        _companyAutoEntered = false;
        _stockList.SetMarketMode(false);
        string[] names = simulator.GetUnlockedStockNames();
        if (!names.Contains(_selectedStock) && names.Length > 0)
        {
            _selectedStock = names[0];
        }

        _stockList.SelectStock(_selectedStock);
        _priceChart.SetStock(_selectedStock);
        _lastCountdownSecond = -1;
        RefreshAll();
        if (!simulator.HasTradingPassword)
        {
            ShowPasswordSetup();
        }
    }

    public void Unbind()
    {
        _tickTimer = null;
        _sound = null;
        if (_simulator is null)
        {
            return;
        }

        _simulator.MarketTicked -= OnMarketTicked;
        _simulator.StateChanged -= OnStateChanged;
        _simulator.MarketReset -= OnMarketReset;
        _simulator.TradeFailed -= OnTradeFailed;
        _stockList.Unbind();
        _priceChart.Unbind();
        _simulator = null;
    }

    public void ShowStartupStatus(bool hadSave, bool loaded)
    {
        if (loaded)
        {
            SetStatus("存档已恢复 // 刷新倒计时已接着上次继续", BlockTradeTheme.UpBright);
        }
        else if (hadSave)
        {
            SetStatus("存档损坏 // 已自动开始新游戏", BlockTradeTheme.DownBright);
        }
        else
        {
            SetStatus($"新市场 // 资产 {Money(BlockTradeSimulator.StartingMarketCash)} // 先设置交易密码", BlockTradeTheme.Gold);
        }
    }

    public void RefreshAll()
    {
        if (_simulator is null)
        {
            return;
        }

        RefreshQuote();
        RefreshPortfolio();
        RefreshStage();
        UpdateOrderPreview();
        _lastAssetValue = _simulator is null ? 0.0 : _simulator.GetStockAsset();
    }

    private void BuildInterface()
    {
        _root = new Control
        {
            Name = "BlockTradeRoot",
            Theme = BlockTradeTheme.CreateTheme(),
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new BlockTradeBackdrop { Name = "Backdrop" };
        _root.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var screen = new MarginContainer { Name = "ScreenMargin" };
        screen.AddThemeConstantOverride("margin_left", 14);
        screen.AddThemeConstantOverride("margin_right", 14);
        screen.AddThemeConstantOverride("margin_top", 12);
        screen.AddThemeConstantOverride("margin_bottom", 12);
        _root.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var page = new VBoxContainer { Name = "Page" };
        page.AddThemeConstantOverride("separation", 8);
        screen.AddChild(page);

        page.AddChild(BuildTopBar());
        page.AddChild(BuildBody());
        page.AddChild(BuildStatusBar());

        _refreshEditor = new PanelContainer
        {
            Name = "RefreshEditor",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(500, 0),
        };
        _refreshEditor.AnchorLeft = 0.5f;
        _refreshEditor.AnchorRight = 0.5f;
        _refreshEditor.AnchorTop = 0.0f;
        _refreshEditor.AnchorBottom = 0.0f;
        _refreshEditor.OffsetLeft = -270;
        _refreshEditor.OffsetRight = 270;
        _refreshEditor.OffsetTop = 70;
        _refreshEditor.OffsetBottom = 248;
        BlockTradeTheme.ApplyPanel(_refreshEditor, BlockTradeTheme.MakeBox(
            BlockTradeTheme.Panel, BlockTradeTheme.Gold, 2));
        _root.AddChild(_refreshEditor);

        var editorMargin = MakePadding(14);
        _refreshEditor.AddChild(editorMargin);
        var editorColumn = new VBoxContainer();
        editorColumn.AddThemeConstantOverride("separation", 6);
        editorMargin.AddChild(editorColumn);
        Label editorTitle = MakeLabel("刷新时间（秒）", 14, BlockTradeTheme.Gold);
        editorTitle.HorizontalAlignment = HorizontalAlignment.Center;
        editorColumn.AddChild(editorTitle);
        _refreshValueLabel = MakeLabel("120 秒", 24, BlockTradeTheme.Text, "RefreshValueLabel");
        _refreshValueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        editorColumn.AddChild(_refreshValueLabel);

        var inputRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        inputRow.AddThemeConstantOverride("separation", 8);
        _refreshSecondsInput = new LineEdit
        {
            Name = "RefreshSecondsInput",
            Text = "120",
            PlaceholderText = "秒",
            MaxLength = 3,
            SelectAllOnFocus = true,
            Alignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(120, 38),
        };
        _refreshSecondsInput.AddThemeFontSizeOverride("font_size", 16);
        _refreshSecondsInput.TextSubmitted += OnRefreshSecondsSubmitted;
        inputRow.AddChild(_refreshSecondsInput);
        inputRow.AddChild(MakeLabel("秒", 14, BlockTradeTheme.TextMuted));
        Button apply = new Button
        {
            Name = "RefreshApplyBtn",
            Text = "应用",
            CustomMinimumSize = new Vector2(90, 38),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(apply, BlockTradeButtonTone.Gold);
        apply.Pressed += OnRefreshApplyPressed;
        inputRow.AddChild(apply);
        editorColumn.AddChild(inputRow);

        Label editorHint = MakeLabel("↑ / ↓ 1 秒    SHIFT+↑ / ↓ 10 秒    ESC 关闭", 12, BlockTradeTheme.TextMuted);
        editorHint.HorizontalAlignment = HorizontalAlignment.Center;
        editorColumn.AddChild(editorHint);

        _transferOverlay = new Control
        {
            Name = "TransferOverlay",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.AddChild(_transferOverlay);
        _transferOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.72f) };
        _transferOverlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var transferPanel = new PanelContainer
        {
            Name = "TransferPanel",
            CustomMinimumSize = new Vector2(460, 280),
        };
        BlockTradeTheme.ApplyPanel(transferPanel, BlockTradeTheme.MakeBox(
            BlockTradeTheme.Panel, BlockTradeTheme.Gold, 2));
        _transferOverlay.AddChild(transferPanel);
        transferPanel.AnchorLeft = 0.5f;
        transferPanel.AnchorRight = 0.5f;
        transferPanel.AnchorTop = 0.5f;
        transferPanel.AnchorBottom = 0.5f;
        transferPanel.OffsetLeft = -230;
        transferPanel.OffsetRight = 230;
        transferPanel.OffsetTop = -140;
        transferPanel.OffsetBottom = 140;

        var transferMargin = MakePadding(16);
        transferPanel.AddChild(transferMargin);
        var transferColumn = new VBoxContainer();
        transferColumn.AddThemeConstantOverride("separation", 8);
        transferMargin.AddChild(transferColumn);
        Label transferTitle = MakeLabel("现金 / 资产互转", 18, BlockTradeTheme.Gold);
        transferTitle.HorizontalAlignment = HorizontalAlignment.Center;
        transferColumn.AddChild(transferTitle);

        var directionRow = new HBoxContainer();
        directionRow.AddThemeConstantOverride("separation", 8);
        _toMarketButton = new Button
        {
            Name = "ToMarketBtn",
            Text = "现金 → 资产",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 34),
        };
        BlockTradeTheme.ApplyButton(_toMarketButton, BlockTradeButtonTone.Gold);
        _toMarketButton.Pressed += () => SetTransferDirection(true);
        directionRow.AddChild(_toMarketButton);
        _toCashButton = new Button
        {
            Name = "ToCashBtn",
            Text = "资产 → 现金",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 34),
        };
        BlockTradeTheme.ApplyButton(_toCashButton, BlockTradeButtonTone.Neutral);
        _toCashButton.Pressed += () => SetTransferDirection(false);
        directionRow.AddChild(_toCashButton);
        transferColumn.AddChild(directionRow);

        _transferHint = MakeLabel("", 12, BlockTradeTheme.TextMuted);
        _transferHint.HorizontalAlignment = HorizontalAlignment.Center;
        transferColumn.AddChild(_transferHint);
        _transferAmountInput = new LineEdit
        {
            Name = "TransferAmountInput",
            PlaceholderText = "金额",
            Alignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _transferAmountInput.AddThemeFontSizeOverride("font_size", 16);
        transferColumn.AddChild(_transferAmountInput);
        var transferButtons = new HBoxContainer();
        transferButtons.AddThemeConstantOverride("separation", 8);
        Button transferConfirm = new Button
        {
            Name = "TransferConfirmBtn",
            Text = "确认转入",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _transferConfirmButton = transferConfirm;
        BlockTradeTheme.ApplyButton(transferConfirm, BlockTradeButtonTone.Gold);
        transferConfirm.Pressed += OnTransferConfirmed;
        transferButtons.AddChild(transferConfirm);
        Button transferCancel = new Button
        {
            Name = "TransferCancelBtn",
            Text = "取消",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38),
        };
        BlockTradeTheme.ApplyButton(transferCancel, BlockTradeButtonTone.Neutral);
        transferCancel.Pressed += HideTransferDialog;
        transferButtons.AddChild(transferCancel);
        transferColumn.AddChild(transferButtons);

        _loanOverlay = new Control
        {
            Name = "LoanOverlay",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.AddChild(_loanOverlay);
        _loanOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var loanShade = new ColorRect { Color = new Color(0, 0, 0, 0.72f) };
        _loanOverlay.AddChild(loanShade);
        loanShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var loanPanel = new PanelContainer
        {
            Name = "LoanPanel",
            CustomMinimumSize = new Vector2(460, 260),
        };
        BlockTradeTheme.ApplyPanel(loanPanel, BlockTradeTheme.MakeBox(
            BlockTradeTheme.Panel, BlockTradeTheme.Gold, 2));
        _loanOverlay.AddChild(loanPanel);
        loanPanel.AnchorLeft = 0.5f;
        loanPanel.AnchorRight = 0.5f;
        loanPanel.AnchorTop = 0.5f;
        loanPanel.AnchorBottom = 0.5f;
        loanPanel.OffsetLeft = -230;
        loanPanel.OffsetRight = 230;
        loanPanel.OffsetTop = -130;
        loanPanel.OffsetBottom = 130;

        var loanMargin = MakePadding(16);
        loanPanel.AddChild(loanMargin);
        var loanColumn = new VBoxContainer();
        loanColumn.AddThemeConstantOverride("separation", 8);
        loanMargin.AddChild(loanColumn);
        Label loanTitle = MakeLabel("银行贷款", 18, BlockTradeTheme.Gold);
        loanTitle.HorizontalAlignment = HorizontalAlignment.Center;
        loanColumn.AddChild(loanTitle);
        _loanDebtLabel = MakeLabel("", 13, BlockTradeTheme.Text);
        _loanDebtLabel.HorizontalAlignment = HorizontalAlignment.Center;
        loanColumn.AddChild(_loanDebtLabel);
        _loanMessage = MakeLabel("最高可贷 $50,000,000", 12, BlockTradeTheme.TextMuted);
        _loanMessage.HorizontalAlignment = HorizontalAlignment.Center;
        loanColumn.AddChild(_loanMessage);
        _loanAmountInput = new LineEdit
        {
            Name = "LoanAmountInput",
            PlaceholderText = "贷款金额",
            Alignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _loanAmountInput.AddThemeFontSizeOverride("font_size", 16);
        loanColumn.AddChild(_loanAmountInput);
        var loanButtons = new HBoxContainer();
        loanButtons.AddThemeConstantOverride("separation", 8);
        Button borrowButton = new Button
        {
            Name = "BorrowBtn",
            Text = "借入",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38),
        };
        BlockTradeTheme.ApplyButton(borrowButton, BlockTradeButtonTone.Gold);
        borrowButton.Pressed += OnBorrowPressed;
        loanButtons.AddChild(borrowButton);
        Button repayButton = new Button
        {
            Name = "RepayBtn",
            Text = "还款",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38),
        };
        BlockTradeTheme.ApplyButton(repayButton, BlockTradeButtonTone.Green);
        repayButton.Pressed += OnRepayPressed;
        loanButtons.AddChild(repayButton);
        Button loanClose = new Button
        {
            Name = "LoanCloseBtn",
            Text = "关闭",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38),
        };
        BlockTradeTheme.ApplyButton(loanClose, BlockTradeButtonTone.Neutral);
        loanClose.Pressed += HideLoanDialog;
        loanButtons.AddChild(loanClose);
        loanColumn.AddChild(loanButtons);

        _authOverlay = new Control
        {
            Name = "AuthOverlay",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.AddChild(_authOverlay);
        _authOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var authShade = new ColorRect { Color = new Color(0, 0, 0, 0.78f) };
        _authOverlay.AddChild(authShade);
        authShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var authPanel = new PanelContainer
        {
            Name = "AuthPanel",
            CustomMinimumSize = new Vector2(500, 390),
        };
        BlockTradeTheme.ApplyPanel(authPanel, BlockTradeTheme.MakeBox(
            BlockTradeTheme.Panel, BlockTradeTheme.Gold, 2));
        _authOverlay.AddChild(authPanel);
        authPanel.AnchorLeft = 0.5f;
        authPanel.AnchorRight = 0.5f;
        authPanel.AnchorTop = 0.5f;
        authPanel.AnchorBottom = 0.5f;
        authPanel.OffsetLeft = -250;
        authPanel.OffsetRight = 250;
        authPanel.OffsetTop = -195;
        authPanel.OffsetBottom = 195;

        var authMargin = MakePadding(18);
        authPanel.AddChild(authMargin);
        var authColumn = new VBoxContainer();
        authColumn.AddThemeConstantOverride("separation", 7);
        authMargin.AddChild(authColumn);

        _authTitle = MakeLabel("设置交易密码", 20, BlockTradeTheme.Gold);
        _authTitle.HorizontalAlignment = HorizontalAlignment.Center;
        authColumn.AddChild(_authTitle);
        _authSubtitle = MakeLabel("", 12, BlockTradeTheme.TextMuted);
        _authSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _authSubtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        authColumn.AddChild(_authSubtitle);
        authColumn.AddChild(Separator());

        authColumn.AddChild(MakeLabel("交易密码（6 位数字）", 12, BlockTradeTheme.TextMuted));
        _authPassword = new LineEdit
        {
            Name = "AuthPasswordInput",
            PlaceholderText = "6 位数字",
            Secret = true,
            MaxLength = 6,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _authPassword.AddThemeFontSizeOverride("font_size", 16);
        authColumn.AddChild(_authPassword);

        _authConfirmBox = new VBoxContainer();
        _authConfirmBox.AddThemeConstantOverride("separation", 4);
        _authConfirmBox.AddChild(MakeLabel("再次输入密码", 12, BlockTradeTheme.TextMuted));
        _authConfirm = new LineEdit
        {
            Name = "AuthConfirmInput",
            PlaceholderText = "再次输入",
            Secret = true,
            MaxLength = 6,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _authConfirm.AddThemeFontSizeOverride("font_size", 16);
        _authConfirmBox.AddChild(_authConfirm);
        authColumn.AddChild(_authConfirmBox);

        _authCaptchaRow = new HBoxContainer();
        _authCaptchaRow.AddThemeConstantOverride("separation", 8);
        _authCaptcha = new BlockTradeCaptcha { Name = "CaptchaImage" };
        _authCaptchaRow.AddChild(_authCaptcha);
        _authCaptchaInput = new LineEdit
        {
            Name = "AuthCaptchaInput",
            PlaceholderText = "图片验证码",
            MaxLength = 4,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 44),
        };
        _authCaptchaInput.AddThemeFontSizeOverride("font_size", 16);
        _authCaptchaRow.AddChild(_authCaptchaInput);
        authColumn.AddChild(_authCaptchaRow);

        _authError = MakeLabel("", 12, BlockTradeTheme.DownBright);
        _authError.HorizontalAlignment = HorizontalAlignment.Center;
        authColumn.AddChild(_authError);

        var authButtons = new HBoxContainer();
        authButtons.AddThemeConstantOverride("separation", 8);
        Button authConfirm = new Button
        {
            Name = "AuthConfirmBtn",
            Text = "确认",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40),
        };
        BlockTradeTheme.ApplyButton(authConfirm, BlockTradeButtonTone.Gold);
        authConfirm.Pressed += OnAuthConfirmed;
        authButtons.AddChild(authConfirm);
        _authCancel = new Button
        {
            Name = "AuthCancelBtn",
            Text = "取消",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40),
        };
        BlockTradeTheme.ApplyButton(_authCancel, BlockTradeButtonTone.Neutral);
        _authCancel.Pressed += HideAuthDialog;
        authButtons.AddChild(_authCancel);
        authColumn.AddChild(authButtons);
    }

    private Control BuildTopBar()
    {
        PanelContainer panel = MakePanel("TopBar");
        var margin = MakePadding(8);
        panel.AddChild(margin);

        var row = new HBoxContainer { Name = "TopBarRow" };
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        row.AddChild(MakeLabel("BLOCKTRADE", 18, BlockTradeTheme.Gold));
        row.AddChild(SeparatorVertical());

        var stageBox = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        stageBox.AddThemeConstantOverride("separation", 6);
        _levelLabel = MakeLabel("阶段 1", 13, BlockTradeTheme.Text, "LevelLabel");
        stageBox.AddChild(_levelLabel);
        _xpBar = new ProgressBar
        {
            Name = "XpBar",
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(80, 10),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        StyleBoxFlat xpBg = BlockTradeTheme.MakeBox(BlockTradeTheme.PanelDark, BlockTradeTheme.BorderDark, 1);
        StyleBoxFlat xpFill = BlockTradeTheme.MakeBox(BlockTradeTheme.Up, BlockTradeTheme.Up, 0);
        _xpBar.AddThemeStyleboxOverride("background", xpBg);
        _xpBar.AddThemeStyleboxOverride("fill", xpFill);
        stageBox.AddChild(_xpBar);
        _xpLabel = MakeLabel("80 XP", 11, BlockTradeTheme.TextMuted);
        stageBox.AddChild(_xpLabel);
        row.AddChild(stageBox);
        row.AddChild(Spacer());

        _cashLabel = MakeLabel("现金 $2,000.00", 12, BlockTradeTheme.Gold, "CashLabel");
        row.AddChild(_cashLabel);
        _stockAssetLabel = MakeLabel("资产 $2,000.00", 12, BlockTradeTheme.Text, "StockAssetLabel");
        _stockAssetLabel.TooltipText = "可用股市资金，买入股票会从这里扣款";
        row.AddChild(_stockAssetLabel);
        _holdingAssetLabel = MakeLabel("持仓 $0.00", 12, BlockTradeTheme.TextMuted, "HoldingAssetLabel");
        _holdingAssetLabel.TooltipText = "持仓股票按当前价计算的市值，随行情涨跌";
        row.AddChild(_holdingAssetLabel);
        _assetLabel = MakeLabel("总资产 $2,000.00", 12, BlockTradeTheme.Text);
        row.AddChild(_assetLabel);

        Button loanButton = MakeSmallButton("LoanBtn", "贷款", BlockTradeButtonTone.Neutral);
        loanButton.TooltipText = "最高可贷 50,000,000";
        loanButton.Pressed += ShowLoanDialog;
        row.AddChild(loanButton);

        Button transferButton = MakeSmallButton("TransferBtn", "转入", BlockTradeButtonTone.Gold);
        transferButton.TooltipText = "现金与股市资产互转";
        transferButton.Pressed += ShowTransferDialog;
        row.AddChild(transferButton);

        _casinoButton = MakeSmallButton("CasinoBtn", "玩法", BlockTradeButtonTone.Gold);
        _casinoButton.Pressed += OnCasinoPressed;
        row.AddChild(_casinoButton);

        _settingsButton = MakeSmallButton("SettingsBtn", "设置", BlockTradeButtonTone.Neutral);
        _settingsButton.Pressed += () => SettingsRequested?.Invoke();
        row.AddChild(_settingsButton);

        _saveQuitButton = MakeSmallButton("SaveQuitBtn", "保存退出", BlockTradeButtonTone.Red);
        _saveQuitButton.Pressed += OnSaveQuitPressed;
        row.AddChild(_saveQuitButton);

        return panel;
    }

    private Control BuildBody()
    {
        var body = new HBoxContainer
        {
            Name = "Body",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        body.AddChild(BuildMarketPanel());
        body.AddChild(BuildDetailPanel());
        return body;
    }

    private Control BuildMarketPanel()
    {
        PanelContainer panel = MakePanel("MarketPanel");
        panel.CustomMinimumSize = new Vector2(320, 0);
        var margin = MakePadding(10);
        panel.AddChild(margin);

        var column = new VBoxContainer { Name = "MarketColumn" };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddChild(MakeLabel("股市", 18, BlockTradeTheme.Text));
        header.AddChild(Spacer());
        _refreshLabel = MakeLabel("02:00 后刷新", 14, BlockTradeTheme.Gold, "RefreshLabel");
        header.AddChild(_refreshLabel);
        column.AddChild(header);
        column.AddChild(Separator());

        var marketTabs = new HBoxContainer { Name = "MarketTabs" };
        marketTabs.AddThemeConstantOverride("separation", 6);
        _itemMarketButton = MakeSmallButton("ItemMarketBtn", "方块市场", BlockTradeButtonTone.Gold);
        _itemMarketButton.CustomMinimumSize = new Vector2(0, 30);
        _itemMarketButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _itemMarketButton.Pressed += () => SwitchMarket(false);
        marketTabs.AddChild(_itemMarketButton);
        _companyMarketButton = MakeSmallButton("CompanyMarketBtn", "公司股市", BlockTradeButtonTone.Neutral);
        _companyMarketButton.CustomMinimumSize = new Vector2(0, 30);
        _companyMarketButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _companyMarketButton.Disabled = true;
        _companyMarketButton.TooltipText = "阶段 4 解锁公司股市";
        _companyMarketButton.Pressed += () => SwitchMarket(true);
        marketTabs.AddChild(_companyMarketButton);
        column.AddChild(marketTabs);

        _stockList = new BlockTradeStockList
        {
            Name = "StockList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 300),
        };
        _stockList.StockSelected += OnStockSelectedFromList;
        column.AddChild(_stockList);

        column.AddChild(Separator());
        var holdingsHeader = new HBoxContainer();
        holdingsHeader.AddChild(MakeLabel("持仓", 14, BlockTradeTheme.Text));
        holdingsHeader.AddChild(Spacer());
        _holdingCountLabel = MakeLabel("0 个持仓", 14, BlockTradeTheme.TextDim, "HoldingCountLabel");
        holdingsHeader.AddChild(_holdingCountLabel);
        column.AddChild(holdingsHeader);

        _holdingsList = new ItemList
        {
            Name = "HoldingsList",
            CustomMinimumSize = new Vector2(0, 86),
            SelectMode = ItemList.SelectModeEnum.Single,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        BlockTradeTheme.ApplyItemList(_holdingsList);
        _holdingsList.AddThemeFontSizeOverride("font_size", 14);
        column.AddChild(_holdingsList);

        return panel;
    }

    private Control BuildDetailPanel()
    {
        PanelContainer panel = MakePanel("DetailPanel");
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var margin = MakePadding(14);
        panel.AddChild(margin);

        var column = new VBoxContainer { Name = "DetailColumn" };
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        var header = new HBoxContainer { Name = "StockHeader" };
        header.AddThemeConstantOverride("separation", 14);
        header.CustomMinimumSize = new Vector2(0, 76);
        column.AddChild(header);

        _stockIcon = new BlockTradeStockIcon
        {
            Name = "StockIcon",
            StockName = "铁锭",
            CustomMinimumSize = new Vector2(66, 66),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        header.AddChild(_stockIcon);

        _stockNameLabel = MakeLabel("铁锭", 30, BlockTradeTheme.Text, "StockNameLabel");
        _stockNameLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(_stockNameLabel);
        header.AddChild(Spacer());

        var priceBlock = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        priceBlock.AddThemeConstantOverride("separation", 2);
        _priceLabel = MakeLabel("$120.00", 26, BlockTradeTheme.Text, "PriceLabel");
        _priceLabel.HorizontalAlignment = HorizontalAlignment.Right;
        priceBlock.AddChild(_priceLabel);
        _changeLabel = MakeLabel("+0.00%", 14, BlockTradeTheme.Gold, "ChangeLabel");
        _changeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        priceBlock.AddChild(_changeLabel);
        header.AddChild(priceBlock);
        column.AddChild(Separator());

        _priceChart = new BlockTradePriceChart
        {
            Name = "PriceChart",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 250),
        };
        column.AddChild(_priceChart);

        var infoRow = new HBoxContainer { Name = "OrderInfo" };
        infoRow.AddThemeConstantOverride("separation", 16);
        _costLabel = MakeLabel("预计花费：$120.00", 14, BlockTradeTheme.Gold, "CostLabel");
        infoRow.AddChild(_costLabel);
        infoRow.AddChild(Spacer());
        _holdingLabel = MakeLabel("持有：0 股", 14, BlockTradeTheme.TextMuted, "HoldingLabel");
        infoRow.AddChild(_holdingLabel);
        column.AddChild(infoRow);
        column.AddChild(Separator());

        var tradingPanel = new HBoxContainer
        {
            Name = "TradingPanel",
            CustomMinimumSize = new Vector2(0, 66),
        };
        tradingPanel.AddThemeConstantOverride("separation", 8);
        column.AddChild(tradingPanel);

        var amountBox = new VBoxContainer { CustomMinimumSize = new Vector2(106, 0) };
        amountBox.AddThemeConstantOverride("separation", 3);
        amountBox.AddChild(MakeLabel("数量", 14, BlockTradeTheme.TextDim));
        _amountInput = new LineEdit
        {
            Name = "AmountInput",
            Text = "1",
            PlaceholderText = "数量",
            MaxLength = 9,
            SelectAllOnFocus = true,
            Alignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(106, 38),
        };
        _amountInput.AddThemeFontSizeOverride("font_size", 16);
        _amountInput.TextChanged += _ => UpdateOrderPreview();
        _amountInput.TextSubmitted += _ => OnBuyPressed();
        amountBox.AddChild(_amountInput);
        tradingPanel.AddChild(amountBox);

        Button quick1 = MakeSmallButton("Quick1Btn", "1 股", BlockTradeButtonTone.Neutral);
        quick1.CustomMinimumSize = new Vector2(62, 38);
        quick1.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        quick1.Pressed += () => SetAmount(1);
        tradingPanel.AddChild(quick1);

        Button quick10 = MakeSmallButton("Quick10Btn", "10 股", BlockTradeButtonTone.Neutral);
        quick10.CustomMinimumSize = new Vector2(70, 38);
        quick10.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        quick10.Pressed += () => SetAmount(10);
        tradingPanel.AddChild(quick10);

        Button quickAll = MakeSmallButton("QuickAllBtn", "全部", BlockTradeButtonTone.Neutral);
        quickAll.CustomMinimumSize = new Vector2(66, 38);
        quickAll.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        quickAll.Pressed += OnAllPressed;
        tradingPanel.AddChild(quickAll);

        _buyButton = MakeActionButton("BuyBtn", "买入 铁锭", BlockTradeButtonTone.Green);
        _buyButton.Pressed += OnBuyPressed;
        tradingPanel.AddChild(_buyButton);

        _sellButton = MakeActionButton("SellBtn", "卖出 铁锭", BlockTradeButtonTone.Red);
        _sellButton.Pressed += OnSellPressed;
        tradingPanel.AddChild(_sellButton);

        return panel;
    }

    private Control BuildStatusBar()
    {
        PanelContainer panel = MakePanel("StatusBar");
        var margin = MakePadding(7);
        panel.AddChild(margin);

        _statusLabel = MakeLabel("准备开始交易", 14, BlockTradeTheme.TextMuted, "StatusLabel");
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.AddChild(_statusLabel);
        return panel;
    }

    private void OnStockSelectedFromList(string ticker)
    {
        if (_simulator is null || !CurrentMarketNames.Contains(ticker))
        {
            return;
        }

        _selectedStock = ticker;
        _priceChart.SetStock(ticker);
        UpdateQuoteLabels();
        RefreshPortfolio();
        UpdateOrderPreview();
    }

    private void CycleStock()
    {
        if (_simulator is null)
        {
            return;
        }

        string[] names = CurrentMarketNames;
        if (names.Length == 0)
        {
            return;
        }

        int index = Array.IndexOf(names, _selectedStock);
        _selectedStock = names[(index + 1) % names.Length];
        _stockList.SelectStock(_selectedStock);
        _priceChart.SetStock(_selectedStock);
        UpdateQuoteLabels();
        RefreshPortfolio();
        UpdateOrderPreview();
    }

    private void OnBuyPressed()
    {
        if (_simulator is null || !TryReadAmount(out int shares))
        {
            return;
        }

        if (!_simulator.HasTradingPassword)
        {
            ShowPasswordSetup();
            return;
        }

        ShowTradeAuth(shares);
    }

    private void ShowPasswordSetup()
    {
        _authSetupMode = true;
        _pendingShares = 0;
        _authOverlay.Visible = true;
        _authTitle.Text = "设置交易密码";
        _authSubtitle.Text = "首次进入股市前必须设置 6 位数字交易密码。\n交易密码会加密保存，现金和资产分开管理。";
        _authConfirmBox.Visible = true;
        _authCaptchaRow.Visible = false;
        _authCancel.Visible = false;
        _authError.Text = "";
        _authPassword.Text = "";
        _authConfirm.Text = "";
        _authPassword.GrabFocus();
    }

    private void ShowTradeAuth(int shares)
    {
        if (_simulator is null)
        {
            return;
        }

        if (!_simulator.HasTradingPassword)
        {
            ShowPasswordSetup();
            return;
        }

        _authSetupMode = false;
        _pendingShares = shares;
        _authOverlay.Visible = true;
        _authTitle.Text = "交易验证";
        _authSubtitle.Text = "投入股市前需要交易密码和图片验证码验证。";
        _authConfirmBox.Visible = false;
        _authCaptchaRow.Visible = true;
        _authCancel.Visible = true;
        _authError.Text = "";
        _authPassword.Text = "";
        _authCaptchaInput.Text = "";
        _authCaptcha.Refresh();
        _authPassword.GrabFocus();
    }

    private void HideAuthDialog()
    {
        if (_authSetupMode)
        {
            return;
        }

        _authOverlay.Visible = false;
    }

    private void OnAuthConfirmed()
    {
        if (_simulator is null)
        {
            return;
        }

        if (_authSetupMode)
        {
            string password = _authPassword.Text.Trim();
            if (password.Length != 6 || !password.All(char.IsDigit))
            {
                _sound?.Play(BlockTradeSound.Error);
                _authError.Text = "交易密码必须是 6 位数字";
                return;
            }

            if (password != _authConfirm.Text.Trim())
            {
                _sound?.Play(BlockTradeSound.Error);
                _authError.Text = "两次输入的密码不一致";
                return;
            }

            if (!_simulator.SetTradingPassword(password))
            {
                _sound?.Play(BlockTradeSound.Error);
                _authError.Text = "交易密码设置失败";
                return;
            }

            _sound?.Play(BlockTradeSound.Auth);
            _authOverlay.Visible = false;
            SaveGame();
            SetStatus("交易密码已设置 // 交易时会要求验证", BlockTradeTheme.Gold);
            return;
        }

        if (!_simulator.VerifyTradingPassword(_authPassword.Text.Trim()))
        {
            _sound?.Play(BlockTradeSound.Error);
            _authError.Text = "交易密码错误";
            _authPassword.Text = "";
            _authPassword.GrabFocus();
            return;
        }

        if (!_authCaptcha.Matches(_authCaptchaInput.Text))
        {
            _sound?.Play(BlockTradeSound.Error);
            _authError.Text = "图片验证码错误，已刷新";
            _authCaptchaInput.Text = "";
            _authCaptcha.Refresh();
            _authCaptchaInput.GrabFocus();
            return;
        }

        _sound?.Play(BlockTradeSound.Auth);
        int shares = _pendingShares;
        _authOverlay.Visible = false;
        ExecuteBuy(shares);
    }

    private void ExecuteBuy(int shares)
    {
        if (_simulator is null)
        {
            return;
        }

        string stock = _selectedStock;
        double cost = _simulator.GetPrice(stock) * shares;
        if (!_simulator.Buy(stock, shares))
        {
            return;
        }

        BlockTradeDeltaPopup.Show(_root, _stockAssetLabel, -cost);
        _sound?.Play(BlockTradeSound.Buy);
        if (_simulator.GetMarketCash() < 0.0)
        {
            _sound?.Play(BlockTradeSound.Negative);
        }
        bool saved = SaveGame();
        RefreshAll();
        SetStatus(saved ? $"买入 {shares} 股 {stock} // 已存档" : $"买入 {shares} 股 {stock} // 存档失败",
            saved ? BlockTradeTheme.UpBright : BlockTradeTheme.DownBright);
    }

    private void OnSellPressed()
    {
        if (_simulator is null || !TryReadAmount(out int shares))
        {
            return;
        }

        string stock = _selectedStock;
        double proceeds = _simulator.GetPrice(stock) * shares;
        if (!_simulator.Sell(stock, shares))
        {
            return;
        }

        BlockTradeDeltaPopup.Show(_root, _stockAssetLabel, proceeds);
        _sound?.Play(BlockTradeSound.Sell);
        bool saved = SaveGame();
        RefreshAll();
        SetStatus(saved ? $"卖出 {shares} 股 {stock} // 已存档" : $"卖出 {shares} 股 {stock} // 存档失败",
            saved ? BlockTradeTheme.UpBright : BlockTradeTheme.DownBright);
    }

    private void OnAllPressed()
    {
        if (_simulator is null)
        {
            return;
        }

        if (_simulator.TryGetHolding(_selectedStock, out int held, out _) && held > 0)
        {
            SetAmount(held);
            return;
        }

        double price = _simulator.GetPrice(_selectedStock);
        int affordable = price <= 0 ? 0 : (int)Math.Floor(_simulator.GetMarketCash() / price);
        SetAmount(Math.Max(1, affordable));
    }

    private void SetAmount(int shares)
    {
        _amountInput.Text = Math.Max(1, shares).ToString(CultureInfo.InvariantCulture);
        _amountInput.CaretColumn = _amountInput.Text.Length;
        UpdateOrderPreview();
    }

    private void OnCasinoPressed()
    {
        if (_simulator is null)
        {
            return;
        }

        if (_simulator.Level < 2)
        {
            SetStatus("阶段 2 解锁第一个付费玩法 // 先完成几笔交易", BlockTradeTheme.DownBright);
            return;
        }

        CasinoRequested?.Invoke();
    }

    private void OnManualSave()
    {
        if (_simulator is null)
        {
            return;
        }

        bool saved = SaveGame();
        if (saved)
        {
            _sound?.Play(BlockTradeSound.Save);
        }
        SetStatus(saved ? "游戏已保存" : "保存失败 // 请查看控制台",
            saved ? BlockTradeTheme.UpBright : BlockTradeTheme.DownBright);
    }

    private void OnSaveQuitPressed()
    {
        if (_simulator is null)
        {
            return;
        }

        if (!SaveGame())
        {
            SetStatus("保存失败 // 已取消退出", BlockTradeTheme.DownBright);
            return;
        }

        SetStatus("游戏已保存 // 正在退出", BlockTradeTheme.UpBright);
        GetTree().Quit();
    }

    private bool SaveGame()
    {
        if (_simulator is null)
        {
            return false;
        }

        if (_tickTimer is not null)
        {
            _simulator.MarketRefreshRemaining = _tickTimer.TimeLeft;
        }

        return _simulator.SaveGame();
    }

    private bool TryReadAmount(out int shares)
    {
        string raw = _amountInput.Text.Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out shares) ||
            shares <= 0)
        {
            shares = 0;
            _sound?.Play(BlockTradeSound.Error);
            SetStatus("数量无效 // 请输入大于 0 的整数", BlockTradeTheme.DownBright);
            _amountInput.GrabFocus();
            return false;
        }

        return true;
    }

    private void OnMarketTicked()
    {
        if (_simulator is null)
        {
            return;
        }

        double previousAsset = _lastAssetValue;
        RefreshQuote();
        RefreshPortfolio();
        double currentAsset = _simulator.GetStockAsset();
        BlockTradeDeltaPopup.Show(_root, _holdingAssetLabel, currentAsset - previousAsset);
        _lastAssetValue = currentAsset;
    }

    public bool CompanyMode => _companyMode;

    public void NotifyAutoSave(bool saved)
    {
        if (!Visible)
        {
            return;
        }

        SetStatus(saved ? "行情已刷新 // 已自动保存" : "行情已刷新 // 自动保存失败",
            saved ? BlockTradeTheme.TextMuted : BlockTradeTheme.DownBright);
    }

    private string[] CurrentMarketNames => _simulator is null
        ? Array.Empty<string>()
        : _companyMode
            ? _simulator.GetCompanyNames()
            : _simulator.GetUnlockedStockNames();

    private void SwitchMarket(bool company, bool announce = true)
    {
        if (_simulator is null)
        {
            return;
        }

        if (company && !_simulator.IsMaxLevel)
        {
            SetStatus("阶段 4 解锁公司股市", BlockTradeTheme.Gold);
            return;
        }

        string[] names = company ? _simulator.GetCompanyNames() : _simulator.GetStockNames();
        if (names.Length == 0)
        {
            return;
        }

        _companyMode = company;
        _stockList.SetMarketMode(company);
        _selectedStock = names[0];
        _stockList.SelectStock(_selectedStock);
        _priceChart.SetStock(_selectedStock);
        _stockIcon.StockName = _selectedStock;
        UpdateMarketTabs();
        RefreshAll();
        if (company)
        {
            _sound?.Play(BlockTradeSound.Company);
        }

        if (announce)
        {
            SetStatus(company
                    ? "公司股市 // 单笔金额巨大，注意风险"
                    : "方块市场 // 小额商品交易",
                company ? BlockTradeTheme.Gold : BlockTradeTheme.TextMuted);
        }
    }

    private void UpdateMarketTabs()
    {
        if (_simulator is null)
        {
            return;
        }

        _companyMarketButton.Disabled = !_simulator.IsMaxLevel;
        BlockTradeTheme.ApplyButton(_itemMarketButton,
            _companyMode ? BlockTradeButtonTone.Neutral : BlockTradeButtonTone.Gold);
        BlockTradeTheme.ApplyButton(_companyMarketButton,
            _companyMode ? BlockTradeButtonTone.Gold : BlockTradeButtonTone.Neutral);
    }

    private void OnStateChanged()
    {
        RefreshAll();
    }

    private void OnMarketReset()
    {
        string[] names = CurrentMarketNames;
        if (!names.Contains(_selectedStock) && names.Length > 0)
        {
            _selectedStock = names[0];
        }

        _stockList.SelectStock(_selectedStock);
        _priceChart.SetStock(_selectedStock);
        RefreshAll();
    }

    private void OnTradeFailed(string reason)
    {
        _sound?.Play(BlockTradeSound.Error);
        string message = reason switch
        {
            "INVALID_ORDER" => "数量无效 // 请输入大于 0 的整数",
            "UNKNOWN_STOCK" => "未知商品",
            "NOT_ENOUGH_CASH" => "金币不足 // 订单取消",
            "NOT_ENOUGH_MARKET_CASH" => "股市资产不足 // 现金不能投入股市",
            "NOT_ENOUGH_SHARES" => "持仓不足 // 订单取消",
            _ => "订单被拒绝",
        };
        SetStatus(message, BlockTradeTheme.DownBright);
    }

    private void RefreshQuote()
    {
        if (_simulator is null)
        {
            return;
        }

        string[] names = CurrentMarketNames;
        if (names.Length == 0 || !names.Contains(_selectedStock))
        {
            if (names.Length > 0)
            {
                _selectedStock = names[0];
            }
            _stockList.SelectStock(_selectedStock);
        }

        _priceChart.SetStock(_selectedStock);
        UpdateQuoteLabels();
        UpdateOrderPreview();
    }

    private void UpdateQuoteLabels()
    {
        if (_simulator is null)
        {
            return;
        }

        double price = _simulator.GetPrice(_selectedStock);
        double change = _simulator.GetChangePercent(_selectedStock);
        Color tone = change > 0.0001
            ? BlockTradeTheme.MarketUp
            : change < -0.0001
                ? BlockTradeTheme.MarketDown
                : BlockTradeTheme.Gold;

        _stockIcon.StockName = _selectedStock;
        _stockNameLabel.Text = _selectedStock;
        _priceLabel.Text = Money(price);
        _changeLabel.Text = $"{change:+0.00;-0.00;0.00}%";
        _changeLabel.AddThemeColorOverride("font_color", tone);
    }

    private void UpdateResponsiveLayout()
    {
        // Keep cash + stock asset visible at narrower windows; total assets is
        // secondary and yields space to the buttons.
        _assetLabel.Visible = GetWindow().Size.X >= 1200;
    }

    private void RefreshPortfolio()
    {
        if (_simulator is null)
        {
            return;
        }

        _cashLabel.Text = $"现金 {Money(_simulator.GetCash())}";
        _stockAssetLabel.Text = $"资产 {Money(_simulator.GetMarketCash())}";
        _stockAssetLabel.AddThemeColorOverride("font_color",
            _simulator.MarketCash < 0.0 ? BlockTradeTheme.MarketDown : BlockTradeTheme.Text);
        _holdingAssetLabel.Text = $"持仓 {Money(_simulator.GetStockAsset())}";
        _assetLabel.Text = $"总资产 {Money(_simulator.GetTotalAsset())}";

        _holdingsList.Clear();
        int positions = 0;
        foreach (string ticker in _simulator.GetAllStockNames())
        {
            if (!_simulator.TryGetHolding(ticker, out int shares, out double avgCost) || shares <= 0)
            {
                continue;
            }

            _holdingsList.AddItem($"{ticker} x{shares}  @{Money(avgCost)}");
            positions++;
        }

        if (positions == 0)
        {
            _holdingsList.AddItem("暂无持仓");
        }
        _holdingCountLabel.Text = $"{positions} 个持仓";

        if (_simulator.TryGetHolding(_selectedStock, out int held, out double avg))
        {
            _holdingLabel.Text = $"持有：{held} 股 / 均价 {Money(avg)}";
        }
        else
        {
            _holdingLabel.Text = "持有：0 股";
        }
    }

    private void RefreshStage()
    {
        if (_simulator is null)
        {
            return;
        }

        _levelLabel.Text = _simulator.IsMaxLevel
            ? $"阶段 {_simulator.Level} MAX"
            : $"阶段 {_simulator.Level}";
        _xpBar.Value = _simulator.LevelProgress;
        _xpLabel.Text = _simulator.IsMaxLevel ? "MAX" : $"{_simulator.XpToNextLevel} XP";
        _casinoButton.Disabled = _simulator.Level < 2;
        _casinoButton.TooltipText = _simulator.Level < 2 ? "阶段 2 解锁付费玩法" : "刮刮乐 / 老虎机 / 大转盘";
        _companyMarketButton.Disabled = !_simulator.IsMaxLevel;
        UpdateMarketTabs();
        _stockList.QueueRedraw();

        if (_simulator.IsMaxLevel && !_companyAutoEntered)
        {
            _companyAutoEntered = true;
            SwitchMarket(true, announce: false);
            SetStatus("阶段 4 // 已进入公司股市 // 单笔金额巨大", BlockTradeTheme.Gold);
        }
    }

    private void UpdateOrderPreview()
    {
        if (_simulator is null)
        {
            return;
        }

        double price = _simulator.GetPrice(_selectedStock);
        bool valid = int.TryParse(_amountInput.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int shares) && shares > 0;
        double cost = valid ? price * shares : 0.0;
        _costLabel.Text = $"预计花费：{Money(cost)}";
        _costLabel.AddThemeColorOverride(
            "font_color",
            valid && cost <= _simulator.GetMarketCash() + 0.000001 ? BlockTradeTheme.Gold : BlockTradeTheme.DownBright);
        _buyButton.Text = $"买入 {_selectedStock}";
        _sellButton.Text = $"卖出 {_selectedStock}";
    }

    private void UpdateCountdown(int seconds)
    {
        if (seconds == _lastCountdownSecond)
        {
            return;
        }

        _lastCountdownSecond = seconds;
        _refreshLabel.Text = $"{FormatCountdown(seconds)} 后刷新";
    }

    private void ShowTransferDialog()
    {
        if (_simulator is null)
        {
            return;
        }

        _transferOverlay.Visible = true;
        bool toMarket = _simulator.GetCash() > 0.0001 || _simulator.MarketCash <= 0.0001;
        SetTransferDirection(toMarket);
    }

    private void SetTransferDirection(bool toMarket)
    {
        if (_simulator is null)
        {
            return;
        }

        _transferToMarket = toMarket;
        BlockTradeTheme.ApplyButton(_toMarketButton,
            toMarket ? BlockTradeButtonTone.Gold : BlockTradeButtonTone.Neutral);
        BlockTradeTheme.ApplyButton(_toCashButton,
            toMarket ? BlockTradeButtonTone.Neutral : BlockTradeButtonTone.Gold);
        _transferHint.Text = $"现金 {Money(_simulator.GetCash())}   |   资产 {Money(_simulator.GetMarketCash())}   |   持仓 {Money(_simulator.GetStockAsset())}\n" +
                             (toMarket
                                 ? $"可转入现金 {Money(_simulator.GetCash())}"
                                 : $"可转出资产 {Money(_simulator.MarketCash)}");
        _transferHint.AddThemeColorOverride("font_color", BlockTradeTheme.TextMuted);
        _transferConfirmButton.Text = toMarket ? "确认转入" : "确认转出";
        _transferAmountInput.Text = (toMarket ? _simulator.GetCash() : _simulator.MarketCash)
            .ToString("0.00", CultureInfo.InvariantCulture);
        _transferAmountInput.GrabFocus();
        _transferAmountInput.SelectAll();
    }

    private void HideTransferDialog()
    {
        _transferOverlay.Visible = false;
    }

    private void OnTransferConfirmed()
    {
        if (_simulator is null)
        {
            return;
        }

        if (!double.TryParse(_transferAmountInput.Text.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double amount) || amount <= 0.0)
        {
            _transferHint.Text = "请输入有效金额";
            _transferHint.AddThemeColorOverride("font_color", BlockTradeTheme.DownBright);
            return;
        }

        bool ok = _transferToMarket
            ? _simulator.DepositToMarket(amount)
            : _simulator.WithdrawToCash(amount);
        if (!ok)
        {
            _transferHint.Text = _transferToMarket
                ? $"现金不足 // 当前现金 {Money(_simulator.GetCash())}"
                : $"资产不足 // 当前可转出 {Money(_simulator.MarketCash)}";
            _transferHint.AddThemeColorOverride("font_color", BlockTradeTheme.DownBright);
            return;
        }

        _transferOverlay.Visible = false;
        _sound?.Play(BlockTradeSound.Transfer);
        RefreshPortfolio();
        if (_transferToMarket)
        {
            BlockTradeDeltaPopup.Show(_root, _cashLabel, -amount);
            BlockTradeDeltaPopup.Show(_root, _stockAssetLabel, amount);
        }
        else
        {
            BlockTradeDeltaPopup.Show(_root, _stockAssetLabel, -amount);
            BlockTradeDeltaPopup.Show(_root, _cashLabel, amount);
        }

        bool saved = SaveGame();
        SetStatus(_transferToMarket
                ? $"已转入 {Money(amount)} 到股市资产 // {(saved ? "已自动保存" : "保存失败")}"
                : $"已转出 {Money(amount)} 到现金 // {(saved ? "已自动保存" : "保存失败")}",
            saved ? BlockTradeTheme.Gold : BlockTradeTheme.DownBright);
    }

    private void ShowLoanDialog()
    {
        if (_simulator is null)
        {
            return;
        }

        _loanOverlay.Visible = true;
        _loanAmountInput.Text = "";
        _loanMessage.Text = "最高可贷 $50,000,000";
        _loanMessage.AddThemeColorOverride("font_color", BlockTradeTheme.TextMuted);
        RefreshLoanLabels();
        _loanAmountInput.GrabFocus();
    }

    private void HideLoanDialog()
    {
        _loanOverlay.Visible = false;
    }

    private void RefreshLoanLabels()
    {
        if (_simulator is null)
        {
            return;
        }

        _loanDebtLabel.Text = $"当前贷款 {Money(_simulator.LoanDebt)} / 上限 {Money(BlockTradeSimulator.MaxLoan)}";
    }

    private bool TryReadLoanAmount(out double amount)
    {
        return double.TryParse(_loanAmountInput.Text.Trim(), NumberStyles.Float,
                   CultureInfo.InvariantCulture, out amount) && amount > 0.0;
    }

    private void OnBorrowPressed()
    {
        if (_simulator is null || !TryReadLoanAmount(out double amount))
        {
            _loanMessage.Text = "请输入有效贷款金额";
            _loanMessage.AddThemeColorOverride("font_color", BlockTradeTheme.DownBright);
            return;
        }

        if (!_simulator.Borrow(amount))
        {
            _loanMessage.Text = $"超过贷款上限 // 剩余额度 {Money(BlockTradeSimulator.MaxLoan - _simulator.LoanDebt)}";
            _loanMessage.AddThemeColorOverride("font_color", BlockTradeTheme.DownBright);
            return;
        }

        BlockTradeDeltaPopup.Show(_root, _cashLabel, amount);
        _sound?.Play(BlockTradeSound.Loan);
        RefreshLoanLabels();
        RefreshPortfolio();
        bool saved = SaveGame();
        SetStatus($"已借入 {Money(amount)} // {(saved ? "已自动保存" : "保存失败")}",
            saved ? BlockTradeTheme.Gold : BlockTradeTheme.DownBright);
    }

    private void OnRepayPressed()
    {
        if (_simulator is null || !TryReadLoanAmount(out double amount))
        {
            _loanMessage.Text = "请输入有效还款金额";
            _loanMessage.AddThemeColorOverride("font_color", BlockTradeTheme.DownBright);
            return;
        }

        if (!_simulator.RepayLoan(amount))
        {
            _loanMessage.Text = $"现金或贷款余额不足 // 现金 {Money(_simulator.GetCash())}";
            _loanMessage.AddThemeColorOverride("font_color", BlockTradeTheme.DownBright);
            return;
        }

        BlockTradeDeltaPopup.Show(_root, _cashLabel, -amount);
        _sound?.Play(BlockTradeSound.Repay);
        RefreshLoanLabels();
        RefreshPortfolio();
        bool saved = SaveGame();
        SetStatus($"已还款 {Money(amount)} // {(saved ? "已自动保存" : "保存失败")}",
            saved ? BlockTradeTheme.Gold : BlockTradeTheme.DownBright);
    }

    private bool IsOverlayOpen()
    {
        Node parent = GetParent();
        return parent.GetNodeOrNull<CanvasLayer>("Casino")?.Visible == true ||
               parent.GetNodeOrNull<CanvasLayer>("Settings")?.Visible == true ||
               _authOverlay.Visible ||
               _transferOverlay.Visible ||
               _loanOverlay.Visible;
    }

    private void AdjustRefreshInterval(int deltaSeconds)
    {
        if (_simulator is null)
        {
            return;
        }

        ApplyRefreshInterval((int)Math.Round(_simulator.RefreshIntervalSeconds) + deltaSeconds);
    }

    private void OnRefreshSecondsSubmitted(string _text)
    {
        ApplyRefreshInput();
    }

    private void OnRefreshApplyPressed()
    {
        ApplyRefreshInput();
    }

    private void ApplyRefreshInput()
    {
        if (!int.TryParse(_refreshSecondsInput.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int seconds))
        {
            SetStatus("刷新时间无效 // 请输入 5~600 的整数秒", BlockTradeTheme.DownBright);
            _refreshSecondsInput.GrabFocus();
            return;
        }

        ApplyRefreshInterval(seconds);
    }

    private void ApplyRefreshInterval(int seconds)
    {
        if (_simulator is null)
        {
            return;
        }

        int next = Math.Clamp(seconds, 5, 600);
        _simulator.RefreshIntervalSeconds = next;
        _simulator.MarketRefreshRemaining = next;
        if (_tickTimer is not null)
        {
            _tickTimer.Stop();
            _tickTimer.WaitTime = next;
            _tickTimer.Start();
        }

        UpdateRefreshEditor();
        UpdateCountdown(next);
        SetStatus($"刷新时间已设为 {next} 秒 // 已保存", BlockTradeTheme.Gold);
        SaveGame();
    }

    private void UpdateRefreshEditor()
    {
        if (_simulator is null)
        {
            return;
        }

        int seconds = (int)Math.Round(_simulator.RefreshIntervalSeconds);
        _refreshValueLabel.Text = seconds % 60 == 0
            ? $"{seconds} 秒 ({seconds / 60} 分钟)"
            : $"{seconds} 秒";
        _refreshSecondsInput.Text = seconds.ToString(CultureInfo.InvariantCulture);
    }

    private void SetStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", color);
    }

    private static Label MakeLabel(string text, int size, Color color, string name = "")
    {
        var label = new Label { Text = text };
        if (!string.IsNullOrEmpty(name))
        {
            label.Name = name;
        }
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeSmallButton(string name, string text, BlockTradeButtonTone tone)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0, 34),
        };
        BlockTradeTheme.ApplyButton(button, tone);
        button.AddThemeFontSizeOverride("font_size", 13);
        return button;
    }

    private static Button MakeActionButton(string name, string text, BlockTradeButtonTone tone)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(150, 56),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        BlockTradeTheme.ApplyButton(button, tone);
        button.AddThemeFontSizeOverride("font_size", 18);
        return button;
    }

    private static PanelContainer MakePanel(string name)
    {
        var panel = new PanelContainer { Name = name };
        BlockTradeTheme.ApplyPanel(panel);
        return panel;
    }

    private static MarginContainer MakePadding(int all)
    {
        var margin = new MarginContainer { Name = "Padding" };
        margin.AddThemeConstantOverride("margin_left", all);
        margin.AddThemeConstantOverride("margin_right", all);
        margin.AddThemeConstantOverride("margin_top", all);
        margin.AddThemeConstantOverride("margin_bottom", all);
        return margin;
    }

    private static Control Spacer()
    {
        return new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static ColorRect Separator()
    {
        return new ColorRect
        {
            Color = BlockTradeTheme.BorderDark,
            CustomMinimumSize = new Vector2(0, 2),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static ColorRect SeparatorVertical()
    {
        return new ColorRect
        {
            Color = BlockTradeTheme.BorderDark,
            CustomMinimumSize = new Vector2(2, 20),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static string Money(double value)
    {
        return value < 0.0
            ? "-$" + (-value).ToString("#,##0.00", CultureInfo.InvariantCulture)
            : "$" + value.ToString("#,##0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatCountdown(int seconds)
    {
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
