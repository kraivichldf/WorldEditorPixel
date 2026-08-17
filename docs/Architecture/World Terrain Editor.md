# World Terrain Editor

## Core problem

The designer thinks in campaign tiles: one complete cell has one terrain classification and a controllable height. The previous system made type a tile overlay but kept height in a much denser sample brush, so the visible cell and the editable authority did not match.

Version 2 makes the campaign tile authoritative. Each tile stores a type and a height at its centre. The continuous surface between centres is deterministic derived data. Sparse campaign resources and the complete static Tile Season layer are peer authorities keyed to the same exact tile coordinates, never encodings inside terrain or height.

## System boundary

```text
World.Editor (Avalonia desktop shell)
        |
        v
World.Core (campaign world, interpolation, commands, validation, files)
        |
        v
Version-2 terrain project folder
  + optional sparse resource sidecars
  + complete season definition/layer sidecars
```

`World.Core` has no UI or engine dependency. A future importer implements [[../Reference/World File Format|the file and interpolation contract]] rather than linking editor code.

## Authoritative model

`CampaignWorldDefinition` owns:

- world width and height in metres;
- campaign tile edge size in metres;
- exact derived `TilesX`, `TilesY`, and total tile count;
- sea level, minimum and maximum height, and default tile height in whole metres.

World dimensions must divide exactly by tile size. There are no partial edge cells. For example, `700 km / 5 km = 140` tiles per axis and `19,600` total tiles.

`CampaignTileMap` owns a sparse `CampaignTileData` value at integer `(x, y)`. The value contains:

- `Type`: Unassigned, Plains, Steppe, Desert, Forest, Hills, Mountain, Sea, Lake, River, Large River, River Junction, Beach, or Cliff. Reserved legacy Coastal may be read but cannot enter the active map;
- `HeightMeters`: signed `Int16` height at the tile centre.
- `CustomTerrainId`: optional identifier for a named, colored safe-land variant.

An absent entry equals `Unassigned` at `DefaultTileHeightMeters`. A tile with only a non-default height is still materialized even when its type is Unassigned.

`CampaignResourceMap` owns zero or more occurrences at each valid campaign coordinate. One tile may contain several different stable resource IDs but at most one occurrence of a given ID. Each occurrence stores exact resource-relative potential `1..100` and an authoring lock. The map stays sparse, validates definition equality with the world, enumerates deterministically, and exposes a bounded area query for visible rendering/stamps. `CampaignResourceCatalog` reconstructs built-ins and carries immutable custom definitions; optional generation settings are retained independently from current occurrences.

`CampaignSeasonMap` owns exactly one stable Season Definition ID and one lock flag for every logical tile, including water and Unassigned cells. Its dense row-major authority is complete: absence has no meaning. Spring, Summer, Autumn, and Winter are protected built-ins; custom definitions carry a portable built-in fallback, appearance, and environmental rule. `DefaultSeasonId` drives blank-world initialization and Reset. An explicit ordered priority supplies first-match generation, with the final enabled row interpreted as the catch-all. In memory locks are stored in a compact bitset; the persisted `KWSEASON` sidecar stores one lock flag byte per tile. The accepted generation recipe is optional metadata rather than tile provenance.

`CustomSeasonsDialog` edits detached catalog/priority drafts. Existing stable IDs and built-in identity are protected; new custom drafts remain manual-only until explicitly enabled. A referenced custom deletion requires an explicit replacement for tile/default/priority references and preserves tile lock flags. `EditorViewModel.UpdateSeasons` validates and constructs the complete replacement map before one atomic swap, clears shared history only for an effective authority change, and leaves an equivalent apply as a no-op.

`CustomResourcesDialog` edits detached definition drafts rather than the live catalog. It can create a `0%` manual-only definition or duplicate a built-in, exposes every bounded definition/rule field, and restricts preferred, avoided, and weighted suitability choices to `CampaignResourceSupportFieldIds`. Preferred and avoided factors are symmetric soft influences. Assigned normalized base surfaces may separately be hard-excluded; medium/range/custom-terrain rules remain hard constraints too. `EditorViewModel.UpdateCustomResources` is the atomic catalog-replacement boundary: it counts requested usage IDs in one sparse pass, locks used identity/category, filters only deleted-ID generation overrides, copies compatible occurrences into a validated replacement map, and swaps map/settings only after the complete candidate succeeds. A real replacement clears history because old commands reference the prior immutable map/catalog; an equivalent set is a no-op.

