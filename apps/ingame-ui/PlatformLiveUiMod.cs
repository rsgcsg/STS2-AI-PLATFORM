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
    private readonly List<STS2HumanAnnotator.Core.RecordingEvent> _actionFeed = new();
    private readonly List<PlatformLiveToast> _toasts = new();
    private static readonly Color TextPrimary = new("#f3f6fb");
    private static readonly Color TextSecondary = new("#b9c5d6");
    private static readonly Color Accent = new("#62c4d8");
    private SceneTree? _tree;
    private Action? _processFrameHandler;
    private PanelContainer _workspace = null!;
    private Control _workspaceSurface = null!;
    private Control _workspaceBody = null!;
    private PanelContainer _recorderPage = null!;
    private Control _recorderDetails = null!;
    private VBoxContainer _toastStack = null!;
    private Label _workspaceTitle = null!;
    private Label _recorderTitle = null!;
    private Label _recorderHealth = null!;
    private Label _lastAction = null!;
    private VBoxContainer _actionFeedList = null!;
    private ScrollContainer _actionFeedScroll = null!;
    private Button _workspaceCollapse = null!;
    private Button _recorderCollapse = null!;
    private Vector2 _dragOrigin;
    private Vector2 _dragStart;
    private Vector2 _resizeOrigin;
    private Vector2 _resizeStart;
    private bool _draggingWorkspace;
    private bool _resizingWorkspace;
    private TabContainer _tabs = null!;
    private PlatformLiveLayoutState _layout;
    private Label _connection = null!;
    private Label _command = null!;
    private string _connectorTransport = "checking";
    private Button _tickButton = null!;
    private PlatformCommandMode _mode = PlatformCommandMode.Human;
    private bool _kWasPressed;
    private bool _escapeWasPressed;
    private bool _disposed;
    private int _pollInFlight;
    private PlatformLiveStatus? _pendingStatus;
    private string? _pendingPollError;
    private long _lastRecordingEventSequence;
    private string? _actionFeedSessionId;

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
            GD.Print("[STS2 Platform Live UI] panel ready; input=K; visible=false; HUD=hidden");
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
        _workspace = new PanelContainer
        {
            Position = _layout.WorkspacePosition,
            Size = _layout.WorkspaceSize,
            CustomMinimumSize = new Vector2(560, 360),
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        _workspace.AddThemeStyleboxOverride("panel", MakePanelStyle(
            new Color("#111c2aeF"), new Color("#4e9bb0"), 12, 2, 16));
        Root.AddChild(_workspace);

        _workspaceSurface = new Control { MouseFilter = MouseFilterEnum.Stop };
        _workspaceSurface.GuiInput += OnWorkspaceInput;
        _workspace.AddChild(_workspaceSurface);
        _workspaceBody = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        _workspaceBody.AddThemeConstantOverride("separation", 7);
        _workspaceSurface.AddChild(_workspaceBody);
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
        _workspaceTitle.AddThemeColorOverride("font_color", TextPrimary);
        titleRow.AddChild(_workspaceTitle);
        titleRow.AddChild(BuildCommandButton("Reset layout", ResetLayout, "Restore the local presentation layout."));
        _workspaceCollapse = BuildCommandButton("Collapse", ToggleWorkspaceCollapse);
        titleRow.AddChild(_workspaceCollapse);
        titleRow.AddChild(BuildCommandButton("Close", HidePanel, "Close workspace and return to gameplay."));
        _workspaceBody.AddChild(titleRow);

        _connection = new Label
        {
            Text = "Connector: polling...",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _connection.AddThemeFontSizeOverride("font_size", 13);
        _connection.AddThemeColorOverride("font_color", TextSecondary);
        _workspaceBody.AddChild(_connection);

        var modeRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        modeRow.AddThemeConstantOverride("separation", 4);
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
        _command.AddThemeFontSizeOverride("font_size", 13);
        _command.AddThemeColorOverride("font_color", Accent);
        _workspaceBody.AddChild(_command);

        var tabs = new TabContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _tabs = tabs;
        tabs.AddThemeFontSizeOverride("font_size", 13);
        tabs.AddThemeColorOverride("font_selected_color", TextPrimary);
        tabs.AddThemeColorOverride("font_unselected_color", TextSecondary);
        _workspaceBody.AddChild(tabs);
        BuildRecorderPage(tabs);
        AddPage(tabs, "Overview");
        AddPage(tabs, "Environment");
        AddPage(tabs, "Policy");
        AddPage(tabs, "Human Data");
        AddPage(tabs, "Diagnostics");
        tabs.CurrentTab = Math.Clamp(_layout.LastPage, 0, 5);
        tabs.TabChanged += page =>
        {
            _layout = _layout with { LastPage = (int)page };
            PersistLayout();
        };

        _toastStack = new VBoxContainer
        {
            Position = new Vector2(16, 360),
            Size = new Vector2(380, 140),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _workspaceSurface.AddChild(_toastStack);
        ApplyLayout();
    }

    private void BuildRecorderPage(TabContainer tabs)
    {
        _recorderPage = new PanelContainer
        {
            Name = "Recorder",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        _recorderPage.AddThemeStyleboxOverride("panel", PageStyle("Recorder"));
        var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        body.AddThemeConstantOverride("separation", 8);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(body);
        var contentScroll = new ScrollContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        contentScroll.AddChild(margin);
        _recorderPage.AddChild(contentScroll);
        // Recorder is a first-class Workspace tab; lifecycle buttons remain interactive.
        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _recorderTitle = new Label
        {
            Text = "RECORDER",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _recorderTitle.AddThemeFontSizeOverride("font_size", 17);
        _recorderTitle.AddThemeColorOverride("font_color", TextPrimary);
        titleRow.AddChild(_recorderTitle);
        _recorderCollapse = BuildCommandButton("Collapse", ToggleRecorderCollapse);
        _recorderCollapse.CustomMinimumSize = new Vector2(88, 32);
        titleRow.AddChild(_recorderCollapse);
        body.AddChild(titleRow);

        _recorderDetails = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _recorderDetails.AddThemeConstantOverride("separation", 6);
        body.AddChild(_recorderDetails);

        _recorderHealth = new Label
        {
            Text = "Session: none · health: waiting",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _recorderHealth.AddThemeFontSizeOverride("font_size", 14);
        _recorderHealth.AddThemeColorOverride("font_color", Accent);
        _recorderDetails.AddChild(_recorderHealth);

        var controls = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        controls.AddThemeConstantOverride("separation", 4);
        _recorderDetails.AddChild(controls);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.StartNewSession] = BuildRecordingButton(
            controls, "New Session", STS2HumanAnnotator.Core.RecordingCommandKind.StartNewSession);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Pause] = BuildRecordingButton(
            controls, "Pause", STS2HumanAnnotator.Core.RecordingCommandKind.Pause);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Resume] = BuildRecordingButton(
            controls, "Resume", STS2HumanAnnotator.Core.RecordingCommandKind.Resume);
        _recordingButtons[STS2HumanAnnotator.Core.RecordingCommandKind.Close] = BuildRecordingButton(
            controls, "Close", STS2HumanAnnotator.Core.RecordingCommandKind.Close);

        _lastAction = new Label
        {
            Text = "LAST ACTION\nNone observed yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 62),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _lastAction.AddThemeFontSizeOverride("font_size", 13);
        _lastAction.AddThemeColorOverride("font_color", Accent);
        _recorderDetails.AddChild(_lastAction);

        var feedHeading = new Label
        {
            Text = "RECENT ACTIONS · canonical evidence",
            MouseFilter = MouseFilterEnum.Ignore
        };
        feedHeading.AddThemeFontSizeOverride("font_size", 12);
        feedHeading.AddThemeColorOverride("font_color", TextPrimary);
        _recorderDetails.AddChild(feedHeading);
        _actionFeedScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 132),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop
        };
        _actionFeedList = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _actionFeedList.AddThemeConstantOverride("separation", 4);
        _actionFeedScroll.AddChild(_actionFeedList);
        _recorderDetails.AddChild(_actionFeedScroll);
        tabs.AddChild(_recorderPage);
    }

    private Button BuildRecordingButton(
        Container body,
        string text,
        STS2HumanAnnotator.Core.RecordingCommandKind kind)
    {
        var button = BuildCommandButton(text, () => ApplyRecordingCommand(kind));
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.CustomMinimumSize = new Vector2(76, 34);
        button.AddThemeFontSizeOverride("font_size", 12);
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
            CustomMinimumSize = new Vector2(88, 34),
            ToggleMode = true,
            Disabled = true
        };
        ApplyButtonTheme(button, mode == PlatformCommandMode.Human);
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
            CustomMinimumSize = new Vector2(118, 40)
        };
        ApplyButtonTheme(button, false);
        button.Pressed += action;
        return button;
    }

    private static StyleBoxFlat MakePanelStyle(
        Color background,
        Color border,
        int radius,
        int borderWidth,
        int padding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = padding,
            ContentMarginBottom = padding
        };
        style.SetBorderWidthAll(borderWidth);
        return style;
    }

    private static void ApplyButtonTheme(Button button, bool selected)
    {
        button.AddThemeFontSizeOverride("font_size", 14);
        button.AddThemeColorOverride("font_color", TextPrimary);
        button.AddThemeColorOverride("font_hover_color", TextPrimary);
        button.AddThemeColorOverride("font_pressed_color", TextPrimary);
        button.AddThemeColorOverride("font_disabled_color", new Color("#708092"));
        button.AddThemeStyleboxOverride("normal", MakePanelStyle(
            selected ? new Color("#245b6b") : new Color("#263442"),
            selected ? Accent : new Color("#4b5c6e"), 7, 1, 8));
        button.AddThemeStyleboxOverride("hover", MakePanelStyle(
            new Color("#315267"), Accent, 7, 1, 8));
        button.AddThemeStyleboxOverride("pressed", MakePanelStyle(
            new Color("#1e819b"), new Color("#9ae8f5"), 7, 2, 8));
        button.AddThemeStyleboxOverride("disabled", MakePanelStyle(
            selected ? new Color("#233b45") : new Color("#1a232d"),
            selected ? new Color("#4d8a98") : new Color("#34404d"), 7, 1, 8));
        button.AddThemeStyleboxOverride("focus", MakePanelStyle(
            new Color("#315267"), Accent, 7, 2, 8));
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
                LayoutWorkspaceSurface();
            }
            else
            {
                _workspace.Position = PlatformLiveLayout.ClampWorkspace(
                    new Rect2(_dragStart + motion.Position - _dragOrigin, _workspace.Size), Root.Size).Position;
            }
            _workspace.GetViewport().SetInputAsHandled();
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
        _recorderDetails.Visible = !_layout.RecorderCollapsed;
        _recorderCollapse.Text = _layout.RecorderCollapsed ? "Expand" : "Collapse";
        PersistLayout();
        PushToast("layout.recorder", _layout.RecorderCollapsed ? "Recorder details collapsed." : "Recorder details expanded.");
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
        LayoutWorkspaceSurface();
        SetWorkspaceCollapsed(_layout.WorkspaceCollapsed);
        _workspaceCollapse.Text = _layout.WorkspaceCollapsed ? "Expand" : "Collapse";
        _recorderDetails.Visible = !_layout.RecorderCollapsed;
        _recorderCollapse.Text = _layout.RecorderCollapsed ? "Expand" : "Collapse";
        ApplyPresentationVisibility();
    }

    private void LayoutWorkspaceSurface()
    {
        if (_workspaceSurface == null || _workspaceBody == null || _recorderPage == null || _toastStack == null)
            return;
        _workspaceSurface.Position = Vector2.Zero;
        _workspaceSurface.Size = _workspace.Size;
        _workspaceBody.Position = new Vector2(16, 16);
        _workspaceBody.Size = new Vector2(
            Math.Max(360, _workspace.Size.X - 32),
            Math.Max(260, _workspace.Size.Y - 32));
        _toastStack.Position = new Vector2(
            16,
            Math.Max(80, _workspace.Size.Y - _toastStack.Size.Y - 16));
    }

    private void ApplyPresentationVisibility()
    {
        bool workspaceVisible = _workspace.Visible;
        // There is exactly one active presentation owner. The Workspace is
        // the only Platform surface; its Recorder and toast regions are children.
        _toastStack.Visible = workspaceVisible;
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
            WorkspaceSize = _workspace.Size
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
            ApplyButtonTheme(item, false);
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
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 240),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        scroll.AddThemeConstantOverride("separation", 10);
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(360, 220),
            MouseFilter = MouseFilterEnum.Ignore
        };
        card.AddThemeStyleboxOverride("panel", PageStyle(name));
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        var label = new Label
        {
            Text = "Waiting for typed Platform live status...",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", TextPrimary);
        margin.AddChild(label);
        card.AddChild(margin);
        scroll.AddChild(card);
        tabs.AddChild(scroll);
        _pageText.Add(name, label);
    }

    private static StyleBoxFlat PageStyle(string name)
    {
        Color background = name switch
        {
            "Recorder" => new Color("#1b3338ed"),
            "Overview" => new Color("#1b2d3be8"),
            "Environment" => new Color("#1c3439e8"),
            "Policy" => new Color("#28233de8"),
            "Human Data" => new Color("#20372fe8"),
            "Diagnostics" => new Color("#3b3025e8"),
            _ => new Color("#222d38e8")
        };
        Color border = name switch
        {
            "Recorder" => new Color("#4d9b8c"),
            "Overview" => new Color("#3e7992"),
            "Environment" => new Color("#4b8f87"),
            "Policy" => new Color("#8276bd"),
            "Human Data" => new Color("#5b9a78"),
            "Diagnostics" => new Color("#ad8551"),
            _ => new Color("#536677")
        };
        return MakePanelStyle(background, border, 8, 1, 4);
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
            ApplyModeButtonState();
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
            _connectorTransport = "unavailable";
            _connection.Text = $"Connector loopback: UI poll failed ({error})";
            ApplyRecordingAvailability(
                STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.QueryStatus());
            PushToast("transport.error", $"Status transport unavailable: {error}");
        }
    }

    private void ApplyStatus(PlatformLiveStatus status)
    {
        _connectorTransport = status.TransportStatus;
        _connection.Text =
            $"Connector: {status.TransportStatus} | Policy Runtime: {status.PolicyRuntimeTransportStatus} | observed {status.ObservedAt:HH:mm:ss} UTC";
        string policyReason = PlatformLiveLayout.PolicyUnavailableReason(status);
        SetPolicyControlsAvailable(status.PolicyRuntime != null, policyReason);
        if (status.PolicyRuntime != null)
            _mode = ParseRuntimeMode(status.PolicyRuntime.Mode);
        ApplyModeButtonState();
        ApplyRecordingAvailability(status.Recording);
        RefreshActionFeed(status.Recording);
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
        ApplyModeButtonState();
    }

    private void ApplyModeButtonState()
    {
        foreach ((PlatformCommandMode mode, Button button) in _modeButtons)
        {
            button.ButtonPressed = mode == _mode;
            ApplyButtonTheme(button, button.ButtonPressed);
        }
    }

    private void ApplyRecordingAvailability(
        STS2HumanAnnotator.Core.RecordingApplicationStatus recording)
    {
        if (_recorderTitle == null)
            return;
        _recorderTitle.Text = "RECORDER";
        _recorderHealth.Text = $"● {recording.Lifecycle.State} | {recording.Counters.Records} records | Connector: {_connectorTransport}";
        _recorderHealth.AddThemeColorOverride("font_color", recording.Lifecycle.State switch
        {
            STS2HumanAnnotator.Core.RecordingLifecycleState.Recording => new Color("#73d39a"),
            STS2HumanAnnotator.Core.RecordingLifecycleState.Paused => new Color("#e6c36a"),
            STS2HumanAnnotator.Core.RecordingLifecycleState.Closing => new Color("#e6c36a"),
            _ => Accent
        });
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

    private void RefreshActionFeed(
        STS2HumanAnnotator.Core.RecordingApplicationStatus recording)
    {
        string? sessionId = recording.Session?.SessionId;
        if (!string.Equals(_actionFeedSessionId, sessionId, StringComparison.Ordinal))
        {
            _actionFeedSessionId = sessionId;
            _lastRecordingEventSequence = 0;
            _actionFeed.Clear();
        }

        try
        {
            STS2HumanAnnotator.Core.RecordingEventBatch batch =
                STS2HumanAnnotator.Mod.RecordingApplicationService.Instance.QueryEvents(
                    _lastRecordingEventSequence);
            if (batch.Gap)
            {
                _actionFeed.Clear();
                _lastRecordingEventSequence = Math.Max(0, batch.OldestAvailableSequence - 1);
            }
            foreach (STS2HumanAnnotator.Core.RecordingEvent value in batch.Events)
            {
                _lastRecordingEventSequence = Math.Max(_lastRecordingEventSequence, value.Sequence);
                if (sessionId == null
                    || !string.Equals(value.SessionId, sessionId, StringComparison.Ordinal)
                    || !PlatformLiveActionFeed.IsActionEvent(value.Kind))
                    continue;
                _actionFeed.RemoveAll(existing => existing.Sequence == value.Sequence);
                _actionFeed.Add(value);
            }
            _lastRecordingEventSequence = Math.Max(_lastRecordingEventSequence, batch.LatestSequence);
            while (_actionFeed.Count > PlatformLiveActionFeed.MaxEntries)
                _actionFeed.RemoveAt(0);
            RenderActionFeed();
        }
        catch (Exception exception)
        {
            _lastAction.Text = "LAST ACTION\nUnavailable (canonical event projection failed).";
            PushToast("recording.feed", $"Action Feed unavailable: {exception.Message}");
        }
    }

    private void RenderActionFeed()
    {
        if (_actionFeedList == null || _lastAction == null)
            return;
        foreach (Node child in _actionFeedList.GetChildren())
            child.QueueFree();

        STS2HumanAnnotator.Core.RecordingEvent? newest = _actionFeed
            .OrderByDescending(value => value.Sequence)
            .FirstOrDefault();
        _lastAction.Text = newest == null
            ? "LAST ACTION\nNone observed yet."
            : $"LAST ACTION\n{PlatformLiveActionFeed.FormatDetail(newest)}";

        foreach (STS2HumanAnnotator.Core.RecordingEvent value in _actionFeed
                     .OrderByDescending(item => item.Sequence))
        {
            var item = new PanelContainer
            {
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 24)
            };
            Color border = value.Kind switch
            {
                STS2HumanAnnotator.Core.RecordingEventKind.DecisionRecorded => new Color("#4fa77c"),
                STS2HumanAnnotator.Core.RecordingEventKind.DecisionInvalidated => new Color("#c26b69"),
                _ => new Color("#4f91a6")
            };
            item.AddThemeStyleboxOverride("panel", MakePanelStyle(
                new Color("#16222de8"), border, 6, 1, 7));
            var label = new Label
            {
                Text = PlatformLiveActionFeed.FormatEntry(value),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", TextPrimary);
            item.AddChild(label);
            _actionFeedList.AddChild(item);
        }
        _actionFeedScroll.ScrollVertical = 0;
    }

    private static string FormatOverview(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "OVERVIEW",
        $"Connector: {status.TransportStatus}",
        $"Policy Runtime: {status.PolicyRuntimeTransportStatus}",
        $"Recording: {status.Recording.Lifecycle.State} · {status.Recording.Counters.Records} records",
        $"Interaction: {status.Snapshot?.Interaction.Kind ?? "none"}",
        $"Snapshot: {ShortValue(status.Snapshot?.SnapshotId, "none")} · {status.Snapshot?.Status ?? "unavailable"}",
        $"Selected: {(status.PolicyRuntime == null ? "unavailable" : status.Selected.Count == 0 ? "none" : string.Join(", ", status.Selected.Select(item => item.Label)))}",
        $"Receipt: {status.Receipt.Status}"
    });

    private static string FormatEnvironment(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "ENVIRONMENT",
        $"Game: {status.ExactIdentity.Game?.Version ?? "unavailable"}",
        $"Interaction: {status.Snapshot?.Interaction.Kind ?? "none"}",
        $"Connector: {status.ExactIdentity.Connector?.Product ?? "unavailable"} {status.ExactIdentity.Connector?.Version ?? ""}".TrimEnd(),
        $"Annotator: {status.ExactIdentity.Annotator?.Product ?? "unavailable"} {status.ExactIdentity.Annotator?.Version ?? ""}".TrimEnd(),
        $"Modset: {status.ExactIdentity.ModsetStatus ?? "unavailable"}",
        $"Loaded Mods: {(status.ExactIdentity.LoadedModIds.Count == 0 ? "none" : string.Join(", ", status.ExactIdentity.LoadedModIds))}",
        $"Connector health: {status.TransportStatus}"
    });

    private static string FormatPolicy(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "POLICY",
        $"Runtime: {status.PolicyRuntimeTransportStatus}",
        $"Mode: {status.PolicyRuntime?.Mode ?? "unavailable"}",
        $"Policy: {status.PolicyRuntime?.Policy.PolicyId ?? "unavailable"} {status.PolicyRuntime?.Policy.PolicyVersion ?? ""}".TrimEnd(),
        $"Lifecycle: {status.PolicyRuntime?.Lifecycle ?? "unavailable"}",
        $"Last decision: {ShortValue(status.PolicyRuntime?.LastDecision?.DecisionId, "none")}",
        $"Receipt: {status.Receipt.Status}",
        status.PolicyRuntime == null
            ? $"Unavailable: {PlatformLiveLayout.PolicyUnavailableReason(status)}"
            : $"Tainted: {status.PolicyRuntime.Tainted}"
    });

    private static string FormatHumanData(PlatformLiveStatus status) => string.Join('\n', new[]
    {
        "HUMAN DATA",
        $"Lifecycle: {status.Recording.Lifecycle.State}",
        $"Session: {ShortValue(status.Recording.Session?.SessionId, "none")}",
        $"Records / invalidations: {status.Recording.Counters.Records} / {status.Recording.Counters.Invalidations}",
        $"Reads: {status.Recording.Counters.ReadsMaterialized} materialized · {status.Recording.Counters.ReadsFailed} failed",
        $"Health: append={status.Recording.Health.Append} · disk={status.Recording.Health.Disk}",
        $"Closeout: {status.Recording.Closeout.State}",
        $"Scope: {status.Recording.Scope.Detail}"
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
        FormatArtifact("Game", status.ExactIdentity.Game),
        FormatArtifact("Connector", status.ExactIdentity.Connector),
        FormatArtifact("Annotator", status.ExactIdentity.Annotator),
        FormatArtifact("Platform Live UI", status.ExactIdentity.LiveUi),
        $"Host kind: {status.ExactIdentity.HostKind ?? "unavailable"}",
        $"Runtime instance: {ShortValue(status.ExactIdentity.RuntimeInstanceId, "unavailable")}",
        $"Environment fingerprint: {status.ExactIdentity.EnvironmentFingerprint ?? "unavailable"}",
        $"Modset fingerprint: {status.ExactIdentity.ModsetFingerprint ?? "unavailable"}",
        $"Loaded Mod IDs: {(status.ExactIdentity.LoadedModIds.Count == 0 ? "none" : string.Join(", ", status.ExactIdentity.LoadedModIds))}",
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

    private static string ShortValue(string? value, string fallback = "unavailable")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return value.Length <= 18 ? value : $"{value[..8]}…{value[^6..]}";
    }

    private void OnProcessFrame()
    {
        ApplyPendingStatus();
        ApplyPendingPollError();
        ExpireToasts();
        if (!_draggingWorkspace && !_resizingWorkspace)
        {
            Rect2 clamped = PlatformLiveLayout.ClampWorkspace(
                new Rect2(_workspace.Position, _workspace.Size), Root.Size);
            if (clamped.Position != _workspace.Position || clamped.Size != _workspace.Size)
            {
                _workspace.Position = clamped.Position;
                _workspace.Size = clamped.Size;
                LayoutWorkspaceSurface();
            }
        }

        bool kPressed = Input.IsKeyPressed(Key.K) || Input.IsPhysicalKeyPressed(Key.K);
        if (kPressed && !_kWasPressed)
        {
            _workspace.Visible = !_workspace.Visible;
            ApplyPresentationVisibility();
            if (_workspace.Visible)
            {
                _tabs.CurrentTab = 0;
                _ = PollAsync();
            }
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
        ApplyPresentationVisibility();
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
