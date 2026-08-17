# Campaign Season Layer Plan

> [!IMPORTANT]
> This is the implemented contract for [[../Decisions/ADR-0030 - Static Preview-First Campaign Season Layer|ADR-0030]]. Slices 1-7 implement and verify the engine-neutral authority, deterministic Earth-like generator, strict authoring sidecars, season-aware staged project/runtime-v3 APIs, complete manual Seasons workspace, preview-first regeneration, generation-backed pinned diagnostics, atomic generated-new-world composition, reviewed changed-lattice lock remapping, native normal/narrow behavior, and the standalone Windows release.

## Goal

Give every campaign tile one static, editable, reproducible seasonal classification that looks geographically coherent at world scale, supports project-defined seasons, and remains independent from terrain and resources.

The complete authoring journey is:

```text
create or open world
  -> configure built-in/custom season definitions and priority
  -> generate a temporary Current/Candidate comparison
  -> inspect distribution, overlap, locks, and warnings
  -> accept the exact candidate
  -> paint/reset/lock complete campaign tiles
  -> shared Undo/Redo
  -> save and reopen identical authority
  -> export stable per-tile season IDs for a game
```

## Non-goals

- no months, dates, years, calendar, world clock, or automatic season advancement;
- no weather simulation, storms, temperature simulation during gameplay, or animated snow accumulation;
- no terrain conversion, elevation changes, River edits, coast edits, or resource placement;
- no forced per-season coverage percentages;
- no independent random seasonal phase per tile;
- no scripts inside custom rules;
- no resource-dependent season generation in this version;
- no engine-specific Unity or Unreal material assets in `World.Core`.

## Canonical authority

### Definition identities

The built-in stable IDs are:

| ID | Initial name | Portable fallback |
|---|---|---|
| `spring` | Spring | Spring |
| `summer` | Summer | Summer |
| `autumn` | Autumn | Autumn |
| `winter` | Winter | Winter |

A portable season ID:

- contains `1..64` lower-case ASCII letters, digits, or hyphens;
- begins with a letter;
- is immutable after creation;
- is unique by ordinal comparison.

Names contain `1..64` trimmed visible characters. Colors use `#RRGGBB`. Tint strength and effect intensity are whole percentages from `0..100`. A custom definition declares exactly one built-in fallback. Built-in IDs and fallback identities cannot be changed or removed.

The catalog supports at most `65,535` definitions because authoring and runtime tile records use unsigned 16-bit catalog indexes. This is a technical interchange ceiling, not a small custom-season product limit. One generation configuration may enable at most `256` definitions.

### Complete tile map

`CampaignSeasonMap` owns:

- one validated `CampaignWorldDefinition`;
- one immutable `CampaignSeasonCatalog`;
- one catalog index for every logical tile in row-major order;
- one lock flag for every logical tile (one bit in the in-memory map; one byte per tile in the persisted sidecar);
- a monotonic process-local revision.

The recommended representation is a dense `ushort[]` plus a packed `ulong[]` lock bitset in runtime memory. At `500 x 500 = 250,000` tiles, identities require about `500 KB` and locks about `31 KB`, excluding object/header overhead. The on-disk sidecar remains a full one-byte lock flag per tile, so each serialized tile record is three bytes. Public APIs accept and return stable IDs; catalog indexes remain an internal/storage optimization.

All single/batch mutations validate coordinates, stable IDs, duplicate coordinates, and replacement definitions before changing state. A failed batch changes nothing. An equivalent batch is a no-op and does not increase revision or clear Redo. Public full-map enumeration is deterministic `Y`, then `X`; viewport queries enumerate only the clipped area.

`DefaultSeasonId` is project authority used by blank-world initialization and **Reset to default**. It initially equals `spring` and may reference a valid custom definition. Deleting the current default requires choosing its replacement first.

### Definition replacement

Deleting a referenced custom definition is one protected document replacement:

1. count current tile/default/priority references in one dense pass;
2. show the affected tile and lock counts;
3. preselect the definition's built-in fallback but allow any valid replacement ID;
4. validate the replacement catalog, map, default, and settings completely;
5. atomically install the replacement tuple;
6. clear shared Undo/Redo and mark the project modified.

No unknown or orphaned season ID is structurally valid.

## Season rules

One rule contains optional inclusive ranges for:

- latitude in degrees `[-90, 90]`;
- elevation in metres within the current world limits;
- local generated temperature in Celsius;
- normalized moisture `[0, 1]`;
- normalized seasonal intensity `[-1, 1]`;
- normalized warming/cooling tendency `[-1, 1]`;
- distance to Sea, Lake, and River in physical kilometres;
- built-in terrain Include/Exclude identities;
- stable custom-terrain Include/Exclude IDs.

