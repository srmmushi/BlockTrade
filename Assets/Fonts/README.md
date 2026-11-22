# BlockTrade Font

## Minecraft AE

- File: `Minecraft(同时支持中英文).ttf`
- Internal family: `Minecraft AE`
- Internal style: `Pixel`
- Role: all Chinese and Latin UI, story, logo, numbers and stock tickers.
- Provided by the project owner; no separate font license file was supplied with this repository.

The font is imported with pixel-friendly settings:

```text
antialiasing=0
hinting=0
subpixel_positioning=0
```

`BlockTradeTheme.FontPath` points directly to this file, so menu, story and trading UI all use the same typeface. A system-font fallback is still configured for any glyph missing from the custom font.

> This is a user-supplied third-party font asset. Confirm redistribution/embedding rights with its source before shipping a public build.