`CampaignCustomTerrainDefinition` is deliberately not a new water, shore, or River enum. A definition has a stable ID, designer name, `#RRGGBB` display color, optional terrain-mix share, and exactly one safe `BaseType`: Plains, Steppe, Desert, Forest, Hills, or Mountain. Its base is the stored portable type and material fallback, not the allocation parent for its share. `CampaignTileMap` validates that a referenced custom ID exists and that the stored base type matches it. Definitions cannot be removed or have their base changed while tiles use them. Older readers that ignore the optional field still see the stored portable base type.

## Optional deterministic world creation

New World can remain Blank or call the engine-neutral `CampaignMapGenerator`. The generator consumes the validated definition plus preset, seed, terrain style, Mountain-system profile, hydrology amount, directional coastline style, tidal-inlet amount, optional `CampaignMapLandMix`, and optional safe custom terrain definitions. For a generated preset, the dialog builds a temporary `CampaignWorld`, applies the complete ordered `CampaignTileEntry` list once through `CampaignTileMap.SetTiles`, then builds an initialized dense Season source over that terrain and runs `CampaignSeasonWorldRegenerator.GenerateNewWorld`. Terrain and Season rasters are separate bounded preview views; the impact well reports terrain counts and observed Season distribution. Input changes invalidate acceptance while retaining the old Candidate for comparison. **Use this world** passes the exact preview world, Season map, priority, saved recipe, and support fields to `EditorViewModel`; the main window does not regenerate either layer. The view model validates the complete tuple, clears history, marks the document unsaved, and enables ordinary painting. Blank bypasses procedural preview, creates its untouched terrain directly, and fills every Season cell from the explicit **Default tile season** without inventing saved generation settings.

The generator combines analytic signed-distance masks with seeded multi-scale fields. Island, Archipelago, and Sea-in-center use domain-warped fBm. **Continental world** builds five seed-varied unequal cratonic masses from oriented core/branch/peninsula lobes, subtracts regional bays, places two minor island arcs, and bends the result with physical-kilometre simplex fields. Sparse ocean anchors allow generated land or Sea at most map edges; those planar edges do not wrap. Directional Coast shapes canonicalize the named orientation into coast-normal and coast-tangent coordinates, then compose deterministic kilometre-scaled geography. **Flowing bays and capes** uses a seeded, mirrored sequence of broad Gaussian headland/bay lobes as the mainland threshold and unions it with a sampled cubic-Bézier cape whose radius tapers from mainland root to pointed tip; that authored skeleton remains deliberate at large scale and requires no offshore component. Smooth/Natural/Rugged use the same regional skeleton on compact worlds but fade it out between `1,400 km` and `4,200 km`; a slower shelf field, amplified two-dimensional nearshore field, distributed macro landmark regions, and scaled sparse island arcs replace it at continental extents. Natural and Rugged still rotate through a major gulf, hooked cape, barrier-island sound, and offshore-island strait without stamping one enlarged form down the whole coast. Physical-kilometre evaluation preserves aspect on non-square worlds. The selected coast character controls amplitudes, feature density, and physical scale. A seeded broad shelf retreat may open one along-coast boundary; its span clamp remains valid through the `10,000 km` maximum. The forced mask preserves only the named Sea edge, so all other boundaries may contain generated land, connected Sea, or both. It first resolves the preset ocean; optional tidal inlets then test separated lowland shoreline opportunities and route only suitable, bounded, sea-connected drowned valleys before ocean resolution is repeated. A full Sea tile means a broad campaign-scale estuary/channel, not a narrow constructed canal.

