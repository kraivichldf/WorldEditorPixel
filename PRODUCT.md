# Kingdom World Editor

<!-- impeccable:product-schema 1 -->

## Platform

desktop

This is a native Windows Avalonia application. Design and finish reviews must use desktop-window conventions and native screenshots; a tool that does not recognize `desktop` must not silently substitute web, iOS, or Android rules.

## Stack

C# 14, .NET 10, Avalonia UI, and xUnit. The application is a standalone desktop editor; its campaign-world model, interpolation, commands, validation, and serialization remain independent of Avalonia and game engines.

## Users

The confirmed workflow is for a strategy/FPS game developer or world designer authoring a large campaign surface on a desktop. Team collaboration and a downstream engine importer remain outside this milestone.

## Product Purpose

Create, optionally generate, inspect, stamp, save, and reopen deterministic campaign tiles. Each tile owns one base terrain type, an optional safe custom-land identity, one centre height, zero or more orthogonal resource occurrences, and zero or more Season Occurrences keyed by stable Season ID; every resource or Season Occurrence carries its own authoring lock. Adjacent centre heights automatically form a continuous surface. Generated terrain, Resources, and Seasons become the same editable authority as manual stamps after explicit preview acceptance. The editor succeeds when terrain identity, centre height, resource potential/lock authority, the complete Tile Season Set, and the interpolation contract survive a save/load roundtrip unchanged.

## Positioning

The campaign tile is the only authoring resolution. This removes the mismatch where a visible campaign cell was only an overlay on an unrelated sample brush. A designer chooses exactly what one tile is and how high its centre is; the system owns the slope between neighbouring centres. A bounded paint-area setting can batch that same atomic decision across a square of complete tiles without introducing sub-tile authority.

## Operating Context

Users work in long desktop sessions with a central world canvas, persistent stamp and inspector rails, mouse-centred zoom, middle-button pan, full-cell drag editing, and keyboard undo/redo. Projects are local folders containing version-2 terrain plus optional Resource and complete Season sidecars.

## Capabilities and Constraints

