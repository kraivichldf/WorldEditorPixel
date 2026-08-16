# ADR-0003: Sparse Campaign Tile Types

- Status: Superseded by [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]]
- Date: 2026-08-10
- Historical scope: Version-1 independent type overlay

## Context

Version 1 added whole-cell campaign types above a separate continuous height-sample field. It solved incomplete-looking type dabs, but it still left two incompatible authoring resolutions: tile type and sample height.

## Decision at the time

Store non-Unassigned types sparsely and paint complete cell rectangles without changing the underlying height samples.

## Reason for supersession

The user needs the campaign tile itself to be the only paintable terrain unit and to control the height of that tile. Keeping type as an overlay did not satisfy that model. ADR-0004 replaces the independent layers with one sparse tile value containing both type and centre height, plus deterministic interpolation between centres.

## Retained compatibility

The version-1 type-only file and classes remain readable so old projects can be converted. They are not exposed by the active version-2 authoring UI.