All populated constraints are combined with logical AND. Empty Include means all terrain; populated Include is a whitelist; Exclude wins. The same exact terrain ID in Include and Exclude is invalid. A custom terrain inherits the membership of its safe built-in base unless its exact custom ID overrides it; exact custom exclusion has final precedence.

Unknown enum values, non-finite endpoints, minimum greater than maximum, negative distance minima, malformed IDs, or contradictory terrain filters are invalid. An invalid custom definition remains paintable but cannot be generation-enabled.

### Priority

Generation-enabled IDs form one unique ordered list:

1. `winter`
2. `spring`
3. `autumn`
4. `summer`

The first matching rule wins. The final entry is always treated as unconditional while it is last; its configured rule is retained for use if the author later moves it upward. A custom definition may be final. At least one definition must be enabled, no ID may appear twice, and the list may contain at most `256` IDs.

Recommended built-in starter rules are:

| Definition | Default winning condition before priority | Purpose |
|---|---|---|
| Winter | local temperature `<= 5 C` | Polar, cold continental, and alpine appearance |
| Spring | temperature `-5..22 C` and tendency `0.05..1` | Mild-to-cold warming transition |
| Autumn | temperature `-5..22 C` and tendency `-1..-0.05` | Mild-to-cold cooling transition |
| Summer | unconditional while final | Warm, tropical, and otherwise unmatched completion |

These values are editable project defaults, not hard-coded terrain conversions. Wet, Dry, and Monsoon definitions can be placed above the four built-ins and use moisture, water distance, latitude, terrain, and direction ranges.

Generation never fills a quota. A definition may win zero tiles. Reports distinguish:

- no tile passed its environmental rule;
- tiles passed but were captured by higher-priority rules;
- locks preserved an existing different assignment;
- the definition is manual-paint-only.

## Geographic coverage

Tile-centre local coordinates are:

```text
xKm = (x + 0.5) * tileSizeKm
yKm = (y + 0.5) * tileSizeKm
```

### Whole globe

For a grid `widthTiles x heightTiles`:

```text
longitude = -180 + 360 * (x + 0.5) / widthTiles
latitude  =   90 - 180 * (y + 0.5) / heightTiles
```

Whole-globe coverage stretches the season-generation interpretation over the campaign rectangle. It does not change campaign dimensions, physical tile size, left/right adjacency, Rivers, coasts, or pathfinding. Longitude-dependent procedural fields sample periodically so the left and right visual edges meet without a generation seam.

### Regional window

Use mean Earth radius `6,371.0088 km`, giving:

```text
kilometresPerLatitudeDegree = PI * 6371.0088 / 180
latitude = centerLatitude
         + (worldHeightKm / 2 - yKm) / kilometresPerLatitudeDegree
```

Validation uses the complete map edges, not only tile centres:

```text
centerLatitude +/- worldHeightKm / (2 * kilometresPerLatitudeDegree)
```

Both values must remain inside `[-90, 90]`. The UI supplies exact numeric entry plus Equator (`0`), Northern/Southern Mid-Latitude (`+/-45`), and Northern/Southern Polar (`+/-70`) presets. A crossing window is rejected with the maximum valid centre range and a Whole-globe suggestion. Longitude is not required for the first regional model; support noise uses physical local X and does not wrap.

## Static orbital model

### Reproducible phase

`SeasonSeed` is a saved signed 32-bit value. Its initial value derives through explicit stable mixing from the accepted terrain seed when available; otherwise it derives from the value-equal world definition plus authoritative row-major terrain contents. After creation it is independent. **Randomize** changes only the Season Seed.

Use a specified stable 32-bit mixer—not `System.Random`, string `GetHashCode`, process hash randomization, dictionary order, or wall-clock time—to map the seed to one continuous phase:

```text
phase01 = mixedUInt32 / 4294967296.0
orbitalAngle = 2 * PI * phase01
```

The UI never labels this phase as a month or date.

### Solar forcing

Default obliquity is `23.44 degrees`; Advanced allows `0..90`. For latitude `phi`, obliquity `epsilon`, and phase `L` in radians:

```text
declination = asin(sin(epsilon) * sin(L))
sunsetHourAngle = acos(clamp(-tan(phi) * tan(declination), -1, 1))

dailyMeanInsolation =
    (sunsetHourAngle * sin(phi) * sin(declination)
     + cos(phi) * cos(declination) * sin(sunsetHourAngle)) / PI
```

