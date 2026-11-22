using Godot;
using FileAccess = Godot.FileAccess;
using GodotDictionary = Godot.Collections.Dictionary;

namespace BlockTrade;

/// <summary>Small JSON-backed preferences file. Not part of the game save.</summary>
public static class BlockTradeSettingsStore
{
    public const string SettingsPath = "user://blocktrade_settings.json";
    public const int CurrentVersion = 1;

    public static bool SoundEnabled { get; set; } = true;
    public static float Volume { get; set; } = 0.8f;
    public static int WindowWidth { get; set; } = 1280;
    public static int WindowHeight { get; set; } = 720;

    private static bool _loaded;

    public static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (!FileAccess.FileExists(SettingsPath))
            {
                return;
            }

            using FileAccess? file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                return;
            }

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok ||
                json.Data.VariantType != Variant.Type.Dictionary)
            {
                return;
            }

            GodotDictionary root = json.Data.AsGodotDictionary();
            if (root.TryGetValue("sound_enabled", out Variant enabled) &&
                enabled.VariantType == Variant.Type.Bool)
            {
                SoundEnabled = enabled.AsBool();
            }

            if (root.TryGetValue("volume", out Variant volume) &&
                volume.VariantType is Variant.Type.Float or Variant.Type.Int)
            {
                Volume = Math.Clamp((float)volume.AsDouble(), 0.0f, 1.0f);
            }

            if (root.TryGetValue("window_width", out Variant width) &&
                root.TryGetValue("window_height", out Variant height) &&
                width.VariantType is Variant.Type.Int or Variant.Type.Float &&
                height.VariantType is Variant.Type.Int or Variant.Type.Float)
            {
                WindowWidth = Math.Clamp(width.AsInt32(), 1120, 3840);
                WindowHeight = Math.Clamp(height.AsInt32(), 680, 2160);
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[BlockTrade] Settings load failed: {exception.Message}");
        }
    }

    public static void ApplyWindowResolution()
    {
        var size = new Vector2I(WindowWidth, WindowHeight);
        if (DisplayServer.WindowGetSize() == size)
        {
            return;
        }

        DisplayServer.WindowSetSize(size);
        Vector2I screen = DisplayServer.ScreenGetSize();
        DisplayServer.WindowSetPosition((screen - size) / 2);
    }

    public static bool Save()
    {
        try
        {
            var root = new GodotDictionary
            {
                ["version"] = CurrentVersion,
                ["sound_enabled"] = SoundEnabled,
                ["volume"] = Volume,
                ["window_width"] = WindowWidth,
                ["window_height"] = WindowHeight,
            };

            using FileAccess? file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
            if (file is null)
            {
                return false;
            }

            file.StoreString(Json.Stringify(root, "  ", true, true));
            file.Flush();
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[BlockTrade] Settings save failed: {exception.Message}");
            return false;
        }
    }
}
