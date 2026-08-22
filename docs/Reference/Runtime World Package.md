# Runtime World Package

## Recommendation

Use the editor's project folder for authoring. For game integration, choose compact `.kworld` or readable `*.world.json`. Do not use a headerless `.raw` file as the primary interchange format: without a header it cannot identify grid dimensions, tile size, coordinate orientation, terrain values, custom mappings, Resources, Seasons, or schema version.

`.kworld` is a ZIP container with a self-describing JSON manifest and binary streams. Version 1 carries only terrain; version 2 adds compact resource streams without changing the terrain record stride; the implemented season-aware version 3 keeps those three streams byte-compatible and adds a dense Season span index plus sparse Season occurrence records.

| Purpose | Format | Behavior |
|---|---|---|
| Continue editing | Project folder: terrain JSON plus optional resource sidecars and Season catalog/generation/occurrence layer | Versioned authoring authority through the staged project coordinator. |
| Compact game import | One `*.kworld` file | Dense deterministic package; smallest and fastest option for large worlds. |
| Readable game import | One `*.world.json` file | Self-describing ordinary JSON; simplest inspection and initial engine integration. |

Export never marks the editor world saved, changes its project folder, or alters tile data. The game package is a derived artifact, not authoring authority.

## Single-file JSON runtime export

**File → Export JSON Data…** writes one indented UTF-8 `*.world.json` file. Its fixed top-level order is `format`, `version`, `world`, `grid`, `tileTypes`, `customTerrain`, `resources`, `seasons`, and `tiles`. Importers must require:

```json
{
  "format": "world-editor-pixel-runtime-json",
  "version": 1,
  "world": {
    "widthMeters": 700000,
    "heightMeters": 700000,
    "campaignTileSizeMeters": 5000,
    "seaLevelMeters": 0,
    "minimumHeightMeters": -1000,
    "maximumHeightMeters": 6000,
    "defaultTileHeightMeters": 0
  },
  "grid": {
    "tilesX": 140,
    "tilesY": 140,
    "tileCount": 19600,
    "origin": "northWest",
    "xAxis": "east",
    "yAxis": "south",
    "order": "rowMajorYThenX"
  }
}
```

The actual document continues with stable catalogs and exactly `grid.tileCount` tile objects. A representative tile is:

```json
{
  "x": 42,
  "y": 18,
  "terrainType": "forest",
  "customTerrainId": null,
  "heightMeters": 220,
  "resources": [
    { "id": "timber", "potential": 54 }
  ],
  "seasons": ["fall", "spring", "summer"]
}
```

`tileTypes` maps stable string IDs to retained numeric values. `customTerrain` carries stable ID, name, safe base type, and color. `resources.catalog` carries stable ID, name, category, and built-in flag; its `occurrenceCount` must equal the sum of all tile Resource arrays. `seasons.catalog` carries stable ID, name, built-in flag, built-in fallback, color, tint, and effect intensity; its `occurrenceCount` must equal the sum of all tile Season arrays.

Every tile is present, including implicit defaults. Tiles are ordered by `y`, then `x`; catalogs and per-tile occurrences use ordinal stable-ID order. Locks, rules, generation settings/recipes, support fields, diagnostics, selections, and preview state are authoring-only and absent. Equal runtime authority therefore exports identical JSON even when locks or insertion order differ.

A JSON importer should:

1. require the exact format identifier and supported version;
2. validate positive dimensions, `tileCount = tilesX * tilesY`, and the array length;
3. validate canonical coordinate order and reject duplicate/out-of-grid coordinates;
4. resolve terrain, custom, Resource, and Season IDs through their catalogs and reject unknown IDs;
5. validate heights against declared bounds, Resource potentials in `1..100`, and both occurrence totals;
6. convert north-west/Y-south coordinates if needed, then build native engine assets.

The exporter streams directly rather than constructing a second world-sized object, flushes after bounded tile batches, checks all three authority revisions, and replaces the destination only after successful completion. JSON remains larger and slower to parse than `.kworld`; use it when transparency and easy integration matter more than compactness. See [[../Decisions/ADR-0031 - Single-File JSON Runtime Export|ADR-0031]].

## Container contract

The retained terrain-only compatibility overload exports version 1 and contains exactly:

```text
tiles.bin
manifest.json
```

`manifest.json` declares:

