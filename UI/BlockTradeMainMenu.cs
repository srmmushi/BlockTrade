using Godot;

namespace BlockTrade;

/// <summary>
/// Title screen for BlockTrade. It exposes plain C# events so BlockTradeMain
/// can own the scene flow without any GDScript or autoloads.
/// </summary>
public partial class BlockTradeMainMenu : CanvasLayer
{
    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    private Button _newGameButton = null!;
    private Button _continueButton = null!;
    private Label _saveHint = null!;
    private Control _root = null!;

    public override void _Ready()
    {
        BuildInterface();
        Visible = false;
    }

    public void ShowMenu(bool hasSave)
    {
        _continueButton.Disabled = !hasSave;
        _newGameButton.Text = hasSave ? "开始新游戏（覆盖存档）" : "开始新游戏";
        _saveHint.Text = hasSave ? "检测到本地存档 // 可继续上次的交易" : "暂无存档 // 从一段矿洞故事开始";
        _saveHint.AddThemeColorOverride("font_color", hasSave ? BlockTradeTheme.UpBright : BlockTradeTheme.TextDim);
        Visible = true;
        _root.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(_root, "modulate:a", 1.0, 0.22);
        _newGameButton.GrabFocus();
    }

    private void BuildInterface()
    {
        var root = new Control
        {
            Name = "MenuRoot",
            Theme = BlockTradeTheme.CreateTheme(),
        };
        _root = root;
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new BlockTradeBackdrop { Name = "MenuBackdrop" };
        root.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var margin = new MarginContainer { Name = "MenuMargin" };
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        root.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer
        {
            Name = "MenuCenter",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        margin.AddChild(center);

        PanelContainer card = MakePanel("MenuCard");
        card.CustomMinimumSize = new Vector2(540, 0);
        center.AddChild(card);

        var cardMargin = new MarginContainer();
        cardMargin.AddThemeConstantOverride("margin_left", 38);
        cardMargin.AddThemeConstantOverride("margin_right", 38);
        cardMargin.AddThemeConstantOverride("margin_top", 30);
        cardMargin.AddThemeConstantOverride("margin_bottom", 28);
        card.AddChild(cardMargin);

        var column = new VBoxContainer { Name = "MenuColumn" };
        column.AddThemeConstantOverride("separation", 12);
        cardMargin.AddChild(column);

        Label title = MakeLabel("BLOCKTRADE", 40, BlockTradeTheme.Text);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);

        Label subtitle = MakeLabel("MARKET SIMULATOR", 16, BlockTradeTheme.Gold);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(subtitle);
        column.AddChild(Separator());

        _newGameButton = MakeButton("NewGameBtn", "开始新游戏", BlockTradeButtonTone.Green);
        _newGameButton.CustomMinimumSize = new Vector2(0, 54);
        _newGameButton.Pressed += () => NewGameRequested?.Invoke();
        column.AddChild(_newGameButton);

        _continueButton = MakeButton("ContinueBtn", "继续游戏", BlockTradeButtonTone.Gold);
        _continueButton.CustomMinimumSize = new Vector2(0, 48);
        _continueButton.Pressed += () => ContinueRequested?.Invoke();
        column.AddChild(_continueButton);

        _saveHint = MakeLabel("", 14, BlockTradeTheme.TextDim);
        _saveHint.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_saveHint);

        var utilityRow = new HBoxContainer();
        utilityRow.AddThemeConstantOverride("separation", 8);
        Button settings = MakeButton("SettingsBtn", "设置", BlockTradeButtonTone.Neutral);
        settings.CustomMinimumSize = new Vector2(0, 44);
        settings.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        settings.Pressed += () => SettingsRequested?.Invoke();
        utilityRow.AddChild(settings);

        Button quit = MakeButton("QuitBtn", "退出", BlockTradeButtonTone.Neutral);
        quit.CustomMinimumSize = new Vector2(0, 44);
        quit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        quit.Pressed += () => QuitRequested?.Invoke();
        utilityRow.AddChild(quit);
        column.AddChild(utilityRow);
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

    private static PanelContainer MakePanel(string name)
    {
        var panel = new PanelContainer { Name = name };
        BlockTradeTheme.ApplyPanel(panel);
        return panel;
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
