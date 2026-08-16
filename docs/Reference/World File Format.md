# World File Format

## Version-2 project layout

```text
MyWorld/
|-- world.json
|-- campaign-tiles.json
`-- custom-terrain.json (optional)
```

Both files are UTF-8 JSON. Version 2 has no sample-spacing or chunk-storage authoring contract. A `chunks` folder copied from an older project is not part of version 2 and is ignored by the version-2 reader.

## Resource sidecar contract

The running editor saves terrain authority plus a separate sparse resource layer through optional sibling sidecars:

```text
resource-definitions.json
resource-generation.json
resource-tiles.json
```

Those files remain isolated from the terrain serializer so resource-only edits do not rewrite terrain records. Missing resource sidecars are valid and mean built-ins only, no saved resource generation settings, and no stored occurrences. See [[Campaign Resource Layer Plan|Campaign Resource Layer Plan]] and [[../Decisions/ADR-0018 - Resource Persistence and Runtime Package v2|ADR-0018]] for the frozen resource-authoring contract and validation rules.

### Custom resource definitions version 3

`resource-definitions.json` is written only when the catalog contains custom resources. Built-ins are reconstructed from the application catalog and are never copied into this project file. Version 2 added `avoidedTerrainTags`; version 3 adds the required `excludedTerrainSurfaces` hard-rule list. Every version-3 rule member is required; absent ranges are explicit `null` and empty lists are explicit `[]`. Version 1 loads with empty avoidance and surface-exclusion lists. Version 2 preserves avoidance and supplies an empty surface-exclusion list. Every new save writes version 3.

```json
{
  "version": 3,
  "definitions": [
    {
      "id": "amber-resin",
      "name": "Amber Resin",
      "category": "finite",
      "distributionProfile": "surfaceDeposit",
      "medium": "land",
      "symbolId": "crystal",
      "color": "#E6A53A",
      "mapPriority": 45,
      "coveragePercent": 6,
      "richness": "balanced",
      "concentration": "manySmall",
      "rules": {
        "elevationMeters": { "minimum": 0, "maximum": 2500 },
        "grade": null,
        "waterDistanceKilometers": null,
        "regionScaleKilometers": { "minimum": 10, "maximum": 40 },
        "preferredTerrainTags": ["forest"],
        "avoidedTerrainTags": ["arid", "open-land"],
        "excludedTerrainSurfaces": ["desert", "tundra"],
        "customTerrainIncludes": [],
        "customTerrainExcludes": [],
        "fieldWeights": [{ "id": "erosion", "weight": 0.5 }],
        "associationWeights": []
      }
    }
  ]
}
```

Definitions, rule identifiers, surface enums, and weight entries use deterministic order. `medium` is stored once at definition level and supplies the reconstructed rule set. A factor cannot appear in both `preferredTerrainTags` and `avoidedTerrainTags`. `excludedTerrainSurfaces` accepts assigned normalized surfaces only: Grassland, Forest, Desert, Wetland, Tundra, BarrenRock, Sea, and Lake.

### Resource generation settings version 1

`resource-generation.json` is optional because an older or manually authored project may have no saved generation policy. Absence stays absent; the reader does not invent a derived seed.

```json
{
  "schemaVersion": 1,
  "resourceSeed": 12345,
  "seedDerivedFromWorld": true,
  "abundance": "balanced",
  "climate": "autoMixed",
  "geology": "autoMixed",
  "overrides": [
    {
      "resourceId": "gold",
      "enabled": true,
      "coveragePercent": 2,
      "richness": "rich",
      "richnessBias": 10,
      "concentration": "manySmall",
      "mapPriority": 60
    }
  ]
}
```

Overrides sort by ordinal resource ID and must reference the built-in/custom catalog loaded from the same project.

### Resource tiles version 1

`resource-tiles.json` stores only non-empty resource tiles. Tiles sort by Y then X; occurrences within a tile sort by ordinal resource ID. Locks are authoring metadata and round-trip here.

```json
{
  "version": 1,
  "tiles": [
    {
      "x": 18,
      "y": 9,
      "resources": [
        { "id": "fresh-water", "potential": 41, "locked": false },
        { "id": "iron-ore", "potential": 72, "locked": true }
      ]
    }
  ]
}
```

Resource readers require exact camel-case properties and string enum values. They reject unknown or duplicate JSON properties, missing/null required values, unsupported versions, malformed or colliding definitions, duplicate/unknown overrides, duplicate tile coordinates or tile resource IDs, empty redundant tile records, out-of-grid coordinates, unknown resource IDs, and potential outside `1..100`. Terrain suitability mismatch remains valid authoring data with a diagnostic warning.

The resource serializer validates all desired documents before touching disk, stages each desired file through a unique sibling temporary path, then atomically replaces each canonical file and removes stale optional files. Direct serializer replacement remains atomic per file. The editor does not save the terrain and resource serializers independently: its project coordinator writes both into one unique sibling staging directory, reloads the complete candidate, checks both captured revisions, and commits only the known terrain/resource files with backup rollback on ordinary I/O failure. This prevents a reported failed save from deliberately leaving a mixed visible project, but does not claim power-loss atomicity across several filesystem entries. Temporary/staging files are non-authoritative; cleanup failure after a successful commit does not make the saved document dirty again.

## Metadata version 2

Representative `world.json`:

```json
{
  "version": 2,
  "worldWidthMeters": 700000,
  "worldHeightMeters": 700000,
  "campaignTileSizeMeters": 5000,
  "seaLevelMeters": 0,
  "minimumHeightMeters": -1000,
  "maximumHeightMeters": 6000,
  "defaultTileHeightMeters": 0
}
```

Derived values are not serialized:

```text
tilesX = worldWidthMeters / campaignTileSizeMeters
tilesY = worldHeightMeters / campaignTileSizeMeters
tileCount = tilesX * tilesY
```

The reader must reject the manifest unless:

- `version` is exactly `2`;
- width, height, and tile size are positive;
- each world dimension is exactly divisible by tile size;
- derived axis counts fit signed 32-bit coordinates;
- minimum height is lower than maximum height;
- sea level and default height are inside the allowed range.

All distances in the file are metres. The desktop form accepts whole kilometres and converts them exactly before creating the definition.

## Campaign tiles version 2

`campaign-tiles.json` stores only tiles that differ from the implicit default `(unassigned, defaultTileHeightMeters)`:

```json
{
  "version": 2,
  "tiles": [
    { "x": 12, "y": 7, "type": "sea", "heightMeters": -120 },
    { "x": 13, "y": 7, "type": "forest", "heightMeters": 20 },
    { "x": 20, "y": 9, "type": "river", "heightMeters": 180 },
    { "x": 21, "y": 9, "type": "largeRiver", "heightMeters": 160 },
    { "x": 22, "y": 9, "type": "riverJunction", "heightMeters": 160 },
    { "x": 23, "y": 9, "type": "desert", "heightMeters": 260 },
    { "x": 24, "y": 9, "type": "steppe", "heightMeters": 250 },
    { "x": 25, "y": 9, "type": "plains", "heightMeters": 240, "customTerrainId": "farmland" }
  ]
}
```

Canonical strings written by the current editor are `unassigned`, `plains`, `steppe`, `desert`, `forest`, `hills`, `mountain`, `sea`, `lake`, `river`, `largeRiver`, `riverJunction`, `beach`, and `cliff`. `heightMeters` is a signed 16-bit whole metre inside the definition's configured range. Readers also accept the early version-2 string `water` and normalize it to `sea`. The removed `coastal` value is accepted only for migration, normalizes to `plains` at the same height, marks the opened document modified, and is never written again.

`customTerrainId` is omitted for ordinary tiles. When present, it must be an ID in the optional catalog and its catalog base must exactly equal the stored `type`. It is valid only for the safe land bases `plains`, `steppe`, `desert`, `forest`, `hills`, or `mountain`; it is never valid on Sea, Lake, River, Large River, River Junction, Beach, Cliff, Unassigned, or the legacy Coastal value. The ordinary `type` remains the portable fallback for consumers that do not recognize custom types.

Unassigned is valid in the sparse file when its height differs from the default. A record exactly equal to the implicit default is redundant and rejected. Readers also reject unknown types, duplicate coordinates, negative or out-of-grid coordinates, out-of-range heights, unknown or mismatched custom terrain IDs, invalid river topology, unsupported file versions, and missing required properties.

The tile file is optional when reading. Its absence means every tile equals the implicit default. The editor writes the file even when its `tiles` array is empty.

## Optional custom terrain catalog

`custom-terrain.json` is written only when the world defines at least one custom land type:

```json
{
  "version": 1,
  "types": [
    {
      "id": "farmland",
      "name": "Farmland",
      "baseType": "plains",
      "color": "#91A85A",
      "generationSharePercent": 30
    }
  ]
}
```

Each ID is a stable 1–32-character lower-case slug beginning with a letter. A name is 1–48 visible characters, color is `#RRGGBB`, and the base is exactly one of Plains, Steppe, Desert, Forest, Hills, or Mountain. A world may contain at most twelve entries. Custom terrain shares together total at most `100`; `0` means the type is manual-paint-only. During generation, positive custom shares join the six default inland ratios in one exact `100%` mix; the stored base remains a fallback/material choice rather than the owner of a share. The catalog is loaded before campaign tiles so every tile reference can be validated. A missing catalog is canonical for worlds with no custom types. Removing the final definition removes this project-owned optional catalog file.

