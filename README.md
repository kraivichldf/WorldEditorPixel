# Kingdom World Editor

A standalone desktop editor for authoring a strategy/FPS world as exact campaign tiles. Every tile owns one portable base terrain type, an optional safe custom-land identity, and one whole-metre height at its centre; the rendered and exported surface automatically interpolates neighbouring centre heights into continuous slopes.

## What works

- Create worlds in kilometres with an exact, complete campaign grid. A `700 × 700 km` world with `5 × 5 km` tiles is `140 × 140 = 19,600` tiles.
- Optionally generate a deterministic editable starting world from Continental World, Island, Archipelago, East/West/North/South Coast, Sea in Center, or Land Only profiles; Blank preserves fully manual creation. Continental World composes unequal major landmasses and broad connected oceans. Directional Natural/Rugged coasts scale from compact regional forms into continental shelf bends, irregular nearshore structure, heterogeneous landmark regions, and sparse island arcs instead of stretching one geometric bay/cape stamp.
- Regenerate an open world through a reviewed preview while starting from—and optionally changing—its dimensions, campaign tile size, elevation contract, and current custom tile catalog. The old definition and tiles remain unchanged until **Use this world**; acceptance keeps the saved project identity, clears obsolete undo history, and marks the document modified.
- Choose Gentle/Balanced/Rugged relief, one/few/several coherent Mountain systems, None/Light/Balanced/Abundant hydrology, optional None/Few/Balanced/Drowned-coast tidal inlets, and a reproducible signed seed. Geological noise uses physical-kilometre simplex wavelengths and stretches ridge detail along tectonic boundaries. Inlets follow low ground and stay Sea-connected; a 5 km Sea tile is a broad estuary/channel rather than a narrow canal. Optionally set one inland mix: the six default land ratios plus positive custom types total 100%; Mountain remains capped at 12%, unsuitable constrained share becomes Plains, and water/shore topology stays controlled by shape and drainage.
- Define up to twelve named, colored custom land types on Plains, Steppe, Desert, Forest, Hills, or Mountain bases. Leave a type at `0%` for paint-only use or give it an independent deterministic portion of the inland mix; its base is a safe data/material fallback, not the owner of that share. Custom types never become water, shore, or River types.
- Stamp complete tiles as Unassigned, Plains, Steppe, Desert, Forest, Hills, Mountain, Sea, Lake, River, Large River, Beach, or Cliff with one centre height; Paint Area expands non-river stamps from `1 × 1` through `25 × 25` complete tiles.
- Read terrain as material, not flat color: grass, dry Steppe grass, dune-and-stone, canopy, ridge, rock, water-wave, and sand textures remain stable in world space and fade when zoomed out.
- Derive coast automatically on every non-water tile beside Sea or Lake: matching water occupies the outer 10% of each facing edge and the tile's original built-in/custom material remains inside.
- Drag four-connected River or Large River paths, then split a pinned endpoint into two, three, or four branches. The editor cascades three-exit Y junctions and blocks every four-way river crossing.
- Fill every touched cell edge to edge during click or drag, including cells crossed between fast pointer events.
- Derive a continuous height surface by bilinearly interpolating neighbouring tile centres.
- Undo or redo one complete drag, restoring both tile type and height together.
- Switch to a dedicated Resources workspace without leaving the shared map. Filter the built-in catalog, paint one selected resource at exact `1..100` potential over `1 × 1` through `25 × 25` complete tiles, erase only that resource, and protect deliberate placements with an authoring lock.
- Create project-owned custom resources in the editor or duplicate a built-in as a starting point. Configure identity, display, independent generation defaults, bounded terrain/water ranges, soft preferred/avoided factors, hard normalized-surface exclusions, and custom-terrain rules; used IDs/categories stay protected while save, export, painting, and procedural generation use the same catalog.
- Inspect all resource occurrences on a pinned tile in stable-ID order, including hard terrain warnings and factors that the current diagnostic layer cannot yet evaluate. Resource edits share the same stroke-level Undo/Redo history as terrain edits.
- Generate or regenerate resources without changing terrain: move any built-in/custom definitions between explicit **Included — Regenerate** and **Excluded — Keep** lists, then set a world-derived or explicit seed, abundance, climate, geology, and per-resource overrides; compare synchronized Current/Candidate maps and accept only the reviewed candidate. Locked manual occurrences survive, excluded resources stay exact, and changed inputs disable acceptance until regeneration.
- Pan, zoom, fit the world, show or hide the grid, switch to height-only shading, and inspect stored and derived values.
- Save and reopen deterministic version-2 projects containing `world.json`, sparse `campaign-tiles.json`, and resource sidecars when resources exist. The project coordinator stages both terrain and resource authority before replacement.
- Export deterministic runtime-package version 2 `.kworld` files containing dense terrain/resource indexes plus compact resource definitions and occurrences.
- Import version-1 sample/chunk projects into averaged tile-centre heights without modifying the source folder.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows, Linux, or macOS supported by Avalonia. This repository is verified on Windows.

