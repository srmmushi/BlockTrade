using Godot;

namespace BlockTrade;

/// <summary>
/// A short in-engine prologue. It uses a typewriter reveal and a skip button,
/// then raises Finished so BlockTradeMain can reveal the trading screen.
/// </summary>
public partial class BlockTradeStory : CanvasLayer
{
    public event Action? Finished;

    private readonly (string Heading, string Body)[] _pages =
    {
        (
            "矿洞深处",
            "你继承了祖父留下的一座废弃矿洞。火把熄灭之前，你在最深处发现了一台还在发光的旧终端。"
        ),
        (
            "方块市场",
            "屏幕亮起，六种熟悉的方块商品跳了出来：铁、金、钻石、红石、绿宝石和煤炭。\n每一行价格，都在等待下一次刷新。"
        ),
        (
            "第一笔交易",
            "这里的市场每 2 分钟刷新一次。你带着 2,000 资产，准备从第一笔交易开始。\n交易会累积阶段经验，新的付费玩法正等着你。"
        ),
    };

    private Control _root = null!;
    private Label _headingLabel = null!;
    private Label _bodyLabel = null!;
    private Label _pageLabel = null!;
    private Button _advanceButton = null!;
    private Tween? _typingTween;
    private int _pageIndex;

    public override void _Ready()
    {
        BuildInterface();
        Visible = false;
    }

    public void Begin()
    {
        _pageIndex = 0;
        Visible = true;
        _root.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(_root, "modulate:a", 1.0, 0.25);
        ShowPage();
    }

    public void Skip()
    {
        _typingTween?.Kill();
        _typingTween = null;
        Visible = false;
        Finished?.Invoke();
    }

    private void ShowPage()
    {
        (string heading, string body) = _pages[_pageIndex];
        _headingLabel.Text = heading;
        _bodyLabel.Text = body;
        _bodyLabel.VisibleCharacters = 0;
        _pageLabel.Text = $"{_pageIndex + 1} / {_pages.Length}";
        _advanceButton.Text = _pageIndex == _pages.Length - 1 ? "进入市场" : "继续";
        _advanceButton.GrabFocus();

        _typingTween?.Kill();
        _typingTween = CreateTween();
        _typingTween.TweenProperty(_bodyLabel, "visible_characters", body.Length, 0.65)
            .SetTrans(Tween.TransitionType.Linear);
    }

    private void Advance()
    {
        if (_typingTween is not null && _typingTween.IsRunning())
        {
            _typingTween.Kill();
            _typingTween = null;
            _bodyLabel.VisibleCharacters = _bodyLabel.Text.Length;
            return;
        }

        if (_pageIndex < _pages.Length - 1)
        {
            _pageIndex++;
            ShowPage();
            return;
        }

        Visible = false;
        Finished?.Invoke();
    }

    private void BuildInterface()
    {
        _root = new Control
        {
            Name = "StoryRoot",
            Theme = BlockTradeTheme.CreateTheme(),
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new BlockTradeBackdrop { Name = "StoryBackdrop" };
        _root.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var margin = new MarginContainer { Name = "StoryMargin" };
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        _root.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var page = new VBoxContainer { Name = "StoryPage" };
        page.AddThemeConstantOverride("separation", 12);
        margin.AddChild(page);

        var top = new HBoxContainer();
        top.AddChild(MakeLabel("BLOCKTRADE // 序章", 14, BlockTradeTheme.Gold));
        top.AddChild(Spacer());
        _pageLabel = MakeLabel("1 / 3", 14, BlockTradeTheme.TextDim);
        _pageLabel.Name = "StoryPageLabel";
        top.AddChild(_pageLabel);
        page.AddChild(top);
        page.AddChild(Separator());

        var center = new CenterContainer
        {
            Name = "StoryCenter",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        page.AddChild(center);

        var card = new PanelContainer
        {
            Name = "StoryCard",
            CustomMinimumSize = new Vector2(860, 0),
        };
        BlockTradeTheme.ApplyPanel(card);
        center.AddChild(card);

        var cardMargin = new MarginContainer();
        cardMargin.AddThemeConstantOverride("margin_left", 46);
        cardMargin.AddThemeConstantOverride("margin_right", 46);
        cardMargin.AddThemeConstantOverride("margin_top", 34);
        cardMargin.AddThemeConstantOverride("margin_bottom", 32);
        card.AddChild(cardMargin);

        var column = new VBoxContainer { Name = "StoryColumn" };
        column.AddThemeConstantOverride("separation", 15);
        cardMargin.AddChild(column);

        var chapter = MakeLabel("CHAPTER 00 // THE OLD TERMINAL", 14, BlockTradeTheme.TextDim);
        chapter.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(chapter);

        _headingLabel = MakeLabel("矿洞深处", 28, BlockTradeTheme.Text);
        _headingLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_headingLabel);
        column.AddChild(Separator());

        _bodyLabel = MakeLabel("", 18, BlockTradeTheme.TextMuted);
        _bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bodyLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _bodyLabel.VerticalAlignment = VerticalAlignment.Center;
        _bodyLabel.CustomMinimumSize = new Vector2(0, 150);
        column.AddChild(_bodyLabel);
        column.AddChild(Separator());

        var stockStrip = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stockStrip.AddThemeConstantOverride("separation", 20);
        foreach (string ticker in new[] { "铁锭", "金锭", "钻石", "红石", "绿宝石", "煤炭" })
        {
            var icon = new BlockTradeStockIcon
            {
                StockName = ticker,
                CustomMinimumSize = new Vector2(34, 34),
            };
            stockStrip.AddChild(icon);
        }
        column.AddChild(stockStrip);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(Spacer());

        var skip = MakeButton("StorySkipBtn", "跳过剧情", BlockTradeButtonTone.Neutral);
        skip.CustomMinimumSize = new Vector2(150, 48);
        skip.Pressed += Skip;
        buttons.AddChild(skip);

        _advanceButton = MakeButton("StoryNextBtn", "继续", BlockTradeButtonTone.Gold);
        _advanceButton.CustomMinimumSize = new Vector2(210, 48);
        _advanceButton.Pressed += Advance;
        buttons.AddChild(_advanceButton);
        column.AddChild(buttons);

        var footer = new HBoxContainer();
        footer.AddChild(MakeLabel("2,000 资产 // 6 种方块商品", 14, BlockTradeTheme.TextDim));
        footer.AddChild(Spacer());
        footer.AddChild(MakeLabel("CLICK TO CONTINUE", 14, BlockTradeTheme.TextDim));
        page.AddChild(footer);
    }

    private static Button MakeButton(string name, string text, BlockTradeButtonTone tone)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(button, tone);
        button.AddThemeFontSizeOverride("font_size", 16);
        return button;
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Control Spacer()
    {
        return new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
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
