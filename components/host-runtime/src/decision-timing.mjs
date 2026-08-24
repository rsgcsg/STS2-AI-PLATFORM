function finiteDuration(value) {
  return Number.isFinite(value) && value >= 0 ? value : null;
}

export function normalizedDecisionTiming({
  snapshotReadyMs,
  policySelectedMs,
  submitStartedMs,
  receiptMs,
  successorReadyMs
}) {
  const timestamps = [
    snapshotReadyMs,
    policySelectedMs,
    submitStartedMs,
    receiptMs,
    successorReadyMs
  ];
  if (timestamps.some((value) => !Number.isFinite(value))
      || timestamps.some((value, index) => index > 0 && value < timestamps[index - 1])) {
    throw new Error("Decision timing timestamps must be finite and monotonic.");
  }
  const timing = {
    policy_ms: finiteDuration(policySelectedMs - snapshotReadyMs),
    submit_to_receipt_ms: finiteDuration(receiptMs - submitStartedMs),
    receipt_to_successor_ms: finiteDuration(successorReadyMs - receiptMs),
    semantic_decision_ms: finiteDuration(successorReadyMs - snapshotReadyMs)
  };
  return timing;
}

export function summarizeDurations(values) {
  const sorted = values.filter((value) => Number.isFinite(value) && value >= 0)
    .sort((left, right) => left - right);
  const percentile = (fraction) => {
    if (sorted.length === 0) return null;
    return sorted[Math.min(sorted.length - 1, Math.ceil(fraction * sorted.length) - 1)];
  };
  return {
    count: sorted.length,
    min_ms: sorted.at(0) ?? null,
    p50_ms: percentile(0.50),
    p95_ms: percentile(0.95),
    p99_ms: percentile(0.99),
    max_ms: sorted.at(-1) ?? null,
    mean_ms: sorted.length === 0
      ? null
      : sorted.reduce((total, value) => total + value, 0) / sorted.length
  };
}
