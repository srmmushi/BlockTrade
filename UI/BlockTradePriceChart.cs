using Godot;

namespace BlockTrade;

/// <summary>
/// Chart for the selected item only. History lives in the simulator so it can
/// be saved and restored. A brand-new game has one point, drawn as a flat line
/// at the bottom; real refreshes extend it upward or downward.
/// </summary>
public partial class BlockTradePriceChart : Control
{
    private const int MaxPoints = BlockTradeSimulator.MaxPriceHistory;
    private const float LineWidth = 2.0f;
    private static readonly Color GridColor = Color.FromHtml("#2a2a2a");

    private readonly List<Vector2> _points = new(64);
    private BlockTradeSimulator? _simulator;
    private Font? _font;
    private string _stockName = "铁锭";

    public string StockName => _stockName;
    public int SeriesCount => _simulator is not null && _simulator.GetPriceHistory(_stockName).Count > 0 ? 1 : 0;
    public int SelectedPointCount => _simulator?.GetPriceHistory(_stockName).Count ?? 0;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
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

    public void SetStock(string stockName)
    {
        if (string.IsNullOrWhiteSpace(stockName) || stockName == _stockName)
        {
            return;
        }

        _stockName = stockName;
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        Unbind();
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
        DrawRect(new Rect2(Vector2.Zero, Size), BlockTradeTheme.PanelDark);
        if (Size.X < 40.0f || Size.Y < 40.0f)
        {
            return;
        }

        float left = 58.0f;
        float top = 12.0f;
        float right = Size.X - 12.0f;
        float bottom = Size.Y - 20.0f;
        var plot = new Rect2(left, top, Math.Max(1.0f, right - left), Math.Max(1.0f, bottom - top));

        DrawGrid(plot);

        if (_simulator is null)
        {
            DrawChartText("等待行情...", new Vector2(left + 12.0f, top + 28.0f), BlockTradeTheme.TextDim, 14);
            DrawBorder(plot);
            return;
        }

        IReadOnlyList<double> history = _simulator.GetPriceHistory(_stockName);
        if (history.Count == 0)
        {
            DrawChartText("等待首次刷新...", new Vector2(left + 12.0f, top + 28.0f), BlockTradeTheme.TextDim, 14);
            DrawBorder(plot);
            return;
        }

        double basePrice = _simulator.GetBasePrice(_stockName);
        double minimum = Math.Min(basePrice, history.Min());
        double maximum = Math.Max(basePrice, history.Max());
        if (maximum - minimum < 0.01)
        {
            maximum = minimum + Math.Max(1.0, minimum * 0.20);
        }
        else
        {
            maximum += (maximum - minimum) * 0.10;
        }

        DrawAxisLabel(maximum, top + 8.0f);
        DrawAxisLabel((maximum + minimum) / 2.0, (top + bottom) / 2.0f + 4.0f);
        DrawAxisLabel(minimum, bottom - 2.0f);

        double change = _simulator.GetChangePercent(_stockName);
        Color lineColor = change > 0.0001
            ? BlockTradeTheme.MarketUp
            : change < -0.0001
                ? BlockTradeTheme.MarketDown
                : BlockTradeTheme.Gold;

        if (history.Count == 1)
        {
            float y = MapPrice(history[0], plot, minimum, maximum);
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), lineColor, LineWidth, false);
            DrawRect(new Rect2(new Vector2(plot.End.X - 3, y - 3), new Vector2(6, 6)), lineColor);
        }
        else
        {
            _points.Clear();
            for (int i = 0; i < history.Count; i++)
            {
                float t = i / (float)(history.Count - 1);
                float x = plot.Position.X + t * plot.Size.X;
                _points.Add(new Vector2(x, MapPrice(history[i], plot, minimum, maximum)));
            }

            DrawPolyline(_points.ToArray(), lineColor, LineWidth, false);
            Vector2 last = _points[^1];
            DrawRect(new Rect2(last - new Vector2(3, 3), new Vector2(6, 6)), lineColor);
        }

        DrawBorder(plot);
        DrawChartText($"{history.Count}/{MaxPoints} 点", new Vector2(plot.Position.X + 8.0f, plot.End.Y + 16.0f),
            BlockTradeTheme.TextDim, 14);
        string ceiling = "$" + _simulator.GetPriceCeiling(_stockName).ToString("0.00");
        DrawChartText($"上限 {ceiling}", new Vector2(plot.End.X - 126.0f, plot.End.Y + 16.0f),
            BlockTradeTheme.Gold, 14);
    }

    private static float MapPrice(double price, Rect2 plot, double minimum, double maximum)
    {
        double normalized = (price - minimum) / (maximum - minimum);
        return plot.End.Y - (float)Math.Clamp(normalized, 0.0, 1.0) * plot.Size.Y;
    }

    private void DrawGrid(Rect2 plot)
    {
        for (int i = 0; i <= 4; i++)
        {
            float y = plot.Position.Y + plot.Size.Y * (i / 4.0f);
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), GridColor, 1.0f, false);
        }
    }

    private void DrawAxisLabel(double value, float baselineY)
    {
        DrawChartText(value.ToString("0.0"), new Vector2(2.0f, baselineY), BlockTradeTheme.TextDim, 14);
    }

    private void DrawChartText(string text, Vector2 position, Color color, int fontSize)
    {
        if (_font is null)
        {
            return;
        }

        DrawString(_font, position, text, HorizontalAlignment.Left, -1, fontSize, color);
    }

    private void DrawBorder(Rect2 rect)
    {
        DrawRect(rect, BlockTradeTheme.Border, false, 2.0f);
    }
}
