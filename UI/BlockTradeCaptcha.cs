using Godot;

namespace BlockTrade;

/// <summary>
/// Small text-image captcha. The code is generated in memory and drawn with
/// random lines and character offsets; no external image asset is used.
/// </summary>
public partial class BlockTradeCaptcha : Control
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly Color[] InkColors =
    {
        Color.FromHtml("#f0e6c8"),
        Color.FromHtml("#e7b52f"),
        Color.FromHtml("#7ed0d2"),
        Color.FromHtml("#e2655c"),
    };

    private Font? _font;
    private int _seed;
    private string _code = string.Empty;

    public string Code => _code;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(150, 44);
        MouseFilter = MouseFilterEnum.Ignore;
        _font = GetThemeFont("font");
        Refresh();
    }

    public void Refresh()
    {
        _seed = Random.Shared.Next();
        var random = new Random(_seed);
        var chars = new char[4];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[random.Next(Alphabet.Length)];
        }

        _code = new string(chars);
        QueueRedraw();
    }

    public bool Matches(string input)
    {
        return string.Equals(input.Trim(), _code, StringComparison.OrdinalIgnoreCase);
    }

    public override void _Draw()
    {
        _font ??= GetThemeFont("font");
        var random = new Random(_seed ^ 0x51ed270b);
        DrawRect(new Rect2(Vector2.Zero, Size), Color.FromHtml("#141210"));
        DrawRect(new Rect2(Vector2.Zero, Size), BlockTradeTheme.Border, false, 2.0f);

        for (int i = 0; i < 7; i++)
        {
            var a = new Vector2(random.Next((int)Size.X), random.Next((int)Size.Y));
            var b = new Vector2(random.Next((int)Size.X), random.Next((int)Size.Y));
            Color color = i % 3 == 0 ? BlockTradeTheme.Gold : BlockTradeTheme.BorderDark;
            color.A = 0.45f;
            DrawLine(a, b, color, 1.0f + random.Next(2), false);
        }

        if (_font is null || _code.Length == 0)
        {
            return;
        }

        float slot = Math.Max(1.0f, Size.X / 4.4f);
        for (int i = 0; i < _code.Length; i++)
        {
            float x = 10.0f + i * slot + random.Next(-4, 5);
            float y = 28.0f + random.Next(-5, 7);
            Color color = InkColors[random.Next(InkColors.Length)];
            DrawString(_font, new Vector2(x, y), _code[i].ToString(),
                HorizontalAlignment.Left, -1, 22, color);
        }

        for (int i = 0; i < 22; i++)
        {
            var p = new Vector2(random.Next((int)Size.X), random.Next((int)Size.Y));
            DrawRect(new Rect2(p, new Vector2(2, 2)), new Color(1, 1, 1, 0.22f));
        }
    }
}
