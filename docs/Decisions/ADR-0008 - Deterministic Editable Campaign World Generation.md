# ADR-0008: Deterministic Editable Campaign World Generation

- Status: Accepted
- Date: 2026-08-11

## Context

Designers need a useful starting world without losing the campaign tile as the only authoring authority. Required starting shapes include islands, directional coastlines, all-land maps, central inland seas, continents, and archipelagos. A generated result must contain plausible relief, shores, lakes, and rivers, but it must remain possible to repaint any tile immediately.

The removed generated-raster workflow is not suitable for this requirement: it created a secondary terrain product instead of normal editable campaign data. A transient read-only rendering of the actual generated campaign tiles is acceptable for pre-commit review because it is neither persisted nor authoritative. Generated drainage may form validated three-exit River Junction confluences while preserving version-2's undirected, maximum-three-exit topology. Normal shores preserve their original land/custom identity and derive a matching 10% water edge; only sufficiently steep water-facing land becomes Cliff. Designers may add explicit undirected River Junction split/distributary shapes afterward through the ordinary editable world.

## Decision

World generation is an optional branch of **New World**. `Blank` preserves the existing untouched grid. Every other preset materializes ordinary version-2 `CampaignTileData` values—one portable base type, optional safe custom-land ID, and one whole-metre centre height per tile—into a new `CampaignWorld`. No generator snapshot, ownership flag, hidden elevation layer, or special generated-mode save format survives creation.

For non-Blank presets, New World keeps that `CampaignWorld` as a temporary preview and remains open. Any input change marks it stale and prevents acceptance until regeneration. **Use this world** commits the exact reviewed object to the editor without a second generator call. Closing or cancelling discards the temporary world. `Blank` requires no preview and creates directly.

Generation is deterministic from the validated world definition, preset, signed 32-bit seed, terrain style, Mountain-system profile, hydrology amount, directional coastline style, tidal-inlet amount, optional custom inland tile ratios, and optional safe custom-land definitions. Equal inputs must produce byte-for-byte equal ordered tile entries.

Land masks use analytic signed-distance fields combined with seeded multi-scale noise. Directional Coast presets additionally compose a seed-derived, bounded continental advance/retreat with deterministic physical-scale geography, so the seed changes both coastline identity and the broad land/water balance. Flowing bays and capes uses smooth regional lobes plus a curved, continuously tapered peninsula field to produce one reference-like mainland silhouette without mandatory islands. Natural and Rugged rotate through distinct landmark families—major gulf, hooked cape, barrier sound, and offshore-island strait—before adding smaller bays, peninsulas, island groups, and nearshore detail. This makes characteristic regional structure explicit rather than treating realism as uniform noise amplitude. All profiles preserve only the forced named Sea edge; other boundaries follow seeded geography and may contain land, connected Sea, or both as refined by [[ADR-0014 - Open Directional Coast Boundaries|ADR-0014]]. After an initial ocean resolve, the optional tidal-inlet pass ranks lowland one-edge coast mouths, chooses inward targets, and routes bounded Sea-connected drowned valleys with a deterministic low-ground A* cost. It reruns ocean resolution before final elevation, drainage, and shore classification. `None` is the compatibility default; `Land only` forces the pass off. A version-2 full Sea tile represents a broad estuary or drowned channel at campaign scale, not a narrow constructed canal.

Elevation uses coast distance, multi-scale regional detail, and a transient physical-kilometre tectonic province field. Convergent motion raises coherent belts, divergence lowers rifts, and shear reinforces boundary relief. Deterministic thermal relaxation and stream-power erosion then modify the centre-height field before Lakes and final Rivers are solved. Lakes come from depressions discovered by a four-neighbour priority flood. Rivers follow the resulting depression-free drainage receiver graph and flow-accumulation field. A compatible tributary may join an accepted downstream route when its independent prefix is long enough and the merge produces exactly one valid three-exit `RiverJunction`; lateral touches and four-way crossings remain invalid. A route at least `100 km` long may classify a qualifying downstream reach as Large River: widening begins no earlier than 60% along the path, retains at least `30 km` and at most `80 km` of broad reach, and requires accumulated drainage of at least `1.10` times the channel-head threshold.

