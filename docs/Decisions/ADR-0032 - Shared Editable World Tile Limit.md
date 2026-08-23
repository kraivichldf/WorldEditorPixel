# ADR-0032: Shared Editable World Tile Limit

- **Status:** Accepted
- **Date:** 2026-08-23
- **Owners:** WorldEditorPixel

## Context

World generation was limited to `250,000` campaign tiles, but Blank creation and project loading accepted much larger exact grids. That exception was unsafe: the canvas fits the whole logical grid and snapshots its visible tile region into dense pooled storage, while Resource and Season workflows also allocate bounded arrays from the full tile count. A valid-looking Blank or imported manifest could therefore reach an overflow or unbounded allocation before the designer painted anything.

The product needs one truthful definition of a world it can edit, open, save, render, and export. A generator-only limit cannot provide that contract.

## Decision

`CampaignWorldDefinition.MaximumTileCount` is `250,000`, and `CampaignWorldDefinition.EnsureValid` rejects every larger derived grid.

That common validation boundary governs:

- Blank and generated New World creation;
- current-world regeneration candidates;
- version-2 project loading before terrain/resource/Season sidecars;
- version-1 import preflight before any height-chunk payload is read;
- terrain, Resource, Season, and version-3 map construction;
- project save, compact `.kworld` export, readable `.world.json` export, and canvas rendering.

World dimensions must still divide exactly by campaign tile size. The limit is a total count, not a forced square shape: `500 × 500` is the maximum square example, while another exact rectangular grid is valid when its product is no greater than `250,000`.

New World checks the shared limit before considering the selected preset. Blank no longer bypasses or is suggested as a workaround. The action is disabled for an oversized or incomplete grid, and the form directs the designer to increase tile size or reduce world dimensions.

## Consequences

- A normal form value or imported manifest cannot reach the former canvas snapshot overflow path.
- Save and both runtime exporters inherit the same invariant rather than maintaining separate limits.
- Legacy import rejects an oversized campaign grid before loading potentially large chunk files.
- Existing projects above the limit are intentionally refused by 1.0.1; reducing dimensions or increasing campaign tile size requires an external migration tool because the editor cannot safely open them.
- The `250,000` value is a product capability boundary. Raising it later requires coordinated memory, rendering, generation, persistence, export, and interaction evidence rather than changing one generator constant.

## Rejected alternatives

### Keep Blank unlimited because its terrain map is sparse

Rejected because sparse terrain authority does not make the canvas, Resource support, Season support, or dense runtime indexes sparse.

### Guard only `ArrayPool.Rent`

Rejected because it would turn one crash into inconsistent late failures across other dense consumers and would still allow projects the editor cannot complete.

### Limit only each axis

Rejected because total memory/work scales with `TilesX × TilesY`; many unsafe rectangular grids have individually reasonable axes.

This decision refines the generation-specific boundary in [[ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]].
