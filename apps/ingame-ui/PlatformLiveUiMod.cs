using Godot;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using static Godot.Control;

namespace STS2PlatformLiveUi;

#if !STS2_PLATFORM_UNIFIED
[ModInitializer("Initialize")]
#endif
public static class PlatformLiveUiMod
{
    private static bool _initialized;
    private static PlatformLivePanel? _panel;

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
            var tree = (SceneTree)Engine.GetMainLoop();
            var panel = new PlatformLivePanel();
            GD.Print($"[STS2 Platform Live UI] identity {JsonSerializer.Serialize(RuntimeIdentity())}");
            layer.AddChild(panel.Root);
            GD.Print("[STS2 Platform Live UI] adding layer to SceneTree root");
            tree.Root.AddChild(layer);
            panel.Mount(tree);
            _panel = panel;
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

internal sealed class PlatformLivePanel : IDisposable
{
    private readonly PlatformLiveStatusClient _statusClient = new();
    private readonly PlatformLiveLayoutState _defaultLayout = PlatformLiveLayoutState.Defaults;
    private readonly Dictionary<string, Label> _pageText = new(StringComparer.Ordinal);
    private readonly Dictionary<PlatformCommandMode, Button> _modeButtons = new();
    private readonly Dictionary<STS2HumanAnnotator.Core.RecordingCommandKind, Button> _recordingButtons = new();
    private readonly List<PlatformLiveToast> _toasts = new();
    private SceneTree? _tree;
    private Action? _processFrameHandler;
    private Control _hud = null!;
    private Label _hudText = null!;
    private PanelContainer _workspace = null!;
    private Control _workspaceBody = null!;
    private PanelContainer _recorderCard = null!;
    private VBoxContainer _toastStack = null!;
    private Label _workspaceTitle = null!;
    private Label _recorderTitle = null!;
    private Button _workspaceCollapse = null!;
    private Button _recorderCollapse = null!;
    private Vector2 _dragOrigin;
    private Vector2 _dragStart;
    private Vector2 _resizeOrigin;
    private Vector2 _resizeStart;
    private bool _draggingWorkspace;
    private bool _draggingRecorder;
    private bool _resizingWorkspace;
    private PlatformLiveLayoutState _layout;
    private Label _connection = null!;
    private Label _command = null!;
    private Button _tickButton = null!;
    private PlatformCommandMode _mode = PlatformCommandMode.Human;
    private bool _kWasPressed;
    private bool _escapeWasPressed;
    private bool _disposed;
    private int _pollInFlight;
    private PlatformLiveStatus? _pendingStatus;
    private string? _pendingPollError;

    internal Control Root { get; } = new()
    {
        Visible = true,
        MouseFilter = MouseFilterEnum.Ignore
    };

    internal PlatformLivePanel()
    {
        Root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _layout = PlatformLiveLayout.Load();
        BuildUi();

        var timer = new Godot.Timer
        {
            WaitTime = 1.0,
            Autostart = true,
            OneShot = false
        };
        timer.Timeout += OnPollTimeout;
        Root.AddChild(timer);
    }

    internal void Mount(SceneTree tree)
    {
        try
        {
            _tree = tree;
            _processFrameHandler = OnProcessFrame;
            tree.ProcessFrame += _processFrameHandler;
            Root.TreeExiting += Dispose;
            ApplyLayout();
            _ = PollAsync();
            GD.Print("[STS2 Platform Live UI] panel ready; input=K; visible=false; HUD=visible");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[STS2 Platform Live UI] panel mount failed: {exception}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_tree != null && _processFrameHandler != null && GodotObject.IsInstanceValid(_tree))
            _tree.ProcessFrame -= _processFrameHandler;
        _statusClient.Dispose();
    }

