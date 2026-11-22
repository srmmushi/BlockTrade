using Godot;
using Timer = Godot.Timer;

namespace BlockTrade;

/// <summary>
/// Scene coordinator: title screen -> prologue -> trading floor, plus the paid
/// activity screen, settings and global top-center notifications.
/// </summary>
public partial class BlockTradeMain : Node
{
    private BlockTradeSimulator _simulator = null!;
    private BlockTradeMainMenu _menu = null!;
    private BlockTradeStory _story = null!;
    private BlockTradeUI _ui = null!;
    private BlockTradeCasino _casino = null!;
    private BlockTradeSettingsScreen _settings = null!;
    private BlockTradeNotifications _notifications = null!;
    private BlockTradeSoundManager _sound = null!;
    private Timer _tickTimer = null!;
    private bool _gameActive;

    public override void _Ready()
    {
        _simulator = GetNode<BlockTradeSimulator>("BlockTradeSimulator");
        _menu = GetNode<BlockTradeMainMenu>("Menu");
        _story = GetNode<BlockTradeStory>("Story");
        _ui = GetNode<BlockTradeUI>("UI");
        _casino = GetNode<BlockTradeCasino>("Casino");
        _settings = GetNode<BlockTradeSettingsScreen>("Settings");
        _notifications = GetNode<BlockTradeNotifications>("Notifications");
        _sound = GetNode<BlockTradeSoundManager>("SoundManager");
        _tickTimer = GetNode<Timer>("TickTimer");

        GetWindow().MinSize = new Vector2I(1120, 680);
        BlockTradeSettingsStore.Load();
        BlockTradeSettingsStore.ApplyWindowResolution();
        GetTree().AutoAcceptQuit = false;

        _tickTimer.Autostart = false;
        _tickTimer.OneShot = true;
        _tickTimer.Stop();
        _tickTimer.WaitTime = _simulator.RefreshIntervalSeconds;
        _tickTimer.Timeout += OnTickTimeout;

        _menu.NewGameRequested += OnNewGameRequested;
        _menu.ContinueRequested += OnContinueRequested;
        _menu.SettingsRequested += OnSettingsRequested;
        _menu.QuitRequested += OnQuitRequested;
        _story.Finished += OnStoryFinished;
        _ui.CasinoRequested += OnCasinoRequested;
        _ui.SettingsRequested += OnSettingsRequested;
        _casino.Closed += OnCasinoClosed;
        _settings.Closed += OnSettingsClosed;
        _simulator.LevelUp += OnLevelUp;

        _sound.AttachClickSounds(_menu);
        _sound.AttachClickSounds(_story);
        _sound.AttachClickSounds(_ui);
        _sound.AttachClickSounds(_casino);
        _sound.AttachClickSounds(_settings);

        _ui.Visible = false;
        _story.Visible = false;
        _casino.Visible = false;
        _settings.Visible = false;
        ShowMenu();
        GD.Print($"[BlockTrade] Menu ready. Save present: {BlockTradeSaveManager.SaveFileExists()}.");
    }

    public override void _Process(double delta)
    {
        if (_gameActive && !_tickTimer.IsStopped())
        {
            _simulator.MarketRefreshRemaining = _tickTimer.TimeLeft;
        }
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_tickTimer))
        {
            _tickTimer.Timeout -= OnTickTimeout;
        }

        if (IsInstanceValid(_simulator))
        {
            _simulator.LevelUp -= OnLevelUp;
        }
    }

    public override void _Notification(int what)
    {
        if (what == (int)NotificationWMCloseRequest)
        {
            if (_gameActive && IsInstanceValid(_simulator))
            {
                _simulator.MarketRefreshRemaining = _tickTimer.TimeLeft;
                _simulator.SaveGame();
            }

            GetTree().Quit();
        }
    }

    private void ShowMenu()
    {
        _gameActive = false;
        _tickTimer.Stop();
        _ui.Visible = false;
        _story.Visible = false;
        _casino.Visible = false;
        _settings.Visible = false;
        _menu.ShowMenu(BlockTradeSaveManager.SaveFileExists());
    }

    private void OnNewGameRequested()
    {
        BlockTradeSaveManager.DeleteSave();
        _simulator.InitMarket();
        _menu.Visible = false;
        _story.Begin();
    }

    private void OnContinueRequested()
    {
        if (!BlockTradeSaveManager.SaveFileExists())
        {
            ShowMenu();
            return;
        }

        if (_simulator.LoadGame())
        {
            StartGameSession(true);
            return;
        }

        BlockTradeSaveManager.DeleteSave();
        _menu.Visible = false;
        _story.Begin();
    }

    private void OnStoryFinished()
    {
        StartGameSession(false);
    }

    private void StartGameSession(bool loaded)
    {
        _gameActive = true;
        _menu.Visible = false;
        _story.Visible = false;
        _ui.Bind(_simulator, _tickTimer, _sound);
        _ui.Visible = true;
        _ui.ShowStartupStatus(loaded, loaded);
        StartMarketTimer(_simulator.MarketRefreshRemaining);
        GD.Print($"[BlockTrade] Market open. Refresh every {_simulator.RefreshIntervalSeconds:0} seconds; loaded={loaded}.");
    }

    private void StartMarketTimer(double remaining)
    {
        double interval = Math.Clamp(_simulator.RefreshIntervalSeconds, 5.0, 600.0);
        double wait = double.IsFinite(remaining) ? Math.Clamp(remaining, 1.0, interval) : interval;
        _tickTimer.Stop();
        _tickTimer.WaitTime = wait;
        _tickTimer.Start();
    }

    private void OnTickTimeout()
    {
        double elapsed = _tickTimer.WaitTime;
        _simulator.Tick(elapsed);
        _simulator.MarketRefreshRemaining = _simulator.RefreshIntervalSeconds;

        if (!_gameActive)
        {
            return;
        }

        _tickTimer.WaitTime = _simulator.RefreshIntervalSeconds;
        _tickTimer.Start();
        _sound.Play(BlockTradeSound.Tick);
        bool saved = _simulator.SaveGame();
        _ui.NotifyAutoSave(saved);
    }

    private void OnCasinoRequested()
    {
        if (!_gameActive || _simulator.Level < 2)
        {
            return;
        }

        _tickTimer.Paused = true;
        _casino.Open(_simulator, _sound);
    }

    private void OnCasinoClosed()
    {
        _tickTimer.Paused = false;
    }

    private void OnSettingsRequested()
    {
        if (_gameActive)
        {
            _tickTimer.Paused = true;
            _simulator.MarketRefreshRemaining = _tickTimer.TimeLeft;
        }

        _settings.Open(_sound);
    }

    private void OnSettingsClosed()
    {
        if (_gameActive)
        {
            _tickTimer.Paused = false;
        }
    }

    private void OnLevelUp(int level, string unlock)
    {
        _notifications.ShowLevelUp(level, unlock);
        _sound.Play(BlockTradeSound.LevelUp);
    }

    private void OnQuitRequested()
    {
        GetTree().Quit();
    }
}
