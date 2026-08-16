---
name: Kingdom World Editor
description: Windows 98 style campaign map workstation for complete-tile terrain authoring.
colors:
  editor-background: "#C0C0C0"
  editor-panel: "#C0C0C0"
  editor-raised: "#D4D0C8"
  editor-pressed: "#A0A0A0"
  editor-border: "#808080"
  editor-text: "#000000"
  editor-muted-text: "#404040"
  editor-accent: "#000080"
  editor-accent-hot: "#000080"
  editor-window-text: "#FFFFFF"
  primary-ink: "#000000"
  selection-amber: "#E3B557"
  canvas: "#0D1317"
  overlay-panel: "rgba(255, 255, 255, 0.90)"
  campaign-unassigned: "#59666A"
  campaign-plains: "#73945D"
  campaign-desert: "#C99142"
  campaign-forest: "#2F684F"
  campaign-hills: "#8B8A62"
  campaign-mountain: "#858784"
  campaign-sea: "#1E6A8B"
  campaign-lake: "#2D8EA3"
  campaign-river: "#3B9BC1"
  campaign-large-river: "#237FA6"
  campaign-beach: "#C3A86D"
  campaign-cliff: "#6F665E"
  elevation-number: "#FFFFFF"
  elevation-number-edge: "#000000"
  blocked-action: "#FF6B6B"
  status-well: "#C0C0C0"
typography:
  body:
    fontFamily: "Tahoma, Microsoft Sans Serif, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 400
    lineHeight: 1.35
  section-title:
    fontFamily: "Tahoma, Microsoft Sans Serif, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 700
    lineHeight: 1.25
    letterSpacing: "0"
  measurement:
    fontFamily: "Courier New, Consolas, monospace"
    fontSize: "12px"
    fontWeight: 400
    lineHeight: 1.2
rounded:
  field: "0px"
  control: "0px"
  dialog: "0px"
  status-well: "0px"
spacing:
  xs: "4px"
  sm: "6px"
  md: "8px"
  lg: "12px"
  xl: "16px"
  xxl: "24px"
components:
  button-primary:
    backgroundColor: "{editor-panel}"
    textColor: "{editor-text}"
    typography: "{body}"
    rounded: "{field}"
    padding: "12px 4px"
    size: "small"
    height: "26px"
    width: "fit-content"
  button-primary-hover:
    backgroundColor: "{editor-raised}"
    textColor: "{editor-text}"
    typography: "{body}"
    rounded: "{field}"
    padding: "12px 4px"
    size: "small"
    height: "26px"
    width: "fit-content"
  button-primary-active:
    backgroundColor: "{editor-pressed}"
    textColor: "{editor-text}"
    typography: "{body}"
    rounded: "{field}"
    padding: "12px 4px"
    size: "small"
    height: "26px"
    width: "fit-content"
  text-input:
    backgroundColor: "#FFFFFF"
    textColor: "{editor-text}"
    typography: "{body}"
    rounded: "{field}"
    padding: "7px 5px"
    height: "24px"
  status-well:
    backgroundColor: "{status-well}"
    textColor: "{editor-text}"
    typography: "{body}"
    rounded: "{status-well}"
    padding: "6px 3px"
    height: "20px"
---

# Design System: Kingdom World Editor

## Overview

**Creative North Star: "Windows 98 Property Workshop"**

The map workstation is designed around one authoritative unit: one complete campaign tile. Terrain/height and sparse resource occurrences remain separate authorities at that same coordinate. The interface uses a Windows 98 workstation language around a dominant central surface.

The dominant viewport is a sunken map area with map commands above it, explicit Terrain/Resources workspace buttons, a grouped active-tool rail, a compact inspector rail, and segmented status wells. Editors can create or open a world, stamp terrain/height or one selected resource over complete tiles, inspect exact authority and diagnostics, then save or export.

Controls are compact, explicit, and non-ornamental. Texture and shading are visual aids; terrain values remain explicit in the model and inspector.

**Key Characteristics:**
- System-gray surfaces and square controls with raised/sunken states.
- One-tile atomicity as the only terrain/height authoring unit.
- One selected resource ID per resource paint operation; other resources and terrain remain untouched.
- Explicit mode and validation visibility in menus, rails, and status wells.
- Separable and inspectable semantics for stored centre height versus derived surface.

