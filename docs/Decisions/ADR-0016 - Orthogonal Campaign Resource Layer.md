# ADR-0016: Orthogonal Campaign Resource Layer

- Status: Accepted; Phase 1 engine-neutral domain implemented
- Date: 2026-08-15

## Context

The running editor stores one authoritative campaign terrain tile and centre height at each exact grid coordinate. The accepted version-3 terrain target separates base surface, derived terrain form, River network, and shore treatment because those meanings overlap.

Natural resources overlap those meanings as well. A forested hill may contain Timber, Fresh Water, Iron Ore, and Gold simultaneously. Treating resources as terrain types would erase surface/height authority, while limiting one resource per tile would not fit a campaign cell spanning several kilometres.

The resource layer must also remain usable before the complete terrain-version-3 migration. It therefore cannot depend on either the mixed version-2 terrain enum or the isolated version-3 aggregate. UI, serialization, procedural generation, and runtime export must not ship partial resource authority before the engine-neutral model and invariants are stable.

## Decision

Add `Kingdom.World.Core.Campaign.Resources` as an engine-neutral, terrain-version-independent campaign layer.

### Resource meaning

A resource occurrence describes natural potential or a deposit. It does not describe inventory, ownership, extraction, production, depletion, replenishment, trade, price, buildings, workers, or AI value.

Every occurrence is keyed by campaign coordinate and stable resource-definition ID. One tile may contain any number of different IDs, but the same ID may occur at most once on a tile. Potential is a resource-relative whole number from `1` through `100`; absence means no occurrence and zero is never stored.

Occurrences may be explicitly locked. Lock is authoring metadata for later regeneration and is not runtime economic state.

### Resource definitions

Definitions own stable identity, name, Renewable/Finite category, portable symbol/color metadata, display priority, spatial distribution profile, default eligible coverage, richness, concentration, bounded physical eligibility ranges, terrain-affinity tags, custom-terrain include/exclude IDs, environmental weights, and association weights.

Portable IDs use `1..64` lowercase letters/digits/hyphens and begin with a letter; symbol IDs use the same contract with a `32` character limit. Definition names use `1..64` trimmed characters with no control characters, and colors use `#RRGGBB`. Display priority is `1..100`, default eligible coverage is `0..100`, and richness bias is `-30..30`. Each named rule list or weight map contains at most `64` entries; weights are finite values from `-10` through `10`. Range endpoints are finite and inclusive with minimum no greater than maximum; grade and water-distance minima are non-negative, and region-scale minima are greater than zero. One catalog contains at most `65,535` definitions, while at most `256` enabled positive-coverage definitions participate in one generation run.

Custom IDs use the same strict portable-ID and color contracts as other project-authored catalogs. Collections reject duplicate IDs. Definition validation rejects unknown enum values, invalid ranges, non-finite values, inconsistent minimum/maximum bounds, invalid weights, and malformed identifiers or presentation values.

The application catalog begins with sixteen stable built-ins accepted in [[../Reference/Campaign Resource Layer Plan|Campaign Resource Layer Plan]]: Fertile Land, Timber, Fresh Water, Fish, Grazing, Wild Game, Stone, Clay, Sand and Gravel, Salt, Iron Ore, Copper Ore, Tin Ore, Coal, Gold, and Silver. Catalog membership never guarantees placement.

### Sparse map and mutation

The resource map is sparse and owns a validated `CampaignWorldDefinition` plus a validated resource catalog. Public enumeration is deterministic by coordinate then ordinal resource ID.

Single and batch mutations validate the complete request before changing state. Duplicate coordinate/resource-ID pairs in one batch, out-of-world coordinates, unknown IDs, duplicate occurrences, and invalid potential are rejected atomically. Removing the last occurrence from a tile removes its sparse entry. Revision increases only when authoritative data actually changes.

The map exposes layer-specific operations; it does not mutate terrain, height, River, or shore data. A future world aggregate may present terrain and resources as one logical campaign tile while retaining these separate authorities.

### Generation settings as data contracts

Phase 1 defines validated vocabularies and immutable settings for abundance, climate, geology, per-resource overrides, coverage, richness bias, and concentration. Independent coverage values never need to total `100%`. `0%` is valid and means manual-only for generation. At most 256 positive-coverage definitions may be enabled in one later generation run.

This phase does not implement suitability, diagnostic fields, spatial growth, preview, or regeneration. Those formulas and UI contracts remain specified in the resource plan.

### Compatibility boundary

The resource domain does not enter `CampaignWorld`, `CampaignWorldV3`, the running editor, version-2 project files, version-3 migration, or `.kworld` output in Phase 1. No project is silently upgraded and no partial resource file is written.

Later authoring persistence will remain a separate sparse resource file set while sharing logical tile coordinates. Later runtime export will add deterministic resource index/record streams rather than changing the fixed terrain record.

## Consequences

- Resources can overlap one another and every terrain meaning without enum multiplication.
- The same resource contracts can be attached to version 2 now and version 3 later through a small terrain-query seam.
- Built-in/custom definitions, occurrence potential, locks, and generation settings have one validation authority before UI or files depend on them.
- Sparse storage scales with actual occurrences rather than every possible tile/resource pair.
- Deterministic ordering supports stable files, tests, diffs, and runtime export later.
- Environmental suitability remains a generator/editor warning concern; a manually authored out-of-profile occurrence is structurally valid.
- The running executable gains no resource capability in this phase.

## Phase 1 acceptance

- Sixteen built-in definitions expose stable IDs, accepted category/profile/default coverage, and valid defaults.
- Custom definition validation covers identifiers, presentation, ranges, terrain/custom filters, environmental/association weights, and unknown enum values.
- Occurrences enforce potential `1..100` and valid IDs/coordinates.
- Sparse set/update/remove and atomic batch operations preserve one-ID-per-tile and deterministic enumeration.
- Catalog and generation settings reject duplicates, unknown overrides, invalid percentages/biases, and more than 256 active generated definitions.
- Domain tests execute without referencing Avalonia, Unity, Unreal, serializers, or procedural generator implementations.

This decision extends [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]] and remains compatible with [[ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]]. It does not supersede either terrain decision.
