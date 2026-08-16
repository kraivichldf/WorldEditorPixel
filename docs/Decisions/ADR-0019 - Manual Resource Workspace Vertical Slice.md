# ADR-0019: Manual Resource Workspace Vertical Slice

- Status: Implemented
- Date: 2026-08-15

## Context

[[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]] through [[ADR-0018 - Resource Persistence and Runtime Package v2|ADR-0018]] provide validated resource authority, terrain diagnostics, shared delta history, sparse project files, and runtime package version 2 without exposing them in the running editor. The next slice must make that foundation usable as one complete manual authoring journey. It must not publish resource files that the user cannot see, edit, validate, undo, reopen, and export.

The editor also needs a safe document boundary. Terrain and resources are separate authorities, but opening, saving, replacing, and exporting them independently could leave the visible document or project folder in a mixed state.

> [!NOTE]
> [[ADR-0021 - Reviewed Changed-Lattice Resource Remapping|ADR-0021]] now implements the later reviewed remap boundary described below. The original block remains here as the historical safety decision for this slice.

## Decision

### One editor document, two authorities

`EditorViewModel` owns one `CampaignWorld`, one definition-compatible `CampaignResourceMap`, nullable `CampaignResourceGenerationSettings`, and one shared `CommandHistory`. Creating a world installs the built-in catalog with an empty resource map. Opening a version-2 project loads and validates terrain first, then resources, and replaces the current document only after both candidates are valid. Missing resource files remain a clean empty layer. Legacy version-1 imports never attach sibling resource files from the source folder.

The main editor adds explicit **Terrain** and **Resources** workspaces. They share the map transform, campaign grid, hover coordinate, and right-click pinned tile. Switching workspace changes only tools and projections; it does not mutate terrain or resources.

### Manual resource tools

The Resources rail exposes:

- Renewable/Finite category filtering and one stable-ID resource selector;
- potential `1..100`;
- an independent complete-tile Paint Area from `1 x 1` through `25 x 25`;
- **Add / update** and **Erase selected** paint tools;
- default-on **Lock manual edits**.

A left click or drag applies only the selected resource ID over the complete clipped footprint. Add/update writes the selected potential and lock state. Erase removes that ID only. Other resources, terrain type, custom terrain identity, centre height, Rivers, and shores remain unchanged. One drag uses `CampaignResourceStrokeBuilder` and becomes one already-applied command in the existing shared history. Escape restores the stroke. Empty strokes do not clear Redo.

The pinned Resources inspector lists every occurrence on the pinned tile in stable-ID order with name, category, exact potential, lock state, and hard-rule warning text. It can adopt a pinned occurrence as the active resource, lock or unlock it, and erase it through ordinary resource commands. Manual out-of-profile placement remains valid authority; warnings never erase or rewrite it. Hover always reports the exact selected-resource value when present.

### Resource map projection

Resources view mutes but retains the terrain surface. Selecting one resource renders a fixed `1..100` heatmap using that definition's portable color. At approximately `28 px/tile`, exact potential numbers appear in the cells. The campaign grid, pinned outline, and paint footprint remain separate overlays. Rendering samples only a bounded visible snapshot and keys the raster cache by terrain revision, resource revision, selected ID, viewport, and display mode; pointer motion alone does not rebuild the raster.

### Project save, reopen, and runtime export

The editor adds a project-level persistence coordinator. It writes both existing serializers into a unique sibling staging directory, reloads the staged project, checks captured terrain/resource revisions, then commits only the known managed files with backups and rollback on ordinary I/O failure. Save marks the document clean only after the combined commit succeeds. Save As replacement detection includes every managed terrain and resource filename. The project still does not claim power-loss atomicity across several filesystem entries.

Runtime export always calls the resource-aware overload once this workspace ships. Therefore editor export produces deterministic `.kworld` version 2 even when the resource map is empty. Export remains derived data and never changes project identity or dirty state.

### Terrain regeneration boundary

Accepting a terrain regeneration with the same physical lattice—world width, world height, and campaign tile size—rebinds and preserves every resource occurrence and setting, then refreshes diagnostics. A changed lattice may proceed only when the resource map is empty. If any occurrence exists, acceptance is blocked and the current document remains untouched until the later resource-remap preview can report locked remaps, merges, drops, regenerated unlocked resources, and suitability changes before commitment. No occurrence is silently retained at a different physical position or silently erased.

Every accepted replacement clears the one shared Undo/Redo history, keeps the current project/import identity, and marks the document modified.

## Consequences

- The first resource product slice completes `paint -> inspect/warn -> lock -> undo/redo -> save -> reopen -> export` without waiting for procedural generation.
- Custom definitions already present in valid project files are selectable and round-trip, while the custom-resource manager remains part of the later generation/settings property pages.
- Resource generation, regeneration preview, climate/geology field views, overview symbols for multiple resource types, and custom-resource creation remain outside this slice.
- Blocking a changed-grid regeneration with populated resources is intentionally conservative; the accepted remap-preview contract remains the next safe expansion.
- Terrain-only projects remain compatible, while all new editor exports advertise the explicit runtime resource layer.

This decision extends [[ADR-0015 - Preview-First Current World Regeneration|ADR-0015]], [[ADR-0017 - Resource Terrain Queries Diagnostics and History|ADR-0017]], and [[ADR-0018 - Resource Persistence and Runtime Package v2|ADR-0018]].

## Implementation evidence

Implemented on 2026-08-15 in the native Avalonia editor. The authoritative seams are `EditorViewModel` for the paired terrain/resource document and shared history, `WorldCanvas` for bounded selected-resource projection and strokes, and `CampaignEditorProjectSerializer` for staged terrain/resource save, reopen validation, and runtime version-2 export. Automated verification passes `317/317` tests with a zero-warning Release build. Native evidence is recorded in `.impeccable/review/resources-workspace-confirmed.png` and `.impeccable/review/resources-pinned-diagnostics-confirmed.png`.
