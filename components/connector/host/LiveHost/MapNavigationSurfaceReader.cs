using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using STS2Connector.LiveHost.Contracts;
using STS2Platform.NativeFoundation;

namespace STS2Connector.LiveHost;

/// <summary>
/// Exact-build adapter for the map's route-selection protocol. Native map
/// destinations own semantic actions; current controls only bind delivery.
/// </summary>
internal sealed class MapNavigationSurfaceReader : ILiveSurfaceReader
{
    private const string SurfaceKind = "map_navigation";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public;
    private static readonly FieldInfo? InputDisabledField =
        typeof(NMapScreen).GetField("_isInputDisabled", Flags);
    private static readonly FieldInfo? DrawingInputField =
        typeof(NMapScreen).GetField("_drawingInput", Flags);
    private static readonly MethodInfo? LocalDrawingModeMethod =
        typeof(NMapDrawings)
            .GetMethods(PublicFlags)
            .Where(method =>
                method.Name == "GetLocalDrawingMode"
                && method.ReturnType == typeof(DrawingMode)
                && IsCompatibleLocalDrawingModeSignature(
                    method.GetParameters().Select(parameter => parameter.ParameterType).ToArray()))
            .OrderByDescending(method => method.GetParameters().Length)
            .FirstOrDefault();
    private static readonly PropertyInfo? DirectionalNavigationProperty =
        typeof(NControllerManager).GetProperty(
            "IsUsingDirectionalNavigation",
            PublicFlags)
        ?? typeof(NControllerManager).GetProperty(
            "IsUsingController",
            PublicFlags);

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Overlay;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        if (!snapshot.MapIsOpen)
            return null;

        NMapScreen? screen = NMapScreen.Instance;
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (screen == null || runState == null || !ConnectorMod.IsLiveNode(screen))
            return BindingUnavailable(game, "The visible map screen or run state is unavailable.");
        if (InputDisabledField?.GetValue(screen) is not bool inputDisabled)
            return BindingUnavailable(game, "The exact map input-readiness binding is unavailable.");

        NativeMapDecision nativeDecision =
            NativeMapDecisionProvider.Capture(runState, entities);
        if (nativeDecision.Status != "captured")
        {
            return BindingUnavailable(
                game,
                nativeDecision.Detail
                ?? "The game-owned map destination catalog is unavailable.");
        }

        NMapPoint[] pointNodes = ConnectorMod.FindAll<NMapPoint>(screen)
            .Where(node => ConnectorMod.IsLiveNode(node) && node.Point != null)
            .OrderBy(node => node.Point.coord.row)
            .ThenBy(node => node.Point.coord.col)
            .ToArray();
        if (pointNodes.Length == 0)
            return BindingUnavailable(game, "The open map has no bound player-visible map points.");

