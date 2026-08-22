# ADR-0031: Single-File JSON Runtime Export

- **Status:** Accepted; implemented
- **Date:** 2026-08-22
- **Owners:** WorldEditorPixel

## Context

The editable project folder is intentionally split across terrain, Resource, and Season files. The compact `.kworld` runtime package is one file, but it is a ZIP containing binary streams and therefore still requires a package-specific importer.

Some game-development workflows need one ordinary file that can be inspected, versioned, deserialized, and converted without ZIP or binary-record code. A headerless `.raw` file cannot identify dimensions, scale, coordinate orientation, catalogs, terrain identity, Resources, Seasons, or schema version. The requested “raw data as JSON” therefore means a self-describing runtime JSON document, not an untyped byte raster.

## Decision

Add **Export JSON Data** as a separate runtime handoff. It writes one UTF-8 `*.world.json` document with:

- format identifier `world-editor-pixel-runtime-json` and version `1`;
- exact metre-based world definition and grid orientation;
- stable built-in/custom terrain, Resource, and Season catalogs;
- every campaign tile in canonical `rowMajorYThenX` order;
- explicit `x`, `y`, terrain ID, optional custom-terrain ID, centre height, Resource ID/potential pairs, and every Season ID on that tile.

Authoring locks, generation rules, recipes, support fields, diagnostics, selection, preview state, and project identity are excluded. They control how data is authored, not what the game world contains.

The exporter sorts catalogs and tile occurrences by stable ordinal ID, checks terrain/Resource/Season revisions throughout the write, streams directly through `Utf8JsonWriter`, yields after bounded tile batches, writes to a unique sibling temporary file, and replaces the destination only after successful validation. Equal runtime authority produces equal JSON bytes regardless of authoring locks or insertion order.

The existing `.kworld` export remains supported. It is the compact choice for large production worlds; `*.world.json` is the readable, low-integration-cost choice. Neither artifact becomes editable project authority or changes document dirty state.

## Consequences

- Unity, Unreal, Godot, or custom tools can deserialize one conventional file and convert it into native assets.
- Importers must still require the known format/version, validate counts and stable IDs, and handle the documented north-west/Y-south coordinates. No engine automatically understands a project-specific JSON schema.
- The JSON repeats field names and coordinates for clarity, so it is larger and slower to parse than `.kworld`.
- The complete logical grid is exported, including implicit default tiles, while exporter memory remains bounded.
- Editor-only generation and lock semantics cannot accidentally become runtime gameplay authority.

The exact schema and importer sequence are documented in [[../Reference/Runtime World Package#Single-file JSON runtime export|Runtime World Package]].