### River topology

River exits are not stored. For a River, Large River, or River Junction record at `(x, y)`, a north, east, south, or west exit exists when the in-bounds tile in that cardinal direction is any of those three values. Normal River and Large River records permit at most two exits. River Junction permits at most three. The reader rejects the whole tile file when a segment has three or four exits or any junction has four exits.

This contract permits isolated River tiles, endpoints, straight segments, bends, loops, transitions between normal and Large River, and explicit three-exit Y junctions. Two-, three-, and four-branch split shapes are represented by one, two, or three cascaded junction records respectively; one tile never represents four branches. River Junction stores neither flow direction nor whether the Y is a confluence or distributary. Continuous physical width, discharge, and flow are not represented. Sea, Lake, Beach, and Cliff carry no adjacency constraint in version 2.

### Automatic coast material

Automatic coast material is derived for every non-water tile and is not serialized as a type or mask. For tile-local coordinates `localX` and `localY` from `0` through `1`, inspect only immediate cardinal neighbours:

- north edge distance is `localY`;
- east edge distance is `1 - localX`;
- south edge distance is `1 - localY`;
- west edge distance is `localX`.

Ignore directions whose neighbour is not Sea or Lake. Choose the remaining direction with the lowest edge distance; exact ties use north, east, south, then west. If no water direction exists or the closest distance is at least `0.10`, use the tile's original built-in/custom material. Below `0.10`, use the neighbouring Sea/Lake material and water texture. Sea, Lake, and the legacy Water alias do not receive a coast treatment themselves.

