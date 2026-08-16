# ADR-0015: Preview-First Current World Regeneration

- Status: Accepted
- Date: 2026-08-14
- Updated: 2026-08-14 — regeneration may replace the complete world definition

## Context

Designers can add or change safe custom land types after a world already exists, but the only generator entry point was **New World**. Reusing New World requires treating the result as a different unsaved document and prompts about discarding the current work before the designer can even inspect a candidate. A direct reset would be worse: it would replace tiles before the designer can judge the seed, geography, and custom-type mix.

Version-2 project files intentionally store authoritative tile values and the custom terrain catalog, not generator provenance. Any regeneration feature must preserve that boundary, must not retain a second generated layer, and must not attach old delta commands to a replacement tile map.

## Decision

Add **Regenerate world…** as a preview-first document replacement command available from the toolbar, **Terrain** menu, and `Ctrl+R`.

Regeneration reuses `NewWorldDialog` with these constraints:

- the current `CampaignWorldDefinition` initializes the dimension, tile-size, sea/default-height, and height-limit controls, but every definition value remains editable;
- Blank is removed because the command must produce generated terrain;
- the current persisted custom terrain catalog is copied into the candidate inputs;
- generation remains off the UI thread and produces a temporary `CampaignWorld` rendered in the existing bounded preview;
- settings changes make the preview stale and disable **Use this world**;
- cancelling discards the candidate without mutating the open document.

For convenience, `EditorViewModel` may retain the last accepted generator settings for the current process session. This state is a lightweight `CampaignMapGenerationOptions` value without tile entries. It is neither serialized nor authoritative. A loaded project therefore starts regeneration from stated defaults while still carrying its persisted custom catalog.

Acceptance passes the exact reviewed world to `EditorViewModel.RegenerateWorld`. The view model rejects Blank, preserves the current project folder and legacy-import safety boundary, replaces the definition and tile map together, clears undo/redo and pointer/pinned state, refreshes the custom palette, clamps the active stamp height into the accepted limits, and marks the document modified. It does not regenerate a second time and does not create an undo snapshot of the old complete world.

## Consequences

- Designers can add custom types, assign generation shares, and preview a new distribution without first discarding or saving the current document.
- Designers can change physical world size, grid resolution, sea/default elevation, and allowed height range in the same reviewed replacement instead of creating a separate project first.
- The current world remains available behind the modal until the explicit **Use this world** action.
- Saved project identity survives acceptance, so the next normal Save updates the same project rather than silently becoming Save As.
- Undo/redo cannot cross the replacement boundary; retaining delta commands would target the discarded `CampaignTileMap`.
- Reopened projects do not reproduce unsaved generator provenance. The dialog states this instead of inventing settings.
- Memory remains bounded to the active world, temporary preview while the dialog is open, and a small session recipe; no retained generated tile list is added to editor state.

This extends [[ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]] without changing its generator formulas or the version-2 file format.