- World width, world height, and campaign tile size are authored in whole kilometres and stored in metres.
- New World offers Blank plus Continental World, Island, Archipelago, four directional Coast, Sea in Center, and Land Only presets with deterministic seed, terrain style, Mountain-system, hydrology, directional coast character, optional tidal-inlet amount, and optional inland tile-ratio controls. Continental World builds a hierarchy of five unequal multi-lobe masses, broad ocean basins, regional bays/peninsulas, and two minor island arcs; sparse ocean anchors allow cropped edge geography without claiming planar left/right wrapping. For directional Coast worlds, only the named Sea edge is forced; the seed varies the broad mainland advance/retreat and may open another map boundary to connected Sea, so neither the land/water ratio nor the opposite edge is locked to one silhouette.
- Generated starts remain inside New World as a read-only campaign preview. Designers may adjust any input and regenerate repeatedly; changing an input marks the displayed result stale, and only **Use this world** commits the exact reviewed tiles to the editable document. Blank still creates directly.
- An open world can be regenerated through the same preview-first contract. **Regenerate world…** starts from the current dimensions, tile size, sea/default height, and height limits but allows every definition value to change; carries the current custom-land catalog into the generator; restores the last generator settings when they still exist in the current editor session; and leaves the editable document untouched until **Use this world**. The preview also shows the exact resource result. Same-lattice replacements preserve every occurrence; changed lattices remap physical tile centres, merge same-ID targets by highest potential while retaining any lock, name locked out-of-bounds drops, and regenerate unlocked occurrences only when saved resource settings exist. Acceptance installs the exact reviewed terrain/resource candidate, clears obsolete undo history, keeps the current project identity/import safety boundary, and marks the document modified. Terrain-generator settings remain transient and are not added to the project format.
- Generated relief combines seeded tectonic provinces, convergent uplift, rifts, shear belts, physical-kilometre simplex fields, plate-boundary-aligned ridges, and bounded connected Mountain systems. Mountain classification follows narrow ridge cores and reserves nearby suitable land as Hills/foothills instead of filling compact painted blobs. Deterministic thermal relaxation and stream-power erosion shape the resulting slopes before Lakes and Rivers are solved, and directional hillshade makes those continuous slopes legible beneath the tile colors. Every directional coast treats its noisy boundary as a continental shelf, carves an asymmetric pair of regional bays, and unions a protected-root curved peninsula with water on both flanks; Natural and Rugged can additionally compose kilometre-scaled landmark systems such as gulfs, barrier sounds, and offshore-island straits. Optional tidal inlets cut lowland Sea-connected drowned valleys; a broad aridity field marks true dry cores as Desert and semi-arid transitions as Steppe; basin Lakes use priority-flood depression analysis; and compatible tributaries may merge through explicit three-exit River Junction confluences before qualifying long downstream reaches widen to Large River.
- World dimensions must divide exactly by tile size; partial campaign tiles are invalid.
- Every tile stores one fixed base terrain type, an optional safe custom-land identity, and one signed 16-bit whole-metre centre height.
- The active palette includes full-tile Steppe semi-arid grassland and Desert dry lowland, distinguishes Sea and Lake water, provides narrow River and broad Large River paths, and retains full-tile Beach/Cliff classifications. Coastal is no longer paintable or persisted; River Junction is a persisted topology value created only by the River Split tool, not a direct palette choice.
- Designers may define up to twelve named, colored custom land types on a safe Plains, Steppe, Desert, Forest, Hills, or Mountain base; they are available in the palette and can be included in seeded generation.
- Orthogonally adjacent River, Large River, and River Junction tiles connect as one route network. Normal and Large River segments allow at most two cardinal exits; an explicit River Junction allows at most three. Atomic validation rejects four-way crossings and any edit or file that exceeds the owning tile type's limit.
- Every non-water tile beside Sea or Lake automatically uses matching water on the outer 10% of each water-facing edge while preserving its original terrain/custom identity and material inside. A typical one-sided coast is 90% original material and 10% water; no automatic sand band is inserted.
- Bilinear interpolation between centre heights is the authoritative derived surface contract.
- A pinned-tile elevation helper can copy the exact pinned centre height or suggest the 10 m-rounded arithmetic mean of its valid cardinal neighbours; it only changes the normal active stamp height.
- Paint Area expands a non-river stamp from `1 × 1` through `25 × 25` complete tiles, clips at world bounds, previews the exact footprint, and deduplicates overlaps into one stroke command. River and Large River stay `1 × 1` to preserve controlled route topology. The pinned River Split action creates two, three, or four separated branches as one atomic command by cascading only three-exit Y junctions; it never creates a four-exit tile.
- Terrain, Resources, and Seasons are explicit workspaces over the same canvas, pan/zoom transform, campaign grid, hover coordinate, and right-click pin. The Resources rail filters the built-in/custom catalog, writes exact per-resource potential `1..100` over an independent `1 × 1` through `25 × 25` complete-tile area, removes only the selected ID, and locks manual edits by default. The Seasons rail adds or erases the selected Season ID and locks or unlocks that exact occurrence without changing any other Season ID on the tile. There is no month or clock.
- A protected Custom resources manager creates a `0%` manual-only definition or duplicates a built-in, edits the complete bounded identity/display/generation/rule contract, preserves compatible occurrences atomically, locks used stable IDs/categories, and feeds the existing save/export/generation paths without JSON editing.
- One tile may contain multiple different resource IDs but only one occurrence of each ID. Manual resource edits never rewrite terrain, height, River, or shore authority. The pinned inspector lists every occurrence with exact potential, lock state, hard-rule warnings, and explicit unevaluated factors; warnings never delete data.
- The selected-resource view mutes but retains terrain, renders a fixed `1..100` heatmap, and shows exact potential numbers at readable zoom. Resource strokes, lock changes, and terrain strokes share one ordered delta history.
- A protected Custom seasons manager owns Spring/Summer/Fall/Winter plus safe custom definitions and portable fallbacks. Each enabled definition is evaluated independently during generation, so all matching Season IDs coexist. Preview-first Season regeneration compares unchanged Current authority with an unapplied Candidate, preserves locked/out-of-scope occurrences, installs only the exact current Candidate, and retains immutable climate support/fingerprints for pinned inspection.
- The Seasons view preserves terrain context while compositing every occurrence color on a tile. Exact identity labels, per-occurrence lock markers, and optional boundary blending are presentation-only; Season strokes share the same ordered terrain/resource history.
- A continuous drag is one delta command containing both fields; full-world snapshots are prohibited.
- The canvas uses deterministic material texture plus derived height shading and offers a texture-free height-only view. Stored tile-centre elevations can be shown as outlined whole-metre numbers in a separate view-only overlay; users may hide them, and the renderer suppresses them automatically until individual tiles are large enough to read.
- Sparse storage omits tiles that equal `Unassigned` at the configured default height.
- Version-1 sample/chunk projects can be imported, but the converted result must be saved to a new folder.
- Save/reopen uses one editor-level staged coordinator for terrain, optional sparse Resource sidecars, and canonical Season catalog/generation plus sparse occurrence sidecars. A project is marked clean only after the combined candidate reload, all-authority revision checks, and managed-file commit succeed.
- Runtime export offers two deterministic one-file handoffs. **Export Runtime Data** writes the compact `.kworld` version-3 package; **Export JSON Data** writes a readable `*.world.json` version-1 document containing metadata, catalogs, and every complete tile. Both include terrain/height, Resource potential, and every Season occurrence while keeping authoring locks, rules, recipes, support fields, and diagnostics editor-only.
- The editor excludes gameplay systems, additional world layers such as roads and settlements, engine integration, networking, databases, ECS, and 3D rendering.
- This delivery remains a local, single-user authoring tool.