For one water-facing edge this produces 10% matching water and 90% original terrain. Multiple water-facing edges each contribute their own 10%-deep transition, so the total original-material area can be lower than 90%. No automatic sand band is inserted; an explicitly stored Beach tile supplies sand as its original material and still receives the same water edge. Custom terrain identity/color remains authoritative outside the water band. Procedural patterns themselves are rendering state, not additional saved fields.

## Coordinates and derived height

Tile `(0, 0)` is the north-west/top-left cell. X grows east/right and Y grows south/down. Tile-space coordinates place cell boundaries at integers and centres at half integers.

For a world position in metres:

```text
tileSpaceX = worldX / campaignTileSizeMeters
tileSpaceY = worldY / campaignTileSizeMeters
typeX = min(floor(tileSpaceX), tilesX - 1)
typeY = min(floor(tileSpaceY), tilesY - 1)
```

The type at that position is the complete cell value at `(typeX, typeY)`.

To reproduce the continuous height surface:

```text
u = tileSpaceX - 0.5
v = tileSpaceY - 0.5
x0raw = floor(u), x1raw = x0raw + 1
y0raw = floor(v), y1raw = y0raw + 1
fx = u - floor(u)
fy = v - floor(v)
```

Clamp `x0raw`, `x1raw`, `y0raw`, and `y1raw` independently to the valid tile index range, fetch the four centre heights, linearly interpolate across X on the top and bottom pairs, then interpolate those two results across Y.

