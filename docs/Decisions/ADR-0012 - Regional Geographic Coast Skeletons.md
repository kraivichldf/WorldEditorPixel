# ADR-0012: Regional Geographic Coast Skeletons

- Status: Accepted
- Date: 2026-08-13

## Context

Directional Coast presets previously began with a single-valued boundary `v = C(u)`. Seeded bends, ellipse landmarks, and nearshore noise could make that line less regular, but they did not guarantee a recognizable two-dimensional regional form. A result could still read as one shoreline running across the map: land on one side, water on the other. Adding more high-frequency noise would only make that line rougher; it would not reliably create the paired bays and attached long peninsula visible in real regional coastlines.

The editor needs a deterministic campaign-scale silhouette with geographic character while preserving whole-tile authority, its named Sea-edge guarantee, editable preview acceptance, physical-kilometre scaling, and bounded generation cost.

## Decision

Every directional Coast character receives one regional geographic skeleton after its supporting bays, capes, and landmark systems are composed. All directional presets continue to rotate or reflect through one canonical coordinate system where `v` points toward the named Sea edge and `u` runs along the coast.

The skeleton is constructive solid geometry over signed land scores:

1. Choose a seeded along-coast root and evaluate the local continental shelf position.
2. Subtract two unequal, slightly rotated water ellipses on opposite sides of the root. Their physical depth and span create neighboring gulf and cove regions rather than a repeated noisy edge.
3. Build a cubic Bézier centreline from a protected inland anchor, through a narrow neck and wider body, to a tapered seaward tip.
4. Union a variable-radius signed tube around that curve. The inland anchor crosses the maximum bay cut plus a five-tile safety margin, so later whole-tile classification cannot leave the peninsula as a diagonally touching island.
5. Apply a restrained two-octave physical-kilometre simplex perturbation to the tube radius so the landform is not mathematically smooth.

The radius profile uses three smooth transitions: root to neck through the first 24% of the curve, neck to body through 64%, then body to tip. Smooth, Flowing, Natural, and Rugged vary the same regional dimensions rather than using unrelated topology. The construction is skipped when the shorter world dimension is below `90 km`, where a regional peninsula cannot be represented honestly.

Supporting offshore island groups are placed after the skeleton. Their baseline distance moves farther offshore so the regional peninsula does not accidentally absorb every island. The named Sea edge remains forced after mask smoothing. The former opposite mainland-edge force is superseded by [[ADR-0014 - Open Directional Coast Boundaries|ADR-0014]].

The closest-point curve evaluation uses 28 bounded physical segments. Its simplex radius perturbation is evaluated once per tile, not once per segment.

Mountain-system seed separation is also made density-stable because the new land mask exposed an ordering weakness: Sparse, Balanced, and Dense now use the Dense profile's maximum system count when separating seeds. Without an explicit land mix, Dense grows its first two systems to the same cumulative Balanced target before adding its third system. Increasing density therefore cannot reduce Mountain coverage merely because a different seed spacing was chosen.

## Consequences

- Directional generated worlds can contain a long mainland-connected peninsula with Sea on both flanks, not only a one-dimensional coast wall.
- Large bay, gulf, neck, body, and tip dimensions remain meaningful in kilometres across valid tile sizes and aspect ratios.
- Flowing stays one continuous mainland; Natural and Rugged may retain separated offshore islands.
- Equal definition, settings, and seed still produce identical ordered tile data.
- The result remains ordinary editable campaign tiles. No coastline curve, mask layer, or generation history is persisted.
- Existing directional seeds intentionally produce different silhouettes and may shift terrain counts because the land mask changed.
- This remains a bounded game-oriented geographic construction, not a simulation of plate tectonics, sea-level history, sediment transport, or real-world coast data.

Exact dimensions and formulas are documented in [[../Reference/Campaign World Generation|Campaign World Generation]].

## Large-world refinement

[[ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024]] refines this decision for continental extents. The full regional skeleton remains the compact-world contract and remains deliberate for Flowing Capes. Smooth, Natural, and Rugged blend the skeleton to zero between `1,400 km` and `4,200 km` of shorter world dimension; their large-world character instead comes from kilometre-scaled macro shelf displacement, distributed heterogeneous landmarks, irregular nearshore structure, and island arcs. This prevents the compact paired-bay/cape construction becoming a conspicuous repeated symbol on a `10,000 km` map while preserving the accepted `700 km` behavior.
