using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

internal static partial class RecorderRuntime
{
    internal sealed record SelectorInput(
        NativeNestedSelectorBindings.Binding Binding,
        object Owner, string Mechanism, ProcessLocalObservedAction Observed,
        ProcessLocalNativeWitnessFrame Frame, RecorderEnvironmentIdentity Environment,
        ProcessLocalNativeMatch Match, CurrentDecisionFrame Pre,
        DecisionOccurrenceIdentity Parent, string ActionId);

    [ThreadStatic] private static SelectorInput? _selectorInput;
    [ThreadStatic] private static bool _selectorInputStaged;
    [ThreadStatic] private static object? _selectorInputOwner;
    [ThreadStatic] private static HumanActionOccurrenceEvidence? _selectorInputAttempt;
    [ThreadStatic] private static string? _selectorInputFailure;
    internal static bool SelectorInputOwns(object owner) => _selectorInputStaged && _selectorInputAttempt != null && ReferenceEquals(_selectorInputOwner, owner);
    internal static bool SelectorInputActive => _selectorInputStaged;

    internal static SelectorInput? BeginSelectorInput(
        object owner, string mechanism, object? subject, params string[] operations)
    {
        if (!AcceptingNewWitnesses() || SelectorInputActive)
            return null;
        _selectorInputStaged = true;
        _selectorInputFailure = "selector_pre_capture_failed";
        _selectorInputOwner = owner;
        try
        {
            _selectorInputAttempt = new HumanActionOccurrenceEvidence(
                $"selector-input-{Guid.NewGuid():N}", mechanism, NativeNestedSelectorBindings.FamilyFor(owner),
                string.Join("|", operations), subject == null ? null : NativeWitnessIdentity.Get(subject, "selected"),
                new Dictionary<string, string>(), NativeWitnessIdentity.Get(owner, "selector_owner"),
                null, null, null, mechanism, "failed_closed");
            ProcessLocalNativeWitnessFrame frame = CaptureSemanticFrame();
            if (frame.ExternalControllerActive)
            {
                _selectorInputAttempt = null;
                return null;
            }
            if (!NativeNestedSelectorBindings.TryGet(owner, out var binding) || binding == null)
            { _selectorInputFailure = "selector_binding_missing"; return null; }
            if (binding.RecordingSessionId != SessionId)
            { _selectorInputFailure = "selector_binding_session_mismatch"; return null; }
            if (binding.ActionWitnessId == "unavailable")
            {
                _selectorInputFailure = $"{binding.FailureReason ?? "selector_parent_unavailable"};factory={binding.FactoryMechanism};native_owner={NativeWitnessIdentity.Get(binding.ParentOwner, "native_owner")}";
                return null;
            }
            DecisionOccurrenceIdentity? parent = binding.ParentDecision
                ?? BoundaryTracker.DecisionIdentity(binding.ActionWitnessId);
            if (parent == null)
            { _selectorInputFailure = "selector_parent_decision_missing"; return null; }
            binding.ParentDecision = parent;
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            var matches = operations.Select(operation => frame.ResolveNativeInput(owner, operation, subject))
                .Where(IsExact).DistinctBy(match => match.BoundActionId).ToArray();
            var blockers = SemanticWitnessBlockers(frame, environment);
            if (blockers.Count != 0 || matches.Length != 1)
            {
                _selectorInputFailure = $"selector_pre_blockers={string.Join(',', blockers)};exact_input_matches={matches.Length}";
                return null;
            }
            CurrentDecisionFrame pre = FreezeSemanticBoundary(frame, environment);
            string actionId = $"selector-decision-{Guid.NewGuid():N}";
            string parentActionId = binding.DecisionHeadActionId ?? binding.ActionWitnessId;
            // This is the state owned by the exact native input callback, before
            // its mutation. It is never recovered from a later overlay/frame.
            SemanticBoundaryObservation boundary = CreateSemanticBoundaryObservation(
                frame, SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady, null, pre) with
            {
                NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                    pre.InteractionKind, NativeWitnessIdentity.Get(owner, "selector_owner"),
                    owner.GetType().FullName!, mechanism)
            };
            if (BoundaryTracker.Contains(parentActionId))
                PersistTrackerMutationOrUnknown(parentActionId,
                    tracker => tracker.ObserveNestedInputBoundary(parentActionId, boundary,
                        new NativeContinuationEvidence($"selector-boundary-{Guid.NewGuid():N}",
                            "exact_selector_input_owner", parentActionId,
                            NativeWitnessIdentity.Get(owner, "selector_owner"),
                            NativeWitnessIdentity.Get(binding.ParentOwner, "native_owner"), true)),
                    "selector_boundary_persistence_failed", "Exact selector input boundary was not persisted.",
                    mechanism);
            var input = new SelectorInput(binding, owner, mechanism,
                new ProcessLocalObservedAction(matches[0].BoundAction!.Verb, subject, new Dictionary<string, object>()),
                frame, environment, matches[0], pre, parent, actionId);
            _selectorInput = input;
            return input;
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report("selector.input.before", exception);
            return null;
        }
    }

    internal static void EndSelectorInput(SelectorInput? input, bool accepted, bool terminal)
    {
        if (input == null)
        {
            if (_selectorInputStaged && accepted && _selectorInputAttempt != null)
                Quarantine("selector_decision_pre_or_lineage_unavailable",
                    $"The accepted selector input has no exact complete pre-state/catalog/parent binding: {_selectorInputFailure ?? "unavailable"}.",
                    _lastSnapshotId, _selectorInputAttempt?.NativeActionType ?? "native_selector_input",
                    "failed_closed", _selectorInputAttempt);
            _selectorInputStaged = false;
            _selectorInputOwner = null;
            _selectorInputAttempt = null;
            return;
        }
        try
        {
            if (!accepted || _store == null || !_semanticBoundaryTraceHealthy)
                return;
            long sequence = Interlocked.Increment(ref _sequence);
            string recordId = $"semantic-record-{sequence:D8}-{Guid.NewGuid():N}";
            var witness = new NativeWitnessEvidence("native_selector_input", input.Mechanism,
                input.Observed.Subject == null ? null : NativeWitnessIdentity.Get(input.Observed.Subject, "selected"),
                new Dictionary<string, string> { ["selector_owner"] = NativeWitnessIdentity.Get(input.Owner, "selector_owner") },
                DateTimeOffset.UtcNow);
            SemanticActionReference action = CreateSemanticActionReference(input.ActionId, sequence,
                recordId, input.Mechanism, null, input.Pre, "direct_ui_commit", witness, input.Match) with
            {
                Decision = new DecisionOccurrenceIdentity(1, $"decision-{recordId}",
                    input.Parent.CausalRootId, input.Parent.DecisionId, input.Pre.InteractionKind,
                    input.Binding.Family, "nested_selector", NativeWitnessIdentity.Get(input.Owner, "selector_owner"))
            };
            var before = CreateSemanticBoundaryObservation(input.Frame,
                SemanticBoundaryWitnessKinds.BeforeHumanActionExecution, input.ActionId, input.Pre);
            lock (Gate)
            {
                SemanticProjectionEnvironments[input.ActionId] = input.Environment;
                using var mutation = BoundaryTracker.BeginDurableMutation(tracker =>
                {
                    var drafts = new List<SemanticBoundaryTraceDraft>();
                    drafts.AddRange(tracker.Accept(action, input.Pre));
                    drafts.AddRange(tracker.ObserveBeforeActionExecution(input.ActionId, before));
                    drafts.AddRange(tracker.Started(input.ActionId));
                    drafts.AddRange(tracker.Finished(input.ActionId));
                    return drafts;
                });
                PersistSemanticBoundaryDrafts(mutation.Drafts,
                    onAuthoritativeSemanticAppend: mutation.MarkAuthoritativeAppend);
            }
            input.Binding.DecisionHeadActionId = input.ActionId;
            AppendJournal("semantic_human_action_accepted", recordId, input.Pre.SnapshotId,
                $"{input.Mechanism}:{input.ActionId};causal_root={input.Parent.CausalRootId}");
            // An in-place selection mutation has its own exact owner-ready S'.
            // Terminal selection waits for a separately observed next decision;
            // task completion or a removed overlay is never S'.
            if (!terminal && NativeNestedSelectorBindings.TryGet(input.Owner, out var same)
                && ReferenceEquals(same, input.Binding))
            {
                var frame = CaptureSemanticFrame();
                if (!frame.HasNativeInputOwner(input.Owner))
                    return;
                var successor = CreateSemanticBoundaryObservation(frame,
                    SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady, null) with
                {
                    NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                        frame.Snapshot.Interaction.Kind, NativeWitnessIdentity.Get(input.Owner, "selector_owner"),
                        input.Owner.GetType().FullName!, input.Mechanism)
                };
                PersistTrackerMutationOrUnknown(input.ActionId,
                    tracker => tracker.ObserveDecisionBoundaryForAction(input.ActionId, successor),
                    "selector_successor_persistence_failed", "Exact selector successor was not persisted.", input.Mechanism);
            }
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
        finally
        {
            if (ReferenceEquals(_selectorInput, input))
                _selectorInput = null;
            _selectorInputStaged = false;
            _selectorInputOwner = null;
            _selectorInputAttempt = null;
        }
    }
}
