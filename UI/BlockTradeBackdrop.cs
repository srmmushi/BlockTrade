using Godot;

namespace BlockTrade;

/// <summary>
/// A cheap, code-drawn block grid behind the UI. This keeps the pixel look
/// without shipping any textures and scales with the window.
/// </summary>
public partial class BlockTradeBackdrop : Control
{
    private const float TileSize = 32.0f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), BlockTradeTheme.Background);

        int columns = Mathf.CeilToInt(Size.X / TileSize);
        int rows = Mathf.CeilToInt(Size.Y / TileSize);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Color color = (x + y) % 2 == 0
                    ? BlockTradeTheme.BackgroundTileA
                    : BlockTradeTheme.BackgroundTileB;
                DrawRect(new Rect2(x * TileSize, y * TileSize, TileSize, TileSize), color);
            }
        }

        // A hard, dark frame keeps the content feeling like a game window.
        DrawRect(new Rect2(Vector2.Zero, Size), BlockTradeTheme.BorderDark, false, 8.0f);
        DrawRect(new Rect2(8, 8, Size.X - 16, Size.Y - 16), Color.FromHtml("#101010"), false, 2.0f);
    }
}
