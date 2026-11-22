using Godot;

namespace BlockTrade;

/// <summary>
/// Tiny 16x16 pixel-art stock icons drawn directly by the CanvasItem API.
/// Keeping the maps in C# avoids texture assets and stays crisp at any integer scale.
/// </summary>
public static class BlockTradePixelArt
{
    private static readonly string[] IngotArt =
    {
        "................",
        "................",
        "................",
        ".....000000.....",
        "....02222220....",
        "...0222222220...",
        "..022222222220..",
        "..011111111110..",
        "..011111111110..",
        "..031111111130..",
        "...0311111130...",
        "....00000000....",
        "................",
        "................",
        "................",
        "................",
    };

    private static readonly string[] GemArt =
    {
        "................",
        "......0000......",
        ".....022220.....",
        "....02222220....",
        "...0222222220...",
        "..022222222220..",
        ".02222222222220.",
        ".02221111112220.",
        "..022111111220..",
        "...0211111120...",
        "....02111120....",
        ".....021120.....",
        "......0220......",
        ".......00.......",
        "................",
        "................",
    };

    private static readonly string[] RedstoneArt =
    {
        "................",
        "....00....00....",
        "...0220..0220...",
        "..022220022220..",
        "..021122111120..",
        "...0111111110...",
        "....00111100....",
        "..00..0110..00..",
        ".0220.0110.0220.",
        ".02220111102220.",
        "..011111111110..",
        "...0111111110...",
        "....00111100....",
        "......0000......",
        "................",
        "................",
    };

    private static readonly string[] CoalArt =
    {
        "...0000..000....",
        "..02222002220...",
        ".0222222222220..",
        "..021122222220..",
        "...0112222220...",
        "..00.0112220....",
        ".0220..01100....",
        ".022220.0000....",
        "..02222220......",
        "...01112220.....",
        "..00.0112200....",
        ".0220..011220...",
        ".0222222222220..",
        "..00000000000...",
        "................",
        "................",
    };

    private static readonly string[] CompanyArt =
    {
        "................",
        "......0000......",
        ".....022220.....",
        "....02222220....",
        "....02111120....",
        "....02222220....",
        "....02111120....",
        "....02222220....",
        "...0222222220...",
        "...0211111110...",
        "...0212222210...",
        "...0211111110...",
        "...0222222220...",
        "...0000000000...",
        "................",
        "................",
    };

    public static string DisplayName(string ticker) => ticker;

    private static bool IsCompany(string ticker) => ticker switch
    {
        "深岩控股" or "潮汐能源" or "苍穹科技" or "星链物流" or
            "极光资本" or "熔炉工业" or "黑曜集团" or "末地银行" => true,
        _ => false,
    };

    private static string ArtKey(string ticker) => ticker;

    public static Color LineColor(string ticker) => IsCompany(ticker)
        ? Color.FromHtml("#e7b52f")
        : ArtKey(ticker) switch
        {
            "金锭" => Color.FromHtml("#e7b52f"),
            "钻石" => Color.FromHtml("#4ed6d8"),
            "红石" => Color.FromHtml("#e2655c"),
            "绿宝石" => Color.FromHtml("#65d66d"),
            "煤炭" => Color.FromHtml("#9a9a9a"),
            _ => Color.FromHtml("#e0e0e0"),
        };

    public static string Description(string ticker) => ticker switch
    {
        "铁锭" => "矿洞里最可靠的硬通货",
        "金锭" => "闪亮而稳重的避险资产",
        "钻石" => "价格昂贵，波动也最大",
        "红石" => "红石电路的昂贵原料",
        "绿宝石" => "村民珍藏的高价宝石",
        "煤炭" => "最朴素也最耐烧的燃料",
        "青金石" => "附魔与蓝色染料的重要原料",
        "石英" => "建筑与红石装置的基础材料",
        "黑曜石" => "坚固而昂贵的深层方块",
        "下界合金" => "极其稀有的顶级材料",
        "深岩控股" => "深岩层资源与基建集团",
        "潮汐能源" => "海洋能源与电力公司",
        "苍穹科技" => "高空平台与科技企业",
        "星链物流" => "连接各维度的物流网络",
        "极光资本" => "专注长期投资的资本集团",
        "熔炉工业" => "重工业与冶炼巨头",
        "黑曜集团" => "高风险高市值的控股集团",
        "末地银行" => "市值最高的金融巨头",
        _ => "方块市场的交易品",
    };

