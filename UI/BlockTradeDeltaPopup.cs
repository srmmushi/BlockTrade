using Godot;

namespace BlockTrade;

/// <summary>
/// Small floating +$100 / -$100 label used above cash and asset balances.
/// Red is an increase, green is a decrease (Chinese market convention).
/// </summary>
public static class BlockTradeDeltaPopup
{
    public static void Show(Control parent, Control anchor, double amount)
    {
        if (Math.Abs(amount) < 0.005)
        {
            return;
        }

        Color color = amount > 0.0 ? BlockTradeTheme.MarketUp : BlockTradeTheme.MarketDown;
        string sign = amount > 0.0 ? "+" : "-";
        string text = $"{sign}${Math.Abs(amount):#,##0.00}";
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 120,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", color);

        parent.AddChild(label);
        Vector2 anchorPosition = anchor.GlobalPosition - parent.GlobalPosition;
        float y = anchorPosition.Y - 22.0f;
        bool belowTopBar = y < 90.0f;
        if (belowTopBar)
        {
            // Top-bar labels have no room above the screen edge; put the popup
            // below the bar so it is always readable.
            y = 86.0f;
        }
        label.Position = new Vector2(anchorPosition.X + anchor.Size.X * 0.5f - 26.0f, y);

        Tween tween = parent.CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", label.Position.Y + (belowTopBar ? 26.0f : -30.0f), 0.9f)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.9f)
            .SetDelay(0.25f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
