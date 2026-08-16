# Campaign Tile Taxonomy v3

## Status and boundary

This is the accepted target specification for the next campaign-data version. **Phase 1 of the engine-neutral domain is implemented**, while persistence, migration, editor tools, history integration, and rendering remain pending. The current executable and [[World File Format|version-2 file format]] continue to use one `CampaignTileType` plus centre height.

Implemented in `Kingdom.World.Core.Campaign.V3` on 2026-08-10:

- the canonical base-surface, terrain-form, River, direction, junction, and shore vocabularies;
- a sparse `CampaignWorldV3` aggregate that protects cross-layer land/water invariants;
- deterministic terrain-form analysis and bilinear derived height;
- automatic and overridden per-edge shore resolution with stale-override cleanup;
- directed River validation, including explicit confluences, mouths, uphill rejection, exact shared-edge orientation, four-way rejection, cycles, and migration-only unresolved flow;
- 30 focused xUnit tests, with all 155 repository tests currently passing.

Nothing in this Phase 1 boundary changes the running version-2 UI or writes version-3 project data.

Version 3 changes classification ownership without changing these established truths:

- one exact campaign grid;
- one signed `Int16` whole-metre height at every tile centre;
- deterministic bilinear height between centres;
- sparse persistence and delta-based undo;
- engine-neutral data and explicit versioned files.

## Why the model changes

Surface cover, terrain form, networks, and shore treatment can overlap. They therefore cannot remain one mutually exclusive enum without combinations such as `ForestedHillsRiver` or `DesertCliffCoast`.

