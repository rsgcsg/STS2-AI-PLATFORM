# Measurement Contract

## Normalized Semantic Decision

The unit used for Host comparison is:

```text
stable interactive Snapshot
-> select one current finite BoundAction
-> submit the exact snapshot-bound action
-> receive a delivery Receipt
-> observe the next stable Snapshot or terminal state
```

One HTTP request, poll, rendered frame, simulator microstep, or animation frame
is not a semantic decision. Host throughput claims must count the unit above.

Each recorded decision separates policy selection, submit-to-Receipt,
Receipt-to-stable-successor, and total stable-Snapshot-to-successor time.
Reports summarize observed samples without inventing interpolated samples.

## Durable Trace

Journey events are appended to rotating JSONL files and flushed after every
record by default. A crash may truncate only the final in-flight record; it
must not erase the preceding trajectory. Raw traces remain local because they
may contain profile or save information.

Every executed test decision records the exact state/action identifiers,
selected action, Receipt attribution, canonical fair-player decision, stable
successor, and timing chain.

The canonical form removes runtime-local identifiers and timestamps while
retaining visible content and referent/action relationships. It is a comparison
artifact only. It never creates legality, execution authority, or an action.

## Integrity Versus Coverage

Journey integrity and surface coverage are independent results:

- **integrity** checks unknown delivery, advertised Read failure, missing stable
  successor, and unsafe termination;
- **coverage** reports which named interactions and action classes were
  exercised against an explicit target.

Missing a particular event in a finite run is not automatically a Host
integrity failure. Visiting many surfaces does not excuse an unknown delivery
or missing successor.

## Promotion Use

This contract admits measurements; it does not define a performance winner.
The current `>=1000` aggregate decisions/s trainer target is a route hypothesis,
not a release fact. Promotion also requires semantic differential, reset and
recovery evidence, resource efficiency, and representative training workload.