Relief is driven by a transient, physical-kilometre Voronoi province model. Seeded province motion separates convergent uplift, divergent rifts, and shear belts. Geological macro, detail, warp, and ridge fields use kilometre-scaled simplex gradient noise instead of normalized-map value noise. Each stable province pair supplies a canonical boundary tangent; anisotropic ridged noise has longer correlation along that tangent and shorter correlation across it, so ranges follow coherent geological arcs without requiring a persistent plate layer. Terrain style scales uplift and selects a bounded erosion profile. Mass-conserving thermal relaxation softens slopes above the style's talus threshold, and one stream-power pass follows a Priority-Flood receiver graph to carve drainage-aligned valleys with bounded downstream deposition. This erosion runs before final Lake and River placement. One stable suitability field feeds Sparse, Balanced, and Dense retention targets; independently grown systems admit only a candidate attached to one current ridge endpoint, preventing loops, mutual seed obstruction, and quota-driven interior fill. Nearby suitable land receives priority as Hills/foothills. The result remains capped at 12% of eligible inland tiles. A separate broad aridity field selects localized dry interior lowlands as Desert and semi-arid transition regions as Steppe; remaining lowlands use deterministic moisture for Forest or Plains.

Four-neighbour priority-flood drainage identifies depression basins and supplies an acyclic receiver graph. Lakes occupy accepted basins; flow accumulation selects River headwaters and routes them to Sea, Lake, or an already accepted downstream route. A candidate may remain a separate basin route or merge through an exactly-three-exit River Junction when its tributary prefix is long enough and no lateral contact or four-way crossing is introduced. Qualifying routes at least 100 km long may widen their accumulated downstream 30–80 km to Large River. Water-facing land retains its base/custom classification unless water-facing grade reaches `0.06`, in which case it becomes Cliff; the 10% coast is derived later by adjacency.

When a custom `CampaignMapLandMix` is present, its six whole percentages plus every positive custom terrain share must total 100%; Mountain is limited to 12%. The mix applies to eligible land after Sea, Lake, River, and steep water-facing Cliff are excluded. Gentle water-facing land remains eligible because automatic coast is not a terrain category. One stable largest-remainder pass converts all default and custom percentages into integer targets that sum to the exact eligible pool. Mountain keeps connected-range candidate selection, Desert keeps strict dry-lowland eligibility, Steppe keeps broader semi-arid lowland eligibility, Hills and Forest are ranked by deterministic relief and moisture scores, and Plains receives the requested remainder plus any constrained Mountain/Desert/Steppe shortfall. A null mix follows the normal classifier; positive custom shares require a mix.

Each custom definition with a positive share is its own land category. Its safe base is stored as `Type` for portability and selects the material foundation, but never limits its target count to a matching generated base. After ordinary Mountain targets are reserved, seeded low-frequency fBm ranks the remaining eligible land so custom types form coherent regions rather than per-cell noise. A `0%` definition remains paint-only. Custom placement never changes height, Sea/Lake/River topology, steep Cliff classification, or tidal-inlet connectivity; custom identity can reach gentle water-facing land and remains visible inside the automatic coast.

The dialog preview is transient presentation state only: it is not saved, added to history, or retained as an authority. There is no generated document mode, retained seed authority, or secondary surface after acceptance. Save data remains version 2 because the generated type and centre-height values are the terrain. Exact formulas and preset boundary guarantees are in [[../Reference/Campaign World Generation|Campaign World Generation]]; the general decision is [[../Decisions/ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]], Continental-world refinement is [[../Decisions/ADR-0023 - Hierarchical Continental World Generation|ADR-0023]], and large directional-coast scaling is [[../Decisions/ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024]].

### Preview-first regeneration

An open document can call the same dialog in regeneration mode. The current `CampaignWorldDefinition` initializes the dimension, tile-size, sea/default-height, and height-limit controls, but all seven values remain editable. Every valid definition therefore creates its own temporary candidate grid; the exact-divisibility, generated minimum-size, and 250,000-tile limits remain authoritative. Blank is removed from the preset list. The dialog copies the current custom terrain catalog into its temporary generator inputs; changes made there affect only the candidate world until acceptance.