```text
Campaign tile
|- authoritative centre height
|- one base surface
|- derived terrain form
|- optional river overlay
`- derived shore plus optional edge overrides
```

## Base surface

Every tile stores exactly one value from this canonical vocabulary:

| Surface | Meaning |
|---|---|
| `unassigned` | No deliberate surface classification yet |
| `grassland` | Grass, open lowland, meadow, or generic temperate ground |
| `forest` | Persistent dense tree cover; forest subtype belongs to a later biome layer |
| `desert` | Arid ground; not limited to dunes |
| `wetland` | Marsh, swamp, bog, or saturated lowland |
| `tundra` | Cold sparse ground and persistent snow-country abstraction |
| `barrenRock` | Exposed stone, scree, or vegetation-poor ground at any elevation |
| `sea` | Salt-water body connected to the global sea system |
| `lake` | Inland water body |

Do not add Hills, Mountain, Cliff, River, Coastal, Beach, road, settlement, farmland, resource, canyon, or valley to `SurfaceType`. Those meanings belong to derived form, an overlay, shore treatment, human-use data, resource data, or derived geometry.

## Derived terrain form

`TerrainForm` is calculated and never stamped:

```text
Flat
Rolling
Hills
Mountain
Cliff
```

The calculation consumes the tile-centre heights in the local 3 × 3 neighbourhood, campaign tile size, and sea level. A `TerrainFormProfile` stored in `world.json` versions the thresholds so editor and engine importers produce the same result.

Initial defaults are intentionally world-configurable:

| Profile value | Initial default |
|---|---:|
| `rollingMinimumGrade` | `0.01` |
| `hillsMinimumGrade` | `0.04` |
| `mountainMinimumGrade` | `0.12` |
| `cliffMinimumGrade` | `0.30` |
| `mountainMinimumProminenceMeters` | `600` |
| `mountainMinimumElevationAboveSeaMeters` | `1500` |

For each tile:

1. Clamp the 3 × 3 neighbourhood at world edges.
2. Compute maximum cardinal grade as absolute centre-height difference divided by tile size.
3. Compute local relief as maximum minus minimum centre height and local prominence as the current centre minus the neighbourhood minimum.
4. `Cliff` wins when maximum grade reaches the cliff threshold.
5. `Mountain` wins when grade reaches its threshold, local prominence reaches its threshold, or the centre reaches the configured elevation above sea level.
6. Otherwise choose Hills, Rolling, or Flat from descending grade thresholds.

These defaults are a deterministic starting point, not a claim of universal geomorphology. Gameplay tuning changes the profile, not individual tile form labels.

## Shore treatment

Every cardinal land-to-water boundary receives a shore. No stored Coastal surface is required.

`ShoreStyle` values are:

```text
Auto
Beach
Cliff
```

`Auto` is the implicit default and is not stored. It resolves to Cliff when the water-facing grade reaches `cliffMinimumGrade`; otherwise it resolves to Beach. An override applies to one edge only, allowing a tile to have a beach on one side and a cliff on another.

The editor preview keeps the current proportions:

- outer 10%: matching Sea or Lake material;
- next 5%: sand for Beach or rock face for Cliff;
- remainder: the tile's real base surface, such as Forest, Desert, or Tundra.

These percentages are visualization fractions. On a 5 km tile they would represent 500 m and 250 m if interpreted literally, so a tactical/FPS importer must generate physical shore widths from its own higher-resolution terrain profile rather than treating the preview fractions as metres.

When multiple water edges meet, the closest edge controls the corner; exact ties use north, east, south, then west. Diagonal water alone does not create a shore.

## River network overlay

A River overlay preserves the underlying base surface and derived form. River editing remains tile-aligned and four-connected.

Each stored River tile contains:

```text
x, y
outflow: north | east | south | west
junction: segment | confluence
size: regular | large
```

Connections to cardinally adjacent River tiles are derived. The outflow may also point into an adjacent Sea or Lake, creating a mouth.

Validation rules:

- a normal segment has at most two River neighbours;
- an explicit confluence has exactly three River neighbours: two must flow into it and it must have one outflow;
- four River neighbours are always invalid;
- River overlays are valid only on land surfaces, never directly on Sea or Lake;
- every resolved outflow must lead to an adjacent River, Sea, or Lake;
- every River-to-River adjacency must be oriented by exactly one of the two tiles flowing across that shared edge;
- River outflow cannot climb to a higher adjacent centre, and directed cycles are invalid;
- one River tile cannot have multiple outflows in version 3;
- drag order runs from source toward mouth and assigns outflow to the next coordinate;
- ending a new tributary on an existing River may create a confluence, but never silently creates a four-way crossing;
- imported v2 Rivers use an in-memory `Unresolved` state until a designer confirms flow; the canonical writer never serializes that state.

Distributaries, deltas, continuous physical width, discharge, bridges, fords, and navigability remain later River-layer fields. This matters for version-2 River Split migration: an undirected three-exit `RiverJunction` might be a confluence or a one-in/two-out distributary. The current Phase 1 v3 record has only one `outflow`, so it must not silently reinterpret a designer-created split as a confluence. A future directed distributary extension needs multiple named outflows (or an equivalent directed-edge representation) before such a split can become canonical v3 data. The implemented `RiverSize` vocabulary preserves the current regular/large campaign category without pretending to store metres.

## Proposed project layout

```text
MyWorld/
|-- world.json
|-- campaign-tiles.json
|-- river-tiles.json       optional
`-- shore-edges.json       optional
```

Representative base record:

```json
{
  "x": 18,
  "y": 9,
  "surface": "forest",
  "heightMeters": 420
}
```

Representative River records:

```json
{
  "version": 3,
  "tiles": [
    { "x": 18, "y": 8, "outflow": "south", "junction": "segment", "size": "regular" },
    { "x": 17, "y": 9, "outflow": "east", "junction": "segment", "size": "regular" },
    { "x": 18, "y": 9, "outflow": "east", "junction": "confluence", "size": "large" },
    { "x": 19, "y": 9, "outflow": "east", "junction": "segment", "size": "large" }
  ]
}
```

This example assumes tile `(20, 9)` is Sea or Lake, so the final eastward outflow is a valid mouth.

Representative shore override:

```json
{
  "version": 3,
  "tiles": [
    { "x": 18, "y": 9, "north": "beach", "east": "cliff" }
  ]
}
```

Omitted River and shore files mean no River overlays and no explicit shore overrides. Sparse base storage still omits `(unassigned, defaultTileHeightMeters)`.

`world.json` adds a required `terrainFormProfile` object containing the six threshold values above. A v3 reader rejects missing, unordered, non-finite, negative, or otherwise inconsistent thresholds. Shore overrides are valid only on land edges currently facing Sea or Lake; stale overrides on other edges are rejected rather than silently ignored.

## Version-2 migration

