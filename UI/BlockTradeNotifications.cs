using Godot;

namespace BlockTrade;

/// <summary>
/// Top-center notification banner used for stage-up unlocks and feature
/// announcements. It stays above menu, story, game, casino and settings.
/// </summary>
public partial class BlockTradeNotifications : CanvasLayer
{
    private PanelContainer _panel = null!;
    private ColorRect _accent = null!;
    private Label _title = null!;
    private Label _body = null!;
    private Tween? _tween;

    public override void _Ready()
    {
        BuildInterface();
    }

    public void ShowLevelUp(int level, string unlock)
    {
        ShowMessage($"阶段提升 // 阶段 {level}", $"解锁新玩法：{unlock}", BlockTradeTheme.Gold);
    }

    public void ShowMessage(string title, string body, Color accent)
    {
        _tween?.Kill();
        _title.Text = title;
        _body.Text = body;
        _accent.Color = accent;
        _panel.Visible = true;
        _panel.Modulate = new Color(1, 1, 1, 0);

        _tween = CreateTween();
        _tween.TweenProperty(_panel, "modulate:a", 1.0f, 0.16);
        _tween.TweenInterval(2.6);
        _tween.TweenProperty(_panel, "modulate:a", 0.0f, 0.28);
        _tween.TweenCallback(Callable.From(() => _panel.Visible = false));
    }

    private void BuildInterface()
    {
        var root = new Control
        {
            Name = "NotificationsRoot",
            Theme = BlockTradeTheme.CreateTheme(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _panel = new PanelContainer
        {
            Name = "LevelUpToast",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = 0.0f;
        _panel.AnchorBottom = 0.0f;
        _panel.OffsetLeft = -280;
        _panel.OffsetRight = 280;
        _panel.OffsetTop = 62;
        _panel.OffsetBottom = 148;
        BlockTradeTheme.ApplyPanel(_panel, BlockTradeTheme.MakeBox(BlockTradeTheme.Panel, BlockTradeTheme.Gold, 2));
        root.AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        _accent = new ColorRect
        {
            Color = BlockTradeTheme.Gold,
            CustomMinimumSize = new Vector2(5, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(_accent);

        var column = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        column.AddThemeConstantOverride("separation", 2);
        _title = MakeLabel("阶段提升", 16, BlockTradeTheme.Gold);
        _body = MakeLabel("解锁新玩法", 14, BlockTradeTheme.Text);
        column.AddChild(_title);
        column.AddChild(_body);
        row.AddChild(column);
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
