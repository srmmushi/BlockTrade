using Godot;

namespace BlockTrade;

/// <summary>Pixel roulette wheel used by the stage-4 paid activity.</summary>
public partial class BlockTradeRouletteWheel : Control
{
    private static readonly int[] Multipliers = { 0, 0, 1, 0, 2, 0, 1, 3 };
    private static readonly string[] Labels = { "0x", "0x", "1x", "0x", "2x", "0x", "1x", "3x" };
    private static readonly Color[] Colors =
    {
        Color.FromHtml("#4b2a26"),
        Color.FromHtml("#2d3c2b"),
        Color.FromHtml("#476e3b"),
        Color.FromHtml("#4b2a26"),
        Color.FromHtml("#6f5a24"),
        Color.FromHtml("#2d3c2b"),
        Color.FromHtml("#476e3b"),
        Color.FromHtml("#8a5a24"),
    };

    private Font? _font;
    private int _selectedIndex = -1;
    private float _rotation;

    public float WheelRotation => _rotation;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(280, 280);
        MouseFilter = MouseFilterEnum.Ignore;
        _font = GetThemeFont("font");
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public int Multiplier(int index) => Multipliers[Math.Clamp(index, 0, Multipliers.Length - 1)];

    public void SetSelectedIndex(int index)
    {
        _selectedIndex = index;
        QueueRedraw();
    }

    public void SetWheelRotation(float radians)
    {
        _rotation = radians;
        QueueRedraw();
    }

    /// <summary>Rotation that puts the centre of a segment under the top pointer.</summary>
    public float RotationForIndex(int index, int turns = 4)
    {
        float step = MathF.Tau / Multipliers.Length;
        return -index * step - MathF.Tau * turns;
    }

    public override void _Draw()
    {
        _font ??= GetThemeFont("font");
        Vector2 center = Size / 2.0f;
        float radius = MathF.Min(Size.X, Size.Y) * 0.44f;
        float step = MathF.Tau / Multipliers.Length;

        DrawCircle(center, radius + 8.0f, Color.FromHtml("#151311"));
        for (int i = 0; i < Multipliers.Length; i++)
        {
            // The extra -step/2 offset makes segment 0 sit under the pointer at
            // rotation 0, so the fixed pointer always selects the visible wedge.
            float start = -MathF.PI / 2.0f - step / 2.0f + i * step + _rotation;
            float end = start + step;
            var points = new Vector2[10];
            points[0] = center;
            for (int p = 0; p <= 8; p++)
            {
                float angle = Mathf.Lerp(start, end, p / 8.0f);
                points[p + 1] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }

            Color color = Colors[i];
            if (i == _selectedIndex)
            {
                color = color.Lightened(0.25f);
            }
            DrawColoredPolygon(points, color);
            DrawLine(center, points[1], Color.FromHtml("#171513"), 2.0f, false);
            DrawLine(center, points[^1], Color.FromHtml("#171513"), 2.0f, false);

            if (_font is not null)
            {
                Vector2 mid = center + new Vector2(MathF.Cos(start + step / 2), MathF.Sin(start + step / 2)) * radius * 0.65f;
                Vector2 textSize = _font.GetStringSize(Labels[i], HorizontalAlignment.Left, -1, 14);
                DrawString(_font, mid - textSize / 2.0f + new Vector2(0, textSize.Y * 0.7f), Labels[i],
                    HorizontalAlignment.Left, -1, 14, BlockTradeTheme.Text);
            }
        }

        DrawCircle(center, 30, Color.FromHtml("#26221d"));
        DrawCircle(center, 24, Color.FromHtml("#171513"));

        // Fixed pointer at the top. The wheel rotates underneath it.
        var pointer = new[]
        {
            center + new Vector2(0, -radius - 26),
            center + new Vector2(-14, -radius - 2),
            center + new Vector2(14, -radius - 2),
        };
        DrawColoredPolygon(pointer, BlockTradeTheme.Gold);
        DrawLine(center + new Vector2(0, -radius - 30), center + new Vector2(0, -radius - 52),
            BlockTradeTheme.Gold, 3.0f, false);
        DrawArc(center, radius, 0, MathF.Tau, 48, BlockTradeTheme.Border, 2.0f, false);
    }
}