The clamped hour-angle expression handles polar day/night deterministically. Normalize insolation at each latitude against its annual minimum/maximum into seasonal intensity `[-1, 1]`; use zero when the span is numerically degenerate.

Warming/cooling tendency is the centered derivative of the same normalized forcing at fixed phase offsets of `1/1024` orbit. Positive means warming; negative means cooling. The derivative, not a random choice, distinguishes Spring from Autumn and naturally reverses between hemispheres.

## Local climate support

Support fields are immutable generation diagnostics, never saved tile authority. All noise wavelengths are physical kilometres and use the existing deterministic campaign noise foundation. Whole-globe X sampling is periodic.

### Maritime response

Let exact Euclidean terrain distances be `dSea`, `dLake`, and `dRiver` in kilometres; missing sources are positive infinity.

```text
maritime = clamp01(
    0.70 * exp(-dSea  / 650)
  + 0.25 * exp(-dLake / 180))

localPhaseLag = 0.08 * maritime        // orbit fraction
localAmplitudeScale = 1 - 0.55 * maritime
```

Re-evaluate seasonal intensity and tendency at `phase01 - localPhaseLag`. This gives oceans and large Lakes a delayed, reduced seasonal response while preserving one global phase. River proximity affects moisture but does not receive ocean-scale thermal inertia.

### Temperature

Recommended Earth-like defaults are:

```text
latitudeMeanC = 30 - 0.42 * abs(latitudeDegrees)
continentalAmplitudeC = 2 + 20 * pow(abs(latitudeDegrees) / 90, 1.35)
heightAboveSeaKm = max(0, elevationMeters - seaLevelMeters) / 1000

temperatureC = latitudeMeanC
  + continentalAmplitudeC * localAmplitudeScale * seasonalIntensity
  - 6.5 * heightAboveSeaKm
  + 2.5 * regionalTemperatureNoise
```

`regionalTemperatureNoise` is normalized `[-1, 1]` coherent noise combining approximately `1,600 km`, `500 km`, and `160 km` wavelengths with fixed ordered weights. Advanced controls expose lapse rate, maritime strength/radius, phase lag, regional-noise strength, and wavelengths with bounded validation.

### Moisture and rain shadow

A latitude prior avoids uniformly wet coasts and uniformly dry interiors:

```text
a = abs(latitudeDegrees)
latitudeMoisture =
    0.42
  + 0.30 * exp(-pow(a / 16, 2))
  - 0.24 * exp(-pow((a - 28) / 10, 2))
  + 0.10 * exp(-pow((a - 55) / 16, 2))

moisture = clamp01(
    latitudeMoisture
  + 0.30 * exp(-dSea / 700)
  + 0.16 * exp(-dLake / 220)
  + 0.08 * exp(-dRiver / 80)
  - 0.24 * rainShadow
  + 0.10 * regionalMoistureNoise)
```

Prevailing wind uses Earth-like latitude cells: tropical and polar easterlies, mid-latitude westerlies, with a bounded seed-stable directional perturbation. `rainShadow` is normalized `[0,1]` from upwind relief over a bounded physical-kilometre fetch; it is not inferred from tile count. Moisture noise uses separate stable seed streams and physical wavelengths so temperature and moisture do not become the same field.

Sea/Lake/River distance fields should reuse the existing exact Euclidean campaign distance-field implementation through an immutable season-terrain snapshot. Support construction is `O(tileCount)` apart from bounded noise/wind samples.

## Deterministic generation contract

Suggested engine-neutral APIs:

```csharp
CampaignSeasonGenerationSource Capture(
    ICampaignSeasonTerrainQuery terrain,
    CampaignSeasonMap seasons,
    CancellationToken cancellationToken = default);

CampaignSeasonGenerationResult Generate(
    CampaignSeasonGenerationSource source,
    CampaignSeasonCatalog catalog,
    CampaignSeasonGenerationSettings settings,
    CampaignSeasonGenerationScope scope,
    CancellationToken cancellationToken = default);
```

Capture runs on the owner thread and copies:

- value-equal world definition;
- normalized row-major terrain/elevation/water samples;
- current row-major season IDs and lock bits;
- terrain and season revisions;
- immutable catalog/settings/scope.

It verifies source revisions before and after enumeration. Worker generation reads only this immutable source. It computes support fields once, then evaluates enabled rules in explicit priority order for each unlocked in-scope tile, breaking at the first match. Out-of-scope tiles and all locks copy exactly. Results are deterministic row-major values and reports; the source is never mutated.

Acceptance requires:

- unchanged live terrain and season revisions;
- value-equal world definition;
- identical immutable catalog identity;
- candidate revision unchanged since generation;
- candidate settings/scope fingerprint matching the visible controls;
- no unresolved changed-lattice lock conflict.