`EditorViewModel` retains a lightweight `CampaignMapGenerationOptions` recipe only for the active process session. It deliberately omits generated tile entries and does not serialize the recipe. A world accepted earlier in the same session can therefore reopen Regenerate with its last preset, seed, relief, hydrology, coastline, inlet, and land-mix values. A loaded project has no invented recipe, so the dialog states that generator defaults are being used while still loading its persisted custom types.

World regeneration captures terrain, resource, and Season source revisions before worker execution. Same-lattice Season candidates rebuild every dense ID and lock exactly. On a changed lattice, `CampaignSeasonWorldRegenerator` maps only locked source rectangles by greatest physical overlap, merges same-ID claims, reports a strictly smaller different-ID claim as displaced, and regenerates unlocked target cells from the reviewed terrain. Equal maxima from different IDs and locked rectangles with no target overlap remain acceptance blockers. `SeasonLockResolutionDialog` records explicit per-target winners and a separate locked-drop permit, then rebuilds only the Season half against the unchanged terrain Candidate. **Use this world** passes the exact temporary terrain/resource/Season tuple to `EditorViewModel.RegenerateWorld`; the view model rejects Blank and stale source/Candidate revisions, requires exact catalog/default/priority/recipe identity and a ready lock report, then swaps every authority together, clears history and pointer/pinned state, refreshes selectors, clamps the active stamp height, preserves project/import identity, and marks the document modified. Cancelling, closing, failed generation, or an unresolved lock decision leaves current authority and history untouched. See [[../Decisions/ADR-0015 - Preview-First Current World Regeneration|ADR-0015]] and [[../Decisions/ADR-0030 - Static Preview-First Campaign Season Layer|ADR-0030]].

## Derived surface

Tile-space coordinates place cell boundaries at integers and centres at `(x + 0.5, y + 0.5)`. At a centre, the surface exactly equals that tile's stored height. Elsewhere the surface bilinearly interpolates the four surrounding centre heights. At world edges, centre indices clamp to the nearest outer tile, extending its height to the boundary.

This gives three useful invariants:

- changing a tile never introduces a height discontinuity at its border;
- neighboring tiles can have different stored centre heights and automatically form a slope;
- every importer can reproduce the same surface without an editor-only raster.

See [[../Decisions/ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]].

## Water and river topology

Sea and Lake are separate water classifications. Beach and Cliff are explicit full-tile classifications usable beside either water type. There is no current Coastal classification: every non-water tile derives an outer 10% band of matching Sea/Lake material on each cardinal water-facing edge and preserves its own built-in/custom material inside. With multiple water neighbours each edge receives a transition and a corner uses the nearest water-facing edge; without a cardinal water neighbour the complete tile keeps its original material. No sand is inserted unless Beach is the stored original type.

River connectivity is derived rather than serialized. `CampaignTileMap.GetRiverConnections` inspects the four cardinal neighbours and returns the N/E/S/W exits that contain River, Large River, or River Junction. A normal/Large segment has at most two exits. A persisted River Junction has at most three and marks an intentional Y; four-way crossings are always prohibited.

`CampaignRiverSplitBuilder` owns the only normal UI path that creates River Junction. It validates a pinned zero/one-neighbour root, resolves an outward cardinal direction, rotates a fixed collision-free template, and atomically applies one Y for two branches, two cascaded Ys for three, or three cascaded Ys for four. It rejects bounds overflow, Sea/Lake replacement, existing River overlap, and outside cardinal River contact before mutation. All new cells copy the root centre height, and leaf segments preserve its River/Large River class.

Single-tile edits, multi-tile commands, undo/redo, and file loading validate the hypothetical final map before applying it. An invalid change is rejected atomically. See [[../Decisions/ADR-0005 - Water and River Tile Topology|ADR-0005]].

`CampaignTileMap.GetAutomaticCoastSurfaceMaterial` is the deterministic `Original`/`Sea`/`Lake` query for a tile-local position. Coast bands are derived rather than serialized, just like River connections. See [[../Decisions/ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006]].

## Editing flow

