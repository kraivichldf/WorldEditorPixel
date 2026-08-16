---
version: 1
slug: "src-world-editor-mainwindow-axaml"
primary_target: "src/World.Editor/MainWindow.axaml"
related_targets: ["src/World.Editor/App.axaml","src/World.Editor/Controls/WorldCanvas.cs","src/World.Editor/Dialogs/NewWorldDialog.axaml","src/World.Editor/Dialogs/CustomTerrainTypesDialog.axaml","src/World.Editor/Dialogs/ChoiceDialog.axaml"]
---

# Main Editor Surface

## Scope and mode

Primary desktop campaign authoring window and supporting dialogs. **Operate** mode: preserve command set, state model, accessibility labels, and terrain-canvas behavior while documenting the completed Windows 98 Property Workshop visual form.

## User, job, and constraints

Strategy/world editors create and reopen deterministic campaign worlds on desktop. They select one terrain identity, one centre height, and one complete-tile paint area per stroke, then inspect, save, and export.

Constraint set:
- One authoring unit is exactly one complete tile.
- Type and centre height are always edited together.
- No sub-tile brush, no secondary height authority, no icon-only critical actions.
- Save is persistence; export is package generation.

## Approved direction

**Windows 98 Property Workshop.** Comp: `.impeccable/mocks/win98-property-workshop.png`.

- System-gray surfaces.
- Navy captions and compact section headers.
- Square controls with raised/sunken bevel language.
- Deep sunken primary viewport.
- Grouped stamp controls and compact inspector.
- Segmented status wells.

This direction is treated as implementation authority for the surface contract.

## Implementation inventory

| Visible ingredient | Commitment | Medium |
|---|---|---|
| Window, menu, toolbar | Windows-98 workbench command bands with native command cadence | Semantic Avalonia XAML + global styles |
| Campaign stamp rail | Grouped selector + height + paint-area controls, compact guidance | Native selectors, borders, numeric fields |
| Campaign terrain | Materials, bilinear derived surface, rivers, coast edges, grid, overlays | Existing `WorldCanvas` renderer |
| Viewport | Deep sunken map frame, no decorative card shell | Avalonia border around `WorldCanvas` |
| Inspector | Compact grouped value panels and helper actions | Semantic Avalonia XAML |
| Buttons and inputs | Square bevel controls and explicit disabled/active states | Reusable global styles |
| Status bar | Segmented sunken measurement wells | Avalonia grid + borders |
| Dialogs | Matching sheet style and fixed action footers | Shared styles and existing dialog XAML |
| Regeneration | Visible toolbar/Terrain command opens the existing preview sheet with editable definition values, current custom catalog, and a bounded textual resource-impact review | Semantic Avalonia command + `NewWorldDialog` regeneration mode |
| Custom resources | Resources menu/rail opens one protected catalog manager with custom list/templates left, scrollable definition groups right, validation state, and fixed Apply/Cancel footer | Semantic Avalonia dialog + atomic view-model catalog replacement |
| Elevation labels | Whole-metre labels rendered at tile centres, white outlined marker style | Overlay path in `WorldCanvas` |

## Verified render and behavior facts

- Elevation colour squares are removed from tile raster material.
- Height is shown via white-outline whole-metre numbers at each tile centre.
- Overlay is default-on and shown in both toolbar and View menu.
- Overlay auto-hides when tile size falls below 28 px/tile.
- Overlay has no effect on stored data or the derived-surface contract.
- Terrain textures and map shading remain visual context and continue to reflect derived bilinear height where enabled.
- The viewport command text line sits above the map and is the dominant control/readout surface for live mode and action state.
- Regeneration remains preview-first: the current canvas stays authoritative until **Use this world**, while the sheet exposes editable grid/elevation inputs initialized from the current world, custom types, generator controls, stale/current preview state, and replacement consequences.
- The regeneration sheet's scrollable **Resource impact** well states exact same-lattice preservation or changed-lattice movement, merge, out-of-bounds drop, lock, and regenerated-unlocked counts. Locked drops include stable resource IDs and source coordinates, so the protected-data consequence is visible without color.
- The custom-resource manager keeps built-ins immutable, offers duplication as a starting point, exposes usage locks in text, and states that Apply preserves compatible occurrences while clearing history. Its list/form/footer composition inherits the existing Windows 98 property-workshop surface rather than creating a new visual language.

## Surface constraints to preserve

1. Campaign tile is the only authoring atom.
2. One complete-tile cursor footprint (Paint Area expands an odd square of full tiles).
3. No sample/stamp radius, falloff, or sub-tile brush geometry.
4. Save/Export remain distinct actions and distinct states.
5. New world generation remains preview-first; only `Use this world` commits preview to document.
6. Current-world regeneration uses that same gate, preserves project identity, installs the exact reviewed terrain/resource candidate, and clears history only after acceptance.
7. Export remains a separate artifact writer (no dirty-state mutation).
8. Accessibility and validation messaging stay visible through controls, status wells, and explicit disabled states.
9. The map is the dominant visual priority; controls remain compact and non-encroaching.
10. Catalog replacement must validate before mutation, protect used identity/category, preserve compatible occurrence values exactly, and disclose history clearing before Apply.

## Obsidian cross-links

- [[PRODUCT.md]]
- [[docs/Reference/Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]]
- `.impeccable/review/desktop-elevation-numbers.png`
- `.impeccable/review/desktop-regenerate-world.png`
- [[docs/Decisions/ADR-0022 - Custom Resource Definition Management|ADR-0022]]

## Unresolved decisions

None.
