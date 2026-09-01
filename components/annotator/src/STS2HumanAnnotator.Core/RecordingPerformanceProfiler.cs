using System.Diagnostics;

namespace STS2HumanAnnotator.Core;

public sealed class RecordingPerformanceProfiler
{
    public const string ReportSchema = "sts2.human-annotator/recording-performance-profile-1";
    private const int MaximumSamplesPerPhase = 100_000;
    private readonly object _gate = new();
    private readonly Dictionary<string, PhaseSamples> _phases = new(StringComparer.Ordinal);

    public T Measure<T>(string phase, Func<T> operation)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            return operation();
        }
        finally
        {
            Observe(phase, Stopwatch.GetTimestamp() - started);
        }
    }

    public void Measure(string phase, Action operation) =>
        Measure(
            phase,
            () =>
            {
                operation();
                return true;
            });

    public void ObserveMicroseconds(string phase, long microseconds)
    {
        if (string.IsNullOrWhiteSpace(phase))
            throw new ArgumentException("Performance phase must be non-empty.", nameof(phase));
        if (microseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(microseconds));
        lock (_gate)
        {
            if (!_phases.TryGetValue(phase, out PhaseSamples? samples))
            {
                samples = new PhaseSamples();
                _phases.Add(phase, samples);
            }
            samples.Add(microseconds);
        }
    }

    public RecordingPerformanceReport Snapshot(string sessionId)
    {
        lock (_gate)
        {
            return new RecordingPerformanceReport(
                ReportSchema,
                sessionId,
                DateTimeOffset.UtcNow,
                _phases
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Value.Summarize(pair.Key))
                    .ToArray(),
                new[]
                {
                    "Stopwatch timings are source/runtime diagnostics, not gameplay evidence.",
                    "A profile does not prove absence of game-frame stalls without an owner canary."
                });
        }
    }

    private void Observe(string phase, long elapsedTicks)
    {
        if (string.IsNullOrWhiteSpace(phase))
            throw new ArgumentException("Performance phase must be non-empty.", nameof(phase));
        long microseconds = (long)Math.Ceiling(
            elapsedTicks * 1_000_000d / Stopwatch.Frequency);
        ObserveMicroseconds(phase, microseconds);
    }

    private sealed class PhaseSamples
    {
        private readonly List<long> _samples = new();
        private long _count;
        private long _total;
        private long _maximum;

        public void Add(long microseconds)
        {
            _count++;
            _total += microseconds;
            _maximum = Math.Max(_maximum, microseconds);
            if (_samples.Count < MaximumSamplesPerPhase)
                _samples.Add(microseconds);
        }

        public RecordingPerformancePhase Summarize(string phase)
        {
            long[] ordered = _samples.Order().ToArray();
            return new RecordingPerformancePhase(
                phase,
                _count,
                _samples.Count,
                _count - _samples.Count,
                _count == 0 ? 0 : _total / _count,
                Percentile(ordered, 0.50),
                Percentile(ordered, 0.95),
                Percentile(ordered, 0.99),
                _maximum,
                _total);
        }

        private static long Percentile(long[] ordered, double percentile)
        {
            if (ordered.Length == 0)
                return 0;
            int index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
            return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
        }
    }
}

public sealed record RecordingPerformancePhase(
    string Phase,
    long Count,
    int SampledCount,
    long DroppedSampleCount,
    long MeanUs,
    long P50Us,
    long P95Us,
    long P99Us,
    long MaxUs,
    long TotalUs);

public sealed record RecordingPerformanceReport(
    string Schema,
    string SessionId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RecordingPerformancePhase> Phases,
    IReadOnlyList<string> NonClaims);
