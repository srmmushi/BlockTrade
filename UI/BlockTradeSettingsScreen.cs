using Godot;

namespace BlockTrade;

/// <summary>Minimal settings overlay: sound toggle, volume and hotkey reference.</summary>
public partial class BlockTradeSettingsScreen : CanvasLayer
{
    public event Action? Closed;

    private BlockTradeSoundManager? _sound;
    private Control _root = null!;
    private Button _soundButton = null!;
    private HSlider _volumeSlider = null!;
    private Label _volumeLabel = null!;
    private OptionButton _resolutionOption = null!;

    private static readonly Vector2I[] ResolutionSizes =
    {
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
    };

    public override void _Ready()
    {
        BuildInterface();
        Visible = false;
    }

    public void Open(BlockTradeSoundManager sound)
    {
        _sound = sound;
        BlockTradeSettingsStore.Load();
        RefreshValues();
        Visible = true;
        _root.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(_root, "modulate:a", 1.0f, 0.15);
        _soundButton.GrabFocus();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible &&
            @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void RefreshValues()
    {
        _soundButton.Text = BlockTradeSettingsStore.SoundEnabled ? "音效：开启" : "音效：关闭";
        _volumeSlider.Value = Math.Round(BlockTradeSettingsStore.Volume * 100.0f);
        _volumeLabel.Text = $"{BlockTradeSettingsStore.Volume * 100.0f:0}%";
        for (int i = 0; i < ResolutionSizes.Length; i++)
        {
            if (ResolutionSizes[i].X == BlockTradeSettingsStore.WindowWidth &&
                ResolutionSizes[i].Y == BlockTradeSettingsStore.WindowHeight)
            {
                _resolutionOption.Selected = i;
                break;
            }
        }
    }

    private void OnResolutionChanged(long index)
    {
        if (index < 0 || index >= ResolutionSizes.Length)
        {
            return;
        }

        Vector2I size = ResolutionSizes[index];
        BlockTradeSettingsStore.WindowWidth = size.X;
        BlockTradeSettingsStore.WindowHeight = size.Y;
        BlockTradeSettingsStore.Save();
        BlockTradeSettingsStore.ApplyWindowResolution();
        _sound?.Play(BlockTradeSound.Click);
    }

    private void OnToggleSound()
    {
        BlockTradeSettingsStore.SoundEnabled = !BlockTradeSettingsStore.SoundEnabled;
        BlockTradeSettingsStore.Save();
        _sound?.ApplySettings();
        RefreshValues();
        _sound?.Play(BlockTradeSound.Click);
    }

    private void OnVolumeChanged(double value)
    {
        BlockTradeSettingsStore.Volume = Math.Clamp((float)(value / 100.0), 0.0f, 1.0f);
        BlockTradeSettingsStore.Save();
        _volumeLabel.Text = $"{value:0}%";
        _sound?.ApplySettings();
    }

    private void BuildInterface()
    {
        var root = new Control
        {
            Name = "SettingsRoot",
            Theme = BlockTradeTheme.CreateTheme(),
        };
        _root = root;
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var dim = new ColorRect
        {
            Name = "SettingsDim",
            Color = new Color(0.02f, 0.02f, 0.02f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        root.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer();
        root.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer
        {
            Name = "SettingsPanel",
            CustomMinimumSize = new Vector2(620, 0),
        };
        BlockTradeTheme.ApplyPanel(panel);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        panel.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);

        var titleRow = new HBoxContainer();
        titleRow.AddChild(MakeLabel("设置", 22, BlockTradeTheme.Text));
        titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        titleRow.AddChild(MakeLabel("SETTINGS", 14, BlockTradeTheme.TextDim));
        column.AddChild(titleRow);
        column.AddChild(Separator());

        _soundButton = new Button
        {
            Name = "SoundToggleBtn",
            Text = "音效：开启",
            CustomMinimumSize = new Vector2(0, 44),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(_soundButton, BlockTradeButtonTone.Neutral);
        _soundButton.Pressed += OnToggleSound;
        column.AddChild(_soundButton);

        var volumeRow = new HBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 12);
        volumeRow.AddChild(MakeLabel("音量", 14, BlockTradeTheme.TextMuted));
        _volumeSlider = new HSlider
        {
            Name = "VolumeSlider",
            MinValue = 0,
            MaxValue = 100,
            Step = 5,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28),
            Value = 80,
        };
        _volumeSlider.ValueChanged += OnVolumeChanged;
        volumeRow.AddChild(_volumeSlider);
        _volumeLabel = MakeLabel("80%", 14, BlockTradeTheme.Gold);
        _volumeLabel.CustomMinimumSize = new Vector2(52, 0);
        _volumeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        volumeRow.AddChild(_volumeLabel);
        column.AddChild(volumeRow);

        var resolutionRow = new HBoxContainer();
        resolutionRow.AddThemeConstantOverride("separation", 12);
        resolutionRow.AddChild(MakeLabel("分辨率", 14, BlockTradeTheme.TextMuted));
        _resolutionOption = new OptionButton
        {
            Name = "ResolutionOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
        };
        foreach (Vector2I size in ResolutionSizes)
        {
            _resolutionOption.AddItem($"{size.X} × {size.Y}");
        }
        _resolutionOption.ItemSelected += OnResolutionChanged;
        resolutionRow.AddChild(_resolutionOption);
        column.AddChild(resolutionRow);
        column.AddChild(Separator());

        column.AddChild(MakeLabel("快捷键", 16, BlockTradeTheme.Gold));
        Label hotkeys = MakeLabel(
            "B  买入        S  卖出        1 / 2 / 3  数量\nTAB  切换股票     C  玩法        F5  保存\nCTRL+SHIFT+T  刷新时间（可输入秒数）     ↑ / ↓  1 秒\nSHIFT+↑ / ↓  10 秒     ESC  打开 / 关闭设置",
            14,
            BlockTradeTheme.TextMuted);
        hotkeys.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(hotkeys);
        column.AddChild(Separator());

        var footer = new HBoxContainer();
        footer.AddChild(MakeLabel("BLOCKTRADE", 14, BlockTradeTheme.TextDim));
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button
        {
            Name = "SettingsCloseBtn",
            Text = "关闭",
            CustomMinimumSize = new Vector2(160, 44),
            FocusMode = Control.FocusModeEnum.All,
        };
        BlockTradeTheme.ApplyButton(close, BlockTradeButtonTone.Gold);
        close.Pressed += Close;
        footer.AddChild(close);
        column.AddChild(footer);
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