    private void BuildUi()
    {
        _hud = new PanelContainer
        {
            Position = new Vector2(16, 16),
            Size = new Vector2(470, 48),
            MouseFilter = MouseFilterEnum.Ignore
        };
        var hudRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _hudText = new Label
        {
            Text = "Platform | Human | Recorder: Ready | Connector: polling",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        hudRow.AddChild(_hudText);
        _hud.AddChild(hudRow);
        Root.AddChild(_hud);

        _workspace = new PanelContainer
        {
            Position = _layout.WorkspacePosition,
            Size = _layout.WorkspaceSize,
            CustomMinimumSize = new Vector2(560, 360),
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        _workspace.GuiInput += OnWorkspaceInput;
        Root.AddChild(_workspace);

        _workspaceBody = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        _workspaceBody.AddThemeConstantOverride("separation", 8);
        _workspace.AddChild(_workspaceBody);
        // Let empty title-bar space bubble to the bounded workspace drag handler;
        // child buttons still stop input themselves.
        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _workspaceTitle = new Label
        {
            Text = "STS2 PLATFORM / LIVE WORKSPACE",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _workspaceTitle.AddThemeFontSizeOverride("font_size", 20);
        titleRow.AddChild(_workspaceTitle);
        titleRow.AddChild(BuildCommandButton("Reset layout", ResetLayout, "Restore the local presentation layout."));
        _workspaceCollapse = BuildCommandButton("Collapse", ToggleWorkspaceCollapse);
        titleRow.AddChild(_workspaceCollapse);
        titleRow.AddChild(BuildCommandButton("Close", HidePanel, "Close workspace; HUD remains visible."));
        _workspaceBody.AddChild(titleRow);

        _connection = new Label
        {
            Text = "Connector: polling...",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _workspaceBody.AddChild(_connection);

        var modeRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        modeRow.AddChild(BuildModeButton("Human", PlatformCommandMode.Human));
        modeRow.AddChild(BuildModeButton("Shadow", PlatformCommandMode.Shadow));
        modeRow.AddChild(BuildModeButton("One-Step", PlatformCommandMode.OneStep));
        modeRow.AddChild(BuildModeButton("Auto", PlatformCommandMode.Auto));
        _tickButton = BuildCommandButton(
            "Tick",
            () => _ = TickRuntimeAsync(),
            "Ask Policy Runtime for one bounded tick; action authority remains Connector/Runtime.");
        _tickButton.Disabled = true;
        modeRow.AddChild(_tickButton);
        _workspaceBody.AddChild(modeRow);

        _command = new Label
        {
            Text = "Human is the safe default. UI never submits gameplay actions.",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _workspaceBody.AddChild(_command);

        var tabs = new TabContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _workspaceBody.AddChild(tabs);
        AddPage(tabs, "Overview");
        AddPage(tabs, "Environment");
        AddPage(tabs, "Policy");
        AddPage(tabs, "Human Data");
        AddPage(tabs, "Diagnostics");
        tabs.CurrentTab = Math.Clamp(_layout.LastPage, 0, 4);
        tabs.TabChanged += page =>
        {
            _layout = _layout with { LastPage = (int)page };
            PersistLayout();
        };

        BuildRecorderCard();
        _toastStack = new VBoxContainer
        {
            Position = new Vector2(500, 18),
            Size = new Vector2(380, 180),
            MouseFilter = MouseFilterEnum.Ignore
        };
        Root.AddChild(_toastStack);
        ApplyLayout();
    }

    private void BuildRecorderCard()
    {
        _recorderCard = new PanelContainer
        {
            Position = _layout.RecorderPosition,
            Size = new Vector2(360, 188),
            CustomMinimumSize = new Vector2(320, 120),
            MouseFilter = MouseFilterEnum.Stop
        };
        _recorderCard.GuiInput += OnRecorderInput;
        var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        _recorderCard.AddChild(body);
        // The card title is the drag surface; lifecycle buttons remain interactive.
        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _recorderTitle = new Label
        {
            Text = "RECORDER / Ready",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        titleRow.AddChild(_recorderTitle);
        _recorderCollapse = BuildCommandButton("Collapse", ToggleRecorderCollapse);
        titleRow.AddChild(_recorderCollapse);
        body.AddChild(titleRow);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.StartNewSession] = BuildRecordingButton(
            body, "New Session", STS2HumanAnnotator.Core.RecordingCommandKind.StartNewSession);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Pause] = BuildRecordingButton(
            body, "Pause", STS2HumanAnnotator.Core.RecordingCommandKind.Pause);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Resume] = BuildRecordingButton(
            body, "Resume", STS2HumanAnnotator.Core.RecordingCommandKind.Resume);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Close] = BuildRecordingButton(
            body, "Close", STS2HumanAnnotator.Core.RecordingCommandKind.Close);
        Root.AddChild(_recorderCard);
    }

    private Button BuildRecordingButton(
        VBoxContainer body,
        string text,
        STS2HumanAnnotator.Core.RecordingCommandKind kind)
    {
        var button = BuildCommandButton(text, () => ApplyRecordingCommand(kind));
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(button);
        return button;
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

    private void OnWorkspaceInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                if (mouseButton.Position.X >= _workspace.Size.X - 32
                    && mouseButton.Position.Y >= _workspace.Size.Y - 32)
                {
                    _resizingWorkspace = true;
                    _resizeOrigin = mouseButton.Position;
                    _resizeStart = _workspace.Size;
                    _workspace.GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingWorkspace = true;
                    _dragOrigin = mouseButton.Position;
                    _dragStart = _workspace.Position;
                }
            }
            else if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed)
            {
                if (_resizingWorkspace || _draggingWorkspace)
                    PersistLayout();
                _resizingWorkspace = false;
                _draggingWorkspace = false;
            }
        }
        else if (@event is InputEventMouseMotion motion && (_resizingWorkspace || _draggingWorkspace))
        {
            if (_resizingWorkspace)
            {
                Vector2 delta = motion.Position - _resizeOrigin;
                Rect2 clamped = PlatformLiveLayout.ClampWorkspace(
                    new Rect2(_workspace.Position, _resizeStart + delta), Root.Size);
                _workspace.Position = clamped.Position;
                _workspace.Size = clamped.Size;
            }
            else
            {
                _workspace.Position = PlatformLiveLayout.ClampWorkspace(
                    new Rect2(_dragStart + motion.Position - _dragOrigin, _workspace.Size), Root.Size).Position;
            }
            _workspace.GetViewport().SetInputAsHandled();
        }
    }