## Evidence on Hand

The supplied implementation brief and the user's confirmed tile-only rework are the product authority. No brand assets, production worlds, customer claims, or performance benchmarks were supplied; future work must not fabricate them.

## Product Principles

- One visible campaign cell equals one editable and persisted tile.
- Type and centre height change atomically.
- Elevation helpers may prepare the active centre height but never paint automatically or introduce another stored height authority.
- Automatic river routing and explicit Y splits must remain legible without weakening the full-cell type and centre-height visual contract.
- Texture adds material legibility but never becomes stored terrain authority or obscures a visible elevation number.
- Slopes are deterministic derivations, never a second hidden authoring layer.
- Generation is a deterministic creation command with a transient pre-commit review step, never a retained snapshot or locked layer. Accepting a preview transfers that exact `CampaignWorld`; every output tile is immediately editable.
- Regeneration is a document replacement command, not a new terrain mode. It initializes from the current world definition and custom catalog, allows a different valid grid and height contract, preserves the project path, and requires explicit reviewed-preview acceptance before replacing terrain, the sparse resource map/settings, and shared history. Physical tile centres—not normalized map percentages or unchanged integer coordinates—own changed-lattice resource placement.
- Directional coast character controls large-scale shoreline geometry independently from tidal inlets. Flowing bays and capes uses a smooth regional profile plus a tapered curved centreline to keep one mainland silhouette; Natural and Rugged rotate through distinct landmark families. Feature sizes and counts scale in physical kilometres so changing campaign tile size changes resolution, not the intended geography.
- Tidal inlets are an optional, opportunity-based coastline treatment rather than a carving quota. Settings raise the number, reach, and acceptance chance of separated lowland opportunities, but unsuitable geography may produce fewer or none. At campaign resolution, a full Sea tile communicates a broad estuary or drowned valley; a narrow constructed canal remains a future network/overlay capability.
- Custom ratios govern eligible Plains/Forest/Desert/Hills/Mountain/Steppe land; shape, drainage, and steep shoreline grade keep authority over Sea, Lake, both River sizes, and Cliff. Automatic coast edges consume no separate terrain category, so custom land can reach the water and retain its identity.
- Custom land types retain one of those six safe bases for portable data and material fallback, not allocation ownership. Their positive terrain-mix shares join the six default land ratios in one exact 100% inland pool; `0%` makes a type paint-only.
- Terrain/resource/Season rendering scales with visible pixels and sparse occurrence data by default. Season generation support and dense runtime span indexes intentionally scale with the full logical tile grid, bounded at `250,000` tiles.
- File formats stay explicit, versioned, debuggable, and straightforward for a future engine importer.
- Authoring save and runtime export are separate responsibilities: export never changes project identity, clears dirty state, or becomes another editable authority.
- Legacy sources remain unchanged during conversion.
- The Season Occurrence layer is a peer authority, not a calendar simulation or an encoding inside terrain/resource values. Generation consumes terrain but never resources and never rewrites either.
- Additional world layers remain explicit consumers or peers, not encodings inside height, resource potential, or Tile Season membership.

