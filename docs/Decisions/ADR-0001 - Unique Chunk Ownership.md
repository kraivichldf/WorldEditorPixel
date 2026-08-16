# ADR-0001: Unique Chunk Ownership

- Status: Superseded for active authoring by [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]]
- Date: 2026-08-07
- Historical scope: Version-1 sample/chunk format and importer

## Context

Version 1 stored a global height-sample lattice in chunks. Duplicating boundary rows or columns would have created two authoritative values for one coordinate and complicated editing and persistence.

## Decision

Partition the version-1 global sample lattice by integer division. A sample belongs to exactly one chunk, including samples on chunk boundaries. Chunks do not duplicate neighboring border samples.

## Current meaning

Version 2 no longer authors or saves height chunks. Campaign tiles own centre heights and derive a continuous surface. The version-1 implementation and this ownership rule remain in the repository so old projects can be loaded strictly and converted deterministically.

## Consequences

- Legacy chunks can still be validated and decoded without border reconciliation.
- Legacy sample averaging during conversion sees one value per coordinate.
- New version-2 projects have no chunk size, sample spacing, or chunk files.
