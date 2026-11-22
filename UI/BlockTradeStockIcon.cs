using Godot;

namespace BlockTrade;

/// <summary>A single pixel-art item icon for a stock.</summary>
public partial class BlockTradeStockIcon : Control
{
    private string _stockName = "铁锭";

    public string StockName
    {
        get => _stockName;
        set
        {
            _stockName = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        BlockTradePixelArt.DrawIcon(this, _stockName, new Rect2(Vector2.Zero, Size));
    }
}
