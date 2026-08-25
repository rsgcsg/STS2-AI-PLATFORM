using Godot;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace STS2PlatformLiveUi;

#if !STS2_PLATFORM_UNIFIED
[ModInitializer("Initialize")]
#endif
public static class PlatformLiveUiMod
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        try
        {
            var layer = new CanvasLayer
            {
                Layer = 100
            };
            GD.Print($"[STS2 Platform Live UI] identity {JsonSerializer.Serialize(RuntimeIdentity())}");
            layer.AddChild(new PlatformLivePanel());
            GD.Print("[STS2 Platform Live UI] adding layer to SceneTree root");
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(layer);
            GD.Print("[STS2 Platform Live UI] layer added; press K to toggle. Gameplay actions are not exposed directly.");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[STS2 Platform Live UI] initialization failed: {exception}");
        }
    }

    private static object RuntimeIdentity()
    {
        PlatformArtifactIdentity identity = CurrentArtifactIdentity();
        return new
        {
            schema = "sts2.platform/live-ui-loaded-identity-1",
            loaded_at = DateTimeOffset.UtcNow.ToString("O"),
            artifact_sha256 = identity.ArtifactSha256,
            module_version_id = identity.ModuleVersionId,
            source_revision = identity.SourceRevision,
            source_digest_sha256 = ReadAssemblyMetadata("LiveUiSourceDigestSha256", "SourceDigestSha256"),
            version = identity.Version
        };
    }

    internal static PlatformArtifactIdentity CurrentArtifactIdentity()
    {
        Assembly assembly = typeof(PlatformLiveUiMod).Assembly;
        Dictionary<string, string> metadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(item => item.Key, item => item.Value ?? "", StringComparer.Ordinal);
        string location = assembly.Location;
        string sha256 = File.Exists(location)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(location))).ToLowerInvariant()
            : "unavailable";
        return new PlatformArtifactIdentity(
            "STS2 Platform Live UI",
            assembly.GetName().Version?.ToString() ?? "unavailable",
            metadata.GetValueOrDefault(
                "LiveUiSourceRevision",
                metadata.GetValueOrDefault("SourceRevision", "unavailable")),
            assembly.ManifestModule.ModuleVersionId.ToString(),
            sha256);
    }

    private static string ReadAssemblyMetadata(params string[] keys)
    {
        AssemblyMetadataAttribute[] metadata = typeof(PlatformLiveUiMod).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToArray();
        foreach (string key in keys)
        {
            string? value = metadata.FirstOrDefault(item =>
                    string.Equals(item.Key, key, StringComparison.Ordinal))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "unavailable";
    }
}

internal enum PlatformCommandMode
{
    Human,
    Shadow,
    OneStep,
    Auto
}

internal sealed class PlatformLivePanel : Control
{
    private readonly PlatformLiveStatusClient _statusClient = new();
    private readonly Dictionary<string, Label> _pageText = new(StringComparer.Ordinal);
    private readonly Dictionary<PlatformCommandMode, Button> _modeButtons = new();
    private Label _connection = null!;
    private Label _command = null!;
    private Button _tickButton = null!;
    private PlatformCommandMode _mode = PlatformCommandMode.Human;
    private int _pollInFlight;
    private PlatformLiveStatus? _pendingStatus;
    private string? _pendingPollError;

