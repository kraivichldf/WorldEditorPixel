# ADR-0013: Opportunity-Based Tidal Inlets

- Status: Accepted
- Date: 2026-08-14

## Context

The tidal-inlet control was described as an amount, and its implementation treated that amount as a target count. It ranked every eligible shoreline cell and continued trying cells until the target was filled. Because adjacent rejected mouths remained available as new attempts, a sufficiently large coast almost always received the maximum number of channels. Target scoring also rewarded maximum inland distance, A* strongly preferred forward movement, and mouth widening could carve an adjacent high cell. The result could read as several deliberately cut canals rather than occasional drowned valleys selected by geography.

The feature must remain deterministic, Sea-connected, bounded, editable as ordinary campaign tiles, and honest about the coarse resolution: one `5 km` Sea tile is not a narrow canal. The setting should express how many natural opportunities generation may consider, not require a quota to be painted.

## Decision

`Few`, `Balanced`, and `Drowned coast` define maximum separated opportunity regions. Candidate mouths are still ranked by low elevation, but now also favor a gentle inland opening. Once the highest-scoring mouth in a separated region has been considered, nearby shoreline cells cannot retry a failed probability roll. Only the configured maximum number of regions is considered.

Each region receives one deterministic acceptance roll. Its probability is the profile chance multiplied by mouth suitability. A seed may therefore accept fewer opportunities than the profile maximum, including zero. Higher settings increase the cap, probability, reach, allowed route suitability, and limited mouth widening; they do not guarantee output.

Target depth is a seeded value inside the profile range rather than always preferring maximum reach. Route search continues to favor low elevation and gentle grade, but follows a bounded curved corridor between mouth and target with a restrained physical-kilometre valley variation. A completed route must pass a combined average-elevation, average-grade, and forward-progress suitability threshold.

Mouth widening is reduced to zero, one, or two route steps. A lateral cell is carved only when its normalized elevation is below the profile limit and its grade from the route is at most `0.045`.

The final ocean flood fill remains authoritative. Any accepted cell that cannot reach the existing external Sea is not retained as Sea, forced land is never carved, and `None` remains byte-for-byte compatible with the unmodified base coast.

## Consequences

- Choosing an inlet setting no longer promises that every world receives a channel.
- Several seeds can use the same setting while producing zero, one, or a few accepted inlets according to separated lowland opportunities.
- Accepted routes are shorter, less uniformly deep, and more likely to bend through low terrain.
- `Drowned coast` remains the strongest treatment, but at the `140 × 140` reference it considers at most three regions instead of attempting to fill five accepted routes.
- Exact inputs still reproduce identical ordered tile data.
- Existing generated previews intentionally change; worlds already accepted or saved remain unchanged.
- A true constructed canal still requires a future directional overlay/network with width, locks, crossings, and underlying terrain rather than full Sea tiles.

Exact thresholds and formulas are documented in [[../Reference/Campaign World Generation|Campaign World Generation]].
