using Godot;
using FileAccess = Godot.FileAccess;

namespace BlockTrade;

/// <summary>Developer helper that renders menu, story and game screenshots.</summary>
public partial class BlockTradeScreenshot : Node
{
    public override async void _Ready()
    {
        string? backup = FileAccess.FileExists(BlockTradeSaveManager.DefaultSavePath)
            ? ReadAllText(BlockTradeSaveManager.DefaultSavePath)
            : null;
        BlockTradeSaveManager.DeleteSave();
        int exitCode = 0;

        try
        {
            var packed = GD.Load<PackedScene>("res://Scenes/Main.tscn");
            Node main = packed.Instantiate();
            AddChild(main);
            await WaitFrame();

            await Capture("res://Docs/blocktrade-menu.png");

            Button newGame = FindNamed<Button>(main, "NewGameBtn")!;
            newGame.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-story.png");

            Button skip = FindNamed<Button>(main, "StorySkipBtn")!;
            skip.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-auth.png");

            LineEdit authPassword = FindNamed<LineEdit>(main, "AuthPasswordInput")!;
            LineEdit authConfirm = FindNamed<LineEdit>(main, "AuthConfirmInput")!;
            authPassword.Text = "123456";
            authConfirm.Text = "123456";
            FindNamed<Button>(main, "AuthConfirmBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();

            var simulator = main.GetNode<BlockTradeSimulator>("BlockTradeSimulator");
            simulator.Buy("煤炭", 4);
            simulator.Buy("铁锭", 1);
            for (int i = 0; i < 24; i++)
            {
                simulator.Tick(0.5);
            }

            Label? status = FindNamed<Label>(main, "StatusLabel");
            if (status is not null)
            {
                status.Text = "市场已开盘 // 选择股票，等待下一轮行情";
                status.AddThemeColorOverride("font_color", BlockTradeTheme.TextMuted);
            }

            await WaitFrame();
            await Capture("res://Docs/blocktrade-ui.png");

            FindNamed<Button>(main, "TransferBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-transfer.png");
            FindNamed<Button>(main, "TransferCancelBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();

            FindNamed<Button>(main, "LoanBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-loan.png");
            FindNamed<Button>(main, "LoanCloseBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();

            simulator.AddExperience(30);
            await WaitFrame();
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-levelup.png");
            await ToSignal(GetTree().CreateTimer(3.2), SceneTreeTimer.SignalName.Timeout);

            simulator.AddExperience(1000);
            simulator.AddCash(500);
            await ToSignal(GetTree().CreateTimer(3.2), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-company.png");
            Button casinoButton = FindNamed<Button>(main, "CasinoBtn")!;
            casinoButton.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            Button scratchTab = FindNamed<Button>(main, "ScratchTabBtn")!;
            scratchTab.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            FindNamed<Button>(main, "ScratchBuyBtn")!.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-scratch.png");
            Button wheelTab = FindNamed<Button>(main, "WheelTabBtn")!;
            wheelTab.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-casino.png");

            Button casinoClose = FindNamed<Button>(main, "CasinoCloseBtn")!;
            casinoClose.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            Button settingsButton = FindNamed<Button>(main, "SettingsBtn")!;
            settingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            await Capture("res://Docs/blocktrade-settings.png");
            Button settingsClose = FindNamed<Button>(main, "SettingsCloseBtn")!;
            settingsClose.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFrame();

            main.QueueFree();
            await WaitFrame();
        }
        catch (Exception exception)
        {
            exitCode = 1;
            GD.PushError($"[SCREENSHOT] {exception}");
        }
        finally
        {
            if (backup is null)
            {
                BlockTradeSaveManager.DeleteSave();
            }
            else
            {
                WriteAllText(BlockTradeSaveManager.DefaultSavePath, backup);
            }
        }

        GetTree().Quit(exitCode);
    }

    private async Task WaitFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private async Task Capture(string path)
    {
        await WaitFrame();
        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(path);
        GD.Print($"SCREENSHOT {path} {image.GetWidth()}x{image.GetHeight()} error={error}");
        if (error != Error.Ok)
        {
            throw new InvalidOperationException($"Could not save screenshot: {path}");
        }
    }

    private static T? FindNamed<T>(Node root, string name) where T : Node
    {
        if (root is T typed && root.Name == name)
        {
            return typed;
        }

        foreach (Node child in root.GetChildren())
        {
            T? found = FindNamed<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string ReadAllText(string path)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText() ?? string.Empty;
    }

    private static void WriteAllText(string path, string text)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }
}
