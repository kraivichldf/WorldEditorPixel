# ADR-0030: Preview-First Campaign Season Occurrences

- **Status:** Accepted; corrective implementation in progress
- **Date:** 2026-08-20
- **Owners:** WorldEditorPixel

## Context

Terrain already records one complete base type and centre height per campaign tile. Resources record independent occurrences, allowing several resource IDs to coexist at one coordinate. The Season feature must answer a similarly static question: **which seasons can occur on this tile?**

The first implementation on `feature/seasons` answered a different question. It stored exactly one season ID per tile, used first-match priority to choose a winner, and presented that value like a current categorical state. That model cannot represent a temperate tile containing Spring, Summer, and Fall while another tile contains Spring, Summer, Fall, and Winter. It also gives generation priority an authority the product does not want.

There is no month, date, calendar, current season, duration, probability, or automatic progression in this layer.

## Decision

### Authority and cardinality

A **Season Occurrence** is the boolean membership of one stable Season Definition on one campaign tile. Its identity is `(x, y, seasonId)` and its only additional authoring value is `Locked`.

A tile contains zero or more different Season Occurrences and at most one occurrence of each Season Definition. Occurrences on the same tile are unordered and do not exclude one another.

Examples:

- `{ Spring, Summer, Fall }`
- `{ Spring, Summer, Fall, Winter }`
- `{ Wet Season, Dry Season }`

No stored percentage accompanies an occurrence. Candidate reports may show the percentage of world tiles containing each definition, but those per-definition coverages are independent and do not sum to 100 percent.

### Definitions and custom seasons

Every project has stable built-in definitions for **Spring**, **Summer**, **Fall**, and **Winter**. A project may add custom definitions such as Monsoon, Wet Season, or Dry Season. Each custom definition declares one built-in fallback for consumers that do not recognize its stable ID.

Catalog order is presentation and serialization order only. There is no first-match Season Priority and no generation catch-all.

Deleting a referenced custom definition requires an explicit atomic choice: remove its occurrences or replace them with another definition. Built-in IDs cannot be removed or renamed.

### Manual editing

The Seasons workspace follows the resource mental model:

- select one Season Definition;
- show the selected definition over terrain wherever its occurrence exists;
- add that occurrence over a complete-tile Paint Area;
- erase only that selected occurrence;
- lock or unlock only that selected occurrence;
- inspect all Season Occurrences on a pinned tile.

Adding or erasing one Season Occurrence never changes terrain, height, resources, or another Season Occurrence. A drag remains one command in the shared Terrain/Resources/Seasons Undo/Redo history.

### Generation

Season generation evaluates every selected definition independently. For each in-scope tile and selected Season Definition:

- a matching rule adds or keeps the unlocked occurrence;
- a non-matching rule removes the unlocked occurrence;
- an existing locked occurrence remains exact;
- unselected definitions remain exact.

Several matching rules therefore create several occurrences on the same tile. Generation has both a spatial scope and an explicit Included/Excluded definition selection, matching the established resource regeneration workflow.

Generation remains preview-first. Current authority never changes until the exact current Candidate is accepted. Input or source drift makes the Candidate stale and disables acceptance.

Earth-like support represents annual climatology rather than one orbital instant. It provides latitude, annual mean temperature, warm-season temperature, cold-season temperature, annual temperature range, moisture, maritime influence, rain shadow, and water distances. The seed controls coherent spatial variation, not a date.

Default built-in rules are intentionally overlapping:

- Spring and Fall require a meaningful temperate transition range;
- Summer requires a sufficiently warm warm-season temperature;
- Winter requires a sufficiently cold cold-season temperature;
- custom rules independently add their own memberships.

### Locks and world replacement

A lock protects one existing `(tile, seasonId)` membership, not the entire Tile Season Set. Regeneration may still add or remove other unlocked definitions on that tile.

Same-lattice world regeneration preserves exact occurrences. Changed-lattice regeneration remaps occurrences by physical tile centre, merges duplicate target identities, retains a lock when any merged source was locked, reports out-of-bounds locked drops, and regenerates selected unlocked definitions against the candidate terrain before acceptance.

### Persistence and runtime export

Authoring persistence stores:

- the complete Season Catalog and rules;
- optional accepted generation settings and selected definition IDs;
- deterministic sorted Season Occurrences and their locks.

The project-level staged coordinator continues to commit terrain, resources, and seasons as one validated candidate.

Runtime package version 3 retains the terrain/resource streams and replaces the rejected one-index-per-tile season stream with:

- one dense per-tile season index containing record offset and count;
- one compact sorted season-occurrence record stream containing catalog indexes.

Locks, rules, support fields, diagnostics, settings, and preview state remain authoring-only.

## Consequences

- One tile can truthfully expose several possible seasons.
- Season distribution percentages are independent, like resource coverage.
- Rendering must focus on one selected Season Definition or list occurrences; a single categorical full-world Season color is no longer authoritative.
- Priority reordering, default tile season, Reset-to-default, and one-value lock conflict resolution are removed.
- Empty Tile Season Sets are representable for blank/manual or deliberately excluded data. Generation may populate any selected matching definitions without forcing a winner.
- A pathological generation request can match many definitions on every tile, so generation and loading enforce explicit total-occurrence safety limits and report an actionable failure instead of exhausting memory.
- The previously published draft-branch season format has no compatibility guarantee because it was never merged into `main`; the corrected format becomes the first supported Season boundary.

## Rejected alternatives

### Exactly one Season Definition per tile

Rejected because it cannot express the user-required overlapping sets.

### Store a `1..100` season value

Rejected because the feature only records whether a season can occur. A numeric value would introduce ambiguous duration, probability, or intensity semantics.

### Store months or a current season

Rejected because calendar simulation and runtime progression are outside this authoring layer.

### Use first-match priority

Rejected because matching one definition must not suppress another. Definition order is not domain authority.

The implementation contract is detailed in [[../Reference/Campaign Season Occurrence Layer Plan|Campaign Season Occurrence Layer Plan]].
