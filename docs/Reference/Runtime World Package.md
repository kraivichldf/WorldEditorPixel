# Runtime World Package

## Recommendation

Use the editor's project folder for authoring and a `.kworld` package for game integration. Do not use a headerless `.raw` file as the primary interchange format: without a header it cannot identify grid dimensions, tile size, coordinate orientation, terrain values, custom terrain mappings, or schema version. Full JSON is easy to inspect but needlessly repeats coordinates and field names for every tile.

`.kworld` is a ZIP container with a self-describing JSON manifest and one or more dense binary streams. Version 1 carries only terrain; the accepted version 2 runtime contract adds compact resource streams without changing the terrain record stride.

| Purpose | Format | Behavior |
|---|---|---|
| Continue editing | Project folder: `world.json`, `campaign-tiles.json`, optional `custom-terrain.json` | Sparse, human-readable, atomic save/load contract. |
| Import into a game | One `*.kworld` file | Dense, deterministic runtime interchange data. |

Export never marks the editor world saved, changes its project folder, or alters tile data. The game package is a derived artifact, not authoring authority.

## Container contract

The default editor export is version 1 and contains exactly:

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

The running editor uses the version 2 runtime package overload with its definition-compatible `CampaignResourceMap`. It keeps `tiles.bin` byte-compatible and contains exactly, in stable ZIP-entry order:

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