    public override void _Ready()
    {
        GD.Print("[STS2 Platform Live UI] panel _Ready entered");
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            Visible = false;
            MouseFilter = MouseFilterEnum.Stop;
            GuiInput += ConsumeGuiInput;
            SetProcessInput(true);
            BuildUi();

            var timer = new Godot.Timer
            {
                WaitTime = 1.0,
                Autostart = true,
                OneShot = false
            };
            timer.Timeout += OnPollTimeout;
            AddChild(timer);
            _ = PollAsync();
            GD.Print("[STS2 Platform Live UI] panel ready; input=K; visible=false");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[STS2 Platform Live UI] panel _Ready failed: {exception}");
        }
    }

    public override void _ExitTree() => _statusClient.Dispose();

    private void BuildUi()
    {
        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(margin);

        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(860, 580)
        };
        margin.AddChild(panel);

        var body = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        body.AddThemeConstantOverride("separation", 10);
        panel.AddChild(body);

        var title = new Label
        {
            Text = "STS2 PLATFORM / LIVE",
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        titleRow.AddChild(title);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleRow.AddChild(spacer);
        titleRow.AddChild(BuildCommandButton("Close", HidePanel));
        body.AddChild(titleRow);

        _connection = new Label
        {
            Text = "Connector loopback: polling...",
            MouseFilter = MouseFilterEnum.Ignore
        };
        body.AddChild(_connection);

        var modeRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        modeRow.AddChild(BuildModeButton("Human", PlatformCommandMode.Human));
        modeRow.AddChild(BuildModeButton("Shadow", PlatformCommandMode.Shadow));
        modeRow.AddChild(BuildModeButton("One-Step", PlatformCommandMode.OneStep));
        modeRow.AddChild(BuildModeButton("Auto", PlatformCommandMode.Auto));
        _tickButton = BuildCommandButton(
            "Tick",
            () => _ = TickRuntimeAsync(),
            "Ask Policy Runtime for one bounded tick. Any action remains Connector-authorized and Runtime-submitted.");
        _tickButton.Disabled = true;
        modeRow.AddChild(_tickButton);
        body.AddChild(modeRow);

        var recordingRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        Button start = BuildCommandButton("Start", () =>
        {
            _command.Text = "Start is not exposed by the current typed Annotator API; the session starts in recording state.";
        });
        start.Disabled = true;
        start.TooltipText = "Pending final Annotator Start() API; current sessions start recording on initialization.";
        recordingRow.AddChild(start);
        recordingRow.AddChild(BuildCommandButton("Pause", () => ApplyRecordingCommand(
            STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.Pause)));
        recordingRow.AddChild(BuildCommandButton("Resume", () => ApplyRecordingCommand(
            STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.Resume)));
        recordingRow.AddChild(BuildCommandButton("Close", () => ApplyRecordingCommand(
            STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.Close)));
        body.AddChild(recordingRow);

        _command = new Label
        {
            Text = "Mode Human. These controls do not submit gameplay actions.",
            MouseFilter = MouseFilterEnum.Ignore
        };
        body.AddChild(_command);

        var tabs = new TabContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddChild(tabs);
        AddPage(tabs, "Overview");
        AddPage(tabs, "Environment");
        AddPage(tabs, "Policy");
        AddPage(tabs, "Human Data");
        AddPage(tabs, "Diagnostics");
    }

    private Button BuildModeButton(string text, PlatformCommandMode mode)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = mode == PlatformCommandMode.OneStep
                ? "Run exactly one Policy Runtime decision, then return to Human."
                : $"Set Policy Runtime mode to {text}; the UI never submits a BoundAction directly.",
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(108, 36),
            Disabled = true
        };
        button.Pressed += () => _ = SetRuntimeModeAsync(mode);
        _modeButtons.Add(mode, button);
        return button;
    }

    private static Button BuildCommandButton(string text, Action action, string? tooltip = null)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip ?? $"Annotator recording control: {text}",
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(108, 36)
        };
        button.Pressed += action;
        return button;
    }

    private void AddPage(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Stop,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        var label = new Label
        {
            Text = "Waiting for typed Platform live status...",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        scroll.AddChild(label);
        tabs.AddChild(scroll);
        _pageText.Add(name, label);
    }

    private void ApplyRecordingCommand(Func<STS2HumanAnnotator.Core.RecordingControlResult> command)
    {
        STS2HumanAnnotator.Core.RecordingControlResult result = command();
        _command.Text = result.Accepted
            ? $"Recording: {result.Snapshot.State}. {result.Detail}"
            : $"Recording control rejected: {result.Detail}";
        _ = PollAsync();
    }

    private void OnPollTimeout() => _ = PollAsync();

    private async Task SetRuntimeModeAsync(PlatformCommandMode mode)
    {
        _mode = mode;
        _command.Text = $"Policy Runtime: setting mode {ToRuntimeMode(mode)}...";
        try
        {
            await _statusClient.SetModeAsync(ToRuntimeMode(mode));
            if (mode == PlatformCommandMode.OneStep)
            {
                await _statusClient.TickAsync();
                _command.Text = "Policy Runtime One-Step completed and returned control according to Runtime status.";
            }
            else
            {
                _command.Text = $"Policy Runtime mode set to {ToRuntimeMode(mode)}.";
            }
            _ = PollAsync();
        }
        catch (Exception exception)
        {
            _command.Text = $"Policy Runtime unavailable: {exception.Message}";
            SetPolicyControlsAvailable(false);
        }
    }

    private async Task TickRuntimeAsync()
    {
        _command.Text = "Policy Runtime: ticking with max_ticks=1...";
        try
        {
            await _statusClient.TickAsync();
            _command.Text = "Policy Runtime tick completed.";
            _ = PollAsync();
        }
        catch (Exception exception)
        {
            _command.Text = $"Policy Runtime unavailable: {exception.Message}";
            SetPolicyControlsAvailable(false);
        }
    }

    private async Task PollAsync()
    {
        if (Interlocked.Exchange(ref _pollInFlight, 1) != 0)
            return;
        try
        {
            PlatformLiveStatus status = await _statusClient.ReadAsync();
            _pendingStatus = status;
            CallDeferred(nameof(ApplyPendingStatus));
        }
        catch (Exception exception)
        {
            _pendingPollError = exception.GetType().Name;
            CallDeferred(nameof(ApplyPendingPollError));
        }
        finally
        {
            Volatile.Write(ref _pollInFlight, 0);
        }
    }

    private void ApplyPendingStatus()
    {
        if (_pendingStatus is { } status)
        {
            _pendingStatus = null;
            ApplyStatus(status);
        }
    }

    private void ApplyPendingPollError()
    {
        if (_pendingPollError is { } error)
        {
            _pendingPollError = null;
            _connection.Text = $"Connector loopback: UI poll failed ({error})";
        }
    }

    private void ApplyStatus(PlatformLiveStatus status)
    {
        _connection.Text =
            $"Connector: {status.TransportStatus} | Policy Runtime: {status.PolicyRuntimeTransportStatus} | observed {status.ObservedAt:HH:mm:ss} UTC";
        SetPolicyControlsAvailable(status.PolicyRuntime != null);
        if (status.PolicyRuntime != null)
            _mode = ParseRuntimeMode(status.PolicyRuntime.Mode);
        _pageText["Overview"].Text = FormatOverview(status);
        _pageText["Environment"].Text = FormatEnvironment(status);
        _pageText["Policy"].Text = FormatPolicy(status);
        _pageText["Human Data"].Text = FormatHumanData(status);
        _pageText["Diagnostics"].Text = FormatDiagnostics(status);
    }

    private void SetPolicyControlsAvailable(bool available)
    {
        foreach (Button button in _modeButtons.Values)
            button.Disabled = !available;
        _tickButton.Disabled = !available;
    }

    private static string FormatOverview(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "LIVE OVERVIEW",
        $"Transport: {status.TransportStatus}",
        $"Policy Runtime: {status.PolicyRuntimeTransportStatus}",
        $"Snapshot: {status.Snapshot?.SnapshotId ?? "none"} ({status.Snapshot?.Status ?? "unavailable"})",
        $"Interaction: {status.Snapshot?.Interaction.Kind ?? "none"}",
        $"Scores: {string.Join(", ", status.Scores.Select(score => $"{score.Name}={score.DisplayValue}"))}",
        $"Selected: {(status.PolicyRuntime == null ? "unavailable" : status.Selected.Count == 0 ? "none" : string.Join(", ", status.Selected.Select(item => item.Label)))}",
        $"Receipt: {status.Receipt.Status} ({status.Receipt.Detail})",
        $"Reads: {status.Reads.Count} current Connector opportunities/materializations",
        $"Recording: {status.Recording.Control.State}"
    });

    private static string FormatEnvironment(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "ENVIRONMENT",
        FormatArtifact("Game", status.ExactIdentity.Game),
        FormatArtifact("Connector", status.ExactIdentity.Connector),
        FormatArtifact("Annotator", status.ExactIdentity.Annotator),
        FormatArtifact("Platform Live UI", status.ExactIdentity.LiveUi),
        $"Host kind: {status.ExactIdentity.HostKind ?? "unavailable"}",
        $"Runtime instance: {status.ExactIdentity.RuntimeInstanceId ?? "unavailable"}",
        $"Environment fingerprint: {status.ExactIdentity.EnvironmentFingerprint ?? "unavailable"}",
        $"Modset: {status.ExactIdentity.ModsetStatus ?? "unavailable"}",
        $"Modset fingerprint: {status.ExactIdentity.ModsetFingerprint ?? "unavailable"}",
        $"Loaded Mod IDs: {(status.ExactIdentity.LoadedModIds.Count == 0 ? "none" : string.Join(", ", status.ExactIdentity.LoadedModIds))}"
    });

    private static string FormatPolicy(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "POLICY",
        $"Runtime: {status.PolicyRuntimeTransportStatus}",
        $"Runtime software: {status.PolicyRuntime?.Runtime.Version ?? "unavailable"} / {status.PolicyRuntime?.Runtime.CodeSha256 ?? "unavailable"}",
        $"Mode: {status.PolicyRuntime?.Mode ?? "unavailable"}",
        $"Policy: {status.PolicyRuntime?.Policy.PolicyId ?? "unavailable"} {status.PolicyRuntime?.Policy.PolicyVersion ?? ""}".TrimEnd(),
        $"Run: {status.PolicyRuntime?.RunId ?? "unavailable"}",
        $"Lifecycle: {status.PolicyRuntime?.Lifecycle ?? "unavailable"}",
        $"Provider/architecture: {status.PolicyRuntime?.Policy.Provider ?? "unavailable"} / {status.PolicyRuntime?.Policy.Architecture ?? "unavailable"}",
        $"Artifact SHA-256: {status.PolicyRuntime?.Policy.ArtifactSha256 ?? "unavailable"}",
        $"Controller: {status.PolicyRuntime?.Controller ?? "unavailable"}",
        $"Refreshing: {status.PolicyRuntime?.Refreshing.ToString() ?? "unknown"}",
        $"Tainted: {status.PolicyRuntime?.Tainted.ToString() ?? "unknown"} ({status.PolicyRuntime?.TaintReason ?? "none"})",
        $"Last decision: {status.PolicyRuntime?.LastDecision?.DecisionId ?? "none"}",
        $"Connector information policy: {status.Snapshot?.InformationPolicy.Id ?? "unavailable"}",
        "UI boundary: Connector observation, Policy Runtime commands, and Annotator recording control only; no direct gameplay action submission."
    });

    private static string FormatHumanData(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "HUMAN DATA",
        $"Control: {status.Recording.Control.State} ({status.Recording.Detail})",
        $"Session: {status.Recording.Control.SessionId ?? "uninitialized"}",
        $"Runtime state: {status.Recording.RuntimeState}",
        $"Current Snapshot: {status.Recording.CurrentSnapshotId ?? "unavailable"}",
        $"Available/materialized Reads: {status.Reads.Count}",
        $"Recorder blockers: {(status.Recording.Blockers.Count == 0 ? "none" : string.Join(", ", status.Recording.Blockers))}",
        $"Runtime invalidations: {(status.PolicyRuntime == null ? "unavailable" : status.Invalidations.Count.ToString())}",
        $"Invalidation reasons: {(status.PolicyRuntime == null ? "unavailable" : string.Join(", ", status.Invalidations))}",
        "Recording directory: intentionally not exposed to the UI"
    });

    private static string FormatDiagnostics(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "DIAGNOSTICS",
        $"Schema: {status.Schema}",
        $"Policy Runtime status: {status.PolicyRuntime?.Schema ?? "unavailable"}",
        $"Runtime errors: {(status.PolicyRuntime?.Errors.Count > 0 ? string.Join(" | ", status.PolicyRuntime.Errors) : "none")}",
        $"Transport errors: {(status.Errors.Count == 0 ? "none" : string.Join(" | ", status.Errors))}",
        $"Runtime detail: {status.PolicyRuntimeTransportDetail ?? "none"}",
        $"Annotator detail: {status.Recording.Detail}",
        "Exact identity is displayed as observed; unavailable fields are not inferred."
    });

    private static string FormatArtifact(string name, PlatformArtifactIdentity? artifact) =>
        artifact == null
            ? $"{name}: unavailable"
            : $"{name}: {artifact.Product} {artifact.Version} | rev={artifact.SourceRevision ?? "unknown"} | module={artifact.ModuleVersionId ?? "unknown"} | sha={artifact.ArtifactSha256 ?? "unknown"}";

    private void ConsumeGuiInput(InputEvent @event)
    {
        if (Visible)
            GetViewport().SetInputAsHandled();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;
        if (key.Keycode == Key.K || key.PhysicalKeycode == Key.K)
        {
            Visible = !Visible;
            if (Visible)
                _ = PollAsync();
            GD.Print($"[STS2 Platform Live UI] toggle; input=K; visible={Visible.ToString().ToLowerInvariant()}");
            GetViewport().SetInputAsHandled();
            return;
        }
        if (Visible && key.Keycode == Key.Escape)
        {
            HidePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void HidePanel()
    {
        Visible = false;
        GD.Print("[STS2 Platform Live UI] toggle; input=close; visible=false");
    }

    private static string ToRuntimeMode(PlatformCommandMode mode) => mode switch
    {
        PlatformCommandMode.Human => "human",
        PlatformCommandMode.Shadow => "shadow",
        PlatformCommandMode.OneStep => "one_step",
        PlatformCommandMode.Auto => "auto",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static PlatformCommandMode ParseRuntimeMode(string mode) => mode switch
    {
        "human" => PlatformCommandMode.Human,
        "shadow" => PlatformCommandMode.Shadow,
        "one_step" => PlatformCommandMode.OneStep,
        "auto" => PlatformCommandMode.Auto,
        _ => PlatformCommandMode.Human
    };
}