    public static void DrawIcon(CanvasItem target, string ticker, Rect2 bounds)
    {
        bool company = IsCompany(ticker);
        string key = ArtKey(ticker);
        string[] art = company
            ? CompanyArt
            : key switch
            {
                "红石" => RedstoneArt,
                "煤炭" => CoalArt,
                "钻石" or "绿宝石" or "青金石" or "下界合金" => GemArt,
                _ => IngotArt,
            };

        (Color outline, Color main, Color light, Color shade) = Palette(key, company);
        int scale = Math.Max(1, (int)MathF.Floor(MathF.Min(bounds.Size.X, bounds.Size.Y) / 16.0f));
        var origin = new Vector2(
            bounds.Position.X + MathF.Floor((bounds.Size.X - 16 * scale) / 2.0f),
            bounds.Position.Y + MathF.Floor((bounds.Size.Y - 16 * scale) / 2.0f));

        for (int y = 0; y < 16; y++)
        {
            string row = art[y];
            for (int x = 0; x < Math.Min(16, row.Length); x++)
            {
                Color color = row[x] switch
                {
                    '0' => outline,
                    '1' => main,
                    '2' => light,
                    '3' => shade,
                    _ => Colors.Transparent,
                };
                if (color.A <= 0.0f)
                {
                    continue;
                }

                target.DrawRect(new Rect2(origin + new Vector2(x * scale, y * scale), new Vector2(scale, scale)), color);
            }
        }
    }

    private static (Color Outline, Color Main, Color Light, Color Shade) Palette(string ticker, bool company)
    {
        if (company)
        {
            return ticker switch
            {
                "深岩控股" => (Color.FromHtml("#1b1711"), Color.FromHtml("#8d7a56"), Color.FromHtml("#d8c59a"), Color.FromHtml("#4f432c")),
                "潮汐能源" => (Color.FromHtml("#071d2a"), Color.FromHtml("#2f8fba"), Color.FromHtml("#8edcf2"), Color.FromHtml("#15506b")),
                "苍穹科技" => (Color.FromHtml("#0b2327"), Color.FromHtml("#3cc7c9"), Color.FromHtml("#b6ffff"), Color.FromHtml("#177679")),
                "星链物流" => (Color.FromHtml("#171126"), Color.FromHtml("#8064c9"), Color.FromHtml("#c9b8ff"), Color.FromHtml("#463476")),
                "极光资本" => (Color.FromHtml("#0b2618"), Color.FromHtml("#49b976"), Color.FromHtml("#b0f7c8"), Color.FromHtml("#226c43")),
                "熔炉工业" => (Color.FromHtml("#2a160b"), Color.FromHtml("#c8752f"), Color.FromHtml("#ffc27d"), Color.FromHtml("#7a3f16")),
                "黑曜集团" => (Color.FromHtml("#0c0912"), Color.FromHtml("#403257"), Color.FromHtml("#9b82c4"), Color.FromHtml("#241a33")),
                "末地银行" => (Color.FromHtml("#241028"), Color.FromHtml("#b34ac7"), Color.FromHtml("#f0a6ff"), Color.FromHtml("#6b2378")),
                _ => (Color.FromHtml("#1c160b"), Color.FromHtml("#b9933a"), Color.FromHtml("#f2d47a"), Color.FromHtml("#6e531b")),
            };
        }

        return ticker switch
        {
            "金锭" => (Color.FromHtml("#2a1c07"), Color.FromHtml("#e7b52f"), Color.FromHtml("#ffe68a"), Color.FromHtml("#a66b12")),
            "钻石" => (Color.FromHtml("#08292e"), Color.FromHtml("#4ed6d8"), Color.FromHtml("#c8ffff"), Color.FromHtml("#258b96")),
            "绿宝石" => (Color.FromHtml("#0b2a14"), Color.FromHtml("#3fbf6b"), Color.FromHtml("#a5f6b8"), Color.FromHtml("#1c743e")),
            "红石" => (Color.FromHtml("#310b09"), Color.FromHtml("#c63f35"), Color.FromHtml("#f47a6b"), Color.FromHtml("#711d18")),
            "煤炭" => (Color.FromHtml("#0b0b0b"), Color.FromHtml("#3b3b3b"), Color.FromHtml("#777777"), Color.FromHtml("#1a1a1a")),
            "青金石" => (Color.FromHtml("#0a1233"), Color.FromHtml("#3155b8"), Color.FromHtml("#8ea8ff"), Color.FromHtml("#1a327d")),
            "石英" => (Color.FromHtml("#332c25"), Color.FromHtml("#d8cbb4"), Color.FromHtml("#fff8e8"), Color.FromHtml("#9b8b76")),
            "黑曜石" => (Color.FromHtml("#0a0711"), Color.FromHtml("#2a203d"), Color.FromHtml("#6d5b91"), Color.FromHtml("#151022")),
            "下界合金" => (Color.FromHtml("#1b0d0d"), Color.FromHtml("#7d3b3b"), Color.FromHtml("#d98a72"), Color.FromHtml("#4a2020")),
            _ => (Color.FromHtml("#202020"), Color.FromHtml("#cfcfcf"), Color.FromHtml("#f8f8f8"), Color.FromHtml("#818181")),
        };
    }
}
