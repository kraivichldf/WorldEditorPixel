# ADR-0009: Versioned Runtime World Package

- Status: Accepted
- Date: 2026-08-12

## Context

The editor's sparse JSON project folder is appropriate for authoring, validation, diffs, and recovery, but a future Unity or Unreal importer needs a compact deterministic grid. A headerless height `.raw` file would lose tile type, custom identity, scale, dimensions, coordinate orientation, and format version. CSV and per-tile JSON remain portable but repeat structure across every cell and are slower to parse into runtime arrays.

Export must not replace **Save**, mark an unsaved world clean, or create a second editable authority. It is a derived handoff artifact for game-development tooling.

## Decision

Add **Export Runtime Data** as a distinct editor action. It writes one `.kworld` ZIP package atomically. The package contains `manifest.json` and `tiles.bin`.

The version-1 manifest identifies `kingdom-world-runtime`, all metre-based world definition values, exact grid dimensions, north-west/Y-south coordinate semantics, row-major order, a numeric type table, indexed custom terrain definitions, the four-byte tile record layout, and SHA-256 of the uncompressed binary data.

`tiles.bin` is dense even though authoring storage is sparse. Every row-major tile record is:

```text
uint8 type
uint8 customTerrainIndex (255 = none)
int16 little-endian centreHeightMeters
```

The exporter streams bounded buffers into the compressed archive, fixes ZIP timestamps, sorts catalogs deterministically, writes through a unique temporary file, and replaces the destination only after the archive is complete. Equal world state produces equal package bytes.

## Consequences

- A game importer has constant-stride tile access and needs no editor assembly.
- JSON retains schema discoverability without expanding every tile record.
- The safe base type remains available when a downstream tool does not support a custom terrain identity.
- Importers can reject unknown versions and corrupted binary data before creating engine assets.
- Export size scales with total tile count, as a runtime grid must include implicit defaults; memory usage remains bounded by the streaming buffer.
- Save/reopen behavior and the version-2 authoring format remain unchanged.
- Engine-specific derived assets, meshes, textures, coordinate conversion, and runtime streaming remain downstream responsibilities.

The exact package and importer contract is [[../Reference/Runtime World Package|Runtime World Package]].