- `format: "kingdom-world-runtime"`;
- `version: 1`;
- world dimensions, campaign tile size, sea level, height bounds, and default height in metres;
- grid dimensions and total tile count;
- origin `northWest`, X axis `east`, Y axis `south`, and order `rowMajorYThenX`;
- binary filename, record size, byte length, byte order, field offsets, and SHA-256;
- the numeric tile-type table;
- every custom terrain definition and its byte index.

The ZIP entry timestamps are fixed, the manifest collections use stable ordering, and tile traversal is canonical. Exporting the same world twice therefore produces identical package bytes.

## Dense tile records

`tiles.bin` stores every tile, including implicit-default tiles. Record index and coordinates are related by:

```text
recordIndex = y * tilesX + x
byteOffset  = recordIndex * 4

x = recordIndex % tilesX
y = recordIndex / tilesX
```

Each record is four bytes:

| Offset | Storage | Meaning |
|---:|---|---|
| `0` | `uint8` | Campaign tile type value. |
| `1` | `uint8` | Custom-terrain index; `255` means no custom identity. |
| `2` | little-endian `int16` | Authoritative centre height in whole metres. |

The byte length must equal `tilesX * tilesY * 4`. Importers must compare the SHA-256 of the uncompressed `tiles.bin` bytes with `tileRecord.sha256` before accepting the package.

### Tile type values

| Value | Name |
|---:|---|
| `0` | Unassigned |
| `2` | Plains |
| `3` | Forest |
| `4` | Hills |
| `5` | Mountain |
| `6` | Sea |
| `7` | Lake |
| `8` | River |
| `9` | Beach |
| `10` | Cliff |
| `12` | Desert |
| `13` | LargeRiver |
| `14` | RiverJunction |
| `15` | Steppe |

Value `1` was the legacy `Water` alias and value `11` was the removed `Coastal` classification. Neither is exported: old Water normalizes to Sea and old Coastal project records normalize to Plains before export. Importers should use the manifest's `tileTypes` mapping and reject unknown values rather than silently guessing.

Custom terrain definitions are sorted by stable ID and assigned indexes `0…n-1`. The tile still stores its safe base terrain in byte `0`; byte `1` selects the custom name, color, and identity from the manifest. This preserves a usable fallback in importers that do not render custom terrain yet.

## Minimal C# importer logic

Use `.kworld` as a source/build asset. Extract or read it in a Unity editor importer, Unreal import plugin, or build pipeline and convert it into the engine's native asset layout. Do not open the ZIP repeatedly during gameplay.

The binary tile read itself is intentionally small:

```csharp
using System.Buffers.Binary;

var recordOffset = ((y * tilesX) + x) * 4;
var type = tileBytes[recordOffset];
var customTerrainIndex = tileBytes[recordOffset + 1]; // 255 = none
var heightMeters = BinaryPrimitives.ReadInt16LittleEndian(
    tileBytes.AsSpan(recordOffset + 2, 2));
```

An importer should then:

1. open the ZIP and require both named entries;
2. parse `manifest.json` and require the known format and version;
3. validate dimensions, record size, byte length, type table, and custom indexes;
4. verify the uncompressed tile SHA-256;
5. read tiles in row-major order or copy the four-byte records into an engine-native array;
6. convert the north-west, Y-south coordinates if the engine world uses a different origin or axis direction;
7. reproduce height between tile centres with the documented bilinear interpolation contract;
8. derive one River network across `River`, `LargeRiver`, and `RiverJunction` cardinal neighbours; require no more than two exits for normal/Large segments, no more than three for RiverJunction, and no four-way tile; then derive matching Sea/Lake material on the outer 10% of every cardinal water-facing edge of a non-water tile while preserving its original built-in/custom material inside.

Version 1 does not contain textures, meshes, roads, resources, settlements, gameplay objects, or a generated height raster. Those remain downstream consumers of the authoritative tile type and centre-height grid.

## Version-2 resource extension

The retained resource-aware version 2 overload uses a definition-compatible `CampaignResourceMap`. It keeps `tiles.bin` byte-compatible and contains exactly, in stable ZIP-entry order:

```text
tiles.bin
resource-index.bin
resource-records.bin
manifest.json
```

`resource-index.bin` stores one eight-byte row-major entry per tile:

| Offset | Storage | Meaning |
|---:|---|---|
| `0` | little-endian `uint32` | First resource-record index for this tile. |
| `4` | little-endian `uint16` | Resource count for this tile. |
| `6` | little-endian `uint16` | Reserved `0`. |

