# ADR-0033: Keyboard-Accessible Campaign Canvas

- **Status:** Accepted
- **Date:** 2026-08-23
- **Owners:** WorldEditorPixel

## Context

Menus and dialogs were keyboard-operable, but the primary authoring surface required a mouse for tile movement, stamping, and pin/inspect. The focusable canvas handled only `Escape`, so a keyboard-only designer could create a world but could not perform the central Terrain, Resource, or Season workflow.

Keyboard editing must preserve the existing product rules: one complete campaign tile is the authoring resolution, the active Paint Area clips at world bounds, each operation produces one shared-history command, and pointer behavior remains unchanged.

## Decision

`WorldCanvas` is a Tab stop with an automation name/help description and a persistent high-contrast keyboard cursor.

- Arrow keys move the cursor by one cardinal campaign tile.
- When the cursor would leave the current viewport, the viewport follows just enough to keep the complete target tile visible.
- `Enter` applies the active Terrain, Resource, or Season tool at the cursor through the same stroke builders, validation, area clipping, topology rules, completion events, and Undo/Redo history used by pointer input.
- `Space` pins the cursor tile and raises the same inspection selection as right-click.
- Changing worlds clears the keyboard cursor; focusing a world initializes it from the pinned/hovered tile when available, otherwise from the grid centre.
- A gold outline remains visible over the exact active footprint, with a minimum screen footprint when a fitted large world makes one logical tile smaller than a few pixels.

One `Enter` press is one completed command. Keyboard input does not emulate a held pointer drag and cannot leave a half-open stroke.

## Consequences

- Terrain, Resource, and Season authoring plus tile inspection are available without a pointing device.
- Existing pointer click/drag, right-click pin, middle-drag pan, wheel zoom, `F` fit, and `Escape` cancellation remain intact.
- The keyboard cursor also drives the existing hover/inspector projection, so coordinate and tile facts use the same source as pointer hover.
- Freeform pan and pointer-centred zoom remain pointer interactions; arrow navigation automatically follows its tile, and `F` remains the keyboard route to a whole-world view.
- Headless native tests cover keyboard movement, all three active-layer stamp routes, pinning, automation help, and maximum-grid fit/render.

## Rejected alternatives

### Add keyboard shortcuts only to the main window

Rejected because the canvas owns coordinates, viewport transform, active footprint, and stroke lifecycle. Main-window shortcuts would duplicate or bypass those rules.

### Make Space paint and add a letter shortcut for pinning

Rejected because `Enter` is the explicit apply action and `Space` provides a direct, memorable equivalent to right-click selection without hidden mnemonic state.

### Create one focusable UI element per tile

Rejected because up to `250,000` logical tiles must not become `250,000` controls or automation peers. One canvas focus target plus one cursor preserves bounded rendering.