## Build, test, and run

From the repository root:

```powershell
dotnet restore KingdomWorldEditor.sln
dotnet build KingdomWorldEditor.sln
dotnet test KingdomWorldEditor.sln --no-build
dotnet run --project src/World.Editor/World.Editor.csproj
```

Create a self-contained Windows executable:

```powershell
dotnet publish src/World.Editor/World.Editor.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o artifacts/publish/large-world-coasts
```

## Editor controls

After running the self-contained publish command above, double-click `Launch Tile Editor.cmd` in the project root. The launcher targets `artifacts/publish/large-world-coasts/World.Editor.exe`, so the .NET SDK is not required to run that locally published Windows executable.

| Action | Control |
|---|---|
| Create a blank or generated world | **New**, choose starting shape, terrain, Mountain systems, hydrology, tidal inlets, optional inland tile ratios/custom land types, and seed |
| Regenerate the open world | **Regenerate**, **Terrain → Regenerate world…**, or `Ctrl+R`; adjust the complete world definition and generation settings, generate a preview, review terrain plus resource moves/merges/drops, then choose **Use this world** |
| Manage custom land types | **Terrain → Custom tile types…** or the button below **Terrain type** |
| Stamp complete campaign tiles | Choose type, centre height, and optional Paint Area, then left-click or left-drag |
| Paint campaign resources | Choose **Resources**, filter and select a resource, set potential/area/action/lock, then left-click or left-drag |
| Manage custom resources | Choose **Resources → Custom resources…** or the Resources-rail button; add a manual-only definition or duplicate a built-in, configure preferred/avoided factors, then choose **Apply resources** |
| Generate or regenerate resources | Choose **Resources → Regenerate resources...** or press `Ctrl+Shift+R`; choose Included/Excluded resources, configure profiles/overrides, generate a candidate, compare it, then choose **Use resources** |
| Inspect resources on a tile | Right-click, then use the pinned occurrence list to select, erase, lock, or unlock an exact resource ID |
| Route a river | Choose River or Large River and drag; diagonal pointer motion becomes one N/E/S/W tile path |
| Split a river | Right-click a River/Large River endpoint, choose 2–4 branches and Auto or a direction, then choose **Create split** |
| Pin a tile for comparison | Right-click |
| Reuse or blend nearby elevation | Right-click a tile, then choose **Copy centre** or **Blend around** in the pinned inspector |
| Pan | Middle-drag |
| Zoom around pointer | Mouse wheel |
| Fit world | `F` |
| New / Open / Save / Save As | `Ctrl+N` / `Ctrl+O` / `Ctrl+S` / `Ctrl+Shift+S` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Cancel the active drag | `Escape` |

One drag is one undo entry. The selected type and tile elevation are a single stamp; elevation arrows move in `10 m` steps. Paint Area is a bounded square selection of complete tiles, not a sample brush: `0` expands to `1 × 1`, `1` to `3 × 3`, through `12` for `25 × 25`, and the preview clips at world edges. Both paintable river sizes always use `1 × 1`; the dedicated split action adds its complete multi-tile Y footprint as one undo entry. There is no sample brush, sub-tile radius, strength, falloff, flatten target, sample spacing, or authoring chunk size.

## Height and type semantics