The active stamp is one `CampaignTileData` value plus an optional bounded `CampaignTileArea` footprint. The left rail chooses its complete-tile type through one native selector; its popup retains the colored type name and description, while the selected description stays visible before painting. Custom land types appear in that same selector and carry their safe base type plus custom ID into the stamp. Expansion `r` selects a centred `(2r + 1) × (2r + 1)` square of complete campaign tiles, clipped at world bounds; the editor currently exposes `r = 0…12`. A left click writes that full footprint, and a drag writes it at every crossed coordinate, including coordinates interpolated between sparse pointer events. This is selection extent only: every covered tile receives the same authoritative type and centre height, with no sample brush, strength, or falloff. River painting deliberately fixes its footprint at `1 × 1` and uses a Manhattan path so even diagonal pointer motion yields a contiguous four-connected route. Invalid crossing coordinates are skipped and counted for user feedback. The pinned River Split action is a separate bounded atomic command because intentional branching cannot be expressed safely by an ordinary stamp.

`CampaignTileStampBuilder` mutates tiles immediately for feedback and retains the first before-value plus latest after-value per coordinate, deduplicating overlapping area footprints in the same stroke. Pointer release records one already-applied `CampaignTileStampCommand`. Undo restores both fields; redo reapplies both fields. `Escape` restores the stroke without adding history.

The pinned-tile elevation helper only prepares that same active stamp. **Copy centre** reads the pinned tile's exact stored height. **Blend around** computes

```text
suggested height = round-to-10m((sum of valid N/E/S/W centre heights) / neighbour count)
```

World-edge directions are omitted. A tile with no cardinal neighbour falls back to its own height before 10 m rounding. The result clamps to the world's allowed range. Reading either value does not mutate tiles, dirty the document, or create history; only a later stamp writes it.

The bounded history pattern is recorded in [[../Decisions/ADR-0002 - Delta-Based Terrain History|ADR-0002]].

Resources mode reuses the same pointer lifecycle through `CampaignResourceStrokeBuilder`. Add/update writes only the selected ID's potential/lock over a clipped `CampaignTileArea`; erase removes only that ID. Release records one already-applied `CampaignResourceEditCommand`, while Escape restores the stroke. `EditorViewModel` owns terrain, the compatible resource map/settings, the complete Season map/catalog/recipe tuple, and one `CommandHistory`, so terrain/resource/season commands interleave in exact LIFO order. Workspace/filter/zoom/pin changes remain view state and do not dirty the document.

Seasons mode uses the same lifecycle through `CampaignSeasonStrokeBuilder`. Paint writes the selected stable ID and the explicit manual-lock choice; Reset writes `DefaultSeasonId` unlocked; Lock/Unlock preserve the assigned ID. Every tool applies to the clipped complete-cell `CampaignTileArea`, one drag records one already-applied `CampaignSeasonEditCommand`, and Escape restores the stroke. Terrain, resources, Season filters, labels, boundary blending, workspace choice, zoom, and pin remain independent.

Custom-resource definition changes use a separate modal document boundary, not the stroke builder. Cancel retains the exact catalog, sparse map, settings, history, and dirty state. Apply preserves every occurrence whose stable ID remains valid, refreshes warnings and selectors, marks the document modified, and clears history with explicit UI/status copy. Used definitions keep their ID and Renewable/Finite category; deleting one first requires erasing all of its occurrences. See [[../Decisions/ADR-0022 - Custom Resource Definition Management|ADR-0022]].

## Rendering and inspection

`WorldCanvas` keeps pan and zoom as transient view state. Its cached visible raster is capped at 1,100 × 800 pixels and keyed by tile-map revision, viewport, view transform, and display mode. A large invalidated raster rents a BGRA work buffer, snapshots the visible tile region plus the two-tile interpolation/material halo into pooled dense storage, renders independent rows in parallel, copies them into a reusable `WriteableBitmap`, and returns both buffers to their pools. The snapshot preserves the canonical tile, slope, and automatic-coast formulas while avoiding repeated sparse-map probes for every screen pixel.

During a middle-button drag, the canvas translates the last complete raster and redraws coordinate-based overlays at the live view transform. Pointer movement therefore does not rebuild the procedural surface; releasing the drag produces one exact raster for the settled viewport. A same-size invalidation reuses the existing bitmap allocation.