        MapCoord[] duplicateCoords = pointNodes
            .GroupBy(node => node.Point.coord)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateCoords.Length > 0)
            return BindingUnavailable(game, "The open map contains ambiguous UI nodes for one or more coordinates.");

        var byCoord = pointNodes.ToDictionary(node => node.Point.coord);
        var presentedPointSet = new HashSet<MapPoint>(
            pointNodes.Select(node => node.Point),
            ReferenceEqualityComparer.Instance);
        IReadOnlyList<MapPoint> semanticDestinations =
            NativeSemanticActionCatalog.Subjects<MapPoint>(
                nativeDecision.Actions,
                "travel");
        var semanticDestinationSet = new HashSet<MapPoint>(
            semanticDestinations,
            ReferenceEqualityComparer.Instance);
        if (semanticDestinations.Any(destination => !presentedPointSet.Contains(destination)))
        {
            return BindingUnavailable(
                game,
                "A native map destination does not have one exact presentation binding.");
        }
        VisibleMapNode[] nodes = pointNodes.Select(node => BuildNode(node, entities)).ToArray();
        VisibleMapCoordinate[] visited = runState.VisitedMapCoords
            .Select(coord => BuildCoordinate(coord, byCoord))
            .ToArray();
        VisibleMapCoordinate? current = runState.CurrentMapCoord is { } currentCoord
            ? BuildCoordinate(currentCoord, byCoord)
            : null;
        var context = new MapLiveContext(
            "map",
            runState.CurrentActIndex,
            current,
            visited,
            nodes);

        if (!TryGetLocalDrawingMode(screen.Drawings, out DrawingMode drawingMode))
        {
            return BindingUnavailable(
                game,
                "The exact current map drawing-mode binding is unavailable.");
        }
        if (!TryGetDirectionalNavigation(out bool usingDirectionalNavigation))
        {
            return BindingUnavailable(
                game,
                "The exact current map input-mode binding is unavailable.");
        }
        NMapDrawingInput? drawingInput = null;
        if (drawingMode != DrawingMode.None)
        {
            if (DrawingInputField?.GetValue(screen) is not NMapDrawingInput activeDrawingInput
                || !ConnectorMod.IsLiveNode(activeDrawingInput)
                || activeDrawingInput.DrawingMode != drawingMode)
            {
                return BindingUnavailable(
                    game,
                    "The active map annotation mode has no exact matching native drawing-input binding.");
            }
            drawingInput = activeDrawingInput;
        }
        bool routeInputReady = CanAdvertiseRouteActions(
            screen.IsOpen,
            screen.IsTravelEnabled,
            screen.IsTraveling,
            inputDisabled,
            drawingMode == DrawingMode.None);
        if (routeInputReady && pointNodes.Any(node =>
                node.State == MapPointState.Travelable
                != semanticDestinationSet.Contains(node.Point)))
        {
            return ContradictoryRouteState(game, context);
        }

        NMapPoint[] travelable = routeInputReady
            ? pointNodes.Where(node =>
                semanticDestinationSet.Contains(node.Point)
                && IsExactUiTravelChoice(screen, node, usingDirectionalNavigation)).ToArray()
            : Array.Empty<NMapPoint>();
        VisibleMapChoice[] options = travelable.Select(node => new VisibleMapChoice(
            entities.GetId(node.Point, "map_point"),
            node.Point.coord.col,
            node.Point.coord.row,
            PointType(node.Point))).ToArray();
        bool canExitAnnotation = drawingInput != null && CanAdvertiseAnnotationExit(
            screen.IsOpen,
            screen.IsTraveling,
            inputDisabled,
            drawingMode != DrawingMode.None,
            drawingInputAvailable: true);

        var surface = new MapNavigationSurface(
            SurfaceKind,
            entities.GetId(screen, "screen"),
            screen.IsTravelEnabled,
            screen.IsTraveling,
            drawingMode.ToString().ToLowerInvariant(),
            options)
        {
            AnnotationInputEntityId = drawingInput == null
                ? null
                : entities.GetId(drawingInput, "map_annotation_input"),
            CanExitAnnotation = canExitAnnotation
        };
        bool hasActionableControl = options.Length > 0 || canExitAnnotation;
        string readiness = hasActionableControl ? "ready" : "settling";
        IReadOnlyList<string> warnings = drawingMode == DrawingMode.None
            ? Array.Empty<string>()
            : new[] { "map_annotation_mode_active_route_actions_suppressed" };
        var completeness = new StateCompleteness(
            "contract_complete_for_visible_singleplayer_map_navigation",
            hasActionableControl
                ? drawingMode == DrawingMode.None
                    ? "native_map_destinations_intersected_with_exact_current_delivery_controls"
                    : "derived_from_exact_active_map_annotation_input_stop_control"
                : "temporarily_empty_while_map_input_is_not_route_ready",
            new[]
            {
                "NMapScreen.IsOpen+IsTravelEnabled+IsTraveling",
                "NMapScreen._isInputDisabled exact-version binding",
                "NMapDrawings.GetLocalDrawingMode",
                "NControllerManager.IsUsingDirectionalNavigation exact current binding",
                "RunState.Map+MapTravel.GetTravelablePointsFrom native destinations",
                "NMapPoint.Point+State+IsEnabled presentation binding",
                "RunState.CurrentMapCoord+VisitedMapCoords",
                "MapPoint.PointType+Children"
            },
            Array.Empty<string>());
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            context,
            surface
        });

        return new LiveObservation(
            signature,
            readiness,
            context,
            surface,
            completeness,
            game,
            warnings);
    }

    internal static bool CanAdvertiseRouteActions(
        bool isOpen,
        bool travelEnabled,
        bool traveling,
        bool inputDisabled,
        bool drawingModeNone) =>
        isOpen && travelEnabled && !traveling && !inputDisabled && drawingModeNone;

    internal static bool CanAdvertiseAnnotationExit(
        bool isOpen,
        bool traveling,
        bool inputDisabled,
        bool annotationModeActive,
        bool drawingInputAvailable) =>
        isOpen
        && !traveling
        && !inputDisabled
        && annotationModeActive
        && drawingInputAvailable;

    internal static bool CanAdvertiseMapChoice(
        bool enabled,
        bool ftueSatisfied,
        bool usingDirectionalNavigation,
        bool nodeOnScreen) =>
        enabled
        && ftueSatisfied
        && (!usingDirectionalNavigation || nodeOnScreen);

    internal static bool IsCompatibleLocalDrawingModeSignature(
        IReadOnlyList<Type> parameterTypes) =>
        parameterTypes.Count == 0
        || parameterTypes.Count == 1 && parameterTypes[0] == typeof(bool);

    internal static bool HasCompatibleLocalDrawingModeBinding =>
        LocalDrawingModeMethod != null;

    internal static string? ControllerInputModeBindingName =>
        DirectionalNavigationProperty?.Name;

    private static bool IsExactUiTravelChoice(
        NMapScreen screen,
        NMapPoint node,
        bool usingDirectionalNavigation)
    {
        return node.Point != null
               && CanAdvertiseMapChoice(
                   node.IsEnabled,
                   node.Point.coord.row != 0 || SaveManager.Instance.SeenFtue("map_select_ftue"),
                   usingDirectionalNavigation,
                   screen.IsNodeOnScreen(node));
    }

    private static VisibleMapNode BuildNode(NMapPoint node, NativeEntityRegistry entities) =>
        new(
            entities.GetId(node.Point, "map_point"),
            node.Point.coord.col,
            node.Point.coord.row,
            PointType(node.Point),
            node.State.ToString().ToLowerInvariant(),
            node.Point.Children
                .OrderBy(child => child.coord.row)
                .ThenBy(child => child.coord.col)
                .Select(child => new VisibleMapCoordinate(
                    child.coord.col,
                    child.coord.row,
                    PointType(child)))
                .ToArray());

    private static VisibleMapCoordinate BuildCoordinate(
        MapCoord coord,
        IReadOnlyDictionary<MapCoord, NMapPoint> byCoord) =>
        new(
            coord.col,
            coord.row,
            byCoord.TryGetValue(coord, out NMapPoint? node) ? PointType(node.Point) : null);

    internal static NativeInputResult StartTravel(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedNodeId)
    {
        NMapScreen? screen = NMapScreen.Instance;
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (screen == null
            || runState == null
            || !ConnectorMod.IsLiveNode(screen)
            || !string.Equals(
                entities.GetId(screen, "screen"),
                expectedScreenId,
                StringComparison.Ordinal)
            || !entities.TryResolve(expectedNodeId, out MapPoint? point)
            || point == null)
        {
            return NativeInputResult.Rejected(
                "map_choice_changed",
                "The exact map screen or destination entity is no longer current.");
        }

        NMapPoint[] matches = ConnectorMod.FindAll<NMapPoint>(screen)
            .Where(node => ReferenceEquals(node.Point, point))
            .ToArray();
        if (matches.Length != 1)
        {
            return NativeInputResult.Rejected(
                "map_choice_changed",
                "The native map destination no longer has one exact presentation binding.");
        }

        return StartTravel(entities, screen, runState, matches[0], point);
    }

    internal static NativeInputResult StopAnnotation(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedInputId)
    {
        NMapScreen? screen = NMapScreen.Instance;
        if (screen == null
            || !ConnectorMod.IsLiveNode(screen)
            || !string.Equals(
                entities.GetId(screen, "screen"),
                expectedScreenId,
                StringComparison.Ordinal)
            || !entities.TryResolve(expectedInputId, out NMapDrawingInput? input)
            || input == null
            || DrawingInputField?.GetValue(screen) is not NMapDrawingInput currentInput
            || !ReferenceEquals(currentInput, input)
            || !TryGetLocalDrawingMode(screen.Drawings, out DrawingMode drawingMode))
        {
            return NativeInputResult.Rejected(
                "map_annotation_input_changed",
                "The exact map annotation owner is no longer current.");
        }

        return StopAnnotation(screen, input, drawingMode);
    }

    private static NativeInputResult StopAnnotation(
        NMapScreen expectedScreen,
        NMapDrawingInput expectedInput,
        DrawingMode expectedMode)
    {
        if (!ReferenceEquals(NMapScreen.Instance, expectedScreen)
            || !expectedScreen.IsOpen
            || expectedScreen.IsTraveling
            || InputDisabledField?.GetValue(expectedScreen) is not bool inputDisabled
            || inputDisabled
            || DrawingInputField?.GetValue(expectedScreen) is not NMapDrawingInput currentInput
            || !ReferenceEquals(currentInput, expectedInput)
            || !ConnectorMod.IsLiveNode(expectedInput)
            || expectedInput.DrawingMode != expectedMode
            || expectedMode == DrawingMode.None
            || !TryGetLocalDrawingMode(expectedScreen.Drawings, out DrawingMode currentMode)
            || currentMode != expectedMode)
        {
            return NativeInputResult.Rejected(
                "map_annotation_input_changed",
                "The advertised native map annotation input is no longer the exact current owner.");
        }

        expectedInput.StopDrawing();
        return NativeInputResult.Delivered("native_map_annotation_stop_submitted");
    }

    private static NativeInputResult StartTravel(
        NativeEntityRegistry entities,
        NMapScreen expectedScreen,
        RunState expectedRunState,
        NMapPoint expectedNode,
        MapPoint expectedPoint)
    {
        NativeMapDecision decision =
            NativeMapDecisionProvider.Capture(expectedRunState, entities);
        if (!ReferenceEquals(NMapScreen.Instance, expectedScreen)
            || !expectedScreen.IsOpen
            || expectedScreen.IsTraveling
            || !expectedScreen.IsTravelEnabled
            || !TryGetLocalDrawingMode(expectedScreen.Drawings, out DrawingMode drawingMode)
            || drawingMode != DrawingMode.None
            || !TryGetDirectionalNavigation(out bool usingDirectionalNavigation)
            || InputDisabledField?.GetValue(expectedScreen) is not bool inputDisabled
            || inputDisabled
            || !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), expectedRunState)
            || !ConnectorMod.FindAll<NMapPoint>(expectedScreen).Any(node => ReferenceEquals(node, expectedNode))
            || !ReferenceEquals(expectedNode.Point, expectedPoint)
            || !NativeSemanticActionCatalog.ContainsExactlyOnce(
                decision.Actions,
                "travel",
                expectedPoint)
            || expectedNode.State != MapPointState.Travelable
            || !IsExactUiTravelChoice(expectedScreen, expectedNode, usingDirectionalNavigation))
        {
            return NativeInputResult.Rejected(
                "map_choice_changed",
                "The advertised map point is no longer the exact current UI travel choice.");
        }

        expectedScreen.OnMapPointSelectedLocally(expectedNode);
        return NativeInputResult.Delivered("native_map_point_selected");
    }

    private static bool TryGetLocalDrawingMode(
        NMapDrawings drawings,
        out DrawingMode drawingMode)
    {
        drawingMode = DrawingMode.None;
        MethodInfo? method = LocalDrawingModeMethod;
        if (method == null)
            return false;

        try
        {
            object?[] arguments = method.GetParameters().Length == 0
                ? Array.Empty<object?>()
                : new object?[] { true };
            if (method.Invoke(drawings, arguments) is not DrawingMode observed)
                return false;
            drawingMode = observed;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryGetDirectionalNavigation(
        out bool usingDirectionalNavigation)
    {
        usingDirectionalNavigation = false;
        NControllerManager? controller = NControllerManager.Instance;
        PropertyInfo? property = DirectionalNavigationProperty;
        if (controller == null
            || property == null
            || property.PropertyType != typeof(bool)
            || property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        try
        {
            if (property.GetValue(controller) is not bool observed)
                return false;
            usingDirectionalNavigation = observed;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string PointType(MapPoint point) => point.PointType.ToString().ToLowerInvariant();

    private static LiveObservation ContradictoryRouteState(
        GameBuildIdentity game,
        MapLiveContext context)
    {
        const string reason = "The map presentation travelable set disagrees with the game-owned native destination catalog.";
        var surface = new UnsupportedSurface("unsupported", SurfaceKind, reason);
        var completeness = new StateCompleteness(
            "partial",
            "empty_fail_closed_due_to_native_presentation_contradiction",
            new[]
            {
                "NMapScreen.IsOpen+IsTravelEnabled+IsTraveling",
                "NMapPoint.State+IsEnabled presentation binding",
                "RunState.Map+MapTravel.GetTravelablePointsFrom native destinations"
            },
            new[] { "legal_actions" });
        string signature = StableIdentityHash.Object(new { game.Version, context, reason });
        return new LiveObservation(
            signature,
            "degraded",
            context,
            surface,
            completeness,
            game,
            new[] { "map_navigation_ui_run_state_contradiction" })
        {
            Diagnostics = new[]
            {
                HostDiagnostics.Create(
                    "host.surface.map_navigation.ui_run_state_contradiction",
                    "error",
                    "surface",
                    "actions_suppressed",
                    "refresh_or_update_host_adapter",
                    reason)
            }
        };
    }

    private static LiveObservation BindingUnavailable(GameBuildIdentity game, string reason)
    {
        var context = new UnknownLiveContext("unknown", "map_open", reason);
        var surface = new UnsupportedSurface("unsupported", "map_navigation", reason);
        var completeness = new StateCompleteness(
            "partial",
            "empty_fail_closed",
            new[] { "NMapScreen exact-version binding" },
            new[] { "map_context", "map_nodes", "legal_actions" });
        string signature = StableIdentityHash.Object(new { game.Version, reason });
        return new LiveObservation(
            signature,
            "degraded",
            context,
            surface,
            completeness,
            game,
            new[] { "map_navigation_binding_unavailable" })
        {
            Diagnostics = new[]
            {
                HostDiagnostics.Create(
                    "host.surface.map_navigation.binding_unavailable",
                    "error",
                    "surface",
                    "actions_suppressed",
                    "update_host_adapter",
                    reason)
            }
        };
    }
}
