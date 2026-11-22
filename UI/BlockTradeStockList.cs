using System.Globalization;
using Godot;

namespace BlockTrade;

/// <summary>
/// The reference-inspired market list: one clickable row per stock with a
/// pixel item icon, live change, price and its own miniature sparkline.
/// This replaces ItemList because ItemList cannot host per-row mini charts.
/// </summary>
public partial class BlockTradeStockList : Control
{
    private const int MaxMiniPoints = 30;
    private const float RowGap = 4.0f;

    private BlockTradeSimulator? _simulator;
    private Font? _font;
    private string _selectedStock = "铁锭";
    private int _hoverIndex = -1;
    private bool _companyMode;
    private int _scrollOffset;

    public event Action<string>? StockSelected;

    public string SelectedStock => _selectedStock;
    public bool CompanyMode => _companyMode;
    public int StockCount => MarketNames.Length;

    private string[] MarketNames => _simulator is null
        ? Array.Empty<string>()
        : _companyMode
            ? _simulator.GetCompanyNames()
            : _simulator.GetUnlockedStockNames();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseExited += OnMouseExited;
        Resized += QueueRedraw;
        _font = GetThemeFont("font");
        QueueRedraw();
    }

    public void Bind(BlockTradeSimulator simulator)
    {
        Unbind();
        _simulator = simulator;
        _simulator.MarketTicked += OnMarketTicked;
        _simulator.MarketReset += OnMarketReset;
        _companyMode = false;
        string[] names = _simulator.GetUnlockedStockNames();
        if (names.Length > 0)
        {
            _selectedStock = names[0];
        }
        QueueRedraw();
    }

    public void Unbind()
    {
        if (_simulator is null)
        {
            return;
        }

        _simulator.MarketTicked -= OnMarketTicked;
        _simulator.MarketReset -= OnMarketReset;
        _simulator = null;
    }

    public void SetMarketMode(bool company)
    {
        _companyMode = company;
        _scrollOffset = 0;
        string[] names = MarketNames;
        if (names.Length > 0)
        {
            _selectedStock = names[0];
        }
        _hoverIndex = -1;
        QueueRedraw();
    }

    public void SelectStock(string ticker, bool notify = false)
    {
        if (_simulator is null || !MarketNames.Contains(ticker))
        {
            return;
        }

        if (_selectedStock == ticker && !notify)
        {
            QueueRedraw();
            return;
        }

        _selectedStock = ticker;
        EnsureVisible(ticker);
        if (notify)
        {
            StockSelected?.Invoke(ticker);
        }
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_simulator is null)
        {
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            int hover = HitTest(motion.Position);
            if (hover != _hoverIndex)
            {
                _hoverIndex = hover;
                QueueRedraw();
            }

            return;
        }

        if (@event is InputEventMouseButton wheel &&
            (wheel.ButtonIndex == MouseButton.WheelUp || wheel.ButtonIndex == MouseButton.WheelDown) &&
            wheel.Pressed)
        {
            int visible = VisibleRowCount(MarketNames.Length);
            int maxOffset = Math.Max(0, MarketNames.Length - visible);
            _scrollOffset = Math.Clamp(
                _scrollOffset + (wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1),
                0,
                maxOffset);
            _hoverIndex = -1;
            QueueRedraw();
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
        {
            int index = HitTest(click.Position);
            string[] names = MarketNames;
            if (index < 0 || index >= names.Length)
            {
                return;
            }

            _selectedStock = names[index];
            StockSelected?.Invoke(_selectedStock);
            QueueRedraw();
            AcceptEvent();
        }
    }

    private void OnMouseExited()
    {
        _hoverIndex = -1;
        QueueRedraw();
    }

    private void OnMarketTicked()
    {
        QueueRedraw();
    }

    private void OnMarketReset()
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        _font ??= GetThemeFont("font");
        if (_font is null || _simulator is null)
        {
            return;
        }

        string[] names = MarketNames;
        if (names.Length == 0)
        {
            DrawString(_font, new Vector2(12, 28), "无行情", HorizontalAlignment.Left, -1, 14, BlockTradeTheme.TextDim);
            return;
        }

        float rowHeight = RowHeight(names.Length);
        if (MarketNames.Length > 0 && !names.Contains(_selectedStock))
        {
            _selectedStock = names[0];
        }

        int visible = VisibleRowCount(names.Length);
        int maxOffset = Math.Max(0, names.Length - visible);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);
        for (int row = 0; row < visible; row++)
        {
            int index = _scrollOffset + row;
            if (index >= names.Length)
            {
                break;
            }

            DrawRow(names[index], index, new Rect2(0, row * (rowHeight + RowGap), Size.X, rowHeight));
        }

        if (maxOffset > 0)
        {
            DrawScrollbar(visible, names.Length);
        }
    }

    private void DrawScrollbar(int visible, int total)
    {
        float x = Size.X - 3.0f;
        DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), BlockTradeTheme.BorderDark, 3.0f);
        float thumbHeight = Math.Max(18.0f, Size.Y * visible / total);
        float progress = total <= visible ? 0.0f : _scrollOffset / (float)(total - visible);
        float y = (Size.Y - thumbHeight) * progress;
        DrawRect(new Rect2(x - 1.0f, y, 3.0f, thumbHeight), BlockTradeTheme.Gold);
    }

    private void DrawRow(string ticker, int index, Rect2 rect)
    {
        if (_simulator is null || _font is null)
        {
            return;
        }

        bool selected = ticker == _selectedStock;
        bool hovered = index == _hoverIndex;
        Color background = selected
            ? BlockTradeTheme.Selection
            : hovered
                ? BlockTradeTheme.PanelLight
                : BlockTradeTheme.PanelDark;

        DrawRect(rect, background);
        DrawRect(
            rect,
            selected ? BlockTradeTheme.BorderLight : BlockTradeTheme.BorderDark,
            false,
            selected ? 2.0f : 1.0f);

        if (selected)
        {
            DrawRect(new Rect2(rect.Position, new Vector2(4, rect.Size.Y)), BlockTradeTheme.Gold);
        }

        float centerY = rect.Position.Y + rect.Size.Y / 2.0f;
        var iconRect = new Rect2(rect.Position.X + 9, centerY - 16, 32, 32);
        BlockTradePixelArt.DrawIcon(this, ticker, iconRect);

        double price = _simulator.GetPrice(ticker);
        double change = _simulator.GetChangePercent(ticker);
        Color tone = change > 0.0001
            ? BlockTradeTheme.MarketUp
            : change < -0.0001
                ? BlockTradeTheme.MarketDown
                : BlockTradeTheme.Gold;

        float textX = rect.Position.X + 49;
        DrawString(_font, new Vector2(textX, rect.Position.Y + rect.Size.Y * 0.35f), ticker,
            HorizontalAlignment.Left, 110, 14, BlockTradeTheme.Text);
        DrawString(_font, new Vector2(textX, rect.Position.Y + rect.Size.Y * 0.72f), Money(price),
            HorizontalAlignment.Left, 110, 14, BlockTradeTheme.TextMuted);

        float chartWidth = Math.Min(92.0f, Math.Max(64.0f, rect.Size.X * 0.27f));
        float chartX = rect.End.X - chartWidth - 8.0f;
        var spark = new Rect2(chartX, rect.Position.Y + 8, chartWidth, rect.Size.Y - 16);
        DrawSparkline(ticker, spark, tone, selected);

        float changeWidth = Math.Max(56.0f, chartX - textX - 112.0f);
        float changeX = chartX - changeWidth - 4.0f;
        DrawString(_font, new Vector2(changeX, rect.Position.Y + rect.Size.Y * 0.40f),
            $"{change:+0.0;-0.0;0.0}%",
            HorizontalAlignment.Right, changeWidth, 14, tone);

        DrawTriangle(new Vector2(changeX + 6, rect.Position.Y + rect.Size.Y * 0.64f), change >= 0, tone);
    }

    private void DrawSparkline(string ticker, Rect2 rect, Color tone, bool selected)
    {
        if (_font is null || _simulator is null)
        {
            return;
        }

        DrawRect(rect, Color.FromHtml("#121110"));
        DrawRect(rect, selected ? BlockTradeTheme.Border : BlockTradeTheme.BorderDark, false, 1.0f);

        IReadOnlyList<double> fullHistory = _simulator.GetPriceHistory(ticker);
        if (fullHistory.Count == 0)
        {
            return;
        }

        int start = Math.Max(0, fullHistory.Count - MaxMiniPoints);
        int count = fullHistory.Count - start;
        if (count == 1)
        {
            float flatY = rect.End.Y - 3;
            DrawLine(new Vector2(rect.Position.X + 3, flatY), new Vector2(rect.End.X - 3, flatY), tone, 1.5f, false);
            DrawRect(new Rect2(new Vector2(rect.End.X - 4.5f, flatY - 1.5f), new Vector2(3, 3)), tone);
            return;
        }

        double minimum = double.MaxValue;
        double maximum = double.MinValue;
        for (int i = start; i < fullHistory.Count; i++)
        {
            minimum = Math.Min(minimum, fullHistory[i]);
            maximum = Math.Max(maximum, fullHistory[i]);
        }

        if (Math.Abs(maximum - minimum) < 0.001)
        {
            minimum -= 1.0;
            maximum += 1.0;
        }

        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            double value = fullHistory[start + i];
            float t = i / (float)(count - 1);
            float x = rect.Position.X + 3 + t * (rect.Size.X - 6);
            double normalized = (value - minimum) / (maximum - minimum);
            float y = rect.End.Y - 3 - (float)normalized * (rect.Size.Y - 6);
            points[i] = new Vector2(x, y);
        }

        DrawPolyline(points, tone, 1.5f, false);
        DrawRect(new Rect2(points[^1] - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), tone);
    }

    private void DrawTriangle(Vector2 center, bool up, Color color)
    {
        Vector2[] points = up
            ? new[] { center + new Vector2(-5, 4), center + new Vector2(5, 4), center + new Vector2(0, -4) }
            : new[] { center + new Vector2(-5, -4), center + new Vector2(5, -4), center + new Vector2(0, 4) };
        DrawColoredPolygon(points, color);
    }

    private int HitTest(Vector2 position)
    {
        if (_simulator is null)
        {
            return -1;
        }

        int count = MarketNames.Length;
        if (count == 0)
        {
            return -1;
        }

        float rowHeight = RowHeight(count);
        int row = (int)MathF.Floor(position.Y / (rowHeight + RowGap));
        int index = _scrollOffset + row;
        return row >= 0 && row < VisibleRowCount(count) && index < count ? index : -1;
    }

    private float RowHeight(int count)
    {
        float available = Size.Y - RowGap * Math.Max(0, count - 1);
        return Math.Clamp(available / Math.Max(1, count), 36.0f, 76.0f);
    }

    private int VisibleRowCount(int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        float rowHeight = RowHeight(count);
        return Math.Max(1, (int)MathF.Floor((Size.Y + RowGap) / (rowHeight + RowGap)));
    }

    private void EnsureVisible(string ticker)
    {
        string[] names = MarketNames;
        int index = Array.IndexOf(names, ticker);
        if (index < 0)
        {
            return;
        }

        int visible = VisibleRowCount(names.Length);
        if (index < _scrollOffset)
        {
            _scrollOffset = index;
        }
        else if (index >= _scrollOffset + visible)
        {
            _scrollOffset = index - visible + 1;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, names.Length - visible));
        QueueRedraw();
    }

    private static string Money(double value)
    {
        return "$" + value.ToString("#,##0.00", CultureInfo.InvariantCulture);
    }
}
