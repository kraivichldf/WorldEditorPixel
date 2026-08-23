# Kingdom World Editor Docs

This is an Obsidian-compatible documentation section: notes use relative wikilinks while remaining readable as ordinary Markdown.

## Start here

- [[Guides/Using the Terrain Editor|Using the Terrain Editor]]
- [[Architecture/World Terrain Editor|World Terrain Editor architecture]]
- [[Reference/World File Format|World file format]]
- [[Reference/Campaign World Generation|Campaign world generation formulas]]
- [[Reference/Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3 and Phase 1 status]]
- [[Reference/Campaign Resource Layer Plan|Campaign resource layer plan and Phase 1 status]]
- [[Reference/Campaign Season Occurrence Layer Plan|Campaign Season Occurrence Layer accepted implementation plan]]
- [[Reference/Application Icon|Application icon source, build integration, and verification]]
- [[Releases/1.0|WorldEditorPixel 1.0 release notes]]
- [[Releases/1.0.1|WorldEditorPixel 1.0.1 release notes]]
- [[Decisions/ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004: Tile-authoritative campaign surface]]
- [[Decisions/ADR-0005 - Water and River Tile Topology|ADR-0005: Water and river tile topology]]
- [[Decisions/ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006: Procedural materials and directional coasts]]
- [[Decisions/ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007: Layered campaign tile taxonomy v3]]
- [[Decisions/ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008: Deterministic editable campaign world generation]]
- [[Decisions/ADR-0009 - Safe Custom Land Variants|ADR-0009: Safe custom land variants]]
- [[Decisions/ADR-0010 - Tectonic Erosion and Hierarchical Drainage|ADR-0010: Tectonic erosion and hierarchical drainage]]
- [[Decisions/ADR-0011 - Physical Terrain Noise and Boundary-Aligned Ridges|ADR-0011: Physical terrain noise and boundary-aligned ridges]]
- [[Decisions/ADR-0015 - Preview-First Current World Regeneration|ADR-0015: Preview-first current world regeneration]]
- [[Decisions/ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016: Orthogonal campaign resource layer]]
- [[Decisions/ADR-0017 - Resource Terrain Queries Diagnostics and History|ADR-0017: Resource terrain queries, diagnostics, and history]]
- [[Decisions/ADR-0018 - Resource Persistence and Runtime Package v2|ADR-0018: Resource persistence and runtime package v2]]
- [[Decisions/ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019: Manual resource workspace vertical slice]]
- [[Decisions/ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020: Preview-first procedural resource generation]]
- [[Decisions/ADR-0021 - Reviewed Changed-Lattice Resource Remapping|ADR-0021: Reviewed changed-lattice resource remapping]]
- [[Decisions/ADR-0022 - Custom Resource Definition Management|ADR-0022: Custom resource definition management]]
- [[Decisions/ADR-0023 - Hierarchical Continental World Generation|ADR-0023: Hierarchical continental world generation]]
- [[Decisions/ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024: Scale-hierarchical directional coasts]]
- [[Decisions/ADR-0025 - Built-in Steppe Terrain|ADR-0025: Built-in Steppe terrain]]
- [[Decisions/ADR-0026 - Soft Avoided Resource Terrain Factors|ADR-0026: Soft avoided resource terrain factors]]
- [[Decisions/ADR-0027 - Hard Resource Surface Exclusions|ADR-0027: Hard resource surface exclusions]]
- [[Decisions/ADR-0028 - Resource Spawn Opportunity Calibration|ADR-0028: Resource spawn opportunity calibration]]
- [[Decisions/ADR-0029 - Explicit Resource Generation Selection|ADR-0029: Explicit resource generation selection]]
- [[Decisions/ADR-0030 - Preview-First Campaign Season Occurrences|ADR-0030: Preview-first Campaign Season Occurrences]]
- [[Decisions/ADR-0031 - Single-File JSON Runtime Export|ADR-0031: Single-file JSON runtime export]]
- [[Decisions/ADR-0032 - Shared Editable World Tile Limit|ADR-0032: Shared editable world tile limit]]
- [[Decisions/ADR-0033 - Keyboard-Accessible Campaign Canvas|ADR-0033: Keyboard-accessible campaign canvas]]
- [[Decisions/ADR-0002 - Delta-Based Terrain History|ADR-0002: Delta-based edit history]]
- [[Testing/Verification|Verification]]

## Historical decisions

- [[Decisions/ADR-0001 - Unique Chunk Ownership|ADR-0001: Unique chunk ownership]] — version-1 sample storage, superseded for active authoring.
- [[Decisions/ADR-0003 - Sparse Campaign Tile Types|ADR-0003: Sparse campaign tile types]] — separate type overlay, superseded by ADR-0004.