The current limit is `250,000` tiles and `256` enabled rules. Worst-case rule checks are bounded to `64,000,000`, but normal evaluation exits at the first match. Keep one dense output, packed locks, shared support arrays, and one bounded diagnostic accumulator; never retain one full suitability array per definition. Check cancellation at support rows, distance passes, rule rows, and remap groups.

## Regeneration scope and locks

Season regeneration scope is either:

- all logical tiles; or
- one clipped inclusive rectangular tile area selected on the shared canvas.

There is no definition-only generation scope because all enabled definitions compete through one priority chain. Selecting a definition in the preview is display/report filtering only and must not stale the candidate.

Same-lattice generation copies every lock exactly and replaces only unlocked tiles inside scope. Manual painting defaults **Lock manual edits** on, but the author may paint unlocked values intentionally.

### Changed lattice

For each locked old tile, intersect its physical campaign rectangle with candidate tile rectangles. Choose the candidate tile with greatest overlap area. Report old coordinate/ID, new coordinate, overlap percentage, and out-of-bounds area.

- a unique greatest overlap preserves the locked ID;
- several old locks with the same ID may merge into one locked tile;
- different IDs claiming one tile choose the strictly greater overlap;
- an equal greatest-overlap tie between different IDs is unresolved and blocks acceptance;
- an old locked rectangle with no candidate overlap is reported dropped and blocks acceptance until the author explicitly permits the drop or changes the world definition;
- unlocked candidate tiles are generated from the reviewed candidate terrain.

Current, remapped-lock, and generated-unlocked data are composed in private candidate state. Acceptance installs terrain and seasons together only after the report is current and conflicts are resolved.

## Editor workflow

### Seasons workspace

Add **Seasons** beside **Terrain** and **Resources** on the existing shared `WorldCanvas`. Preserve pan, zoom, campaign grid, hover coordinate, right-click pin, and Windows 98 visual language.

The rail contains:

- searchable Season Definition selector with built-in/custom and generated/manual-only state;
- **Paint selected**, **Reset to default**, **Lock**, and **Unlock** tools;
- complete-tile Paint Area `1 x 1` through `25 x 25`;
- default-on **Lock manual edits**;
- **Elevation numbers**, **Season labels**, and **Blend boundaries** display toggles;
- **Manage seasons...** and **Generate seasons...** commands.

One drag is one already-applied shared-history command. Escape restores the stroke. Reset writes `DefaultSeasonId`; it never creates an absent tile. Painting and lock changes never modify terrain or resources.

The pinned inspector shows stable ID/name, built-in/custom/fallback, lock state, generated support values, winning rule, higher-priority overlaps, terrain/elevation/water distances, and generation-stale status. Support diagnostics are recomputed/cached projections and are not saved authority.

At readable zoom, labels show the definition's configured short label or name abbreviation with high-contrast outline. Full-tile categorical composition is default. Blend Boundaries samples neighboring presentation colors only and never changes cell identity or export.

### Definition manager

Reuse the validated custom-resource management pattern:

- list entries with explicit Up/Down controls rather than drag-only ordering;
- add custom, duplicate built-in/custom, edit, delete with replacement;
- stable ID editable only before first apply;
- built-in identity protected while rule, tint, effect, and priority remain project-editable;
- new custom entries start manual-paint-only;
- enabling requires valid rule and capacity below `256`;
- final enabled row is visibly labelled **Catch-all**;
- applying a catalog/priority replacement is atomic and clears history only when authority actually changes.

### Generation dialog

Use a modal Windows 98 property-workshop dialog:

- left: scope, seed/derive/randomize, coverage, regional centre latitude, axial tilt, priority summary, and collapsed Advanced climate controls;
- right: synchronized **Current - unchanged** and **Candidate - not applied** canvases;
- report: per-definition Current/Candidate count and percentage, environmental matches, shadowed matches, locks, changed tiles, zero reason, and warnings;
- commands: **Generate**, **Cancel**, and disabled-until-current **Use seasons**.

Changing an in-dialog generation input or scope leaves the previous preview visible with **Previous result - settings or source changed** and disables acceptance. Display filters, selected report row, Current/Candidate tab, pan, zoom, grid, labels, and Blend Boundaries do not stale it. Catalog/priority changes require closing the modal and therefore discard its Candidate; unexpected source drift is rejected before acceptance. Generation runs away from the UI thread with cancellation; closing never applies partial output.

For narrow windows, use one Current/Candidate tab switch over the same two canvas instances rather than duplicating state.

