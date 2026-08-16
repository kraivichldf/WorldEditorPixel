# ADR-0018: Resource Persistence and Runtime Package v2

- Status: Accepted; core persistence/export implementation complete, product integration pending
- Date: 2026-08-15

## Context

[[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]] and [[ADR-0017 - Resource Terrain Queries Diagnostics and History|ADR-0017]] establish resource definitions, settings, sparse occurrences, terrain diagnostics, and shared history without attaching them to the running editor. The next slice must freeze a lossless authoring format and a compact game-development handoff while preserving the existing version-2 terrain project and version-1 `.kworld` contracts.

Resource authority must remain separate from terrain records. Missing resource files must keep old projects valid, malformed cross-file references must fail before an open world is replaced, and export must omit authoring-only locks and generation policy.

## Decision

### Authoring files

Add an isolated `CampaignResourceProjectSerializer` rather than changing `CampaignWorldProjectSerializer` or `CampaignWorld`. Its save boundary receives a validated `CampaignResourceMap`, nullable `CampaignResourceGenerationSettings`, and project directory. Its load boundary receives the already validated `CampaignWorldDefinition` and project path, then returns a resource map plus nullable settings.

The optional sibling files are:

```text
resource-definitions.json
resource-generation.json
resource-tiles.json
```

`resource-definitions.json` version 1 stores only custom definitions. Built-ins remain code-owned and are reconstructed before custom definitions are validated. Each definition stores every immutable definition field, with `medium` stored once at definition level. The nested rule record stores nullable ranges, sorted identifier lists, and sorted weight entries.

> [!NOTE]
> [[ADR-0026 - Soft Avoided Resource Terrain Factors|ADR-0026]] later advances only `resource-definitions.json` to version 2 by adding required `avoidedTerrainTags`. Version 1 still loads with an empty avoidance list. The `.kworld` runtime package remains version 2.

> [!NOTE]
> [[ADR-0027 - Hard Resource Surface Exclusions|ADR-0027]] advances that authoring sidecar to version 3 with required `excludedTerrainSurfaces`. Versions 1 and 2 remain readable with empty hard exclusions. Runtime export is still unchanged.

`resource-generation.json` stores schema version 1, exact resource seed, seed-derived flag, abundance, climate, geology, and ordinally sorted sparse overrides. Absence remains `null`; the loader does not invent a derived seed because the version-2 world manifest has no persisted world-generation seed.

`resource-tiles.json` version 1 stores non-empty tile records sorted by `Y`, then `X`. Each tile stores occurrences sorted by ordinal resource ID with potential `1..100` and the authoring-only lock flag.

Load order is custom definitions, generation settings, then occurrences. Readers reject unsupported versions, missing required values, integer enum forms, unknown or duplicate properties, malformed definitions/settings, built-in ID collisions, duplicate overrides, duplicate tile records, duplicate resource IDs within a tile, unknown IDs, invalid coordinates, invalid potential, and empty redundant tile records. Environmental mismatch remains a diagnostic warning and is never file corruption.

Missing definition, generation, and tile files mean built-ins only, absent generation settings, and an empty resource map respectively. Canonical save removes a stale optional file when its corresponding state is absent. Every desired file is serialized and validated before disk mutation, written through a unique sibling temporary file, and atomically replaced per file. The three-file set is not claimed to be transactionally atomic as a group.

### Runtime package version 2

Keep `CampaignWorldRuntimeExporter.ExportAsync(world, path, token)` byte-compatible as `.kworld` version 1 with exactly `tiles.bin` and `manifest.json`.

Add an opt-in overload receiving `CampaignResourceMap`. It requires a value-equal world definition and writes `.kworld` version 2 with exactly:

```text
tiles.bin
resource-index.bin
resource-records.bin
manifest.json
```

`tiles.bin` retains the version-1 four-byte terrain record unchanged.

`resource-index.bin` contains one eight-byte little-endian record per row-major tile:

```text
uint32 firstRecordIndex
uint16 recordCount
uint16 reserved = 0
```

`resource-records.bin` contains four-byte records grouped by tile and sorted by ordinal resource ID:

```text
uint16 resourceCatalogIndex
uint8  potential
uint8  reserved = 0
```

The version-2 manifest adds a resource layer with an ordinal-ID-sorted catalog of all built-in and custom definitions. Runtime catalog entries contain only index, stable ID, name, Renewable/Finite category, and built-in/custom identity. Locks, symbols, colors, rules, settings, diagnostics, and editor display policy are excluded.

The resource manifest declares both binary layouts, record counts, byte lengths, little-endian order, and SHA-256 of each uncompressed stream. An empty resource map still writes a dense zero-count index and an empty occurrence stream. Entry order and timestamps are fixed, output uses bounded buffers, checked arithmetic, and a unique temporary package, and both the terrain-world revision and resource revision must remain unchanged across export. Cancellation and revision changes are checked again before destination replacement so an existing package is preserved.

## Consequences

- Save/reopen can round-trip every authoritative resource field without coupling resources to either terrain model.
- Old projects remain valid because all three authoring files are optional.
- Existing editor export remains version 1 until the complete resource UX explicitly calls the opt-in version-2 overload.
- A version-1 importer must reject version 2 rather than guessing; version 2 retains byte-compatible terrain records while adding explicit resource streams.
- Deterministic ordering, reserved bytes, hashes, and fixed ZIP metadata provide a stable cross-engine interchange contract.
- This slice still does not expose resource UI, generation, regeneration preview, or automatic save/export integration.

This decision extends [[ADR-0009 - Versioned Runtime World Package|ADR-0009]], [[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]], and [[ADR-0017 - Resource Terrain Queries Diagnostics and History|ADR-0017]].
