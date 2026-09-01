using System.Text.Json;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecordingPerformanceProfilerTests
{
    [Fact]
    public void ExplicitSubphaseObservationIsIncludedInReport()
    {
        var profiler = new RecordingPerformanceProfiler();

        profiler.ObserveMicroseconds("full_capture.read_rich.game_identity", 17);
        profiler.ObserveMicroseconds("full_capture.read_rich.game_identity", 23);

        RecordingPerformancePhase phase = Assert.Single(
            profiler.Snapshot("session-test").Phases);
        Assert.Equal("full_capture.read_rich.game_identity", phase.Phase);
        Assert.Equal(2, phase.Count);
        Assert.Equal(20, phase.MeanUs);
        Assert.Equal(23, phase.MaxUs);
    }

    [Fact]
    public void ProfilerReportsOrderedBoundedPhaseStatistics()
    {
        var profiler = new RecordingPerformanceProfiler();
        profiler.Measure("second", () => Thread.SpinWait(100));
        int result = profiler.Measure("first", () => 7);

        RecordingPerformanceReport report = profiler.Snapshot("session-test");

        Assert.Equal(7, result);
        Assert.Equal(RecordingPerformanceProfiler.ReportSchema, report.Schema);
        Assert.Equal(new[] { "first", "second" }, report.Phases.Select(phase => phase.Phase));
        Assert.All(report.Phases, phase => Assert.Equal(1, phase.Count));
        Assert.All(report.Phases, phase => Assert.True(phase.MaxUs >= 0));
    }

    [Fact]
    public void ClosingStoreWritesOperationalProfileWithoutChangingEvidenceStreams()
    {
        string root = Path.Combine(Path.GetTempPath(), $"annotator-profile-{Guid.NewGuid():N}");
        try
        {
            HumanCaptureProfile profile = HumanCaptureProfiles.CombatReadRichV2;
            var manifest = new RecordingManifestV2(
                HumanRecorderV2Contract.SchemaVersion,
                HumanRecorderV2Contract.ManifestSchema,
                "session-profile",
                "timeline-profile",
                DateTimeOffset.UtcNow,
                "test",
                new string('a', 40),
                "test-runtime",
                profile.ProfileId,
                EvidenceIdentity.Sha256Json(profile),
                profile.SupportedActionFamilies,
                Array.Empty<string>());
            string directory;
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
            {
                directory = store.DirectoryPath;
                store.AppendRunEvent(new RunJournalEvent(
                    HumanRecorderV2Contract.SchemaVersion,
                    HumanRecorderV2Contract.RunJournalSchema,
                    "event-profile",
                    "session-profile",
                    "run-1",
                    "timeline-profile",
                    1,
                    DateTimeOffset.UtcNow,
                    "run_started",
                    null,
                    null,
                    null));
            }

            string path = Path.Combine(directory, "performance-profile.json");
            Assert.True(File.Exists(path));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(
                RecordingPerformanceProfiler.ReportSchema,
                document.RootElement.GetProperty("schema").GetString());
            Assert.Contains(
                document.RootElement.GetProperty("phases").EnumerateArray(),
                phase => phase.GetProperty("phase").GetString() == "journal_append_buffered");
            Assert.Contains(
                document.RootElement.GetProperty("phases").EnumerateArray(),
                phase => phase.GetProperty("phase").GetString() == "close_evidence_durable_flush");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
