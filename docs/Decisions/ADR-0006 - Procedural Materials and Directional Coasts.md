# ADR-0006: Procedural Materials and Directional Coasts

- Status: Accepted
- Date: 2026-08-10
- Amended: 2026-08-12 — remove the authored Coastal tile and derive a 90/10 coast from every non-water terrain

## Context

Flat classification colors identify tiles but do not make grass, dry desert ground, forest, rock, sand, and water feel materially different. A coast also needs to face the Sea or Lake automatically without adding rotation controls, hand-painted sub-tile masks, or another authoring resolution.

The original explicit Coastal tile erased the terrain that reached the shoreline: Forest, Hills, Cliff, and custom land all had to become a generic grassy coast. The revised requirement is a typical one-sided coast with 90% of the original terrain and 10% matching water, without a mandatory sand strip.

## Decision

Remove `Coastal` from the authoring palette, generator output, canonical project writer, and runtime export. Numeric value `11` remains reserved only so older version-2 `coastal` records can load; each normalizes to Plains at the same centre height, the document is marked modified, and the next save writes `plains`.

Every current non-water tile derives an automatic coast from its original stored type/custom identity:

- inspect immediate north, east, south, and west neighbours for Sea or Lake;
- choose the closest water-facing edge;
- use matching Sea/Lake for the outer `0.10` edge depth;
- use the tile's original built-in or custom material from `0.10` inward;
- use the original material everywhere when there is no cardinal water neighbour;
- do not apply the treatment to Sea, Lake, or the legacy Water alias themselves.

Every water-facing edge participates. At corners the nearest edge wins, with N/E/S/W as the deterministic tie order. Therefore 90/10 describes a one-sided coast; multiple water sides reduce the total original-material area while preserving the same 10% per-edge depth. An explicit Beach tile remains full-tile sand as its original material and receives the same 10% matching-water edge.

The editor renders deterministic world-anchored procedural patterns for Grass, Desert, Forest, Hills, Rock, Sea, Lake, Sand, and Cliff. Desert uses a warmer dry dune-and-stone pattern distinct from Beach sand. Version-2 River classes use Grass as their presentation fallback because that format does not retain the underlying land surface. River draws a narrow auto-connected bank-and-water ribbon; Large River uses a broader, deeper-blue corridor while retaining visible ground on both sides. Mixed sizes share a centreline and connect at the same cardinal tile edges. A route endpoint beside Sea or Lake extends visually to that water edge. Pattern strength fades toward zero at low screen-space tile size. Height-only mode bypasses material hue and texture. Full-cell classification, centre elevation marker, derived height shading, river ribbon, grid, cursor, and pinned selection remain separate visual channels.

Both ribbon widths are screen-readable editor symbology, not physical percentages of the campaign tile. Large River identifies a major corridor, but tactical/FPS terrain generation must choose its actual physical width from downstream design rules rather than infer metres from this preview.

The procedural pattern is editor presentation and is not serialized. The automatic coast-band rule is engine-neutral derived data and is documented for consumers. The inspector retains the stored type/custom name and appends automatic Sea/Lake coast state so the derivation remains visible without pretending it is another terrain value.

## Consequences

- Designers paint only the terrain they mean; changing an adjacent Sea/Lake tile adds, removes, or reorients the visible coast automatically.
- Plains, Steppe, Forest, Desert, Hills, Mountain, Beach, Cliff, custom land, River fallback ground, and Unassigned retain their material identity inside the edge.
- No bitmap asset bundle, UV metadata, sub-tile mask, or second brush is required.
- Texture remains stable while panning and does not animate or alter saved data.
- River reads as connected water surrounded by ground instead of a complete cyan water tile.
- Existing `coastal` records lose their unavailable original material during migration and therefore use the historically accurate grass/Plains fallback; the status reports the count instead of hiding this lossy assumption.
- Curved shore splines, variable beach width, automatic beach sand, erosion, tides, and user-supplied texture packs remain future work.

This amendment brings the running version-2 coast closer to [[ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]] by removing authored Coastal and preserving land identity. Version 3 still goes further by making Beach/Cliff sparse per-edge treatments and separating River from base surface.
