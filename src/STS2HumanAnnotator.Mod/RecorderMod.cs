using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace STS2HumanAnnotator.Mod;

[ModInitializer("Initialize")]
public static class RecorderMod
{
    public const string Version = "0.1.0";

    public static void Initialize()
    {
        try
        {
            string modDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location)
                ?? throw new InvalidOperationException("Annotator assembly directory is unavailable.");
            AnnotatorConfiguration configuration = AnnotatorConfiguration.Load(modDirectory);
            RecorderRuntime.Initialize(configuration);
            new Harmony("rsgcsg.sts2-human-annotator").PatchAll(typeof(RecorderMod).Assembly);
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Connect(
                SceneTree.SignalName.ProcessFrame,
                Callable.From(RecorderRuntime.OnProcessFrame));
            GD.Print(
                $"[STS2 Human Annotator] v{Version} observer initialized; session={RecorderRuntime.SessionId}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[STS2 Human Annotator] initialization failed: {exception}");
        }
    }
}
