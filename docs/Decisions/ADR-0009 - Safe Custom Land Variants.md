# ADR-0009: Safe Custom Land Variants

- Status: Accepted
- Date: 2026-08-12

## Context

Designers need more named terrain categories than the running version-2 enum provides—for example Farmland, Ancient Forest, Volcanic Hills, or Moorland—without turning every request into a new engine-level enum or allowing a cosmetic setting to break water, shore, and River topology. [[ADR-0025 - Built-in Steppe Terrain|ADR-0025]] later promotes the shared Steppe biome into the built-in palette while retaining this custom-variant boundary.

The accepted version-3 taxonomy will eventually separate base surface, terrain form, River overlay, and shore treatment. It is not yet the executable editor/file boundary. A version-2 addition therefore must remain portable to readers that only understand the stored base terrain type.

## Decision

Add a versioned optional `custom-terrain.json` catalog alongside the existing version-2 files. Each `CampaignCustomTerrainDefinition` has a stable lower-case ID, a designer name, `#RRGGBB` display color, optional whole-percent inland-mix share, and exactly one safe base: Plains, Steppe, Desert, Forest, Hills, or Mountain. A `CampaignTileData` can store an optional `CustomTerrainId`, but its required ordinary `Type` remains that definition's base. The base is a portable fallback and material foundation, never the allocation parent of the custom share.

The tile map validates every custom reference and rejects a base mismatch. A definition in active use cannot be deleted or have its base changed. The desktop manager permits naming, recoloring, and terrain-mix changes; it deliberately never offers Sea, Lake, River, Large River, Beach, or Cliff as a custom base. A custom tile beside Sea/Lake retains its own identity and material inside the universally derived 10% water edge.

Generation treats each positive custom share as an independent category in the same eligible inland pool as Plains, Forest, Desert, Hills, Mountain, and Steppe. The combined default/custom mix must total exactly `100%`; all custom shares together may not exceed `100%`. Deterministic largest-remainder allocation plus coherent seeded noise chooses resulting regions. The safe base remains a stored fallback and material foundation, never a limit on the category’s allocation. A zero share makes the definition paint-only.

## Consequences

- The editor gets extensible named/color-coded land types without an enum explosion.
- Existing systems and older readers can fall back to the stored base type without losing legal terrain behavior.
- Custom types cannot create water, widen a River, alter a Coast, or bypass the River crossing validator.
- Custom color changes retain the safe base texture; height remains ordinary centre-height authority.
- A custom catalog persists so later painting and future generation can reuse the same definitions, but generation provenance/seed history remains outside the terrain model.
- A future version-3 migration can map these IDs into a richer surface/biome layer without treating them as a substitute for the accepted separate River and shore architecture.

See [[../Reference/World File Format|World File Format]] and [[../Reference/Campaign World Generation|Campaign World Generation]] for the exact contract and formula.