## Accessibility & Inclusion

Use keyboard-accessible standard desktop controls, visible focus, sufficient contrast, text labels alongside terrain/resource/Season colors, high-contrast outlined elevation, resource-potential, and Season-identity labels, an outlined pinned selection, and direct validation/recovery messages. Resource and Season lock/suitability/staleness states must include text rather than depend on color. Map stamping remains mouse-led in this milestone; keyboard tile navigation and stamping are future accessibility work.

## Accepted Next Architecture — Core Implemented, Product Integration Pending

Tile Taxonomy v3 replaces the mixed type enum with one base surface, derived terrain form, a directed River overlay, and automatic shores with sparse Beach/Cliff edge overrides. The accepted minimum base surfaces are Unassigned, Grassland, Forest, Desert, Wetland, Tundra, BarrenRock, Sea, and Lake. Version 2 already exposes Desert and Steppe as complete-tile values; Steppe maps to Grassland until a later biome/climate layer owns that finer ecological distinction. Version 2 does not yet split surface from form, River, or shore data.

The engine-neutral Phase 1 domain and validators are implemented and tested, but no editor journey or file format uses them yet. Version 2 remains the product currently shipped. See [[docs/Reference/Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]] before changing UI or persistence.

The ADR-0016 through ADR-0029 campaign-resource slice is implemented in the running product: definitions/catalog, sparse occurrences, terrain diagnostics, one shared history, Terrain/Resources workspaces, exact potential heatmap/labels, pinned warnings and locks, staged save/reopen, deterministic runtime-v2 Resource streams, immutable current-world capture, deterministic support/suitability fields, preview-first explicit-subset regeneration, reviewed changed-lattice full-world Resource remapping/regeneration, and protected custom-resource definition management. Same-lattice terrain regeneration preserves Resource authority exactly; changed-lattice preview reports physical moves, merges, drops, locks, and regenerated unlocked results before atomic acceptance. Climate/geology field overlays, overview symbols, and full New/Regenerate World Resource pages remain later Resource milestones. Continue from [[docs/Decisions/ADR-0029 - Explicit Resource Generation Selection|ADR-0029]] and [[docs/Reference/Campaign Resource Layer Plan|Campaign Resource Layer Plan]].

ADR-0030 is implemented as a sparse Season Occurrence authority: zero or more stable Season IDs per tile, per-identity locks, independent Earth-like rule evaluation, shared-history add/erase/lock tools, strict sidecars, deterministic runtime package version 3 span/record streams, preview-first scoped regeneration, exact acceptance, pinned support/rule/staleness diagnostics, generated-new-world composition, empty blank-world defaults, and changed-lattice physical-centre lock remapping with same-ID merges. Continue from [[docs/Decisions/ADR-0030 - Preview-First Campaign Season Occurrences|ADR-0030]] and [[docs/Reference/Campaign Season Occurrence Layer Plan|Campaign Season Occurrence Layer Plan]].

ADR-0031 is implemented as a separate single-file JSON runtime boundary. It streams one self-describing `*.world.json` file with the full row-major grid and stable terrain/Resource/Season identities, remains deterministic and atomic, and preserves the smaller `.kworld` path for production-scale import. Continue from [[docs/Decisions/ADR-0031 - Single-File JSON Runtime Export|ADR-0031]] and [[docs/Reference/Runtime World Package|Runtime World Package]].
