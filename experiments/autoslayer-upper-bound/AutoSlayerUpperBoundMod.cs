using System;
using System.IO;
using System.Security.Cryptography;
using Godot;
using MegaCrit.Sts2.Core.AutoSlay;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace STS2Headless.Experiments;

[ModInitializer("Initialize")]
public static class AutoSlayerUpperBoundMod
{
    private const string ExpectedSts2Sha256 =
        "0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9";
    private static SceneTree? _tree;
    private static Callable _processFrameCallback;
    private static string? _seed;
    private static string? _logFile;
    private static AutoSlayer? _autoSlayer;

    public static void Initialize()
    {
        if (!string.Equals(
                System.Environment.GetEnvironmentVariable("STS2_HEADLESS_AUTOSLAYER_EXPERIMENT"),
                "1",
                StringComparison.Ordinal))
        {
            GD.PrintErr("[STS2 Headless AutoSlayer] Disabled: explicit experiment flag is absent.");
            return;
        }

        string assemblyPath = typeof(AutoSlayer).Assembly.Location;
        string actualSha256 = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(assemblyPath))).ToLowerInvariant();
        if (!string.Equals(actualSha256, ExpectedSts2Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"AutoSlayer experiment refuses sts2.dll SHA {actualSha256}; expected {ExpectedSts2Sha256}.");
        }

        _seed = System.Environment.GetEnvironmentVariable("STS2_HEADLESS_AUTOSLAYER_SEED")
            ?? throw new InvalidOperationException("AutoSlayer experiment seed is required.");
        _logFile = System.Environment.GetEnvironmentVariable("STS2_HEADLESS_AUTOSLAYER_LOG")
            ?? throw new InvalidOperationException("AutoSlayer experiment log path is required.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_logFile))!);

        GD.Print(
            $"[STS2 Headless AutoSlayer] Waiting for initialized main menu with seed={_seed}.");
        _tree = (SceneTree)Engine.GetMainLoop();
        _processFrameCallback = Callable.From(TryStart);
        _tree.Connect(SceneTree.SignalName.ProcessFrame, _processFrameCallback);
    }

    private static void TryStart()
    {
        Control? mainMenu = _tree?.Root.GetNodeOrNull<Control>(
            "/root/Game/RootSceneContainer/MainMenu");
        if (NGame.Instance == null || mainMenu?.IsVisibleInTree() != true)
            return;

        if (_tree?.IsConnected(SceneTree.SignalName.ProcessFrame, _processFrameCallback) == true)
            _tree.Disconnect(SceneTree.SignalName.ProcessFrame, _processFrameCallback);
        GD.Print(
            $"[STS2 Headless AutoSlayer] Starting exact-build upper-bound experiment with seed={_seed}.");
        _autoSlayer = new AutoSlayer();
        _autoSlayer.Start(_seed!, _logFile);
    }
}