    private void OnRecorderInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _draggingRecorder = true;
                _dragOrigin = mouseButton.Position;
                _dragStart = _recorderCard.Position;
            }
            else
            {
                if (_draggingRecorder)
                    PersistLayout();
                _draggingRecorder = false;
            }
        }
        else if (@event is InputEventMouseMotion motion && _draggingRecorder)
        {
            _recorderCard.Position = PlatformLiveLayout.ClampRecorder(
                _dragStart + motion.Position - _dragOrigin,
                _recorderCard.Size,
                Root.Size);
            _recorderCard.GetViewport().SetInputAsHandled();
        }
    }

    private void ToggleWorkspaceCollapse()
    {
        _layout = _layout with { WorkspaceCollapsed = !_layout.WorkspaceCollapsed };
        SetWorkspaceCollapsed(_layout.WorkspaceCollapsed);
        _workspaceCollapse.Text = _layout.WorkspaceCollapsed ? "Expand" : "Collapse";
        PersistLayout();
        PushToast("layout.workspace", _layout.WorkspaceCollapsed ? "Workspace collapsed." : "Workspace expanded.");
    }

    private void ToggleRecorderCollapse()
    {
        _layout = _layout with { RecorderCollapsed = !_layout.RecorderCollapsed };
        foreach (Button button in _recordingButtons.Values)
            button.Visible = !_layout.RecorderCollapsed;
        _recorderCollapse.Text = _layout.RecorderCollapsed ? "Expand" : "Collapse";
        PersistLayout();
        PushToast("layout.recorder", _layout.RecorderCollapsed ? "Recorder card collapsed." : "Recorder card expanded.");
    }

    private void ResetLayout()
    {
        _layout = _defaultLayout;
        ApplyLayout();
        PersistLayout();
        PushToast("layout.reset", "Layout reset to defaults.");
    }

    private void ApplyLayout()
    {
        Rect2 workspace = PlatformLiveLayout.ClampWorkspace(
            new Rect2(_layout.WorkspacePosition, _layout.WorkspaceSize), Root.Size);
        _workspace.Position = workspace.Position;
        _workspace.Size = workspace.Size;
        SetWorkspaceCollapsed(_layout.WorkspaceCollapsed);
        _workspaceCollapse.Text = _layout.WorkspaceCollapsed ? "Expand" : "Collapse";
        _recorderCard.Position = PlatformLiveLayout.ClampRecorder(
            _layout.RecorderPosition, _recorderCard.Size, Root.Size);
        foreach (Button button in _recordingButtons.Values)
            button.Visible = !_layout.RecorderCollapsed;
        _recorderCollapse.Text = _layout.RecorderCollapsed ? "Expand" : "Collapse";
    }

    private void SetWorkspaceCollapsed(bool collapsed)
    {
        _workspaceBody.Visible = true;
        var children = _workspaceBody.GetChildren();
        for (int index = 1; index < children.Count; index++)
            if (children[index] is CanvasItem item)
                item.Visible = !collapsed;
    }

    private void PersistLayout()
    {
        _layout = _layout with
        {
            WorkspacePosition = _workspace.Position,
            WorkspaceSize = _workspace.Size,
            RecorderPosition = _recorderCard.Position
        };
        if (!PlatformLiveLayout.Save(_layout))
            PushToast("layout.persistence", "Layout could not be saved; using this session only.");
    }

    private void PushToast(string key, string message)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _toasts.RemoveAll(toast => toast.Key == key);
        _toasts.Add(new PlatformLiveToast(key, message, now.AddSeconds(4)));
        while (_toasts.Count > 4)
            _toasts.RemoveAt(0);
        RenderToasts();
    }

    private void RenderToasts()
    {
        if (_toastStack == null)
            return;
        foreach (Node child in _toastStack.GetChildren())
            child.QueueFree();
        foreach (PlatformLiveToast toast in _toasts)
        {
            var item = new Button
            {
                Text = $"{toast.Message}  ×",
                TooltipText = "Dismiss notification",
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            item.Pressed += () =>
            {
                _toasts.RemoveAll(current => current.Key == toast.Key);
                RenderToasts();
            };
            _toastStack.AddChild(item);
        }
    }

    private void ExpireToasts()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_toasts.RemoveAll(toast => toast.ExpiresAt <= now) > 0)
            RenderToasts();
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

    private void ApplyRecordingCommand(STS2HumanAnnotator.Core.RecordingCommandKind kind)
    {
        var command = new STS2HumanAnnotator.Core.RecordingCommand(
            $"live-ui-{Guid.NewGuid():N}",
            kind);
        STS2HumanAnnotator.Core.RecordingCommandResult result =
            STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.Execute(command);
        STS2HumanAnnotator.Core.RecordingApplicationStatus authoritative =
            STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.QueryStatus();
        _command.Text = result.Accepted
            ? $"Recording: {authoritative.Lifecycle.State}. {authoritative.Detail}"
            : $"Recording control rejected: {result.Detail}";
        PushToast(
            $"recording.{kind}",
            result.Accepted
                ? $"Recording {kind.ToString().Replace("StartNewSession", "started", StringComparison.Ordinal).ToLowerInvariant()}."
                : $"Recording command rejected: {result.Detail}");
        ApplyRecordingAvailability(authoritative);
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
            PushToast("policy.mode", $"Policy mode: {ToRuntimeMode(mode)}.");
            _ = PollAsync();
        }
        catch (Exception exception)
        {
            _command.Text = $"Policy Runtime unavailable: {exception.Message}";
            PushToast("policy.error", $"Policy command rejected: {exception.Message}");
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
            PushToast("policy.tick", "Policy Runtime tick completed.");
            _ = PollAsync();
        }
        catch (Exception exception)
        {
            _command.Text = $"Policy Runtime unavailable: {exception.Message}";
            PushToast("policy.error", $"Policy tick rejected: {exception.Message}");
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
                Interlocked.Exchange(ref _pendingStatus, status);
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref _pendingPollError, exception.GetType().Name);
        }
        finally
        {
            Volatile.Write(ref _pollInFlight, 0);
        }
    }

    private void ApplyPendingStatus()
    {
        if (Interlocked.Exchange(ref _pendingStatus, null) is { } status)
        {
            ApplyStatus(status);
        }
    }

    private void ApplyPendingPollError()
    {
        if (Interlocked.Exchange(ref _pendingPollError, null) is { } error)
        {
            _connection.Text = $"Connector loopback: UI poll failed ({error})";
            _hudText.Text = $"Platform | Human | Recorder: unknown | Connector: error ({error})";
            PushToast("transport.error", $"Status transport unavailable: {error}");
        }
    }

    private void ApplyStatus(PlatformLiveStatus status)
    {
        _connection.Text =
            $"Connector: {status.TransportStatus} | Policy Runtime: {status.PolicyRuntimeTransportStatus} | observed {status.ObservedAt:HH:mm:ss} UTC";
        string policyReason = PlatformLiveLayout.PolicyUnavailableReason(status);
        SetPolicyControlsAvailable(status.PolicyRuntime != null, policyReason);
        if (status.PolicyRuntime != null)
            _mode = ParseRuntimeMode(status.PolicyRuntime.Mode);
        ApplyRecordingAvailability(status.Recording);
        _hudText.Text = $"Platform | {status.PolicyRuntime?.Mode ?? "Human"} | Recorder: {status.Recording.Lifecycle.State} | Connector: {status.TransportStatus}";
        _pageText["Overview"].Text = FormatOverview(status);
        _pageText["Environment"].Text = FormatEnvironment(status);
        _pageText["Policy"].Text = FormatPolicy(status);
        _pageText["Human Data"].Text = FormatHumanData(status);
        _pageText["Diagnostics"].Text = FormatDiagnostics(status);
    }

    private void SetPolicyControlsAvailable(bool available, string? reason = null)
    {
        foreach (Button button in _modeButtons.Values)
        {
            button.Disabled = !available;
            button.TooltipText = available
                ? button.TooltipText
                : $"Unavailable: {reason ?? "Policy Runtime is unavailable."}";
        }
        _tickButton.Disabled = !available;
        _tickButton.TooltipText = available
            ? "Ask Policy Runtime for one bounded tick; action authority remains Connector/Runtime."
            : $"Unavailable: {reason ?? "Policy Runtime is unavailable."}";
    }

    private void ApplyRecordingAvailability(
        STS2HumanAnnotator.Core.RecordingApplicationStatus recording)
    {
        if (_recorderTitle == null)
            return;
        _recorderTitle.Text = $"RECORDER / {recording.Lifecycle.State} · {recording.Counters.Records} records";
        STS2HumanAnnotator.Core.RecordingLifecycleState state = recording.Lifecycle.State;
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.StartNewSession].Disabled =
            state is not (STS2HumanAnnotator.Core.RecordingLifecycleState.Ready
                or STS2HumanAnnotator.Core.RecordingLifecycleState.Closed);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Pause].Disabled =
            state != STS2HumanAnnotator.Core.RecordingLifecycleState.Recording;
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Resume].Disabled =
            state != STS2HumanAnnotator.Core.RecordingLifecycleState.Paused;
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Close].Disabled =
            state is not (STS2HumanAnnotator.Core.RecordingLifecycleState.Recording
                or STS2HumanAnnotator.Core.RecordingLifecycleState.Paused);
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
        $"Recording: {status.Recording.Lifecycle.State}"
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
        $"Lifecycle: {status.Recording.Lifecycle.State} ({status.Recording.Detail})",
        $"Session: {status.Recording.Session?.SessionId ?? "none"}",
        $"Run / timeline: {status.Recording.Session?.RunId ?? "none"} / {status.Recording.Session?.TimelineId ?? "none"}",
        $"Profile: {status.Recording.Session?.CaptureProfileId ?? "none"}",
        $"Runtime state: {status.Recording.RuntimeState}",
        $"Records / invalidations: {status.Recording.Counters.Records} / {status.Recording.Counters.Invalidations}",
        $"Recorded by family: {FormatCounts(status.Recording.Scope.RecordedByActionFamily)}",
        $"Native-accepted but failed closed (not recorded): {FormatCounts(status.Recording.Scope.AcceptedFailedClosedByActionFamily)}",
        $"Supported, not observed: {FormatItems(status.Recording.Scope.SupportedNotObserved)}",
        $"Declared out of scope: {FormatItems(status.Recording.Scope.DeclaredOutOfScope)}",
        $"Profile boundary: {status.Recording.Scope.Detail}",
        $"Reads materialized / failed: {status.Recording.Counters.ReadsMaterialized} / {status.Recording.Counters.ReadsFailed}",
        $"Pending root: {status.Recording.PendingRoot?.RecordId ?? "none"}",
        $"Last record: {status.Recording.LastRecord?.Id ?? "none"}",
        $"Last invalidation: {status.Recording.LastInvalidation?.Id ?? "none"}",
        $"Required Reads / append / disk: {status.Recording.Health.RequiredReads} / {status.Recording.Health.Append} / {status.Recording.Health.Disk}",
        $"Closeout: {status.Recording.Closeout.State}",
        $"Recording event sequence: {status.Recording.LatestEventSequence}",
        $"Current Snapshot: {status.Recording.CurrentSnapshotId ?? "unavailable"}",
        $"Available/materialized Reads: {status.Reads.Count}",
        $"Recorder blockers: {(status.Recording.Blockers.Count == 0 ? "none" : string.Join(", ", status.Recording.Blockers))}",
        $"Runtime invalidations: {(status.PolicyRuntime == null ? "unavailable" : status.Invalidations.Count.ToString())}",
        $"Invalidation reasons: {(status.PolicyRuntime == null ? "unavailable" : string.Join(", ", status.Invalidations))}",
        "Recording directory: intentionally not exposed to the UI"
    });

    private static string FormatCounts(IReadOnlyDictionary<string, long> values) =>
        values.Count == 0
            ? "none"
            : string.Join(", ", values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));

    private static string FormatItems(IReadOnlyList<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);

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

    private void OnProcessFrame()
    {
        ApplyPendingStatus();
        ApplyPendingPollError();
        ExpireToasts();
        if (!_draggingWorkspace && !_draggingRecorder && !_resizingWorkspace)
        {
            Rect2 clamped = PlatformLiveLayout.ClampWorkspace(
                new Rect2(_workspace.Position, _workspace.Size), Root.Size);
            if (clamped.Position != _workspace.Position || clamped.Size != _workspace.Size)
            {
                _workspace.Position = clamped.Position;
                _workspace.Size = clamped.Size;
            }
            _recorderCard.Position = PlatformLiveLayout.ClampRecorder(
                _recorderCard.Position, _recorderCard.Size, Root.Size);
        }

        bool kPressed = Input.IsKeyPressed(Key.K) || Input.IsPhysicalKeyPressed(Key.K);
        if (kPressed && !_kWasPressed)
        {
            _workspace.Visible = !_workspace.Visible;
            if (_workspace.Visible)
                _ = PollAsync();
            GD.Print($"[STS2 Platform Live UI] toggle; input=K; visible={_workspace.Visible.ToString().ToLowerInvariant()}");
            Root.GetViewport().SetInputAsHandled();
        }
        _kWasPressed = kPressed;

        bool escapePressed = Input.IsKeyPressed(Key.Escape);
        if (_workspace.Visible && escapePressed && !_escapeWasPressed)
        {
            HidePanel();
            Root.GetViewport().SetInputAsHandled();
        }
        _escapeWasPressed = escapePressed;
    }

    private void HidePanel()
    {
        _workspace.Visible = false;
        PersistLayout();
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
