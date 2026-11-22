using Godot;

namespace BlockTrade;

public enum BlockTradeSound
{
    Click,
    Buy,
    Sell,
    Error,
    LevelUp,
    Win,
    Lose,
    Spin,
    Scratch,
    Transfer,
    Loan,
    Repay,
    Auth,
    Company,
    Tick,
    Save,
    Negative,
}

/// <summary>
/// Tiny chiptune sound-effect bank synthesized for this project. A small pool of
/// AudioStreamPlayers lets click, payout and level-up sounds overlap naturally.
/// </summary>
public partial class BlockTradeSoundManager : Node
{
    private const int PlayerPoolSize = 10;

    private readonly Dictionary<BlockTradeSound, AudioStream> _streams = new();
    private readonly List<AudioStreamPlayer> _players = new();
    private int _nextPlayer;

    public override void _Ready()
    {
        BlockTradeSettingsStore.Load();
        LoadStream(BlockTradeSound.Click, "click");
        LoadStream(BlockTradeSound.Buy, "buy");
        LoadStream(BlockTradeSound.Sell, "sell");
        LoadStream(BlockTradeSound.Error, "error");
        LoadStream(BlockTradeSound.LevelUp, "levelup");
        LoadStream(BlockTradeSound.Win, "win");
        LoadStream(BlockTradeSound.Lose, "lose");
        LoadStream(BlockTradeSound.Spin, "spin");
        LoadStream(BlockTradeSound.Scratch, "scratch");
        LoadStream(BlockTradeSound.Transfer, "transfer");
        LoadStream(BlockTradeSound.Loan, "loan");
        LoadStream(BlockTradeSound.Repay, "repay");
        LoadStream(BlockTradeSound.Auth, "auth");
        LoadStream(BlockTradeSound.Company, "company");
        LoadStream(BlockTradeSound.Tick, "tick");
        LoadStream(BlockTradeSound.Save, "save");
        LoadStream(BlockTradeSound.Negative, "negative");

        for (int i = 0; i < PlayerPoolSize; i++)
        {
            var player = new AudioStreamPlayer { Name = $"SfxPlayer{i}" };
            AddChild(player);
            _players.Add(player);
        }
    }

    public void Play(BlockTradeSound sound)
    {
        if (!BlockTradeSettingsStore.SoundEnabled ||
            BlockTradeSettingsStore.Volume <= 0.001f ||
            _players.Count == 0 ||
            !_streams.TryGetValue(sound, out AudioStream? stream))
        {
            return;
        }

        AudioStreamPlayer player = _players[_nextPlayer++ % _players.Count];
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(BlockTradeSettingsStore.Volume);
        player.Play();
    }

    public void ApplySettings()
    {
        foreach (AudioStreamPlayer player in _players)
        {
            player.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, BlockTradeSettingsStore.Volume));
        }
    }

    /// <summary>Attach one generic click sound to every button under a screen.</summary>
    public void AttachClickSounds(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is BaseButton button && !button.HasMeta("blocktrade_click_sound"))
            {
                button.SetMeta("blocktrade_click_sound", true);
                button.Pressed += () => Play(BlockTradeSound.Click);
            }

            AttachClickSounds(child);
        }
    }

    private void LoadStream(BlockTradeSound sound, string fileName)
    {
        string path = $"res://Assets/Audio/{fileName}.wav";
        AudioStream? stream = GD.Load<AudioStream>(path);
        if (stream is null)
        {
            GD.PushWarning($"[BlockTrade] Missing sound: {path}");
            return;
        }

        _streams[sound] = stream;
    }
}