Consequences:

- at `(x + 0.5, y + 0.5)`, derived height equals tile `(x, y)` exactly;
- halfway between two centres, height is their arithmetic midpoint;
- the result is continuous across cell boundaries;
- the outermost centre height extends to the world edge.

## Save protocol

The editor serializes `custom-terrain.json` when present and `campaign-tiles.json` to sibling `.tmp` files before atomic replacement, then repeats the protocol for `world.json`. Tile records are ordered by Y then X and catalog types by ID for stable diffs. A leftover `.tmp` file is non-authoritative.

Saving version 2 does not write or consume chunk files.

## Version-1 import

Version 1 used an independent height-sample lattice, sparse type-only campaign file, and little-endian `Int16` chunk files. It remains supported for import, not active authoring.

The converter requires the legacy world dimensions to divide exactly by its campaign tile size. For every new tile it:

1. copies the legacy campaign type, normalizing generic `Water` to `Sea`;
2. assigns each legacy sample to exactly one tile using half-open world intervals `[start, end)`, with the last tile including the final world endpoint;
3. computes the arithmetic mean of those owned sample heights;
4. rounds midpoint values away from zero into the configured `Int16` range;
5. uses bilinear legacy height at the tile centre only if an unusually small tile owns no sample.

Opening performs no write. The desktop editor requires the converted result to be saved outside the source directory, so the version-1 manifest, `campaign-tiles.json`, and `chunks` remain unchanged.

Historical version-1 details are represented by [[../Decisions/ADR-0001 - Unique Chunk Ownership|ADR-0001]] and [[../Decisions/ADR-0003 - Sparse Campaign Tile Types|ADR-0003]].

## Determinism

Saving and reopening a canonical version-2 project must return the same definition, custom catalog, and same type/custom-ID/centre-height value at every tile. Given those stored values, every conforming consumer must derive the same bilinear surface, cardinal River connections, and automatic 10% water-facing coast bands. Zoom, pan, grid visibility, height-only view, procedural texture pattern, hover, and pinned selection are editor state and are not persisted.

World-generation preset, seed, terrain style, Mountain-system profile, hydrology amount, and optional inland tile ratios are creation-time editor inputs and are not persisted in version 2. Custom terrain definitions and their shares persist because they remain available for later painting and regeneration choices, but the fact that a particular tile was generated rather than painted does not. A generated world serializes exactly like a manually painted world because its ordinary type/custom-ID/centre-height tile values are authoritative. See [[Campaign World Generation|Campaign World Generation]] for reproducibility inputs and formulas.

See [[../Decisions/ADR-0005 - Water and River Tile Topology|ADR-0005]] for the water and river decision.

See [[../Decisions/ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006]] for automatic coast derivation, legacy Coastal normalization, and editor texture decisions.

## Planned version 3 — not implemented

The accepted next format further separates base surface and centre height from River and explicit per-edge Beach/Cliff overrides. It is specified in [[Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]] and decided by [[../Decisions/ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]].

No current executable writes version 3. Version 2 remains canonical until the v3 domain model, migration, grouped tools, renderer, persistence, and tests ship as one release boundary.
