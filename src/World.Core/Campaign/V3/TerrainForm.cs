namespace Kingdom.World.Core.Campaign.V3;

public enum TerrainForm
{
    Flat,
    Rolling,
    Hills,
    Mountain,
    Cliff,
}

public readonly record struct TerrainFormAnalysis(
    TerrainForm Form,
    double MaximumCardinalGrade,
    int LocalReliefMeters,
    int LocalProminenceMeters);