All centre heights remain exact. Conversion is in memory and must save to a new folder.

| Version-2 type | Version-3 result | Review required |
|---|---|---|
| Unassigned | Unassigned | No |
| legacy Water | Sea | No |
| Plains | Grassland | No |
| Steppe | Grassland | No; later biome/climate data may restore the finer ecological distinction |
| Desert | Desert | No |
| Forest | Forest | No |
| Hills | Grassland; form derives from height | Warn if derived form is not Hills |
| Mountain | BarrenRock; form derives from height | Warn if derived form is not Mountain or Cliff |
| Sea | Sea | No |
| Lake | Lake | No |
| River | Grassland plus River overlay with unresolved flow | Always review underlying surface and flow |
| Large River | Grassland plus Large River overlay with unresolved flow | Always review underlying surface and flow |
| River Junction | Grassland plus unresolved River junction; classify as confluence or preserve as a future distributary split | Always review underlying surface and directed topology; canonical v3 save is blocked until representable |
| legacy Coastal | Grassland plus automatic shore adjacency | Current version-2 readers already normalize this removed value to Plains and report the conversion; retain a migration note if importing an older file directly |
| Beach | Grassland plus Beach override on each water-facing edge | Warn when no cardinal water exists |
| Cliff | BarrenRock plus Cliff override on each water-facing edge | Warn when no cardinal water exists |

The converted document remains marked `Migration review required` until all unresolved River flows and warnings are acknowledged. Saving canonical version 3 is blocked while any River outflow remains unresolved; warnings about inferred base surface may be acknowledged without changing the mapped value.

## Editing and history

- Surface stamp: changes `SurfaceType` and centre height atomically.
- Changing one land surface to another preserves River and still-valid shore data.
- Changing a River-bearing tile to Sea or Lake is blocked until its River overlay is removed.
- Removing a land-water boundary clears its now-invalid shore override in the same surface command and reports that cleanup.
- Height-only adjustment remains unavailable; changing height is part of a complete base stamp unless a later decision reintroduces it.
- River tool: changes River overlay only.
- Shore tool: changes one or more edge overrides only.
- Terrain form inspector is read-only because the value is derived.
- Each continuous drag produces one delta command scoped to the layer it edits.
- Undoing a surface stroke does not remove a River; undoing a River stroke does not change height or surface.

The left tool rail should be grouped as `SURFACE`, `RIVER`, and `SHORE`. Derived form belongs in the inspector, not the paint palette.

## Implementation order

- [x] Add engine-neutral v3 domain types, terrain-form projection, validation, and unit tests.
- [ ] Add strict v3 files plus read-only v2-to-v3 conversion with warnings; do not change the UI yet.
- [ ] Replace the mixed palette with grouped Surface, River, and Shore tools.
- [ ] Compose base texture, derived form shading, River overlay, and shore edge rendering.
- [ ] Add layer-scoped undo/redo and migration-review UI.
- [ ] Publish v3 only after roundtrip, migration, topology, rendering, and startup verification pass together.

Do not partially write version-3 files from a version-2 UI. The format switch is one release boundary.

## Acceptance criteria

- A Forest tile can simultaneously derive as Hills, carry a River, and own a Beach override.
- Desert, Wetland, Tundra, and BarrenRock can be painted, persisted, reopened, undone, and redone.
- Changing height can change derived form without rewriting surface data.
- Every land surface receives an automatic shore beside Sea or Lake.
- Three-way River topology is accepted only as a valid directed confluence; four-way topology is rejected atomically.
- Every v2 type converts deterministically and all lossy assumptions are visible.
- A v2 River Junction is never silently converted into a confluence; migration either resolves a valid direction-supported topology or remains blocked for the directed distributary extension.
- Saving and reopening returns identical base tiles, River overlays, shore overrides, form profile, and derived results.
- A future engine importer can reproduce form, River connections, and shore selection without linking editor code.

## Explicitly deferred

Biome subclasses, climate simulation, seasonal snow, geology, resource potential, roads, settlements, farmland, bridges, fords, continuous river width/discharge, deltas, erosion, separate water-surface/seabed elevation, tactical meshes, and physical shoreline width remain separate future work.

See [[../Decisions/ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]] for the decision boundary.
