# Campaign Season Occurrence Layer Plan

## Purpose

Add an engine-neutral, preview-first Season layer that records **which Season Definitions can occur on each campaign tile**. The layer parallels resource membership while remaining boolean: one tile may contain several Season Occurrences, and each occurrence is either present or absent.

This plan implements [[../Decisions/ADR-0030 - Preview-First Campaign Season Occurrences|ADR-0030]].

## Product examples

```text
Tile (14, 8):  Spring, Summer, Fall
Tile (15, 8):  Spring, Summer, Fall, Winter
Tile (62, 19): Wet Season, Dry Season
```

These sets are availability, not current calendar state. No value says which season is active now.

## Goals

- Store zero or more stable Season Occurrences per complete campaign tile.
- Permit several different Season IDs on one tile and reject duplicate `(x, y, seasonId)` identities.
- Provide built-in Spring, Summer, Fall, and Winter plus safe project-owned custom definitions.
- Add, erase, lock, unlock, inspect, save, reopen, regenerate, and export exact occurrences.
- Generate overlapping Earth-like season availability from deterministic annual climate support.
- Preserve current authority until a reviewed Candidate is explicitly accepted.
- Keep terrain, resources, seasons, and future calendar simulation independent.

## Non-goals

- months, dates, year length, day length, or time progression;
- a current or next season;
- per-occurrence probability, duration, strength, or `1..100` value;
- weather, temperature simulation, material animation, crop cycles, or gameplay effects;
- forcing season coverage percentages to sum to 100 percent;
- storing a priority winner or catch-all classification.

## Core invariants

1. A Season Occurrence identity is `(X, Y, SeasonId)`.
2. One identity appears at most once.
3. One tile may contain multiple different Season IDs.
4. Occurrence value is exactly `SeasonId + Locked`.
5. Occurrence order never changes meaning.
6. Manual edits affect only the selected Season ID.
7. Generation affects only selected definitions inside the spatial scope.
8. Locked selected occurrences and every unselected occurrence remain exact.
9. Candidate generation never mutates its source map.
10. Runtime export omits authoring locks and rules.

## Domain contracts

### Occurrence

```csharp
public readonly record struct CampaignSeasonOccurrence(
    string SeasonId,
    bool Locked = false);

public readonly record struct CampaignSeasonEntry(
    int X,
    int Y,
    CampaignSeasonOccurrence Occurrence);

public readonly record struct CampaignSeasonMutation
{
    public static CampaignSeasonMutation Upsert(
        int x,
        int y,
        CampaignSeasonOccurrence occurrence);

    public static CampaignSeasonMutation Remove(
        int x,
        int y,
        string seasonId);
}
```

No `CampaignSeasonTile` single-value type remains.

### Occurrence map

`CampaignSeasonMap` owns:

- one value-equal `CampaignWorldDefinition`;
- one immutable `CampaignSeasonCatalog` reference;
- materialized occurrences grouped by tile and keyed by stable Season ID;
- monotonic `Revision`, `OccurrenceCount`, `MaterializedTileCount`, and `LockedOccurrenceCount`.

Required queries mirror `CampaignResourceMap`:

- `TryGetOccurrence(x, y, seasonId, out occurrence)`;
- `GetOccurrences(x, y)` in ordinal stable-ID order;
- `GetOccurrences(area, optionalSeasonId)` with bounded sparse/area traversal;
- `GetMaterializedOccurrences()` in `Y, X, SeasonId` order;
- usage counts per definition.

Atomic `Apply` validates coordinates, catalog membership, duplicate identities, and value identity before changing any state. A no-op does not advance `Revision`.

### Definitions

Built-in stable IDs are:

```text
spring
summer
fall
winter
```

Custom IDs use the existing portable lowercase identifier rules. Definitions retain name, color, tint/effect presentation values, built-in fallback, and one independent environmental rule.

There is no priority array and no default tile season. Catalog order is built-ins followed by ordinal custom IDs.