Normal water-facing low-slope land remains Desert, Steppe, Plains, Forest, Hills, Mountain, or custom land and derives matching water over the outer 10% of each facing edge. No Coastal or automatic sand value is generated. A sufficiently steep shore becomes `Cliff`. Land classification uses the eroded centre elevation, land-neighbour grade, deterministic ridged relief, tectonic/orogenic suitability, bounded Mountain-system selection, deterministic aridity, and deterministic moisture.

Custom land ratios are an opt-in target over Plains, Forest, Desert, Hills, Mountain, and Steppe after Sea, Lake, River, Large River, and steep water-facing Cliff are excluded. Gentle water-facing land participates because automatic coast is not a terrain category. Ratios must total 100%, and Mountain remains capped at 12%. Deterministic largest-remainder apportionment produces integer targets. Mountain, Desert, and Steppe keep their geographical eligibility rules; any target shortfall becomes Plains rather than invalid terrain. [[ADR-0025 - Built-in Steppe Terrain|ADR-0025]] freezes the appended value, thresholds, and compatibility path.

The exact formulas, preset constraints, threshold values, processing order, and invariants are part of [[../Reference/Campaign World Generation|Campaign World Generation]].

## Consequences

- A designer can choose Blank or a reproducible generated starting point in the same New World journey.
- A designer can compare repeated results without replacing the open document, and can commit only an up-to-date preview.
- Generated tiles can be painted, dragged, undone, saved, and reopened exactly like hand-authored tiles.
- Changing the seed creates a different world without non-deterministic runtime randomness.
- Designers can choose a restrained, natural, or rugged directional shoreline independently from hydrology and tidal-inlet density.
- Designers can opt into a sparse through drowned coastal treatment without allowing disconnected Sea cells or a second canal-authoring layer.
- Designers can directly tune the eligible land-type mix while world shape, drainage, and shoreline grade continue to own Sea, Lake, River, and Cliff counts; automatic coast adds no count of its own.
- Ratios are honest constrained targets: suitable geography can lower Mountain, Desert, or Steppe output, and the UI states that the difference returns to Plains.
- Priority-flood drainage prevents rivers from being routed by a naive local downhill choice that becomes trapped in single-tile pits.
- Generated Rivers follow one depression-free receiver graph and may form geographically informed tributary confluences through validated three-exit junctions. They never create a four-way crossing. The post-generation River Split tool remains the explicit way to add designer-controlled undirected split/distributary topology.
- A generated `Cliff` remains a full version-2 tile; normal shores keep their original terrain so a multi-kilometre tile is never mislabeled as generic coast or beach.
- Version-2 terrain type and height remain portable authority. An optional versioned custom-land catalog persists definitions and safe per-tile IDs, while generator provenance remains a creation input rather than persistent terrain authority.
- Generation is bounded by an explicit tile-count limit and runs away from the UI thread.

The accepted [[ADR-0007 - Layered Campaign Tile Taxonomy v3|version-3 taxonomy]] can later preserve River direction and underlying land. Until that complete format boundary ships, this generator targets the running version-2 model and validates all output through `CampaignTileMap`. The geological and drainage-method upgrade is refined by [[ADR-0010 - Tectonic Erosion and Hierarchical Drainage|ADR-0010]]. Tidal-inlet counts and routing are refined by [[ADR-0013 - Opportunity-Based Tidal Inlets|ADR-0013]].

The original single-ellipse Continent mask is replaced by the hierarchical multi-mass **Continental world** construction in [[ADR-0023 - Hierarchical Continental World Generation|ADR-0023]].

Directional Coast behavior at continental extents, including the valid maximum-size span clamp and the separation between stochastic Natural/Rugged hierarchy and intentionally authored Flowing Capes, is refined by [[ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024]].
