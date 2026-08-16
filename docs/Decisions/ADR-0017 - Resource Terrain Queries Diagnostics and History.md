# ADR-0017: Resource Terrain Queries, Diagnostics, and History

- Status: Accepted; engine-neutral terrain queries, diagnostics, and shared history implemented
- Date: 2026-08-15

## Context

[[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]] establishes sparse natural-resource authority without attaching it to the running version-2 world or the isolated version-3 terrain aggregate. The next domain slice must let resource rules inspect either terrain model, report manual out-of-profile occurrences without deleting them, and represent ordinary resource edits in the existing delta-based undo history.

The seam must not make resource storage depend on a mixed version-2 tile enum or version-3 layer classes. Diagnostics must also distinguish proven rule mismatches from climate, geology, association, and preference factors that have not been calculated yet.

## Decision

### Version-neutral terrain query

Add an engine-neutral `ICampaignResourceTerrainQuery` with:

- the shared validated `CampaignWorldDefinition`;
- a monotonic terrain revision;
- one normalized `CampaignResourceTerrainSample` query by exact tile coordinate.

The normalized sample contains base surface, derived terrain form, optional custom-terrain ID, centre elevation, maximum cardinal grade, separate exact Sea/Lake/River distances in physical kilometres, River feature flags, and coast features. River flags preserve Present, Large, and Junction independently, so a version-3 River can remain both Large and a Junction without losing information. Coast flags preserve land adjacency to Sea and Lake, coastal water, and effective Beach/Cliff shore presence. The sample classifies the current cell as Unassigned, Land, or Water without exposing either source terrain enum.

The resource surface vocabulary mirrors the accepted version-3 base surfaces: Unassigned, Grassland, Forest, Desert, Wetland, Tundra, BarrenRock, Sea, and Lake. The terrain-form vocabulary is Flat, Rolling, Hills, Mountain, and Cliff.

Two adapters implement the seam:

- version 2 maps Plains/Hills/Beach/legacy Coastal to Grassland, Mountain/Cliff to BarrenRock, preserves Forest and Desert, treats River-family values as Grassland with their exact available River features, preserves custom ID/base fallback, and derives form from the physical elevation neighbourhood rather than trusting the old mixed type enum;
- version 3 maps its base surface and derived form directly, exposes River size/junction and effective per-edge shore meaning, and leaves custom terrain absent until that aggregate supports it.

Both adapters derive coast features from cardinal land/water boundaries; diagonal water alone is not a coast. Three exact multi-source Euclidean cell-centre distance fields separately measure Sea, Lake, and River, transformed into kilometres by campaign tile size. A source cell is `0`, a cardinal neighbour is one tile size away, a diagonal neighbour is `sqrt(2)` tile sizes away, and a missing source is positive infinity. The fields rebuild lazily when their source layers change; version-3 shore-only edits do not rebuild them because shore style does not move Sea, Lake, or River sources. No dense field is persisted.

The implemented adapters are live owner-thread projections over mutable worlds. A later off-thread generator must capture an immutable normalized snapshot on the world-owner thread before background evaluation; it must not concurrently traverse these live adapters while editing continues.

### Suitability diagnostics

Add a pure evaluator and a revision-cached occurrence diagnostic service. The evaluator checks only facts available through the frozen seam:

- assigned terrain and Land/Water/Either medium;
- elevation, grade, and water-distance ranges;
- explicit custom-terrain include/exclude rules.

The existing generic water-distance rule evaluates the minimum of the separate Sea, Lake, and River distances. Keeping the three inputs separate prevents a later contract break when aquatic, coast, or freshwater rules need to distinguish them.

For a custom terrain cell, a non-empty include list is a whitelist for custom IDs; an explicit exclude is a mismatch. Non-custom cells are not rejected merely because a custom include list exists. Existing validation continues to reject the same ID appearing in both lists.

Climate, geology, field weights, association weights, preferred terrain tags, distribution shape, region scale, and final generator suitability remain unevaluated in this slice. Therefore “no current warning” means only that no implemented hard rule is violated; it does not promise procedural placement.

Diagnostics are projections, not authority. They never modify or erase occurrences. The service recomputes lazily when either resource-map revision or terrain-query revision changes, and returns deterministic `Y`, `X`, then ordinal resource-ID results.

### Delta commands and shared history

Represent one resource edit as:

```text
x, y, resourceId, nullable Before, nullable After
```

Null means absence; potential zero never acts as a tombstone. Command construction defensively copies, validates, filters net no-ops, rejects duplicate composite identities, and sorts by `Y`, `X`, then ordinal resource ID.

Execute applies all `After` values in one `CampaignResourceMap.Apply` call. Undo applies all `Before` values in one call. A live stroke builder captures the first Before and latest After for each coordinate/resource pair, applies edits immediately, completes into one command, or restores every first Before atomically on cancellation.

Resource commands use the existing shared `CommandHistory`. There is no second resource-only history. Ordinary add/update/erase/lock edits are undoable; accepted resource regeneration remains a replacement boundary that clears history rather than becoming a giant command.

## Consequences

- Version 2 and version 3 produce the same resource-facing terrain vocabulary and physical measurements.
- Terrain changes preserve resource authority while diagnostics refresh from revisions.
- Manual out-of-profile resources remain valid data with inspectable warning reasons.
- Undo/redo changes only targeted resource identities and cannot partially apply a mixed batch.
- Exact Sea/Lake/River distance calculation has bounded dense working memory and is cached; it is not serialized.
- Version-3 shore edits invalidate diagnostic projections without unnecessarily rebuilding water-distance fields.
- Background generation requires a future immutable terrain-query snapshot boundary.
- Climate/geology and procedural suitability remain deliberately outside this slice.
- The running editor, authoring files, generator, regeneration preview, and `.kworld` export remain unchanged.

This decision extends [[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]] and reuses [[ADR-0002 - Delta-Based Terrain History|ADR-0002]].
