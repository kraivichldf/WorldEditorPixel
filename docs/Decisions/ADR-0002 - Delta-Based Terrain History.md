# ADR-0002: Delta-Based Edit History

- Status: Accepted, amended by [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]]
- Date: 2026-08-07

## Context

A world can contain many tiles, while one pointer stroke normally changes a narrow path. Full-world snapshots make undo cost proportional to theoretical world size rather than the user's action.

## Decision

Each pointer drag owns one stroke builder. For every touched authoritative coordinate it retains the first before-value and latest after-value. On release, non-empty changes become one command that is already applied. Undo writes before-values; redo writes after-values. Starting a new command after undo clears the redo branch. Canceling restores before-values without adding history.

For version 2, the coordinate is a campaign tile and the value is the complete `(type, centre height)` pair. Both fields always undo and redo together. The earlier sample and type-only command implementations remain only for version-1 tests and import support.

## Consequences

- History memory scales with unique tiles changed by recorded strokes.
- Repeated visits to one tile collapse into one delta.
- Undo and redo are independent of the current stamp settings.
- One drag has one understandable history label.
- Command history is session state and is not serialized.
