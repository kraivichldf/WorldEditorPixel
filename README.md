# WorldEditorPixel

WorldEditorPixel contains **Kingdom World Editor**, a standalone Windows app for building large campaign worlds from exact square tiles.

Each tile can store:

- one terrain type and one whole-metre centre height;
- zero or more resource occurrences with potential and authoring locks;
- exactly one static Tile Season with an authoring lock.

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
6. Save the editable project or export a deterministic `.kworld` game-data package.

Nothing generated becomes authoritative until you explicitly accept its preview.

## Exact campaign scale

World dimensions and campaign tile size are entered in kilometres. Heights are stored in metres.

For example:

```text
World: 700 × 700 km
Tile:  5 × 5 km
Grid:  140 × 140 = 19,600 complete tiles
```

The editor never creates partial edge tiles. The supported maximum generated grid is `500 × 500 = 250,000` tiles.

## The three editable layers

| Layer | Authoritative value | Main tools |
|---|---|---|
| Terrain | One complete-tile type, optional custom-land ID, and centre height | Paint, Paint Area, rivers, automatic coasts, elevation helpers |
| Resources | Zero or more stable resource IDs, each with potential `1..100` and a lock | Add/update, erase, custom definitions, preview-first regeneration |
| Seasons | Exactly one built-in/custom Tile Season ID and a lock | Paint, Reset, Lock/Unlock, custom definitions, preview-first generation |

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

Regenerating an open world uses the same preview-first rule. If its grid changes, the preview reports resource and Season remaps, merges, lock conflicts, and out-of-bounds drops before acceptance.

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

## Static Tile Seasons

Every tile—including water and Unassigned tiles—has exactly one Tile Season.

Built-ins are Spring, Summer, Autumn, and Winter. Projects may add custom definitions such as Monsoon, Wet Season, or Dry Season with a portable built-in fallback.

Tile Seasons are static classifications. There are no months, calendar, automatic progression, or weather simulation.

Season generation uses:

- one reproducible Season Seed;
- Whole-globe or Regional latitude coverage;
- axial tilt and a coherent global seasonal phase;
- elevation cooling;
- Sea/Lake moderation;
- moisture, water distance, wind, and rain-shadow support;
- an explicit top-to-bottom first-match priority with a final Catch-all.

The generation dialog compares **Current — unchanged** with **Candidate — not applied**. Locked tiles and tiles outside a selected rectangle remain exact. Changing generation inputs keeps the previous image visible but disables **Use seasons** until a fresh candidate is generated.

## Essential controls

| Action | Control |
|---|---|
| New world | `Ctrl+N` |
| Open / Save / Save As | `Ctrl+O` / `Ctrl+S` / `Ctrl+Shift+S` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Regenerate world | `Ctrl+R` |
| Regenerate resources | `Ctrl+Shift+R` |
| Generate Tile Seasons | `Ctrl+Shift+G` |
| Paint | Left-click or left-drag |
| Pin and inspect a tile | Right-click |
| Pan | Middle-drag |
| Zoom around pointer | Mouse wheel |
| Fit world | `F` |
| Cancel the active stroke | `Escape` |

One drag creates one Undo entry. Pressing `Escape` during a stroke restores every tile touched by that unfinished drag.

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

Older projects without Season sidecars open as a clean, unlocked Spring layer. Version-1 sample/chunk projects can be imported without modifying their source folders.

## Runtime export

**Export Runtime Data** creates a deterministic `.kworld` version-3 ZIP package for a Unity importer, Unreal importer, or build pipeline.

It contains:

```text
tiles.bin
resource-index.bin
resource-records.bin
season-tiles.bin
manifest.json
```

The manifest describes grid scale, coordinate orientation, binary layouts, stable terrain/resource/Season mappings, custom fallbacks, and SHA-256 values. Authoring locks, generation settings, diagnostics, and preview data are intentionally excluded.

The package is a game-development interchange format, not another editable project. Convert it into native engine assets during import instead of decompressing it repeatedly during gameplay.

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
  -o artifacts/publish/seasons
```

Then run:

```text
Launch Tile Editor.cmd
```

The launcher targets `artifacts/publish/seasons/World.Editor.exe`.

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
        └── deterministic .kworld package
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
- Map stamping and pinning remain mouse-led; standard menus and dialogs retain native keyboard behavior.
- There is no autosave, collaboration, cloud storage, or crash-recovery journal.
- Runtime package version 3 is documented and tested, but a Unity or Unreal importer is not included yet.
- The experimental layered terrain model exists in `World.Core` only; the current editor and project terrain still use version 2.
