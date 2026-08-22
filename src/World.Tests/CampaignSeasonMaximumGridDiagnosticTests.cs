using System.Diagnostics;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Xunit.Abstractions;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonMaximumGridDiagnosticTests
{
    private const int MaximumTilesPerAxis = 500;
    private const int MaximumTileCount = MaximumTilesPerAxis * MaximumTilesPerAxis;
    private const int EnabledDefinitionCount = CampaignSeasonGenerationSettings.MaximumEnabledDefinitionCount;
    private readonly ITestOutputHelper _output;

    public CampaignSeasonMaximumGridDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "MaximumGridDiagnostic")]
    public void Generate_MaximumGridWithMaximumEnabledDefinitionsUsesBoundedWorkingMemory()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 10_000_000,
            worldHeightMeters: 10_000_000,
            campaignTileSizeMeters: 20_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        Assert.Equal(MaximumTilesPerAxis, definition.TilesX);
        Assert.Equal(MaximumTilesPerAxis, definition.TilesY);
        Assert.Equal(MaximumTileCount, definition.TileCount);

        var impossibleRule = new CampaignSeasonRule(
            terrainIncludes: [CampaignTileType.Desert]);
        var customDefinitions = Enumerable.Range(0, EnabledDefinitionCount)
            .Select(index => new CampaignSeasonDefinition(
                $"diagnostic-{index:D3}",
                $"Diagnostic {index:D3}",
                CampaignBuiltInSeason.Spring,
                "#6688AA",
                tintStrengthPercent: 40,
                effectIntensityPercent: 40,
                index == EnabledDefinitionCount - 1
                    ? CampaignSeasonRule.Unrestricted
                    : impossibleRule))
            .ToArray();
        var catalog = new CampaignSeasonCatalog(customDefinitions);
        var settings = new CampaignSeasonGenerationSettings(
            17_029,
            enabledSeasonIds: customDefinitions.Select(static definition => definition.Id));
        var map = new CampaignSeasonMap(definition, catalog);
        var query = new UniformTerrainQuery(
            definition,
            new CampaignSeasonTerrainSample(
                CampaignTileType.Plains,
                CustomTerrainId: null,
                ElevationMeters: 120,
                CampaignSeasonWaterFeatures.None));

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        var source = CampaignSeasonGenerationSource.Capture(query, map);
        var captureElapsed = timer.Elapsed;
        var result = CampaignSeasonGenerator.Generate(
            source,
            catalog,
            settings,
            CampaignSeasonGenerationScope.All);
        timer.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var matchingId = customDefinitions[^1].Id;

        _output.WriteLine(
            "Maximum-grid diagnostic: {0:N0} tiles, {1:N0} enabled definitions, " +
            "capture {2:F3}s, total {3:F3}s, current-thread allocations {4:F1} MiB.",
            definition.TileCount,
            settings.EnabledSeasonIds.Count,
            captureElapsed.TotalSeconds,
            timer.Elapsed.TotalSeconds,
            allocatedBytes / 1024d / 1024d);

        Assert.Equal(MaximumTileCount, result.CandidateMap.TileCount);
        Assert.Equal(MaximumTileCount, result.CandidateMap.GetUsageCount(matchingId));
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, matchingId, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(499, 499, matchingId, out _));
        Assert.Equal(
            MaximumTileCount,
            result.Reports
                .Where(static report => report.Selected)
                .Sum(static report => report.CandidateOccurrenceCount));
        Assert.Equal(EnabledDefinitionCount, result.Reports.Count(static report => report.Selected));
        Assert.Empty(result.CandidateMap.Validate());

        // These broad regression ceilings catch accidental tile-by-definition retention or runaway work.
        // They are intentionally much looser than the observed release diagnostic on ordinary hardware.
        Assert.True(
            timer.Elapsed < TimeSpan.FromSeconds(60),
            $"Maximum-grid generation took {timer.Elapsed.TotalSeconds:F1}s; expected less than 60s.");
        Assert.True(
            allocatedBytes < 768L * 1024 * 1024,
            $"Maximum-grid generation allocated {allocatedBytes / 1024d / 1024d:F1} MiB; expected less than 768 MiB.");
    }

    private sealed class UniformTerrainQuery : ICampaignSeasonTerrainQuery
    {
        private readonly CampaignSeasonTerrainSample _sample;

        public UniformTerrainQuery(
            CampaignWorldDefinition definition,
            CampaignSeasonTerrainSample sample)
        {
            Definition = definition;
            _sample = sample;
        }

        public CampaignWorldDefinition Definition { get; }

        public long Revision => 0;

        public CampaignSeasonTerrainSample GetSample(int x, int y) => _sample;
    }
}