Stored elevation display is a separate transient overlay rather than terrain pixels. When **Elevation numbers** is enabled and zoom reaches `28 px/tile`, the canvas enumerates only visible logical tiles, formats each signed Int16 centre height as an invariant whole-metre number, and draws high-contrast white text with a dark four-direction outline. Labels that cannot fit inside their tile are culled, repeated height/font pairs reuse cached `FormattedText`, and the cache is bounded. Toggling the overlay invalidates only the control render; it does not dirty the terrain bitmap, mutate the world, enter history, or change interpolation.

For each visible pixel inside the world, the canvas reads the containing tile's full-cell type, optional custom terrain identity, and derived interpolated height. It first resolves whether that position lies in an automatic Sea/Lake edge. Water wins only inside the outer 10%; otherwise the stored type selects material texture/base hue and a valid custom identity replaces only that hue, retaining its safe base texture. Stable world-space noise or wave functions create grass, dry Steppe grass, dune-and-stone, canopy, ridge, rock, water, and sand texture; texture strength fades to zero when the tile becomes too small on screen. Version-2 River uses a grass-textured presentation fallback because that format does not retain underlying land. Absolute height and the bilinear surface gradient jointly modulate brightness with bounded northwest hillshade. Height-only mode uses grayscale and deliberately bypasses material texture.

A narrow three-tone bank-and-water ribbon is drawn through visible River tiles from each centre to every derived cardinal exit, with a visual mouth added when an endpoint touches Sea or Lake. Large River uses the broad style; River Junction inherits that broad style when any cardinal neighbour is Large River. `CampaignTileMap` maintains a River-only coordinate index alongside sparse tile storage, so overlay frames enumerate Rivers rather than every generated tile. Ribbon width is screen-readable symbology rather than physical River width. The world border and optional grid render above terrain and rivers; optional elevation numbers render next, followed by the amber pinned outline and cyan/red stamp cursor. A prohibited River stamp uses a red crossed cursor.

The inspector deliberately separates:

- tile coordinate and type;
- stored height at that tile's centre;
- derived height at the exact pointer position;
- world position in kilometres.

Resources mode preserves those terrain facts and adds the exact selected-resource value. A separate cached resource raster mutes terrain, then overlays the selected definition's portable color on a fixed `1..100` scale. Exact potential labels appear at `28 px/tile`; the pinned inspector lists every occurrence with stable identity, category, potential, textual lock state, hard-rule warnings, and explicit unevaluated factors. Resource-cache invalidation uses resource revision/selection/viewport independently from the terrain raster key, so pointer motion does not rebuild either raster. The Current/Candidate preview's shared viewport uses one dispatcher-deferred, latest-value-coalescing synchronizer: a fit notification may originate inside one canvas render pass, so it never invalidates the peer canvas synchronously from that stack.

Seasons mode adds a third independently keyed bounded visible raster over the unchanged terrain surface. Every cell uses its definition color/tint; optional boundary blending samples only neighbor presentation colors and never changes the dense Season ID stream. At `28 px/tile`, outlined abbreviation labels expose the configured identity and append `L` for a lock. Hover and pin report exact identity, fallback, lock, terrain/elevation, and rule summary. After accepted generation, a cached immutable support/fingerprint projection supplies latitude, temperature, moisture, intensity/tendency, rain shadow, exact water distances, and canonical source/input staleness. The inspector evaluates the current catalog rules against the active accepted priority on demand for first-winner and shadowed/higher-priority overlap text; those outcomes are not persisted. A saved recipe rebuilds the support/fingerprint cache after reopen behind revision/reference guards.

`SeasonGenerationDialog` is a resource-independent preview boundary over the current lattice. It captures terrain and Season state on the owner thread, runs `CampaignSeasonGenerator` on the immutable snapshot away from the UI thread, and gives unchanged Current and unapplied Candidate canvases one dispatcher-safe synchronized viewport. A read-only whole-cell rectangle gesture defines inclusive scope without entering a paint stroke. In-dialog generation settings and scope stale the retained Candidate; report selection, grid, labels, blending, pan, zoom, and narrow Current/Candidate switching are display state only. Catalog/priority management requires closing the modal, so that dialog Candidate is discarded. Acceptance is centralized in `EditorViewModel`: it rechecks both source revisions, value-equal definition, exact catalog/priority, Candidate revision, settings, and scope, then installs the exact candidate/recipe, clears shared history, and marks dirty while preserving terrain, resources, and project/import identity.

