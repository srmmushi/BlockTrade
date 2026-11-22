using Godot;

namespace BlockTrade;

public enum BlockTradeButtonTone
{
    Neutral,
    Gold,
    Green,
    Red,
}

/// <summary>
/// BlockTrade's pixel-art design system: warm dark panels, square bevels,
/// Minecraft-like pixel icons and the user-provided Minecraft AE CJK font.
/// </summary>
public static class BlockTradeTheme
{
    public static readonly Color Background = Color.FromHtml("#171614");
    public static readonly Color BackgroundTileA = Color.FromHtml("#1b1917");
    public static readonly Color BackgroundTileB = Color.FromHtml("#1e1c19");
    public static readonly Color Panel = Color.FromHtml("#242220");
    public static readonly Color PanelDark = Color.FromHtml("#1c1a18");
    public static readonly Color PanelLight = Color.FromHtml("#2b2824");
    public static readonly Color Border = Color.FromHtml("#786c5c");
    public static readonly Color BorderDark = Color.FromHtml("#3a352f");
    public static readonly Color BorderLight = Color.FromHtml("#9b8e7a");
    public static readonly Color Text = Color.FromHtml("#ede3cc");
    public static readonly Color TextMuted = Color.FromHtml("#b1a58f");
    public static readonly Color TextDim = Color.FromHtml("#7d7364");
    public static readonly Color Gold = Color.FromHtml("#e7bf4f");
    public static readonly Color GoldBright = Color.FromHtml("#f8d96b");
    public static readonly Color Up = Color.FromHtml("#3fa84b");
    public static readonly Color UpBright = Color.FromHtml("#65d66d");
    public static readonly Color Down = Color.FromHtml("#a8403b");
    public static readonly Color DownBright = Color.FromHtml("#e2655c");
    // Chinese market convention: red means rising, green means falling.
    public static readonly Color MarketUp = Color.FromHtml("#e2655c");
    public static readonly Color MarketDown = Color.FromHtml("#65d66d");
    public static readonly Color Buy = Color.FromHtml("#2e7d36");
    public static readonly Color BuyHover = Color.FromHtml("#399845");
    public static readonly Color BuyPressed = Color.FromHtml("#1f5a26");
    public static readonly Color Sell = Color.FromHtml("#87332f");
    public static readonly Color SellHover = Color.FromHtml("#a7423c");
    public static readonly Color SellPressed = Color.FromHtml("#62221f");
    public static readonly Color ButtonNormal = Color.FromHtml("#3a3733");
    public static readonly Color ButtonHover = Color.FromHtml("#4a4640");
    public static readonly Color ButtonPressed = Color.FromHtml("#1d1b19");
    public static readonly Color ButtonDisabled = Color.FromHtml("#26231f");
    public static readonly Color Selection = Color.FromHtml("#34302a");

    // Kept as aliases for older call sites.
    public static readonly Color PanelInset = PanelDark;

    public const string FontPath = "res://Assets/Fonts/Minecraft(同时支持中英文).ttf";

    public static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFontSize = 12 };

        Font? pixelFont = GD.Load<Font>(FontPath);
        if (pixelFont is not null)
        {
            var fallback = new SystemFont
            {
                FontNames = new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "Noto Sans CJK SC",
                    "sans-serif",
                },
            };
            pixelFont.Fallbacks = new Godot.Collections.Array<Font> { fallback };
            theme.DefaultFont = pixelFont;
        }

        theme.SetColor("font_color", "Label", Text);
        theme.SetColor("font_color", "Button", Text);
        theme.SetColor("font_color", "ItemList", Text);
        theme.SetColor("font_selected_color", "ItemList", Text);
        theme.SetStylebox("panel", "PanelContainer", PanelStyle());
        theme.SetStylebox("panel", "ItemList", InsetStyle());
        theme.SetStylebox("normal", "LineEdit", InputStyle());
        theme.SetStylebox("focus", "LineEdit", FocusStyle());
        theme.SetColor("font_color", "LineEdit", Text);
        theme.SetColor("caret_color", "LineEdit", Gold);
        theme.SetColor("font_color", "RichTextLabel", Text);
        return theme;
    }

    public static StyleBoxFlat PanelStyle()
    {
        return MakeBox(Panel, Border, 2);
    }

    public static StyleBoxFlat PanelDarkStyle()
    {
        return MakeBox(PanelDark, BorderDark, 2);
    }

    public static StyleBoxFlat InsetStyle()
    {
        return MakeBox(PanelDark, BorderDark, 2);
    }

    public static StyleBoxFlat ButtonStyle(Color background, Color? border = null)
    {
        StyleBoxFlat box = MakeBox(background, border ?? Border, 2);
        box.SetContentMarginAll(10.0f);
        return box;
    }

    public static StyleBoxFlat FocusStyle()
    {
        return MakeBox(new Color(0, 0, 0, 0), Gold, 2);
    }

    public static StyleBoxFlat InputStyle()
    {
        StyleBoxFlat box = MakeBox(PanelDark, Border, 2);
        box.SetContentMarginAll(9.0f);
        return box;
    }

    public static StyleBoxFlat SelectedStyle()
    {
        return MakeBox(Selection, BorderLight, 2);
    }

    public static StyleBoxFlat MakeBox(Color background, Color border, int borderWidth)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
        };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(0);
        return box;
    }

    public static void ApplyPanel(PanelContainer panel, StyleBoxFlat? style = null)
    {
        panel.AddThemeStyleboxOverride("panel", style ?? PanelStyle());
    }

    public static void ApplyButton(Button button, BlockTradeButtonTone tone = BlockTradeButtonTone.Neutral)
    {
        (Color normal, Color hover, Color pressed, Color border, Color font) = tone switch
        {
            BlockTradeButtonTone.Gold => (Gold, GoldBright, Color.FromHtml("#8b6d1f"), BorderLight, Color.FromHtml("#191613")),
            BlockTradeButtonTone.Green => (Buy, BuyHover, BuyPressed, Color.FromHtml("#78a765"), Color.FromHtml("#f4f0df")),
            BlockTradeButtonTone.Red => (Sell, SellHover, SellPressed, Color.FromHtml("#b56b5e"), Color.FromHtml("#f4e7de")),
            _ => (ButtonNormal, ButtonHover, ButtonPressed, Border, Text),
        };

        button.AddThemeStyleboxOverride("normal", ButtonStyle(normal, border));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(hover, border));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(pressed, border));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(ButtonDisabled, BorderDark));
        button.AddThemeStyleboxOverride("focus", FocusStyle());
        button.AddThemeColorOverride("font_color", font);
        button.AddThemeColorOverride("font_hover_color", tone == BlockTradeButtonTone.Gold ? font : Colors.White);
        button.AddThemeColorOverride("font_pressed_color", tone == BlockTradeButtonTone.Gold ? font : Text);
        button.AddThemeColorOverride("font_disabled_color", TextDim);
        button.AddThemeFontSizeOverride("font_size", 12);
    }

    public static void ApplyItemList(ItemList list)
    {
        list.AddThemeStyleboxOverride("panel", InsetStyle());
        list.AddThemeStyleboxOverride("selected", SelectedStyle());
        list.AddThemeStyleboxOverride("selected_focus", SelectedStyle());
        list.AddThemeStyleboxOverride("focus", FocusStyle());
        list.AddThemeColorOverride("font_color", Text);
        list.AddThemeColorOverride("font_selected_color", Colors.White);
        list.AddThemeColorOverride("guide_color", BorderDark);
        list.AddThemeFontSizeOverride("font_size", 12);
    }
}
