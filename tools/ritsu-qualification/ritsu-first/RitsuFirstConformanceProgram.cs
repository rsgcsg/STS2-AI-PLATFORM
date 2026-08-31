using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace STS2Platform.Qualification.RitsuFirst;

internal static class RitsuFirstConformanceProgram
{
    public static void Main()
    {
        var room = new object();
        var inventory = new object();
        var entries = new[]
        {
            Entry("entry-card", ExperimentalShopEntryKind.Card, 50, true, true, true),
            Entry("entry-relic", ExperimentalShopEntryKind.Relic, 250, true, false, true),
            Entry("entry-potion", ExperimentalShopEntryKind.Potion, 75, true, true, false),
            Entry("entry-removal", ExperimentalShopEntryKind.CardRemoval, 75, true, true, true),
            Entry("entry-sold", ExperimentalShopEntryKind.Card, 0, false, true, true)
        };

        ExperimentalShopProjection roomFixture = new(
            "merchant-room-1",
            room,
            inventory,
            ExperimentalShopStage.Room,
            100,
            entries,
            new[] { "fixture" });
        ExperimentalShopProjection inventoryFixture = roomFixture with
        {
            Stage = ExperimentalShopStage.Inventory
        };
        ExperimentalShopProjection resolvingFixture = roomFixture with
        {
            Stage = ExperimentalShopStage.Resolving
        };

        AssertEquivalent(roomFixture, new[] { "open|merchant-room-1", "proceed|merchant-room-1" });
        AssertEquivalent(
            inventoryFixture,
            new[] { "close|merchant-room-1", "purchase|entry-card", "remove_card|entry-removal" });
        AssertEquivalent(resolvingFixture, Array.Empty<string>());

        AssertEqual("closed", RitsuFirstTreasureProvider.ClassifyStage(
            false, false, false, null, false));
        AssertEqual("opening", RitsuFirstTreasureProvider.ClassifyStage(
            false, false, true, null, false));
        AssertEqual("completed", RitsuFirstTreasureProvider.ClassifyStage(
            true, false, true, null, false));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = "pass",
            shop_fixtures = 3,
            shop_lanes = new[] { "direct", "ritsu_first" },
            treasure_stage_fixtures = 3,
            production_mutation = false,
            qualification_artifact_mvid = typeof(RitsuFirstConformanceProgram)
                .Assembly
                .ManifestModule
                .ModuleVersionId
                .ToString()
        }));
    }

    private static ExperimentalShopEntry Entry(
        string id,
        ExperimentalShopEntryKind kind,
        int cost,
        bool stocked,
        bool affordable,
        bool capacity) =>
        new(
            id,
            $"item-{id}",
            kind,
            new object(),
            cost,
            stocked,
            affordable,
            capacity,
            "fixture-native-legality");

    private static void AssertEquivalent(
        ExperimentalShopProjection fixture,
        IReadOnlyList<string> expectedActionKeys)
    {
        ExperimentalShopDecision direct =
            ShopDirectExperimentalProvider.ProjectForConformance(fixture);
        ExperimentalShopDecision ritsu =
            ShopRitsuFirstExperimentalProvider.ProjectForConformance(fixture);

        AssertEqual(direct.Status, ritsu.Status);
        AssertEqual(direct.Stage, ritsu.Stage);
        AssertEqual(direct.OwnerReferentId, ritsu.OwnerReferentId);
        AssertEqual(direct.Gold, ritsu.Gold);
        AssertSequence(
            direct.Inventory.Select(DescribeEntry),
            ritsu.Inventory.Select(DescribeEntry));
        AssertSequence(
            direct.Actions.Select(DescribeAction),
            ritsu.Actions.Select(DescribeAction));
        AssertSequence(
            expectedActionKeys.OrderBy(value => value, StringComparer.Ordinal),
            direct.Actions.Select(action => action.Key));
    }

    private static string DescribeEntry(ExperimentalShopEntry entry) =>
        $"{entry.ReferentId}|{entry.Kind}|{entry.Cost}|{entry.IsStocked}|" +
        $"{entry.IsAffordable}|{entry.NativeCapacityAllowsPurchase}";

    private static string DescribeAction(ExperimentalShopAction action) =>
        $"{action.Key}|{action.Verb}|{action.SubjectReferentId}";

    private static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        T[] expectedArray = expected.ToArray();
        T[] actualArray = actual.ToArray();
        if (!expectedArray.SequenceEqual(actualArray))
        {
            throw new InvalidOperationException(
                $"Sequence mismatch. Expected [{string.Join(",", expectedArray)}], " +
                $"actual [{string.Join(",", actualArray)}].");
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }
}