## Persistence and conversion

`CampaignWorldProjectSerializer` writes version-2 tile data, the optional custom terrain catalog, and the manifest through temporary files before replacement. It rejects unsupported versions, inconsistent definitions, duplicate records, unknown types, invalid heights, unknown/mismatched custom IDs, redundant explicit defaults, invalid river topology, and out-of-world coordinates. A missing tile file means the entire world is implicit default data; a missing custom catalog means no custom types. Early version-2 and version-1 `Water` values normalize to canonical `Sea` values.

`CampaignResourceProjectSerializer` owns the optional custom-definition, generation-settings, and sparse occurrence sidecars. `CampaignSeasonProjectSerializer` owns the complete catalog/priority/default, optional accepted recipe, and dense Season/lock stream. `CampaignEditorProjectSerializer` is the running lifecycle boundary: save serializes terrain, resources, and seasons into one unique sibling staging directory, reloads the complete candidate, verifies all three captured revisions, then commits the nine known managed files with backups and rollback for ordinary I/O failure. Stale optional sidecars are removed, post-commit staging cleanup is non-fatal, and `MarkSaved` runs only after success. Open similarly loads all authorities and replaces the visible document only when the complete candidate is valid. Missing resource sidecars produce the built-in catalog/null settings/empty map; missing all Season sidecars projects a clean unlocked Spring layer with no invented recipe; legacy imports attach neither sibling layer.

When it detects a version-1 manifest, it delegates strict loading to the legacy serializer, then builds a new campaign world in memory. Existing types are copied and each tile's legacy samples are averaged into its centre height. The editor does not attach the source folder as a save target and rejects selecting that same folder, keeping the original files unchanged.

## Runtime export

`CampaignWorldRuntimeExporter` is a one-way engine-neutral handoff boundary. The editor calls its season-aware version-3 overload. It retains byte-compatible row-major four-byte `tiles.bin`, eight-byte per-tile `resource-index.bin`, and compact four-byte `resource-records.bin`, then adds dense two-byte `season-tiles.bin` indexes plus the canonical Season catalog/fallback/appearance manifest. Terrain/resource/season authoring locks, rules, warnings, support fields, and generation settings do not enter runtime data. Bounded buffers, stable catalog/occurrence ordering, fixed ZIP timestamps, three-authority revision checks, and SHA-256 values make equal authority produce equal package bytes without mutation.

The manifest explicitly records physical dimensions, axis orientation, binary field offsets, type mappings, custom identities, and version. A Unity/Unreal build importer can therefore validate and convert the package without referencing Avalonia or `World.Editor`. Export is atomic, does not mutate the world, and does not clear dirty state. See [[../Reference/Runtime World Package|Runtime World Package]] and [[../Decisions/ADR-0009 - Versioned Runtime World Package|ADR-0009]].

## Performance model

- Sparse storage scales with tiles that differ from the implicit default.
- One history entry scales with unique tiles changed by the stroke.
- Rendering scales with the capped visible raster, not the theoretical tile count.
- Raster sampling scales sparse-map lookups with visible logical tiles, then uses dense pooled reads per pixel; independent rows use available CPU cores above the large-raster threshold.
- Middle-drag frames reuse and translate the cached terrain raster; only the settled viewport is rerasterized.
- River overlay traversal scales with River tiles rather than all materialized campaign tiles.
- Elevation-label work scales with visible tiles only, is suppressed below `28 px/tile`, and reuses a bounded formatted-text cache; toggling it never rebuilds terrain pixels.
- Selected-resource raster/label work scales with visible tiles and sparse occurrences in the visible area; resource revision does not invalidate the terrain raster.
- Season raster/label work scales with the bounded visible logical area plus one neighbor-tile blending halo; season revision does not invalidate terrain or resource caches.
- Generation is bounded to 250,000 tiles and performs its priority queues and sorting away from the UI thread.
- Runtime export is streamed in bounded buffers; package size and traversal scale with the complete logical grid because game data must include implicit-default tiles.
- Procedural textures require no bitmap assets, remain stable under pan, and fade out when insufficient pixels exist to represent them cleanly.
- Grid drawing selects a screen-space stride rather than iterating every invisible line.
- No UI control or separate object is allocated per logical tile.

