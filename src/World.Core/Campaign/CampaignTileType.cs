namespace Kingdom.World.Core.Campaign;

public enum CampaignTileType : byte
{
    Unassigned = 0,
    // Retained only so version-1 and early version-2 files using "water" can migrate to Sea.
    Water = 1,
    Plains = 2,
    Forest = 3,
    Hills = 4,
    Mountain = 5,
    Sea = 6,
    Lake = 7,
    River = 8,
    Beach = 9,
    Cliff = 10,
    // Retained only so older version-2 files using "coastal" can migrate to Plains.
    Coastal = 11,
    // Appended so existing version-2 numeric values remain stable.
    Desert = 12,
    // A broad major-river corridor. It shares River topology but keeps a distinct portable value.
    LargeRiver = 13,
    // An intentional three-exit Y junction created only by the multi-tile River Split tool.
    RiverJunction = 14,
    // Semi-arid grassland between wetter Plains and true Desert.
    // Appended so every existing version-2 numeric value remains stable.
    Steppe = 15,
}