- `(0, 0)` is the north-west/top-left tile. X grows east/right and Y grows south/down.
- World dimensions and tile size are entered in kilometres and stored in metres.
- Tile heights are signed `Int16` whole metres stored at tile centres.
- Type is discrete for the complete cell. An optional high-contrast number at the cell centre shows its stored whole-metre elevation when the view is zoomed in far enough. Height is continuous: at a tile centre the surface equals that tile's stored height; between centres it is bilinearly interpolated.
- Sea and Lake are distinct water types. Beach and Cliff remain explicit full-tile classifications and receive the same automatic water edge when beside either water type.
- Coastal is not a tile type in current authoring. On a typical one-water-edge land tile, 90% remains its original material and 10% becomes matching Sea/Lake water. Multiple water-facing edges each receive the same 10%-deep transition; diagonal water alone does nothing.
- River connections are derived across orthogonally adjacent River, Large River, and River Junction tiles. River shows a narrow bank-and-water ribbon; Large River shows a broad major-river corridor. Both keep grass visible and use symbolic preview widths rather than literal kilometres. Normal/Large segments may have up to two exits; explicit junctions may have up to three; four exits are always rejected.
- World edges extend the nearest centre height outward, avoiding an artificial drop beyond the outermost centres.
- Height-only view changes rendering only; it never changes stored data.

## Architecture

```text
World.Editor (Avalonia shell, shared canvas/input/history, terrain/resource inspectors)
    -> World.Core (campaign terrain + resource authority, diagnostics, commands, validation)
        -> project folder (terrain files + optional deterministic resource sidecars)
        -> runtime package v2 (.kworld)
```

`World.Core` has no UI or game-engine dependency. `CampaignWorld` owns one validated definition and one sparse `CampaignTileMap`. An absent map entry means `Unassigned` at the world's default height. `CampaignMapGenerator` optionally materializes deterministic ordinary tiles from analytic land masks, kilometre-scaled shelf/landmark/island hierarchy, compact regional coast skeletons, simplex geology, transient tectonic boundary fields, bounded erosion, optional lowland tidal-inlet carving, priority-flood drainage, basin selection, flow accumulation, constrained custom land-ratio targets, semi-arid Steppe transition, and safe custom land identity targets. Flowing Capes keeps its protected-root curved peninsula; Smooth/Natural/Rugged fade that compact symbol out by `4,200 km` and use broad stochastic shelf plus distributed geographic features at continental scale. Only the named Sea edge is forced; a seeded broad shelf retreat allows any other boundary to contain natural land, connected Sea, or both. `CampaignTileMap` derives river exits, validates per-type river topology atomically, validates custom IDs against their safe bases, and resolves automatic 10% water-facing material bands for every non-water tile. `CampaignRiverSplitBuilder` constructs collision-free multi-Y footprints as one command. `CampaignTileStampBuilder` records the first before-value and final after-value for each tile touched during a drag. `WorldCanvas` rasterizes original built-in/custom materials, automatic coast water edges, and derived height shading, then draws connected river channels, the grid, optional culled elevation-number labels, cursor, border, and pinned selection.

See [[docs/Architecture/World Terrain Editor|the architecture note]], [[docs/Reference/Campaign World Generation|the generation formulas]], [[docs/Decisions/ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]], [[docs/Decisions/ADR-0005 - Water and River Tile Topology|ADR-0005]], [[docs/Decisions/ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006]], [[docs/Decisions/ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]], [[docs/Decisions/ADR-0010 - Tectonic Erosion and Hierarchical Drainage|ADR-0010]], [[docs/Decisions/ADR-0011 - Physical Terrain Noise and Boundary-Aligned Ridges|ADR-0011]], [[docs/Decisions/ADR-0012 - Regional Geographic Coast Skeletons|ADR-0012]], [[docs/Decisions/ADR-0013 - Opportunity-Based Tidal Inlets|ADR-0013]], [[docs/Decisions/ADR-0014 - Open Directional Coast Boundaries|ADR-0014]], [[docs/Decisions/ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024]], [[docs/Decisions/ADR-0025 - Built-in Steppe Terrain|ADR-0025]], [[docs/Decisions/ADR-0026 - Soft Avoided Resource Terrain Factors|ADR-0026]], and [[docs/Decisions/ADR-0027 - Hard Resource Surface Exclusions|ADR-0027]].