### New world

Extend new-world preview into a Terrain-and-Season Candidate:

1. terrain generation completes in private candidate state;
2. season source is built from that terrain and an initialized default layer;
3. season generation completes;
4. preview reports terrain plus season distribution;
5. **Use this world** accepts the exact terrain and Season Layer atomically.

If either stage fails or is cancelled, neither authority changes. Blank/manual creation shows **Default tile season**, initially Spring, and creates the complete layer directly without inventing generation settings. Resource generation is not part of this transaction.

## Persistence target

Use three managed authoring sidecars:

```text
season-definitions.json
season-generation.json       optional; only after accepted generation
season-layer.bin
```

`season-definitions.json` version 1 contains the complete canonical catalog order, stable definitions, built-in/custom flag, built-in fallback, appearance, rules, enabled priority, and `defaultSeasonId`. Persisting full project values prevents future application-default changes from rewriting an existing project's meaning.

`season-generation.json` version 1 contains seed/link state, coverage mode, nullable regional centre, axial tilt, Advanced controls, and stable source/input fingerprints used only to report generation staleness. Missing settings mean no procedural recipe; loaders do not invent one.

`season-layer.bin` version 1 is little-endian:

```text
8 bytes   ASCII magic "KWSEASON"
2 bytes   version = 1
2 bytes   recordStride = 3
4 bytes   widthTiles
4 bytes   heightTiles
4 bytes   tileCount
32 bytes  SHA-256 of canonical ordered catalog IDs
tileCount records:
  2 bytes unsigned catalog index
  1 byte  flags (bit 0 = locked; bits 1..7 = zero)
```

Readers require exact length, valid dimensions/tile count, known version/stride, matching catalog fingerprint, in-range indexes, and zero reserved bits. They reject corruption before replacing the visible document. Revisions are process-local and never serialized.

An older project with no season files loads as built-in catalog defaults, `DefaultSeasonId = spring`, a complete implicit Spring/unlocked map, and null generation settings. This compatibility projection is clean until edited. The first save from the Seasons-capable editor writes the complete season sidecars through the project-level staged coordinator.

Save captures terrain/resource/season/catalog revisions, serializes all authorities into one sibling staging directory, reload-validates the staged project, and commits the complete managed set with rollback on ordinary I/O failure. `MarkSaved` runs only after every authority commits.

## Runtime package version 3

Keep every version-2 terrain/resource entry byte-compatible and add:

```text
season-tiles.bin             tileCount x 2-byte catalog index
```

The version-3 manifest adds:

- season record width and row-major coordinate contract;
- SHA-256 and exact uncompressed length;
- ordered season catalog with index, stable ID, name, built-in/custom flag, built-in fallback, and portable appearance metadata;
- no locks, rules, support fields, settings, diagnostics, or preview data.

Export captures and rechecks terrain, resource, and season revisions, writes fixed ZIP entry order/timestamps, streams bounded buffers, validates all catalog indexes, and atomically replaces the destination only after the final cancellation/revision gate. Equal authoritative inputs produce byte-identical packages.

## Implementation slices

Ship only complete safe boundaries; do not expose partial saved authority.

### Slice 1 - engine-neutral domain — implemented 2026-08-17

- definitions, rule ranges, catalog, settings, coverage, scope;
- dense map and locks;
- validated atomic mutations and delta commands;
- stable seed/fingerprint helpers;
- no editor, files, generator, or export.

Implementation: `Kingdom.World.Core.Campaign.Seasons` plus shared-history season commands. Verification: `31/31` focused season tests, `464/464` full Release tests, zero-warning Release build, and clean formatting.

### Slice 2 - immutable terrain query and generator — implemented 2026-08-17

- version-2 and version-3 season terrain adapters;
- immutable owner-thread snapshot;
- exact water distance fields;
- coverage, solar, temperature, moisture, wind/rain-shadow support;
- first-match generator, reports, cancellation, deterministic tests;
- no running editor entry point yet.

Implementation: owner-thread version-2/version-3 adapters normalize terrain, elevation, and Sea/Lake/River sources into `CampaignSeasonGenerationSource`; generation consumes only its immutable row-major snapshot. `CampaignSeasonSupportFields` implements tile-centre Whole-globe/Regional latitude, one stable orbital phase, axial-tilt insolation and tendency, exact Euclidean water distances, maritime phase/amplitude response, physical elevation lapse, separate periodic temperature/moisture noise, latitude-cell wind, and bounded physical-fetch rain shadow. `CampaignSeasonGenerator` applies the explicit priority, treats the final entry as catch-all, preserves locks and out-of-scope values exactly, reports environmental matches/shadowing/lock overrides/manual-only and zero results, and returns a revision-guarded candidate without mutating source authority.