### Commands and strokes

`CampaignSeasonChange` contains coordinate, Season ID, nullable Before, and nullable After. `CampaignSeasonEditCommand` and `CampaignSeasonStrokeBuilder` follow the resource delta pattern.

Supported stroke operations:

- `Upsert`: add the selected occurrence or update only its lock;
- `Remove`: erase the selected occurrence;
- `SetLocked`: change only an existing selected occurrence;
- `Cancel`: restore the first observed Before value for every touched identity.

One continuous drag produces one shared-history command.

## Deterministic Earth-like generation

### Immutable capture

The owner thread captures:

- a row-major immutable terrain snapshot;
- sorted current Season Entries;
- exact terrain and Season revisions;
- the exact catalog reference.

The worker generator reads no live map or UI object.

### Geographic coverage

Whole-globe tile-centre latitude:

```text
latitude = 90 - 180 * (y + 0.5) / tilesY
longitude = -180 + 360 * (x + 0.5) / tilesX
```

Whole-globe environmental noise is periodic across longitude, but campaign adjacency does not wrap.

Regional coverage uses physical north-south kilometres around an explicit centre latitude:

```text
latitude = centreLatitude
         + (worldHeightKm / 2 - tileCentreYKm) / 111.195
```

A regional span crossing a pole is invalid.

### Annual climate support

Generation describes a climatological year, not one orbital moment.

For tile-centre latitude `lat`, elevation above Sea `hKm`, coherent temperature noise `nT`, and maritime influence `m`:

```text
meanBaseC = 30 - 0.42 * abs(lat)
tiltScale = sin(axialTilt) / sin(23.44 degrees)
continentalAmplitudeC = (2 + 20 * (abs(lat) / 90)^1.35) * tiltScale
amplitudeC = continentalAmplitudeC * (1 - maritimeReduction * m)
meanC = meanBaseC - lapseRateCPerKm * hKm + temperatureNoiseC * nT
warmSeasonC = meanC + amplitudeC
coldSeasonC = meanC - amplitudeC
annualRangeC = 2 * amplitudeC
seasonality = clamp01(annualRangeC / 40)
```

Axial tilt of zero produces zero astronomical annual range. Earth-like default tilt is `23.44` degrees.

Moisture retains deterministic latitude circulation, Sea/Lake/River distance influence, coherent physical-kilometre noise, and bounded orographic rain shadow. Support fields are diagnostics, not editable or persisted tile authority.

### Built-in overlapping rules

Defaults must produce the user's required overlap rather than exclusive climate bands:

| Definition | Default availability rule |
|---|---|
| Spring | `seasonality >= 0.12` and `warmSeasonC >= 5` |
| Summer | `warmSeasonC >= 10` |
| Fall | same transition requirement as Spring |
| Winter | `seasonality >= 0.12` and `coldSeasonC <= 5` |

These are inclusive rules. A mild temperate tile can match Spring, Summer, and Fall; a colder temperate tile can match all four. Extremely warm low-seasonality tiles may match only Summer until custom Wet/Dry/Monsoon definitions are enabled.

Custom definitions use the same rule vocabulary:

- latitude;
- elevation;
- annual mean temperature;
- warm-season temperature;
- cold-season temperature;
- annual temperature range or normalized seasonality;
- annual moisture;
- maritime influence and rain shadow;
- Sea/Lake/River distance;
- built-in/custom terrain Include and Exclude.

### Selection, scope, and locks

Generation receives:

- explicit Included Season IDs in stable ordinal order;
- Excluded definitions implicitly preserved;
- All-world or inclusive rectangular tile scope.

For every selected identity in scope:

```text
if current occurrence is locked:
    preserve it
else if rule matches:
    upsert unlocked occurrence
else:
    remove occurrence
```

A lock protects presence only. There is no locked absence. Authors prevent unwanted automatic additions by excluding the definition from the run or tightening its rule.

### Result and report

The Candidate starts as an exact copy of all current occurrences, then replaces only selected unlocked identities in scope.