## Colors

### Neutral
- **System face** (`#C0C0C0`): shell, panel, status strip, and button base surfaces.
- **Raised surface** (`#D4D0C8`): input borders, active control faces, and fixed list/table surfaces.
- **Border/shadow** (`#808080`): control and panel edging.
- **Text** (`#000000`): normal labels and form body copy.
- **Muted text** (`#404040`): secondary help text.

### Primary
- **Active title/accent** (`#000080`): active section captions and emphasis.
- **Accent text on accent** (`#FFFFFF`): label copy where active accent backgrounds are used.
- **Pressed** (`#A0A0A0`): depressed command state.
- **Accent-hot** (`#000080`): hover/active emphasis states.

### Secondary
- **Campaign map shell** (`#0D1317`): canvas underlay for raster/terrain output.
- **Campaign white window** (`#FFFFFF`): text inputs and window surfaces.

### Campaign palette
- **Terrain swatches** (`#59666A`, `#73945D`, `#C99142`, etc.): whole-tile map identity.
- **Pinned amber** (`#E3B557`): persistent pinned tile outline/action context.
- **Blocked red** (`#FF6B6B`): invalid river/topology action and blocked stamp state.
- **Height number foreground** (`#FFFFFF`) with opaque black outline (`#000000`): centre-meter overlay labels.

### Named Rules
**Authority Rule.** Terrain, centre-height, and resource-occurrence edits are model edits; visual overlays, map textures, heatmaps, and diagnostics do not introduce extra stored authority.

### Elevation marker rule
Elevation uses white centre numbers as the explicit overlay layer. The old elevation square raster is not part of the rasterized tile map.

## Typography

Body and section text uses compact Win98 sans stack for dense desktop controls. Measurement values use fixed-width numeric typography.

### Hierarchy
- **Body** (`12px`, `400`, 1.35): normal controls, labels, and helper text.
- **Section title** (`12px`, `700`, 0 letter spacing): grouped control headers.
- **Measurement** (`12px`, `400`, 1.2): coordinates, heights, counters, and dimensions.

**Numeric Truth Rule:** comparison and transfer values use the measurement face and include units.

## Layout

The desktop shell uses:
- menu and text-command band at the top,
- a compact workspace strip,
- a grouped left rail for either terrain/height stamp settings or selected-resource/potential settings,
- a dominant campaign map viewport in a sunken frame,
- a compact right rail for pointer/pinned authority, resource warnings/actions, and world metadata,
- segmented wells in the status strip.

Primary flow:

1. Create/Open world, or preview a regeneration of the current document.
2. Configure or generate.
3. Choose Terrain or Resources without changing the shared map transform/pin.
4. Stamp complete tiles through the active grouped rail.
5. Inspect saved terrain/elevation or exact resource potential, lock state, and warnings.
6. Save document or export runtime package.

The map remains visually and operationally dominant. Rails are short and dense to protect viewport area at desktop scale.

## Elevation & Depth

Depth is rendered through legacy control articulation and explicit overlays:
- square controls with crisp borders and raised/sunken surfaces,
- sunken map viewport with clear overlay separation,
- overlay text and grid planes above terrain output.

### Named Rule
**Flat-By-Default Rule.** Depth emerges from panel state and overlay contrast, not complex shadow stacks.

## Shapes

- **Corners:** square controls (`0px`) across most widgets.
- **Command shells:** raised/sunken Win98 control geometry with explicit border states.
- **View geometry:** square footprints for cursor and pinned selection to reinforce complete-tile atomicity.
- **Rail style:** compact fixed-height rows and dense input strips.

## Components

### Buttons and inputs
- Square, tactile surfaces with explicit pressed/hover states.
- Default button face is `#C0C0C0` with visible `#808080` edge.
- Hover uses `#D4D0C8`; pressed uses `#A0A0A0`.
- Inputs use white windows background (`#FFFFFF`) with border contrast.
- Buttons are 26px minimum height, with `12px 4px` button padding as the standard interactive size.

### Campaign stamp controls
- Terrain selector, centre-height field, and paint-area control are grouped as one primary action set.
- Paint area expands stamps to full-square batches only.