Verification: `56/56` focused season tests and `489/489` full Release tests pass. Coverage includes both terrain versions, revision-drift capture, value-derived fallback seed, physical water distances, hemispheric and zero-tilt forcing, exact lapse rate, River-only moisture, maritime decay, periodic longitude noise, physical-scale consistency, rain shadow, default four-season bands, first-match/custom catch-all, truthful zero/shadow reports, rectangular scope, locks, catalog identity, cancellation, deterministic replay, stale candidates, and the representative `140 x 140 = 19,600` grid. Release build remains zero-warning and formatting remains clean.

### Slice 3 - persistence and runtime v3 — implemented 2026-08-17

- strict three-sidecar serializer;
- project coordinator atomic save/open;
- missing-sidecar Spring compatibility;
- deterministic runtime package version 3;
- corruption, cancellation, revision-race, and byte-level tests.

Implementation: `CampaignSeasonProjectSerializer` writes and strictly reads the complete canonical catalog/priority/default, optional accepted recipe/fingerprints, and dense `KWSEASON` tile/lock stream. Missing all sidecars creates the clean Spring compatibility projection; partial authority is rejected. `CampaignEditorProjectSerializer` exposes explicit season-aware load/save/export boundaries that stage and revision-gate terrain, resources, and seasons as one nine-file rollback set while its old six-file overloads preserve season files they do not own. `CampaignWorldRuntimeExporter` version 3 reuses the exact version-2 binary writers, adds canonical row-major `season-tiles.bin`, and exports only stable runtime identity/fallback/appearance metadata.

Verification: focused `CampaignSeason*` tests pass `107/107` and full Release tests pass `540/540`. Coverage includes exact catalog/rule/priority/climate/lock round trips, deterministic bytes, missing/partial compatibility, strict duplicate/unknown/null/version/canonical-order rejection, every binary header/length/index/reserved-bit corruption class, stale optional recipe cleanup, legacy-import isolation, nine-file rollback, season revision-race rejection, old-overload season preservation, runtime season SHA/layout/catalog mapping, lock omission, v2 stream byte compatibility, deterministic replay, definition mismatch, and cancellation cleanup. Release build remains zero-warning and formatting remains clean.

### Slice 4 - complete manual Seasons workspace — implemented 2026-08-17

- workspace selector, rail, painting/reset/locks, shared history;
- bounded visible raster and labels;
- pinned diagnostics;
- save/reopen/export available in the same release;
- custom definition manager with protected replacement.

Implementation: `EditorViewModel` now owns the complete Season document tuple and one shared terrain/resource/season history. `WorldCanvas` routes clipped complete-cell Paint/Reset/Lock/Unlock strokes, renders one independently revision-keyed bounded visible Season raster, and draws exact abbreviations/lock labels at readable zoom. `CustomSeasonsDialog` edits detached built-in/custom definitions and explicit first-match priority, keeps existing stable IDs immutable, labels the final enabled row Catch-all, and requires an explicit replacement for tile/default/priority references. The running Main Window loads/saves all three authorities through the nine-file coordinator and exports runtime package version 3. Same-lattice world replacement preserves exact Season authority; changed-lattice replacement is blocked whenever it would discard assignments, locks, or a saved recipe.

Verification: full Release verification passes `556/556`, including focused manager conversion/replacement, canvas tool routing and clipping, shared LIFO history, exact open state, no-op catalog application, pinned locks, and changed-lattice rejection. Release build and format verification are clean. The authority-first pinned inspector reports exact ID/fallback/lock, terrain/elevation, retained rule summary, and whether an accepted recipe exists. Generated support values, winning/shadowed-rule details, and source/input staleness required the immutable support/candidate projection and were therefore deferred to Slice 5 rather than approximated on the UI thread.

### Slice 5 - preview-first generation — implemented 2026-08-17

- Current/Candidate dialog and synchronized viewports;
- stale state machine, reports, Advanced controls, cancellation;
- exact candidate acceptance and history boundary;
- rectangle scope and lock preservation.
- cached generated support/fingerprints plus current-rule winning, higher-priority overlap, and source/input staleness diagnostics for pinned tiles.

