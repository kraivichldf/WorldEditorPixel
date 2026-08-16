# ADR-0007: Layered Campaign Tile Taxonomy v3

- Status: Accepted; Phase 1 core implemented, persistence and editor integration pending
- Date: 2026-08-10

## Context

Version 2 proves that one campaign cell can be the designer-facing unit for type and centre height. Its single `CampaignTileType` now mixes four different meanings:

- surface cover: Plains and Forest;
- terrain form: Hills and Mountain;
- water or network: Sea, Lake, River, Large River, and River Junction;
- shore presentation: legacy Coastal plus current Beach and Cliff.

These meanings are not mutually exclusive. A valid world may need a forested hill crossed by a river, a barren mountain beside a lake, or a desert coast. Encoding every combination as another enum value would create an unbounded palette and make save data difficult to extend.

## Decision

Version 3 keeps the campaign tile and its signed whole-metre centre height authoritative, but replaces the single mixed type with coordinated layers.

### Base surface

Every tile owns exactly one `SurfaceType`:

```text
Unassigned
Grassland
Forest
Desert
Wetland
Tundra
BarrenRock
Sea
Lake
```

Grassland replaces the ambiguous name Plains. Desert, Wetland, Tundra, and BarrenRock provide the missing minimum land coverage without introducing biome-specific variants prematurely.

### Terrain form

Flat, Rolling, Hills, Mountain, and Cliff are deterministic projections of stored centre height, local relief, slope, sea level, and a versioned `TerrainFormProfile`. They are not painted or stored per tile.

### River network

River becomes a sparse tile-aligned network overlay. A River no longer erases the underlying surface. Each overlay preserves a discrete `Regular` or `Large` campaign size; this is categorical and does not claim a physical metre width. A normal route has at most two cardinal river neighbours. An explicit confluence may have three connections with two incoming branches and one outflow. Four-way crossings remain invalid. Drag order supplies flow direction, and an outflow into adjacent Sea or Lake creates a mouth.

The running version-2 River Split tool also persists an undirected three-exit `RiverJunction`. It may express a distributary with one incoming route and two outgoing branches, which is not equivalent to the Phase 1 v3 confluence and cannot fit a record with only one outflow. Migration must keep it unresolved until the designer classifies its direction, and canonical preservation of a split requires a later directed multi-outflow/distributary extension. Version 3 must never silently relabel every version-2 junction as a confluence.

### Shore treatment

Any land surface cardinally adjacent to Sea or Lake receives a derived shore. Sparse per-edge overrides select `Beach` or `Cliff`; absence means `Auto`. Coastal, Beach, and Cliff stop being full base-surface types.

The editor may retain the current 10% water plus 5% shore bands as preview proportions. Those fractions are editor visualization, not literal physical widths for FPS terrain generation.

### Independent editing

Base surface and height remain one atomic tile stamp. River and shore tools mutate only their own layers. Painting Grassland over a forested river changes the base surface without deleting the River overlay or a still-valid shore override. A surface edit cannot turn a River-bearing land tile into Sea or Lake until the River is removed, and a surface edit that removes a land-water boundary clears its now-invalid shore override inside the same undoable command.

## Compatibility

Version 2 remains the implemented and supported **application and file format** until the complete version-3 reader, writer, migration, UI, renderer, and tests ship together. The engine-neutral Phase 1 domain is now implemented in `Kingdom.World.Core.Campaign.V3`, but no running editor or serializer consumes it yet. Version-2 projects will convert in memory and must be saved to a different folder. Every stored v2 type has a deterministic mapping and any lossy assumption produces an explicit migration warning.

The complete target contract and mapping table are in [[../Reference/Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]].

## Implementation status

Phase 1 completed on 2026-08-10:

- `CampaignWorldV3` owns sparse base tiles, River overlays, shore overrides, and one validated terrain-form profile without depending on Avalonia or a game engine;
- terrain form follows the accepted 3 × 3 neighbourhood, cardinal-grade, prominence, elevation, precedence, and edge-clamping rules;
- River data preserves regular/large size, while validation covers land ownership, directed adjacency, mouths, uphill flow, segment/confluence degree, four-way rejection, and directed cycles;
- shore treatment derives Beach or Cliff per land-water edge, supports sparse per-edge overrides, and clears invalid overrides when a base edit removes the boundary;
- canonical validation rejects unresolved River outflow, while relaxed validation retains it for the future v2 migration review path;
- 30 Phase 1 tests pass, including Large River size preservation and unknown-size rejection, alongside the active version-2 and legacy coverage.

Phase 1 intentionally does not add version-3 files, conversion, commands/history, rendering, editor controls, or a new executable. Those remain the later release-boundary work described in the reference specification.

## Consequences

- A tile can represent combinations such as Forest + Hills + River without enum multiplication.
- Desert, Wetland, Tundra, and BarrenRock become first-class minimum surfaces.
- Height and slope regain authority over terrain form while surface remains designer-authored.
- River confluences become deliberate and valid without permitting arbitrary crossings.
- Shore rendering adapts to every land surface instead of assuming all coasts are grassy.
- The editor needs grouped tools for Surface, River, and Shore rather than one increasingly long list.
- Version-3 persistence adds optional sparse river and shore files plus a versioned terrain-form profile.
- Migration from v2 River, Large River, River Junction, Hills, Mountain, legacy Coastal, Beach, and Cliff can require designer review because v2 did not retain the now-separated meanings or River direction. Current v2 loading already normalizes legacy Coastal to Plains and reports that lossy fallback.

## Supersession boundary

This decision preserves the exact campaign grid, tile-centre height, interpolation, sparse storage, and delta-history principles of [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]]. When version 3 is implemented, it supersedes the single-type River and full-tile shore placement decisions in [[ADR-0005 - Water and River Tile Topology|ADR-0005]] and [[ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006]]. Until then, ADR-0004 through ADR-0006 remain the truth for the running version-2 application.