### Resource paint controls
- Renewable/Finite category and resource selectors remain text-labeled beside the portable definition color.
- Potential is always an explicit `1..100` value; the independent Paint Area expands to complete clipped tiles.
- **Add / update** and **Erase selected** are mutually exclusive tools. Their button state and status text communicate the active operation without relying on color.
- **Lock manual edits** is on by default and names its regeneration meaning directly.

### Inspector
- Pointer and pinned blocks are compact and text-first.
- Pin helper actions do not alter model data; they only set active stamp height state:
  - **Copy centre**
  - **Blend around**
- Resources mode keeps terrain surface context visible, adds the selected-resource value, and lists every pinned occurrence with exact potential/category, textual lock state, hard-rule warning, and unevaluated-factor text.
- Pinned **Use selected**, **Erase**, **Lock**, and **Unlock** commands act on one occurrence only.

### Canvas
- Deterministic paint-and-overlay draw order in `WorldCanvas`:
  1. terrain raster,
  2. river layers,
  3. selected-resource heatmap when Resources is active,
  4. world boundary,
  5. optional campaign grid,
  6. context-appropriate elevation or resource-potential numbers,
  7. pinned selection,
  8. stamp cursor.
- River, Large River, and junction visuals are network-aware and directional.
- River previews are route-focused and constrained to tile topology.
- Resources mode mutes rather than removes terrain. Its heatmap uses a fixed `1..100` scale and the selected definition color; exact numbers appear at `28 px/tile` and above.

#### Derived elevation overlay
- White centre labels are visible by default.
- Toggle exposed in toolbar and View menu.
- Auto-hide below 28 px/tile.
- Overlay does not alter stored values.

### Dialogs
- New-world and custom-terrain dialogs use property-sheet-style fixed action areas and matching controls.
- Generation previews are explicit and keep settings, preview, and commit actions clear.
- Regeneration reuses the New World property sheet with a distinct title, editable definition fields initialized from the current world, no Blank option, and the same **Use this world** commit gate. Its right preview keeps terrain dominant and adds one bounded, scrollable **Resource impact** well. The well states same-grid preservation or changed-grid moved/merged/dropped/locked/regenerated counts in text, names locked out-of-bounds coordinates, and labels stale results without relying on color. Acceptance preserves project identity, installs the exact reviewed terrain/resource candidate, marks the document modified, and clears undo history.

## Do's and don'ts

### Do
- **Do** keep one-tile atomicity as the only terrain/height edit unit.
- **Do** keep controls explicit, low-noise, and square.
- **Do** keep save and export paths distinct.
- **Do** show stored vs derived values in separate labels.
- **Do** show terrain surface and selected-resource potential as separate inspector facts.
- **Do** pair resource heatmap color, lock state, and warnings with exact text.
- **Do** keep generated-world preview in review/commit mode until accepted.
- **Do** preserve project identity and current tiles while a regeneration preview is being adjusted.
- **Do** keep current resources untouched while regeneration is being adjusted, and name physical moves, same-ID merges, locked drops, and saved-recipe regeneration before acceptance.
- **Do** keep regeneration generation options as session-only: previously accepted options may prefill, but no generation recipe is persisted to project storage.
- **Do** expose elevation label visibility controls in toolbar and View menu.
- **Do** keep disabled control states obvious during generation-option changes.
- **Do** finish with review verdict and updated docs before closing the work item.

### Don't
- **Don't** add sub-tile brush strength, falloff, radius, sample-mode, or additional height authority.
- **Don't** rely on non-text cues for pinned/blocked/pending states.
- **Don't** hide validation outcomes behind transient visual styling.
- **Don't** reintroduce elevation raster squares to communicate heights.
- **Don't** blur boundaries between reviewed preview and saved authoring document.
- **Don't** let a resource paint/erase operation change another resource ID or any terrain authority.

## Accessibility

Required accessibility conventions are retained:
- keyboard-accessible controls and standard menu semantics where implemented,
- explicit labels for commands and rail controls,
- pinned/selection state shown with outline and text, not color alone,
- clear dirty/review/invalid status signals.

Known unresolved gap remains keyboard tile traversal and stamping on the custom canvas; this is product scope and is surfaced in runtime messaging and roadmap materials.

## Do's and don'ts references

- [[PRODUCT.md]]
- [[docs/Reference/Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]]
- `.impeccable/mocks/win98-property-workshop.png`
- `.impeccable/review/desktop-elevation-numbers.png`
