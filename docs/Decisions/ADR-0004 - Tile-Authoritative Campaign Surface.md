# ADR-0004: Tile-Authoritative Campaign Surface

- Status: Accepted
- Date: 2026-08-10

## Context

The editor must match the designer's unit of thought and action. In the prior design, a campaign tile held only a type while a dense, independent sample lattice held elevation. Painting could fill a tile's classification, but changing its shape still required sample brushes, radii, strengths, falloff, and spacing. The visible campaign cell was not the authoritative terrain unit.

A `700 × 700 km` world with `5 × 5 km` cells has exactly `140 × 140 = 19,600` designer-facing tiles. The required operation is: choose what one tile is, choose how high its centre is, and let the editor create the slope to its neighbors.

## Decision

Version 2 makes the campaign tile the only authoring resolution.

Each tile owns one atomic value:

```text
CampaignTileData = (CampaignTileType Type, Int16 HeightMetersAtCentre)
```

The height at any non-centre position is derived by bilinear interpolation of the four surrounding tile-centre heights, with nearest-centre clamping at world edges. Type remains discrete and fills the complete containing cell.

World dimensions must be exactly divisible by tile size. The New World form exposes only world width, world height, tile size, sea level, default centre height, and allowed height range. Sample spacing, chunk size, terrain brush choice, sub-tile brush radius, strength, falloff, and flatten target are removed from active authoring. The editor may select a bounded square **of complete campaign tiles** around a pointer coordinate; that area extent applies the same atomic value to every included tile, clips at world bounds, and is not a sample brush. Version-2 River and Large River painting keeps a fixed one-tile footprint so route topology remains legible. The explicit River Split tool may stamp a small fixed multi-tile footprint, but every changed coordinate still receives one complete `CampaignTileData` value and the entire split is one atomic delta command.

Sparse persistence omits tiles equal to `(Unassigned, DefaultTileHeightMeters)`. One drag records complete before/after tile values and participates in bounded undo/redo.

Version-1 projects are imported by copying type and averaging the legacy samples owned by each tile. Conversion is in memory; the desktop application requires saving to a different folder.

## Consequences

- One visible grid cell equals one editable and persisted campaign tile.
- Type stamps fill the complete tile by construction.
- A multi-tile paint area is only a batch selection of those same complete tiles; it adds no persisted shape, interpolation control, or hidden resolution.
- Different neighboring heights create slopes automatically without a second brush system.
- The surface is continuous but generally not differentiable at centre-aligned interpolation seams; this is acceptable for the campaign-scale contract.
- File and memory costs scale with tiles that differ from the implicit default.
- A future importer must implement the documented interpolation, not invent a different smoothing rule.
- Detailed sub-tile terrain, erosion, splines, and tactical meshes are future derived or separate systems rather than hidden sample authority.
- Version-1 classes remain for strict import and regression tests, increasing short-term code volume while avoiding destructive migration.

The fixed type vocabulary and derived River connections, including explicit three-exit Y junctions, are extended by [[ADR-0005 - Water and River Tile Topology|ADR-0005]] without changing this tile-authoritative height contract.

[[ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]] preserves tile and height authority while accepting layered classification as the next, not-yet-implemented format boundary.
