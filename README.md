# WorldEditorPixel

WorldEditorPixel is a standalone Windows app for building large campaign worlds from exact square tiles.

## Download version 1.0.1

Download the self-contained Windows x64 executable and checksum from [WorldEditorPixel 1.0.1](https://github.com/kraivichldf/WorldEditorPixel/releases/tag/v1.0.1). It runs on Windows 10 or 11 without a separately installed .NET runtime. Windows may show a SmartScreen warning because the build is not code-signed.

Each tile can store:

- one terrain type and one whole-metre centre height;
- zero or more resource occurrences with potential and authoring locks;
- zero or more Season Occurrences, each identified by a stable Season ID and protected by its own authoring lock.

Neighbouring centre heights are blended into a continuous surface automatically. Generated terrain, resources, and seasons remain fully editable after you accept their previews.

## Quick start

Requirements:

- Windows 10 or 11;
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

From the repository root:

```powershell
dotnet restore WorldEditorPixel.sln
dotnet run --project src/World.Editor/World.Editor.csproj -c Release
```

To build and test everything:

```powershell
dotnet build WorldEditorPixel.sln -c Release --no-restore
dotnet test WorldEditorPixel.sln -c Release --no-build --no-restore
```

## Typical workflow

1. Create a Blank world or choose a generated world shape.
2. Review the temporary Terrain and Seasons preview.
3. Adjust the seed or settings until the result is satisfactory.
4. Choose **Use this world** to accept that exact candidate.
5. Paint terrain, resources, and static seasons on the shared map.
6. Save the editable project, export a compact `.kworld` package, or export one readable `.world.json` game-data file.

Nothing generated becomes authoritative until you explicitly accept its preview.

## Exact campaign scale

World dimensions and campaign tile size are entered in kilometres. Heights are stored in metres.

For example:

```text
World: 700 × 700 km
Tile:  5 × 5 km
Grid:  140 × 140 = 19,600 complete tiles
```

The editor never creates partial edge tiles. Every Blank, generated, opened, imported, saved, rendered, and exported world is limited to `250,000` editable tiles; `500 × 500` is the square maximum example.

## The three editable layers

| Layer | Authoritative value | Main tools |
|---|---|---|
| Terrain | One complete-tile type, optional custom-land ID, and centre height | Paint, Paint Area, rivers, automatic coasts, elevation helpers |
| Resources | Zero or more stable resource IDs, each with potential `1..100` and a lock | Add/update, erase, custom definitions, preview-first regeneration |
| Seasons | Zero or more built-in/custom Season IDs, each with its own lock | Add selected, erase selected, Lock/Unlock, custom definitions, preview-first generation |

All three workspaces share the same pan, zoom, grid, hover position, pinned tile, and Undo/Redo history.

## Terrain and world generation

Create a Blank world or generate an editable starting point from:

- Continental World;
- Island;
- Archipelago;
- East, West, North, or South Coast;
- Sea in Center;
- Land Only.

Generation is deterministic for the same settings and seed. Controls include terrain ruggedness, mountain density, hydrology, tidal inlets, coastline character, and optional inland terrain ratios.

The generator builds connected, large-scale geography: continents, coastlines, island groups, mountain ranges, lakes, and river networks. It does not scatter each tile independently.

Terrain editing includes:

- Unassigned, Plains, Steppe, Desert, Forest, Hills, Mountain, Sea, Lake, River, Large River, Beach, and Cliff;
- up to twelve custom land types based on safe land materials;
- complete-tile Paint Areas from `1 × 1` through `25 × 25`;
- whole-metre centre heights with automatic slopes between neighbouring centres;
- deterministic grass, dry grass, forest, rock, sand, and water textures;
- automatic 10%-deep Sea/Lake edges on cardinally adjacent non-water tiles;
- connected River paths and a collision-safe split tool for two, three, or four branches.

Regenerating an open world uses the same preview-first rule. If its grid changes, the preview reports resource and Season remaps, same-ID merges, and out-of-bounds locked occurrences before acceptance. Different Season IDs may coexist on the same target tile.

## Campaign resources

Resources are independent from terrain. Several different resource IDs may coexist on one tile.

You can:

- paint exact potential values from `1` through `100`;
- lock deliberate manual placements;
- create custom Renewable or Finite resources;
- duplicate a built-in resource as a starting point;
- configure coverage, richness, concentration, terrain/water ranges, preferred factors, avoided factors, and hard surface exclusions;
- inspect every occurrence and warning on a pinned tile;
- regenerate an explicit Included subset while Excluded resources remain unchanged;
- compare synchronized Current and Candidate maps before acceptance.

Coverage is independent for each resource. It is not a combined 100% terrain ratio, and unsuitable geography may legitimately produce fewer occurrences than requested.

## Season Occurrences

Every tile—including water and Unassigned tiles—has a Season Set containing zero or more Season Occurrences. For example, one tile may contain Spring, Summer, and Fall while another contains all four built-ins.

Built-ins are Spring, Summer, Fall, and Winter. Projects may add custom definitions such as Monsoon, Wet Season, or Dry Season with a portable built-in fallback.

Season Occurrences describe which seasons exist on a tile. They are not a current season or a calendar: there are no months, automatic progression, or weather simulation.

Season generation uses:

- one reproducible Season Seed;
- Whole-globe or Regional latitude coverage;
- axial tilt and a coherent global seasonal phase;
- elevation cooling;
- Sea/Lake moderation;
- moisture, water distance, wind, and rain-shadow support;
- independent evaluation of every enabled Season Definition, so every environmental match can coexist on the tile.

The generation dialog compares **Current — unchanged** with **Candidate — not applied**. Locked occurrences and occurrences outside a selected rectangle remain exact. Changing generation inputs keeps the previous image visible but disables **Use seasons** until a fresh candidate is generated.

## Essential controls

| Action | Control |
|---|---|
| New world | `Ctrl+N` |
| Open / Save / Save As | `Ctrl+O` / `Ctrl+S` / `Ctrl+Shift+S` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Regenerate world | `Ctrl+R` |
| Regenerate resources | `Ctrl+Shift+R` |
| Generate Tile Seasons | `Ctrl+Shift+G` |
| Move the keyboard tile cursor | Focus the canvas, then use arrow keys |
| Paint with the active Terrain/Resource/Season tool | `Enter` on the focused canvas, or left-click/drag |
| Pin and inspect a tile | `Space` on the focused canvas, or right-click |
| Pan | Middle-drag |
| Zoom around pointer | Mouse wheel |
| Fit world | `F` |
| Cancel the active stroke | `Escape` |

One drag creates one Undo entry; each keyboard `Enter` creates one equivalent complete-tile command. Pressing `Escape` during a pointer stroke restores every tile touched by that unfinished drag.

## Project files

An editable project is a folder. Terrain uses version-2 project files, with optional resource files and complete Season sidecars:

```text
MyWorld/
├── world.json
├── campaign-tiles.json
├── custom-terrain.json          optional
├── resource-definitions.json    optional
├── resource-generation.json     optional
├── resource-tiles.json          optional
├── season-definitions.json
├── season-generation.json       optional accepted recipe
└── season-layer.bin
```

Save stages terrain, resources, and seasons together before replacing the existing project files. A failed save does not silently install a partial project.

Older projects without Season sidecars open with a clean, empty Season Occurrence layer. Version-1 sample/chunk projects can be imported without modifying their source folders.

## Runtime export

WorldEditorPixel offers two one-file game-development exports:

- **Export Runtime Data** (`Ctrl+E`) creates a compact deterministic `.kworld` version-3 ZIP package.
- **Export JSON Data** (`Ctrl+Shift+E`) creates one readable UTF-8 `*.world.json` document.

The JSON document includes world scale, grid orientation, catalogs, and every row-major tile with its terrain/custom identity, centre height, Resource ID/potential pairs, and complete Season ID set. It deliberately omits authoring locks, generator recipes, diagnostics, and preview state. A game importer should require `format: "world-editor-pixel-runtime-json"` and `version: 1`, validate the declared counts and IDs, then convert the data into native engine assets.

The `.kworld` package contains:

It contains:

```text
tiles.bin
resource-index.bin
resource-records.bin
season-index.bin
season-records.bin
manifest.json
```

The manifest describes grid scale, coordinate orientation, binary layouts, stable terrain/resource/Season mappings, custom fallbacks, and SHA-256 values. Authoring locks, generation settings, diagnostics, and preview data are intentionally excluded.

Both files are game-development interchange formats, not editable projects. JSON is easiest to inspect and integrate; `.kworld` is smaller and faster for large production worlds. Convert either one into native engine assets during import instead of parsing it repeatedly during gameplay.

## Self-contained Windows build

Create a Windows executable that does not require a separately installed .NET runtime:

```powershell
dotnet restore src/World.Editor/World.Editor.csproj -r win-x64
dotnet publish src/World.Editor/World.Editor.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  --no-restore `
  -p:PublishSingleFile=true `
  -o artifacts/publish/1.0.1
```

Then run:

```text
Launch Tile Editor.cmd
```

The launcher targets `artifacts/publish/1.0.1/WorldEditorPixel.exe`.

The project embeds a multi-resolution terrain-map icon in the executable and uses the same mark for the main window. The transparent source, shipping `.ico`, frame sizes, and verification method are documented in [Application Icon](docs/Reference/Application%20Icon.md).

## Architecture

```text
World.Editor
  Avalonia Windows UI, shared canvas, dialogs, project lifecycle
        │
        ▼
World.Core
  engine-neutral terrain, resources, seasons, generation,
  commands, validation, persistence, and runtime export
        │
        ├── editable project folder
        ├── deterministic .kworld package
        └── readable .world.json file
```

`World.Core` does not depend on Avalonia or a game engine. This keeps world authority, validation, generation, persistence, and export reusable by future tools and engine importers.

Repository layout:

```text
src/World.Core/    engine-neutral world model and serialization
src/World.Editor/  Avalonia desktop application
src/World.Tests/   xUnit domain, integration, persistence, UI, and stress tests
```

## Current boundaries

- The editor is a 2D campaign-authoring tool; it does not generate tactical/FPS meshes or provide a 3D preview.
- Tile Seasons are static and do not drive time, weather, terrain, or resources.
- Resource potential is authoring data, not inventory, production, or economy simulation.
- River widths and junctions are campaign symbols; flow direction, discharge, bridges, deltas, and navigation are not yet modeled.
- Automatic coasts use immediate cardinal water neighbours and do not create sub-tile shoreline geometry.
- The focused canvas supports a visible keyboard tile cursor, arrow navigation with automatic viewport following, `Enter` stamping, and `Space` pin/inspect. Pointer drag painting, middle-drag free pan, and wheel-centered zoom remain available.
- There is no autosave, collaboration, cloud storage, or crash-recovery journal.
- Runtime package version 3 is documented and tested, but a Unity or Unreal importer is not included yet.
- The experimental layered terrain model exists in `World.Core` only; the current editor and project terrain still use version 2.