`resource-records.bin` stores four-byte records grouped by tile and sorted by ordinal resource ID:

| Offset | Storage | Meaning |
|---:|---|---|
| `0` | little-endian `uint16` | Resource catalog index from the manifest. |
| `2` | `uint8` | Potential `1..100`. |
| `3` | `uint8` | Reserved `0`. |

The version-2 manifest adds a `resources` section containing:

- a stable ordinal catalog of built-in and custom resource IDs;
- per-stream file name, record size, record count, byte length, byte order, field offsets, and SHA-256;
- only runtime-relevant resource identity fields: catalog index, stable ID, name, Renewable/Finite category, and built-in/custom flag.

Locks, symbols, colors, rules, diagnostics, and generation settings do not enter the runtime package. An empty resource map still produces the dense `tileCount × 8` index and a zero-byte occurrence stream. The exporter hashes all three uncompressed binary streams, fixes ZIP timestamps and entry order, and replaces the destination only if cancellation has not been requested and both terrain and resource revisions remain unchanged. Editor export always produces this explicit version 2 contract; the version-1 overload remains available only for compatibility/tests and terrain-only callers outside the current product workflow.

A version-2 importer must require all four entries, validate manifest version `2`, validate every declared length/record count, verify all three SHA-256 values, require every reserved field to be zero, require potentials in `1..100`, reject catalog indexes outside the manifest catalog, and verify that row-major index spans are monotonic, in bounds, and end at the occurrence-record count. A version-1 importer must reject version 2 rather than guessing.

## Version-3 season extension

The season-aware exporter keeps the version-2 `tiles.bin`, `resource-index.bin`, and `resource-records.bin` bytes unchanged and writes exactly, in stable ZIP-entry order:

```text
tiles.bin
resource-index.bin
resource-records.bin
season-index.bin
season-records.bin
manifest.json
```

`season-index.bin` stores one eight-byte little-endian row-major span per campaign tile:

```text
recordIndex = y * tilesX + x
byteOffset  = recordIndex * 8

offset 0: uint32 firstRecordIndex
offset 4: uint16 recordCount
offset 6: uint16 reserved = 0
```

`season-records.bin` concatenates each tile's occurrences in catalog order. Every two-byte record is one little-endian `uint16 seasonCatalogIndex`. A tile may reference zero, one, or several consecutive records; no duplicate catalog index is valid within a span.

The version-3 manifest adds a `seasons` section. `seasons.indexRecord` and `seasons.occurrenceRecord` declare both files, record sizes/counts, exact uncompressed lengths, little-endian storage, SHA-256, field offsets, and the mapping from `seasonCatalogIndex` to `seasons.catalog`. The catalog uses the same canonical order as authoring. Each entry contains:

- unsigned 16-bit index;
- stable ID and name;
- built-in/custom flag;
- built-in fallback (`spring`, `summer`, `fall`, or `winter`);
- portable `#RRGGBB` color, tint strength, and effect intensity.

Authoring locks, rules, generation settings, source/input fingerprints, climate support fields, diagnostics, and preview reports are intentionally absent. Two maps with identical tile/Season-ID memberships but different lock bits therefore export identical runtime packages.

A version-3 importer must require all six entries and manifest version `3`; verify every v2 stream using the version-2 rules; require `season-index.bin` length `tileCount * 8` and `season-records.bin` length `occurrenceCount * 2`; verify both SHA-256 values; require zero reserved fields and monotonic contiguous in-bounds spans ending at the declared occurrence count; require contiguous catalog indexes beginning at zero; and reject unknown or duplicate Season indexes within a tile span. Importers must reject unsupported versions instead of treating v3 as v2.

The exporter captures terrain, resource, and season revisions, writes bounded buffers with fixed ZIP timestamps and entry order, and performs a final cancellation/revision gate before atomic destination replacement. Equal authoritative inputs produce byte-identical packages. `CampaignEditorProjectSerializer.ExportWithSeasonsAsync` exposes this boundary, and the running Main Window now calls it for every export. Version 1 and version 2 overloads remain compatibility/test seams for callers that intentionally own fewer authorities.

ADR-0030 verification covers zero-to-many Season records per tile, catalog resolution and fallback, exact stream lengths and SHA-256 values, absent authoring locks, and byte-identical version-2 terrain/resource streams inside version 3.