Implementation: `SeasonGenerationDialog` presents unchanged Current authority and an unapplied Candidate in synchronized read-only `WorldCanvas` instances. Its owner-thread capture/background-generation state machine retains stale previews, separates generation inputs from display-only controls, supports All/Rectangle scope, exposes the complete accepted seed/coverage/axial-tilt/Advanced-climate settings, and never applies on cancel, close, failure, or stale source. `EditorViewModel.AcceptSeasonGeneration` revalidates source revisions, value-equal definition, exact catalog/priority identity, Candidate revision, settings, and scope before one exact candidate swap, accepted-recipe install, shared-history clear, dirty transition, and diagnostic-cache refresh. Canonical fingerprints distinguish authoritative source and generation inputs from presentation-only catalog changes. The pinned inspector reads cached immutable support/fingerprints and evaluates the current catalog rules against the active accepted priority to derive winning/shadowed rules, higher-priority overlaps, authority agreement, and source/input staleness.

Verification: focused Slice 5 tests pass `17/17` and full Release verification passes `573/573`. Coverage includes fingerprint stability/change boundaries, first-match shadowing, rectangular selection normalization/clipping, deterministic seed resolution, exact candidate/settings install, terrain/resource/project/import preservation, history clearing, every stale/mismatch rejection path, diagnostic rebuild after reopen, and busy gating. Release build remains zero-warning; format and diff checks are clean. Native normal/narrow visual and keyboard acceptance is deliberately retained for Slice 7.

### Slice 6 - new-world and changed-lattice integration — implemented 2026-08-17

- atomic Terrain-and-Season Candidate;
- blank Default tile season;
- physical-overlap lock remap and conflict resolution;
- combined source/candidate stale validation.

Implementation: `NewWorldDialog` now treats procedurally generated terrain and its complete generated Season Layer as one private candidate. It derives the initial Season Seed from the terrain seed, offers separate Terrain/Seasons preview views, reports observed Season distribution, and returns the exact dense map/priority/recipe/support tuple. Blank creation instead initializes every cell from the explicit **Default tile season** and stores no generation recipe. `EditorViewModel.CreateWorld` validates the tuple before replacing document authority, so cancellation or either-stage failure cannot leave a terrain-only world.

`CampaignSeasonWorldRegenerator` captures source terrain/Season revisions, exact catalog/default/priority identity, saved recipe, and row-major authority. Same-lattice replacement reconstructs every ID and lock exactly. Changed-lattice replacement intersects locked source rectangles with target cells in physical metres, chooses greatest area, merges equal same-ID claims, gives a strictly larger different-ID claim authority, and preserves equal different-ID maxima as explicit conflicts. No-overlap locked sources remain explicit drops. An unresolved target uses a non-authoritative dense-map placeholder only internally; the preview renders it magenta and excludes it from observed distribution percentages, so no claimant appears to win before review. `SeasonLockResolutionDialog` requires one winner for every conflict and a separate affirmative permit for drops; **Use this world** remains disabled while either blocker exists. Unlocked targets are generated only after remapped locks are composed with the reviewed terrain. Final ViewModel acceptance revalidates source and Candidate revisions, definition, exact catalog/default, priority, recipe identity, and report readiness before atomically installing terrain, resources, and seasons and clearing shared history.

Verification: `9/9` focused core remap/new-world tests plus `4/4` new ViewModel atomicity tests pass; the combined focused set passes `25/25`. Full Release verification passes `586/586`, with a zero-warning Release build and clean formatting. Native normal/narrow visual and keyboard acceptance remains deliberately assigned to Slice 7.

### Slice 7 - product verification and documentation — implemented 2026-08-17

- native Windows acceptance pass at normal and narrow sizes;
- maximum-grid diagnostic;
- user guide, architecture, file format, runtime reference, verification log;
- self-contained publish and bounded startup smoke;
- mark ADR status Implemented only after all required gates pass.

Implementation: native dialog behavior is exercised in-process through Avalonia Headless at normal and narrow sizes, including bounds, accessibility names, validation recovery, stale Candidate retention, Current/Candidate switching, keyboard traversal, and dynamic Generate/Use default actions. Shared canvas render resources were made immutable after the full suite exposed cross-UI-thread ownership. The maximum-grid diagnostic runs `250,000` tiles against all `256` enabled first-match definitions without retaining a dense per-definition grid. The verified self-contained `win-x64` executable and root launcher now form the publication boundary.

Verification: `588/588` Release tests pass. The maximum-grid run completed in `0.814 s` with `39.9 MiB` measured current-thread allocation, under broad regression ceilings of `60 s` and `768 MiB`. Release build has zero warnings/errors; format and diff checks are clean. The published executable is `artifacts/publish/seasons/World.Editor.exe`, `103,028,109` bytes, SHA-256 `3AD6572E208497C23533F711E2B317FDE0A12271A8497AEC785D7DC5F846397A`; hidden startup reached a native main-window handle before only that process was stopped. ADR-0030 is therefore Implemented.