## Persistence and legacy projects

A version-2 project is portable when `world.json` and `campaign-tiles.json` are copied together; include `custom-terrain.json`, `resource-definitions.json`, `resource-generation.json`, and `resource-tiles.json` when those optional files exist. The project coordinator stages the complete terrain/resource file set before replacement and removes stale resource sidecars when the saved map becomes empty. Loading rejects unsupported versions, duplicate records, unknown types/custom IDs, invalid heights, and coordinates outside the exact grid.

Opening a version-1 project converts it in memory. Each campaign tile receives its existing type and the rounded average of the legacy samples owned by that cell. Legacy `Water` becomes `Sea`. The editor marks the result unsaved and requires a different destination folder, so the original manifest, chunks, and campaign file remain unchanged.

The full contract is in [[docs/Reference/World File Format|World File Format]].

## Repository layout

```text
src/World.Core/    engine-neutral campaign world and persistence; legacy v1 reader retained
src/World.Editor/  Avalonia desktop application
src/World.Tests/   xUnit behavior, interpolation, conversion, and roundtrip coverage
docs/              Obsidian-compatible architecture, guides, ADRs, and verification
```

## Current limits

- Procedural resource generation, reviewed changed-lattice remapping, and custom-resource definition management are implemented. Overview symbols, climate/geology field views, and full New/Regenerate World resource property pages remain later milestones.
- Map traversal, pinning, and stamping are mouse-led; keyboard-only canvas navigation/painting is not yet implemented. Standard menus, workspace controls, and inspector controls retain native keyboard behavior.
- Custom types are deliberately safe land variants only. Creating new water, shore, River, topology, or gameplay semantics from the custom-type manager is not implemented.
- River branching is explicit and geometric: the split tool stores a `RiverJunction` Y tile and cascades Y shapes for three or four outgoing branches. It does not store flow direction, distinguish upstream from downstream after creation, generate deltas automatically, or model discharge, bridges, continuous physical width, or navigability.
- Version-2 generation can create validated three-exit River confluences, but it does not preserve flow direction, discharge, sediment, or confluence semantics. Its tectonic, erosion, and climate fields are bounded deterministic terrain synthesis, not scientific simulation or tactical/FPS mesh generation.
- Tidal inlets are optional opportunity-based broad Sea-tile estuaries/drowned valleys; a requested profile may accept fewer or none when the coast lacks suitable low terrain. True narrow constructed canals, locks, width, tides, bridges, and flow direction need a future overlay/network.
- Generator provenance/settings are creation-time inputs and are not stored as history; custom type definitions persist because they remain paintable, while base type/custom ID/height stay terrain authority.
- Beach/Cliff placement is explicit; the editor does not yet require or generate valid shoreline adjacency.
- Automatic coast derives its 10%-deep water edge from immediate cardinal water neighbours only; it does not insert sand, model tides/erosion, curve shorelines, or add sub-tile authoring.
- Heights use whole metres and the signed `Int16` range.
- The canvas is 2D; there is no 3D preview or game-engine importer yet.
- Editing is local and single-user, without autosave, collaboration, cloud persistence, or a crash-recovery journal.
- Mouse input is required for map stamping; keyboard shortcuts cover commands but not tile traversal or painting.

The accepted [[docs/Reference/Campaign Tile Taxonomy v3|Tile Taxonomy v3]] addresses the remaining mixed palette by adding Wetland, Tundra, and BarrenRock; deriving terrain form; moving River to an overlay; and moving Beach/Cliff to shore edges. Version 2 now has full-tile Desert and Steppe values. Desert maps one-to-one to the future base surface; Steppe maps to Grassland until a later biome/climate layer owns the finer ecological distinction. Its isolated engine-neutral Phase 1 domain and validation are implemented under `src/World.Core/Campaign/V3`. The current executable, editor UI, and project files remain version 2.

Future resources, roads, detailed biomes, geology, climate, settlements, advanced hydrology, and engine importers should consume the same tile coordinate and derived-surface contract without hiding extra meaning inside the height value.

## Documentation

Start at [[docs/index|the documentation index]]. The vault uses ordinary Markdown and relative Obsidian wikilinks.