One report per catalog definition includes:

- selected/unselected state;
- scope tile count;
- Current occurrence count;
- environmental match count;
- added count;
- removed count;
- generated/retained unlocked count;
- preserved lock count;
- Candidate occurrence count;
- independent Candidate coverage percent;
- zero/no-match reason and warnings.

`ChangedIdentityCount` counts changed `(tile, seasonId)` identities, not changed tiles.

Generation is synchronous, deterministic, and cancellable. The editor calls it away from the UI thread after immutable capture. Equal source, catalog, settings, selection, scope, and seed produce byte-identical Candidate ordering.

### Safety limits

- at most `256` definitions may be selected in one run;
- a Candidate may contain at most `2,000,000` Season Occurrences;
- exceeding the cap fails before acceptance with an instruction to generate a smaller definition subset or narrow the spatial scope;
- deterministic row/chunk cancellation checks are required.

## Editor workflow

### Seasons workspace

The workspace mirrors Resources while retaining Season terminology:

- searchable Season selector;
- selected definition swatch and fallback;
- **Add season** and **Erase selected** tools;
- **Lock manual additions** default-on checkbox;
- `1 x 1` through `25 x 25` complete-tile Paint Area;
- **Manage seasons...** and **Regenerate seasons...** commands;
- selected-definition occurrence count.

The canvas preserves terrain context, then highlights only the selected Season Definition. Exact labels at readable zoom name presence and show `L` when that occurrence is locked. It never combines several occurrences into a misleading single categorical tile color.

Pinned inspection lists every occurrence on the tile in stable-ID order with name, built-in/custom status, fallback, and lock state. Pinned Add/Erase/Lock/Unlock affects only the selected occurrence.

### Custom manager

The manager edits a detached catalog:

- create manual definitions or duplicate a built-in;
- edit identity, name, fallback, appearance, and rule ranges;
- built-in IDs/names/fallbacks remain protected;
- referenced custom deletion requires Remove occurrences or Replace with another definition;
- no priority reorder or catch-all UI exists.

### Generation dialog

The left side provides:

- Included — Regenerate and Excluded — Keep lists;
- deterministic seed options;
- Whole-globe/Regional coverage;
- axial tilt and collapsed Advanced climate controls;
- All-world/rectangle scope.

The right side compares synchronized read-only Current and Candidate canvases for one report-selected Season Definition. The report makes independent coverage explicit and never presents a summed terrain ratio.

Changing generation inputs, included IDs, scope, catalog, or source authority stales the previous Candidate and disables acceptance. Display selection, grid, pan, zoom, and Current/Candidate switching do not stale it.

### New and regenerated worlds

- Blank world creation starts with an empty Season Map; the author may paint or generate later.
- Generated world creation generates terrain privately, then generates the selected built-in/custom Season Occurrences against that candidate terrain, and accepts both only through **Use this world**.
- Same-lattice regeneration preserves exact Season Occurrences and locks.
- Changed-lattice preview remaps occurrences by physical tile centre, merges same target IDs, retains any lock, reports locked drops, and regenerates selected unlocked occurrences against candidate terrain before combined acceptance.

## Authoring persistence

Project files:

```text
season-definitions.json
season-generation.json       optional accepted recipe
season-layer.bin
```

### `season-definitions.json` version 1

Stores the complete canonical catalog. It contains no default season and no priority. Definitions include stable identity, built-in/custom flag, fallback, appearance, and independent rule fields.

### `season-generation.json` version 1

Stores seed/link state, coverage, optional regional centre latitude, axial tilt, Advanced climate controls, ordinal enabled Season IDs, and source/input fingerprints. Spatial scope is operation intent and is not persisted.

### `season-layer.bin` version 1

Little-endian deterministic layout:

```text
header
  magic "KWSEASON"
  version
  index record stride
  occurrence record stride
  tilesX / tilesY / tileCount
  occurrenceCount
  catalog fingerprint

tile index [tileCount]
  uint32 firstOccurrence
  uint32 occurrenceCount

occurrence records [occurrenceCount]
  uint16 seasonCatalogIndex
  byte flags bit0 = Locked
```

Tile index is row-major. Occurrences per tile are catalog-index sorted. The reader validates exact lengths, contiguous ranges, catalog indexes, reserved flag bits, duplicate per-tile IDs, counts, fingerprints, and trailing bytes before constructing authority.

Missing all Season files loads an empty map with built-ins and no accepted recipe. Partial authority is invalid. The first save writes the corrected Season format.

The previously published draft-branch dense single-ID format is intentionally not read because it was never merged into `main` and encodes the rejected domain.

## Runtime package version 3

Version 3 retains `tiles.bin`, `resource-index.bin`, and `resource-records.bin`, then adds:

```text
season-index.bin
season-records.bin
```

`season-index.bin` is one row-major eight-byte record per tile:

```text
uint32 firstRecord
uint32 recordCount
```

`season-records.bin` is one two-byte `uint16 seasonCatalogIndex` per occurrence, ordered by tile then catalog index. Runtime does not export locks.

The manifest publishes the complete Season Catalog with stable ID, name, custom flag, fallback, color, and effect values; exact record layouts/counts/lengths; and SHA-256 for both streams. Importers reject unsupported versions, out-of-range indexes, inconsistent offsets/counts, duplicates, or hash mismatches.

## Changed-lattice remapping

For each occurrence source tile centre in world metres:

```text
targetX = floor(sourceCentreX / targetTileSize)
targetY = floor(sourceCentreY / targetTileSize)
```

- out-of-bounds unlocked occurrences drop with counts;
- out-of-bounds locked occurrences are named in the preview and require explicit acceptance of the drop;
- multiple sources with the same target Season ID merge to one occurrence;
- a merged result is locked when any source occurrence was locked;
- different Season IDs never conflict because they may coexist.

After remap, selected unlocked definitions are regenerated against candidate terrain. Unselected occurrences remain the reviewed remap result.

## Verification matrix

### Domain

- several different Season IDs coexist on one tile;
- duplicate identity rejection is atomic;
- stable ordering, usage counts, area query, no-op revision behavior;
- add/erase/lock/unlock commands and interleaved shared Undo/Redo;
- custom deletion remove/replace behavior.

### Generation

- user example: mild tile gets Spring/Summer/Fall; cold tile gets all four;
- independent matches never shadow one another;
- Included replaces only selected unlocked identities;
- Excluded and locked occurrences remain exact;
- independent coverages can total above 100 percent;
- same input is exact; different seed changes coherent boundaries;
- Whole-globe periodic longitude and Regional physical latitude;
- altitude cooling, maritime amplitude reduction, moisture, and rain shadow;
- cancellation and occurrence-cap failure leave source unchanged.

### Persistence/export

- exact empty, single, multi-ID, custom, and locked round trips;
- deterministic bytes independent of insertion order;
- corrupt headers, ranges, flags, duplicates, indexes, hashes, and partial files rejected;
- staged save rollback preserves previous terrain/resource/season authority;
- runtime index resolves every tile's exact sorted Season Set;
- authoring locks/rules/settings absent from runtime records.

### Editor/native

- selected-season overlay at normal and narrow window sizes;
- Add/Erase/Lock/Unlock changes only selected identity;
- pinned tile lists multiple occurrences;
- generator Included/Excluded flow and independent reports;
- stale Candidate remains visible and cannot be accepted;
- cancel/failure/source drift never mutate current authority;
- New World and changed-lattice preview remain atomic;
- keyboard traversal, focus names, and validation messages.

### Performance

- representative `140 x 140 = 19,600` world;
- maximum `500 x 500 = 250,000` grid with four built-ins;
- visible rendering scales with visible tiles plus selected occurrences;
- generator does not retain a `tileCount x catalogCount` matrix;
- persistence/export streams use bounded buffers.