## Automated verification matrix

### Domain

- fixed built-in identities and protected removal;
- custom ID/name/color/fallback/range validation;
- `65,535` catalog and `256` enabled boundaries;
- complete dense map, bounds, deterministic enumeration, no-op revision;
- atomic batches, shared history interleaving, empty-command Redo preservation;
- referenced custom deletion/replacement/default migration.

### Geography and formulas

- Whole-globe north/south inversion and periodic X seam;
- Regional exact kilometre-to-latitude mapping and pole-cross rejection;
- same seed exact support fields/candidate/report; different seed changes phase;
- zero tilt removes hemispheric seasonal amplitude while retaining latitude climate;
- altitude delta equals configured lapse rate;
- Sea/Lake moderation and lag decrease with exact distance;
- River affects moisture without ocean-scale thermal lag;
- Spring positive tendency and Autumn negative tendency;
- equatorial support remains weakly seasonal and moisture-sensitive;
- physical-scale comparison at equivalent kilometre positions for `5 km` and `20 km` tiles;
- deterministic latitude-cell winds, rain shadow, and separate noise streams.

### Rules and generation

- first match, editable priority, custom final catch-all;
- default Winter/Spring/Autumn/Summer outcomes;
- no forced placement and truthful zero/shadow reasons;
- empty Include, whitelist Include, Exclude precedence, custom-base inheritance;
- manual-only definitions never generate but remain paintable;
- all/rectangle scope, out-of-scope exact preservation, lock exact preservation;
- source/candidate immutability, cancellation, source revision drift;
- stable results independent of dictionary/catalog insertion order;
- `250,000 x 256` bounded-memory diagnostic without per-definition dense arrays.

### Persistence/export

- older missing-sidecar project -> clean complete Spring layer;
- definitions/settings/binary exact round trip;
- strict duplicate/unknown/property/version/length/index/reserved-bit rejection;
- staged save rollback and stale optional-file cleanup;
- runtime v2 stream byte compatibility inside v3;
- exact runtime season index/catalog mapping and SHA validation;
- equal worlds byte-identical; cancellation/revision change preserves destination.

### Editor

- full-cell paint/reset, area clipping, one drag/one command, Escape rollback;
- lock/unlock and terrain/resource non-mutation;
- candidate stale/display-only controls distinction;
- cancel during generation leaves current map/history/settings untouched;
- acceptance installs exact candidate, settings, dirty state, project identity, and clears history;
- custom manager safe defaults, validation, replacement, no-op apply;
- new-world atomic success/failure;
- changed-lattice unique overlap, same-ID merge, unequal different-ID winner, equal-overlap different-ID conflict block, and out-of-bounds report;
- bounded raster invalidation independent of terrain/resource raster caches;
- keyboard traversal, focus visibility, readable labels, and narrow Current/Candidate switch.

## Native acceptance journey

1. Open an older terrain/resource project and confirm every tile initially reads Spring without changing dirty state.
2. Switch to Seasons, paint Winter over a multi-tile Paint Area, confirm complete-cell fill, default lock, shared Undo/Redo, and unchanged terrain/resources.
3. Add Monsoon, confirm it starts manual-only, set Wet/Summer fallback, configure moisture/terrain ranges, enable it, and move it above built-ins.
4. Generate Whole globe twice with the same seed and confirm identical Current/Candidate maps, opposite hemispheres, coherent regions, alpine Winter, and no per-cell speckle.
5. Change only a display filter/pan/zoom and confirm acceptance stays enabled; change an in-dialog climate input or scope and confirm the old preview remains visible but stale. Close, change a rule, and confirm the closed dialog Candidate is not retained and the accepted input fingerprint reports stale after reopening.
6. Accept, save, reopen, and confirm exact IDs, locks, catalog, settings, and distribution.
7. Edit terrain/elevation and confirm Tile Seasons remain unchanged while generation inputs show stale.
8. Generate a rectangle and confirm outside/locked cells remain exact.
9. Change tile size, inspect overlap remaps, force an equal lock conflict, and confirm acceptance stays blocked until resolved.
10. Export runtime version 3 and verify a small importer maps each dense index to the expected stable ID/fallback.

## Deferred extensions

- calendar-driven season transitions and gameplay time;
- persistent climate/weather authority;
- one-way season inputs to a future resource regeneration revision;
- engine-specific materials, particles, snow depth, foliage swaps, and audio;
- spherical campaign topology or wrapped pathfinding;
- resource, settlement, road, or economy changes caused by a season.

These require separate decisions because each changes authority or dependency direction; none is implied by Tile Season.