## Extension seam

Roads, detailed biomes, persistent geology/climate fields, settlements, tactical meshes, and advanced hydrology should be explicit layers or consumers sharing tile/world coordinates. The implemented campaign-resource layer demonstrates that peer-authority pattern. The terrain generator's transient tectonic and erosion fields shape creation-time centre heights but are not saved as new authorities. Flow direction, discharge, physical river width, confluence/distributary semantics, deltas, and curved shoreline geometry must not be encoded into `HeightMeters` or resource potential.

The resource product path now implements ADR-0016 through ADR-0022. Resource-only generation captures an immutable terrain/resource snapshot, runs deterministic climate/geology-backed placement on a worker, compares current and candidate maps side by side, and accepts the exact reviewed candidate as one shared-history-clearing document boundary. Full-world regeneration captures a smaller immutable source containing the current definition, revisions, catalog, settings, and stable sparse occurrence entries before worker execution. Same-lattice candidates copy every occurrence exactly. Changed lattices map old tile centres in physical metres into the replacement grid; same-ID collisions keep maximum potential and OR the lock, while out-of-bounds locked sources remain explicit report entries. Saved settings cause unlocked resources to regenerate against the candidate terrain after locks are remapped; without settings, all occurrences remap and no recipe is invented. Acceptance validates source and candidate terrain/resource revisions, definition equality, and catalog identity before installing the exact reviewed terrain/map/settings tuple. The custom-resource manager supplies validated project-owned definitions to that same persistence, generation, remapping, and export path.

[[../Decisions/ADR-0030 - Static Preview-First Campaign Season Layer|ADR-0030]] owns the third peer authority: one static Tile Season ID and lock flag per logical tile, generated from an immutable terrain snapshot and saved Season Seed without calendar semantics. Slices 1-7 implement definitions, rules, catalog, settings, scope, dense map/locks, atomic mutations, stable seeds/fingerprints, shared-history commands, version-2/version-3 terrain adapters, immutable capture, Earth-like support fields, ordered first-match generation, reports, strict sidecars, missing-sidecar Spring compatibility, nine-file staged coordination, runtime package version 3, manual canvas editing, protected custom-definition management, Current/Candidate regeneration, exact acceptance, generation-backed pinned diagnostics, atomic generated-new-world composition, explicit blank defaults, greatest-overlap changed-lattice lock review, headless native normal/narrow verification, maximum-grid diagnostics, and the standalone Windows publication boundary. Shared static canvas brushes/pens are immutable so multiple native windows and test UI threads can render safely.

## Version-3 target — Phase 1 core implemented

The current model proves tile authority but its single enum cannot express overlapping cover, form, networks, and shores. [[../Decisions/ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]] accepts the next architecture boundary:

```text
one base surface + centre height
        + derived terrain form
        + optional directed River overlay
        + automatic shore with sparse edge overrides
```

The target base surfaces are Grassland, Forest, Desert, Wetland, Tundra, BarrenRock, Sea, and Lake, plus Unassigned. The current version-2 Desert tile maps directly to that future surface; Steppe maps to Grassland until a later biome/climate layer owns the finer distinction, while Hills, Mountain, and Cliff become derived form and River becomes a network. Running version 2 already derives a simple 10% coast for every non-water type; version 3 additionally moves Beach/Cliff to explicit per-edge treatments.

`Kingdom.World.Core.Campaign.V3` now implements the engine-neutral aggregate, sparse layer maps, terrain-form projection, River validation, shore resolution, and cross-layer invariants. `World.Editor`, the version-2 serializers, and the running executable do not reference that namespace yet.

All preceding sections therefore remain the verified architecture of the running version-2 editor. The detailed schema, migration, remaining implementation order, tool grouping, and acceptance criteria are in [[../Reference/Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]].
