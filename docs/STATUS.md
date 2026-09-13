# Current Status

PR25 rc.4 passes the bounded Full-Run Human gate: two uninterrupted native starts
through natural defeat, all 524 accepted decisions proved/canonical, zero real
failures or unexplained loss, and independently verified bundle3.
See the [final exact Human audit](evidence/PR25_RC4_FULL_RUN_HUMAN_PASS_2026-09-12.md).
Resolve live PR25/merge refs and CI for integration status. Main release promotion
and external research admission are separate from this develop integration.

See [current context](memory/CURRENT.md), [data contracts](FULL_RUN_DATA_CHAIN.md)
and [collection / future repairs](ANNOTATOR_COLLECTION.md).

## Historical evidence

These dated reports describe their own exact candidates, not current readiness.

- [rc.3 audit and rc.4 repair](evidence/PR25_RC3_TWO_SESSION_AUDIT_2026-09-11.md)
- [reward-owner repair](evidence/PR25_TWO_SESSION_REWARD_HANDOFF_2026-09-11.md)
- [final engineering cleanup](evidence/PR25_FINAL_ENGINEERING_2026-09-11.md)

- [audit and repair](evidence/PR25_BLOCKING_CHOICE_REPAIR_2026-09-11.md)
- [audit and repair](evidence/PR25_REWARD_OWNER_NOOP_REPAIR_2026-09-11.md)
- [audit and repair](evidence/PR25_POTION_INPUT_VALIDATOR_REPAIR_2026-09-11.md)
- [audit,
repair and bounded canary](evidence/PR25_DEFERRED_POTION_INGRESS_REPAIR_2026-09-11.md)
- [audit, repair and limits](evidence/PR25_ANYTIME_POTION_REPAIR_2026-09-11.md)
- [audit and repair report](evidence/PR25_ACT_EXECUTION_REPAIR_2026-09-11.md)
- [audit and repair report](evidence/PR25_POPUP_PRECEDENCE_REPAIR_2026-09-10.md)
- [candidate report](evidence/PR25_NATIVE_SELECTOR_CANARY_2026-09-10.md)
- [Follow-up report](evidence/PR25_FULLRUN_FOLLOWUP_2026-09-10.md)
- [decision repair report](evidence/PR25_DECISION_REPAIR_2026-09-10.md)
- [dated integration closeout](evidence/LIVE_UI_HISTORY_INTEGRATION_SOURCE_CLOSEOUT_2026-09-03.md)
- [failed-session forensic inventory](evidence/FULL_RUN_COVERAGE_INVENTORY_2026-09-03.md)
- [source closeout](evidence/PR11_EXECUTION_SEMANTIC_ACTION_SPACE_SOURCE_CLOSEOUT_2026-09-02.md)
- [performance source closeout](evidence/PLATFORM_RECORDING_HOTPATH_PERFORMANCE_SOURCE_CLOSEOUT_2026-09-01.md)
- [continuation source closeout](evidence/NATIVE_FOUNDATION_FULL_RUN_SOURCE_CLOSEOUT_2026-08-31.md)
- [Treasure closeout](evidence/NATIVE_FOUNDATION_TREASURE_SOURCE_CLOSEOUT_2026-08-31.md)
- [completion-lineage source
closeout](evidence/NATIVE_FOUNDATION_COMPLETION_LINEAGE_SOURCE_CLOSEOUT_2026-09-01.md)
- [qualification repair source closeout](evidence/PR6_HUMAN_QUALIFICATION_REPAIR_SOURCE_CLOSEOUT_2026-09-01.md)
- [successor owner-ready
closeout](evidence/PR6_SUCCESSOR_OWNER_READY_SOURCE_CLOSEOUT_2026-09-01.md)
- [Windows pre-Human gate](evidence/NATIVE_FOUNDATION_WINDOWS_PRE_HUMAN_GATE_2026-08-31.md)
- [Windows closeout](evidence/NATIVE_FOUNDATION_WINDOWS_HUMAN_CLOSEOUT_2026-08-31.md)
- [final decision packet](evidence/RITSU_ROUTE_FINAL_DECISION_2026-08-31.md)
- [source closeout](evidence/NATIVE_FOUNDATION_SOURCE_CLOSEOUT_2026-08-30.md)
- [source closeout](evidence/NATIVE_SEMANTIC_RUNTIME_DISCRIMINATOR_SOURCE_CLOSEOUT_2026-08-30.md)
- [Human closeout](evidence/NATIVE_SEMANTIC_RUNTIME_DISCRIMINATOR_HUMAN_CLOSEOUT_2026-08-30.md)
- [schema-3 closeout](evidence/SCHEMA3_HUMAN_DATA_LIFECYCLE_CLOSEOUT_2026-08-29.md)
- [causal performance baseline](evidence/RECORDER_CAUSAL_PERFORMANCE_BASELINE_2026-08-29.md)
- [canonical causality decision](evidence/RECORDER_CANONICAL_CAUSALITY_DECISION_2026-08-29.md)
- [schema-2 owner closeout](evidence/SEMANTIC_TIMELINE_OWNER_CLOSEOUT_2026-08-27.md)
- [batch canary](evidence/FULL_RUN_BATCH1_OWNER_CANARY_2026-08-28.md)
- [runtime-seal report](evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md)
- [V2 closeout](evidence/HUMAN_EVIDENCE_V2_READ_RICH_COMBAT_CLOSEOUT_2026-08-25.md)
- [owner validation](evidence/RECORDING_APPLICATION_OWNER_VALIDATION_2026-08-25.md)
- [decision gate](evidence/RECORDING_APPLICATION_DECISION_GATE_2026-08-25.md)
- [repair evidence](evidence/PR25_POTION_CAUSAL_REPAIR_2026-09-10.md)

## Collection delivery engineering

Evidence 0.1.0-rc.3 adds opt-in fixed-tool closed-session delivery and persistent
HTTP receipt recovery. See [ADR 0008](adr/0008-release-bound-closed-session-delivery.md)
and [delivery operation](../components/evidence/DELIVERY.md). Native gameplay
source and the PR25 Human artifact are unchanged. Real cloud deployment and a
fresh Close-to-cloud Human delivery journey are not established by portable tests.

- First dedicated Human Close-to-R2 gate and delivery diagnostic repair:
  [bounded audit](evidence/B_PIPELINE_FIRST_HUMAN_UPLOAD_2026-09-13.md).

Evidence rc.7 extends the B delivery candidate with explicit same-upload credential recovery.
Typed Hub 401/403 blocks are recoverable under the owner lock; historical incident text is not
reclassified. No additional Human, game runtime or training claim follows from these tests.