## Current milestone

`Create exact tile grid -> optionally preview an editable seeded terrain start -> accept the exact result -> stamp textured base/custom type + centre height -> paint sparse Resources -> add/erase/lock zero or more Season IDs per complete tile -> inspect exact authority -> undo/redo all three layers in one history -> save/reopen the complete project -> export compact .kworld or readable .world.json runtime data`

The campaign tile is the only authoring resolution. Sea/Lake water, original land/custom tiles with automatic 10% water-facing edges, full-tile Beach/Cliff, River/Large River paths, and three-exit River Junctions are current terrain classifications. Sparse campaign resources are a separate authority at the same coordinates: multiple different IDs may coexist, each with exact potential `1..100` and an authoring lock. Season Occurrences are another orthogonal sparse authority: every tile has a set containing zero or more built-in/custom Season IDs, each with its own lock, and no month or clock. The editor can preview-first regenerate Resources or Season Occurrences against accepted terrain, accepts generated terrain and its Season candidate atomically, and reviews changed-lattice Resource and Season impact before replacement. Gameplay, 3D rendering, engine integration, roads, detailed biomes, persistent weather, settlements, and advanced hydrology remain later explicit systems.

## Campaign resources — manual + preview-first generation implemented

[[Reference/Campaign Resource Layer Plan|Campaign Resource Layer Plan]] records the complete accepted resource design. ADR-0016 through [[Decisions/ADR-0029 - Explicit Resource Generation Selection|ADR-0029]] now ship the manual, generation, changed-lattice, custom-definition, soft terrain-avoidance, hard normalized-surface safety, calibrated spawn-opportunity, and explicit Include/Exclude path in the native editor: built-in/custom catalog loading and management, Terrain/Resources workspaces, selected-resource heatmap and exact labels, complete-tile add/update/erase, default locks, pinned warnings/actions, shared history, staged project save/reopen, deterministic climate/geology-backed generation, deliberate mixed-subset replacement, side-by-side resource comparison, and exact reviewed full-world terrain/resource replacement. Overview symbols, field diagnostics, and full World/Terrain/Resources property pages remain pending.

## Campaign Season Occurrences — implemented

[[Reference/Campaign Season Occurrence Layer Plan|Campaign Season Occurrence Layer Plan]] and [[Decisions/ADR-0030 - Preview-First Campaign Season Occurrences|ADR-0030]] define the sparse Season Occurrence authority with Spring/Summer/Fall/Winter plus safe custom definitions. Every enabled rule evaluates independently, allowing several Season IDs on one tile. The implementation covers domain, generation reports, persistence/export, Terrain/Resources/Seasons workspace switching, Add selected/Erase selected/Lock/Unlock, composite labels and color, pinned authority, shared history, protected custom-definition management, Current/Candidate regeneration, exact acceptance, cached climate/rule/staleness diagnostics, generated-world candidates, empty blank-world defaults, changed-lattice physical-centre lock remapping, maximum-grid coverage, and a self-contained Windows build. It deliberately has no month, calendar, automatic time progression, resource dependency, winner, catch-all, or terrain rewrite.

## Accepted next milestone — Phase 1 core implemented

[[Reference/Campaign Tile Taxonomy v3|Tile Taxonomy v3]] keeps one base surface and centre height per tile, derives terrain form, moves River into a directed network overlay, and makes Beach/Cliff sparse shore-edge treatments. Its minimum surfaces are Unassigned, Grassland, Forest, Desert, Wetland, Tundra, BarrenRock, Sea, and Lake. The isolated engine-neutral domain and validators are implemented and tested. Version 2 remains the running application and file format until persistence, migration, tools, rendering, and history ship as the complete v3 boundary.

## Source map

- `src/World.Core`: active version-2 campaign world, resource authority/diagnostics/commands/persistence, interpolation, validation, the retained version-1 importer, the isolated `Campaign/V3` terrain Phase 1 domain, campaign-season authority/generator/sidecars, compact runtime-v3 package export, and streamed runtime JSON export.
- `src/World.Editor`: Avalonia Terrain/Resources/Seasons shell, shared canvas/history, custom terrain/resource/season managers, preview-first terrain/resource workflows, and the season-aware staged load/save plus `.kworld`/JSON export boundaries.
- `src/World.Tests`: executable version-1/version-2 contracts, hierarchical Continental-world ADR-0023, maximum-size directional Coast ADR-0024, built-in Steppe ADR-0025 coverage, terrain-taxonomy version-3 and campaign-resource ADR-0016 through ADR-0029 coverage, plus campaign-season ADR-0030 Slices 1-7 including headless native dialogs and the `250,000 x 256` diagnostic.
