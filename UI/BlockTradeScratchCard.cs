using Godot;

namespace BlockTrade;

/// <summary>
/// A real scratch card: hold the left mouse button and drag to erase the silver
/// cover. Symbols underneath are drawn by this control; the player wins when
/// at least three symbols match after enough of the card has been scratched.
/// </summary>
public partial class BlockTradeScratchCard : Control
{
    private const int Cols = 3;
    private const int Rows = 3;
    private const float ScratchRadius = 16.0f;
    private const float CompleteCoverage = 0.56f;

    private readonly string[] _symbols = new string[Cols * Rows];
    private Image? _mask;
    private ImageTexture? _maskTexture;
    private Font? _font;
    private bool _active;
    private bool _scratching;
    private int _erasedPixels;
    private int _lastMaskWidth;
    private int _lastMaskHeight;

    public event Action? Completed;

    public bool IsActive => _active;
    public float Coverage { get; private set; }
    public int SymbolCount => _symbols.Length;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _font = GetThemeFont("font");
        Resized += OnResized;
        BuildMask();
        QueueRedraw();
    }

    public void StartRound(IReadOnlyList<string> symbols)
    {
        for (int i = 0; i < _symbols.Length; i++)
        {
            _symbols[i] = i < symbols.Count ? symbols[i] : string.Empty;
        }

        _active = true;
        _scratching = false;
        Coverage = 0.0f;
        BuildMask();
        QueueRedraw();
    }

    public void RevealAll()
    {
        if (_mask is null || _maskTexture is null)
        {
            return;
        }

        for (int y = 0; y < _mask.GetHeight(); y++)
        {
            for (int x = 0; x < _mask.GetWidth(); x++)
            {
                _mask.SetPixel(x, y, Colors.Transparent);
            }
        }

        _erasedPixels = _mask.GetWidth() * _mask.GetHeight();
        Coverage = 1.0f;
        _maskTexture.Update(_mask);
        _active = false;
        QueueRedraw();
    }

    /// <summary>Public scratch hook used by both mouse input and automated tests.</summary>
    public void ScratchAt(Vector2 localPosition, float radius = ScratchRadius)
    {
        if (!_active || _mask is null || _maskTexture is null)
        {
            return;
        }

        int cx = Mathf.RoundToInt(localPosition.X);
        int cy = Mathf.RoundToInt(localPosition.Y);
        int r = Mathf.RoundToInt(radius);
        int r2 = r * r;
        bool changed = false;
        for (int y = Math.Max(0, cy - r); y <= Math.Min(_mask.GetHeight() - 1, cy + r); y++)
        {
            for (int x = Math.Max(0, cx - r); x <= Math.Min(_mask.GetWidth() - 1, cx + r); x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy > r2)
                {
                    continue;
                }

                if (_mask.GetPixel(x, y).A > 0.0f)
                {
                    _mask.SetPixel(x, y, Colors.Transparent);
                    _erasedPixels++;
                    changed = true;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        _maskTexture.Update(_mask);
        Coverage = _erasedPixels / (float)(_mask.GetWidth() * _mask.GetHeight());
        QueueRedraw();
        if (Coverage >= CompleteCoverage)
        {
            _active = false;
            RevealAll();
            Completed?.Invoke();
        }
    }

    /// <summary>Scratches a whole cell; useful for automated tests and accessibility.</summary>
    public void ScratchCell(int index)
    {
        if (index < 0 || index >= _symbols.Length)
        {
            return;
        }

        Rect2 cell = CellRect(index);
        const int steps = 4;
        for (int y = 0; y < steps; y++)
        {
            for (int x = 0; x < steps; x++)
            {
                var point = new Vector2(
                    cell.Position.X + (x + 0.5f) * cell.Size.X / steps,
                    cell.Position.Y + (y + 0.5f) * cell.Size.Y / steps);
                ScratchAt(point, Math.Max(cell.Size.X, cell.Size.Y) / (steps * 0.70f));
            }
        }
    }

    public void ScratchAllForTest()
    {
        for (int i = 0; i < _symbols.Length && _active; i++)
        {
            ScratchCell(i);
        }

        if (_active)
        {
            RevealAll();
            Completed?.Invoke();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_active)
        {
            return;
        }

        if (@event is InputEventMouseButton button &&
            button.ButtonIndex == MouseButton.Left)
        {
            _scratching = button.Pressed;
            if (button.Pressed)
            {
                ScratchAt(button.Position);
            }
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseMotion motion && _scratching &&
            (motion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            ScratchAt(motion.Position);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        _font ??= GetThemeFont("font");
        var card = new Rect2(Vector2.Zero, Size);
        DrawRect(card, Color.FromHtml("#1b1916"));
        DrawRect(card, BlockTradeTheme.Border, false, 2.0f);

        for (int i = 0; i < _symbols.Length; i++)
        {
            DrawSymbolCell(i);
        }

        if (_maskTexture is not null)
        {
            DrawTextureRect(_maskTexture, card, false);
        }

        for (int i = 1; i < Cols; i++)
        {
            float x = Size.X * i / Cols;
            DrawLine(new Vector2(x, 2), new Vector2(x, Size.Y - 2), BlockTradeTheme.BorderDark, 2.0f);
        }

        for (int i = 1; i < Rows; i++)
        {
            float y = Size.Y * i / Rows;
            DrawLine(new Vector2(2, y), new Vector2(Size.X - 2, y), BlockTradeTheme.BorderDark, 2.0f);
        }

        if (_active && Coverage < 0.02f)
        {
            DrawChartText("长按鼠标左键刮开", new Vector2(12, Size.Y - 12), BlockTradeTheme.TextDim, 12);
        }
    }

    private void DrawSymbolCell(int index)
    {
        Rect2 cell = CellRect(index);
        string symbol = _symbols[index];
        if (string.IsNullOrEmpty(symbol))
        {
            return;
        }

        var iconRect = new Rect2(cell.Position.X + 12, cell.Position.Y + 8, 38, 38);
        BlockTradePixelArt.DrawIcon(this, symbol, iconRect);
        if (_font is not null)
        {
            Vector2 textSize = _font.GetStringSize(symbol, HorizontalAlignment.Left, -1, 14);
            DrawString(_font, new Vector2(cell.Position.X + (cell.Size.X - textSize.X) / 2.0f, cell.End.Y - 10),
                symbol, HorizontalAlignment.Left, -1, 14, BlockTradeTheme.Text);
        }
    }

    private Rect2 CellRect(int index)
    {
        int col = index % Cols;
        int row = index / Cols;
        float w = Size.X / Cols;
        float h = Size.Y / Rows;
        return new Rect2(col * w, row * h, w, h);
    }

    private void OnResized()
    {
        if (!_active)
        {
            BuildMask();
        }
        QueueRedraw();
    }

    private void BuildMask()
    {
        int width = Math.Max(3, Mathf.RoundToInt(Size.X));
        int height = Math.Max(3, Mathf.RoundToInt(Size.Y));
        if (width == _lastMaskWidth && height == _lastMaskHeight && _mask is not null)
        {
            // Reuse the image but restore the silver cover.
        }
        else
        {
            _lastMaskWidth = width;
            _lastMaskHeight = height;
            _mask = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int shade = 134 + ((x * 13 + y * 7) % 18);
                _mask.SetPixel(x, y, Color.Color8((byte)shade, (byte)shade, (byte)(shade + 4)));
            }
        }

        _maskTexture = ImageTexture.CreateFromImage(_mask);
        _erasedPixels = 0;
        Coverage = 0.0f;
    }

    private void DrawChartText(string text, Vector2 position, Color color, int fontSize)
    {
        if (_font is null)
        {
            return;
        }

        DrawString(_font, position, text, HorizontalAlignment.Left, -1, fontSize, color);
    }
}
