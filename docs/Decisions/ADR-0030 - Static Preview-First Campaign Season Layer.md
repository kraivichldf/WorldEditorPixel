# ADR-0030: Static Preview-First Campaign Season Layer

- Status: Implemented
- Date: 2026-08-17

## Context

The editor already owns terrain and campaign resources as independent authorities at the same exact campaign-tile coordinates. Climate, geology, and erosion fields used by existing generators are transient creation inputs rather than saved layers. The next feature must give every tile a season-like classification that can be generated, painted, locked, saved, reopened, and exported without turning the editor into a calendar simulation or rewriting terrain.

An Earth-like result also cannot be produced by rolling Spring, Summer, Autumn, or Winter independently per tile. It needs one coherent planetary phase, opposite hemispheric behavior, physical latitude, altitude cooling, maritime moderation, and broad regional variation. Custom identities such as Wet Season, Dry Season, and Monsoon must remain portable even when a consumer only understands the four built-ins.

## Decision

### Complete orthogonal authority

Add `Kingdom.World.Core.Campaign.Seasons` as an engine-neutral peer layer. Every logical campaign tile owns exactly one stable Season Definition ID, including Sea, Lake, River, and Unassigned tiles. Tile Season is static authoring authority: it does not advance with time, contain a month, or mutate when terrain or generation settings change.

The layer is complete and dense rather than sparse because absence has no meaning. Authoring locks are stored separately from the definition identity. Mutations validate complete batches before changing state, enumerate deterministically, increase revision only on effective changes, and participate in the editor's shared Undo/Redo history. Terrain, height, River topology, resources, and Tile Season remain separate authorities.

### Definitions and priority

Every project has stable built-in IDs for Spring, Summer, Autumn, and Winter and may add custom definitions. A custom definition retains its own stable ID, name, tint, and effect strength while declaring one built-in Season Fallback for portable rendering. Built-ins cannot be removed or have their IDs changed.

The project Season Catalog has no small product-level cap; the persistence/runtime representation supports at most `65,535` definitions. At most `256` definitions may be generation-enabled in one configuration. Additional definitions remain visible and usable for manual painting. New custom definitions start manual-paint-only and cannot be enabled until their rules validate.

Generation evaluates one explicit Season Priority from top to bottom. The first matching rule wins, and the final enabled entry is an unconditional catch-all that may be built-in or custom. Default priority is **Winter -> Spring -> Autumn -> Summer**: Winter captures cold/alpine conditions, Spring and Autumn distinguish warming from cooling transitions, and Summer completes unmatched warm/tropical tiles. Resulting percentages are diagnostics, never quotas.

Rules use inclusive physical/environmental ranges for latitude, elevation, generated temperature, moisture, seasonal intensity, warming/cooling tendency, distance to Sea/Lake/River, and terrain inclusion/exclusion. Empty Include means unrestricted, a populated Include is a whitelist, and Exclude wins. Custom terrain inherits its safe built-in base membership unless its stable custom ID is explicitly included or excluded.

Deleting a referenced custom definition requires an explicit atomic replacement, initially its built-in fallback. No tile may retain an orphaned definition ID.

### Static Earth-like snapshot

Generation uses a saved Season Seed to derive one continuous global orbital phase. No month, date, calendar, or advancing clock is stored. Comparable northern and southern latitudes receive opposite phase; equatorial tiles depend mainly on local temperature/moisture and higher-priority custom rules. Spring is selected from warming tendency and Autumn from cooling tendency rather than temperature alone.

Season Coverage is generation-only geographic interpretation. **Whole globe** maps tile-centre Y from `+90` to `-90` degrees and makes procedural longitude fields periodic without changing terrain adjacency or wrapping the campaign map. **Regional** maps physical north-south kilometres around an explicit centre latitude using Earth-scale distance; a window crossing either pole is rejected rather than clamped.

Earth's `23.44` degree axial tilt is the default, with an Advanced fictional-world control. Support fields use physical-kilometre wavelengths, `6.5 C/km` default altitude cooling, Sea/Lake maritime moderation and thermal lag, water-distance moisture, latitude circulation, orographic rain shadow, and restrained coherent noise. One immutable terrain snapshot is captured on the owner thread before cancellable worker generation; background code never reads live mutable terrain or season maps.

Season generation consumes terrain, elevation, water topology, coverage, and season settings only. It does not consume campaign resource occurrences, avoiding a circular dependency. Generation never changes terrain or resource authority.

### Preview, editing, and replacement

Seasons is a third workspace on the shared canvas beside Terrain and Resources. It provides stable-ID selection, complete-tile painting, Paint Area, explicit Reset to the project default season, lock/unlock, pinned inspection, exact labels, and shared history. Each cell displays fully as its assigned season by default. Optional boundary blending is presentation-only.

Generation is preview-first. Current and Candidate maps share a synchronized viewport and report per-definition counts/percentages, changed tiles, locks, rule overlap, unmatched-before-catch-all tiles, zero-result reasons, and warnings. In-dialog configuration changes retain but stale the old Candidate and disable acceptance; unexpected source drift is detected before acceptance. Successful acceptance installs the exact reviewed candidate, preserves project/import identity, marks the document modified, and clears shared Undo/Redo. Cancellation or failure leaves current authority unchanged.

Procedurally created worlds preview terrain and a complete Season Layer together and accept them atomically. Blank/manual worlds initialize every tile from an explicit Default tile season selector, initially Spring.

Same-lattice regeneration preserves locks in place and replaces only unlocked tiles in scope. A changed lattice remaps locked assignments by greatest physical-area overlap, regenerates unlocked assignments, and reports preserved/conflicted/dropped locks. Equal-overlap claims from different locked definitions block acceptance until explicitly resolved; unresolved target cells are visibly marked and excluded from Candidate distribution percentages rather than receiving a silent ID-order winner.

### Persistence and runtime boundary

Authoring persistence uses strict versioned season definition, generation-setting, and dense tile/lock sidecars managed by the project-level staged save coordinator. Older projects with no season sidecars load a complete implicit Spring layer and no invented generation recipe. Saving and reopening preserves exact IDs, locks, catalog metadata, settings, priority, coverage, and candidate-independent authority.

Runtime package version 3 retains the version-2 terrain/resource streams and adds one dense season catalog index per row-major tile plus a manifest mapping every index to stable identity and custom fallback. Authoring locks, rules, support fields, noise, diagnostics, preview reports, and generation settings do not enter runtime data.

## Consequences

- A project gains a complete static seasonal classification without acquiring calendar or simulation semantics.
- Earth-like coherence comes from one orbital phase and physical support fields rather than forced ratios or per-tile randomness.
- Custom Wet/Dry/Monsoon identities remain usable by simple consumers through built-in fallbacks.
- Dense storage is predictable: at the current `250,000`-tile limit, in-memory two-byte definition indexes use about `500 KB` and the packed lock bitset about `31 KB`. The strict authoring sidecar deliberately uses one full flags byte per tile, so its three-byte tile records total about `750 KB` before the header.
- Generation is deterministic for equal terrain, catalog, settings, scope, and seed, while edits remain ordinary explicit authority.
- The strict authoring sidecars, season-aware staged coordinator, deterministic runtime package version 3, manual Seasons workspace, preview-first Season generation, atomic generated-new-world acceptance, changed-lattice lock review, and generation-backed pinned diagnostics now form the running editor boundary.
- Calendar-driven seasons, resource-dependent season generation, persistent climate simulation, animated weather, and engine-specific material behavior remain separate future features.

The implementation contract and formulas are detailed in [[../Reference/Campaign Season Layer Plan|Campaign Season Layer Plan]]. This decision extends [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]], [[ADR-0015 - Preview-First Current World Regeneration|ADR-0015]], [[ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019]], and [[ADR-0021 - Reviewed Changed-Lattice Resource Remapping|ADR-0021]] without superseding them.

## Slice 1 implementation evidence

`Kingdom.World.Core.Campaign.Seasons` first implemented validated built-in/custom definitions, terrain/environment rule contracts, the `65,535` catalog and `256` enabled-priority boundaries, Whole-globe/Regional settings, Advanced climate settings, rectangular scope, a complete dense season/lock map, atomic batches, stable seed/catalog fingerprints, and shared-history season commands. Persistence, runtime package version 3, remapping, rendering, and editor workflows remained outside that slice.

Focused verification passes `31/31` season tests. Full Release verification passes `464/464`; the solution builds with zero warnings and zero errors, and format verification is clean.

## Slice 2 implementation evidence

The core now adds version-2/version-3 terrain adapters, owner-thread revision-checked immutable capture, a terrain-content fallback seed, exact cancellable Sea/Lake/River distance fields, Whole-globe/Regional geographic support, axial-tilt insolation and warming/cooling tendency, maritime lag and amplitude moderation, elevation cooling, separate periodic physical noise, latitude-cell wind, orographic rain shadow, and water-distance moisture. The deterministic generator uses the exact captured catalog and source revisions, applies ordered first match with an unconditional final catch-all, preserves every lock and out-of-scope tile, emits per-definition Current/Candidate/environmental/shadowed/lock/change/zero reports, and returns a stale-guarded candidate without touching live authority.

At the Slice 2 boundary, focused verification passed `56/56` season tests, including zero-tilt/default-band behavior, periodic longitude noise, physical-scale consistency, and the representative `140 x 140 = 19,600` grid. Full Release verification passed `489/489`; the solution built with zero warnings and zero errors, and format verification was clean. No dialog, workspace, serializer, project coordinator, remapper, or runtime package exposed Seasons at that boundary.

## Slice 3 implementation evidence

The core now persists the complete canonical built-in/custom catalog, appearance, rules, priority, default season, optional accepted generation recipe and fingerprints, and every dense season/lock tile through strict `season-definitions.json`, optional `season-generation.json`, and `KWSEASON` version-1 binary sidecars. Missing all three sidecars projects to a clean complete Spring layer; partial or corrupt authority is rejected. The season-aware editor coordinator stages terrain, resources, and seasons together, reload-validates the candidate, gates all three revisions, and commits the nine-file managed set with rollback. Its earlier overloads deliberately retain their six-file ownership so the still-pre-season UI cannot delete season files it cannot edit.

Runtime package version 3 keeps the version-2 terrain, resource-index, and resource-record streams byte-identical, adds row-major `season-tiles.bin`, and publishes the canonical runtime season catalog plus hashes/layout metadata. Locks, rules, recipes, support fields, and reports remain authoring-only. Export uses fixed entry order/timestamps, bounded stream buffers, final cancellation/revision checks, and atomic destination replacement.

At the Slice 3 boundary, focused `CampaignSeason*` verification passed `107/107` and full Release verification passed `540/540`; the solution built with zero warnings/errors and formatting was clean. That boundary intentionally left the running executable on the version-2 project/export path until Slice 4 integrated Season document state and UI.

## Slice 4 implementation evidence

The Avalonia editor now owns `CampaignSeasonMap`, catalog priority, and optional accepted generation metadata beside terrain and resources. **Terrain**, **Resources**, and **Seasons** share one canvas transform, pin, and `CommandHistory`. The Seasons rail provides stable-ID search/selection, full-cell Paint/Reset/Lock/Unlock tools, clipped `1 x 1` through `25 x 25` Paint Area, default-on manual locks, labels, presentation-only boundary blending, exact hover/pinned authority, and a protected custom-definition/priority manager. Existing custom IDs are immutable; referenced deletion requires an explicit replacement; the final enabled priority row is visibly the generator's catch-all even when its retained rule is constrained.

Main-window Open/Save/Export now use the season-aware project coordinator and runtime package version 3. Older projects still open as a clean implicit unlocked Spring layer; the first ordinary save writes complete season sidecars. Same-lattice terrain regeneration preserves season IDs and locks exactly. Changed-lattice regeneration permits only a uniform unlocked default layer with no saved recipe until Slice 6 supplies the reviewed overlap remap; any meaningful Season authority is blocked rather than discarded.

Focused editor regressions cover shared terrain/resource/season LIFO history, catalog replacement/no-op behavior, pinned locks, manager rule round trips and referenced deletion, canvas Paint/Reset/Lock/Unlock routing with edge clipping, exact open state, and changed-lattice protection. Full Release verification passes `556/556`; the solution builds with zero warnings and zero errors, and format verification is clean. Exact climate support, winning-rule/overlap inspection, Current/Candidate generation, and native Season-journey acceptance remain pending and are not claimed by this slice.

## Slice 5 implementation evidence

The running editor now exposes **Generate seasons...** from the Seasons menu, the Seasons rail, and `Ctrl+Shift+G`. Its Windows 98 property workshop captures the current terrain and Season authority on the owner thread, runs the deterministic generator away from the UI thread, and compares unchanged Current authority with an unapplied Candidate through two synchronized read-only canvases. Whole-world and inclusive rectangular scopes, terrain-derived/explicit/random seed selection, Whole-globe/Regional coverage, axial tilt, every Advanced climate parameter, the first-match priority, locks, per-definition reports, cancellation, and a narrow Current/Candidate switch are explicit.

In-dialog generation inputs and scope leave the old Candidate visible as a stale previous result and disable **Use seasons**. Report selection, grid, labels, boundary blending, pan, zoom, and Current/Candidate display switching do not stale it. Catalog/priority management requires closing the modal and discards that dialog Candidate; unexpected source drift is detected before acceptance. Acceptance rechecks terrain and Season revisions, value-equal world definition, exact catalog identity and priority, Candidate revision, scope, and settings before installing the exact reviewed map and recipe. It preserves terrain, resources, project/import identity, marks the document modified, and clears the shared history; cancel, close, stale results, validation errors, and generator failures do not mutate current authority.

Accepted generation retains immutable support fields and canonical source/input fingerprints. The pinned inspector reads those exact fields, then re-evaluates the current catalog rules and active accepted priority to report the first winner, shadowed matches, higher-priority overlaps, authority agreement, and current/stale source and input state. Rule outcomes are derived diagnostics, not frozen or persisted authority. A saved accepted recipe can rebuild the support/fingerprint cache after reopen without changing the Season map.

Focused Slice 5 regressions cover input/source fingerprints, presentation-only catalog edits, exact first-winner/shadow semantics, rectangular canvas selection, seed resolution, exact acceptance, history and identity boundaries, stale terrain/Season/Candidate rejection, definition/catalog/priority mismatch, diagnostic rebuild after reopen, and busy-state gating. Full Release verification passes `573/573`; the solution builds with zero warnings and zero errors, formatting is clean, and `git diff --check` is clean. Native visual and keyboard acceptance remains the Slice 7 gate and is not claimed here.

## Slice 6 implementation evidence

**New World** now exposes an explicit **Default tile season**, initially Spring, plus protected custom Season management. Blank creation builds the complete selected-default layer directly and records no invented recipe. A generated preset builds terrain first in private candidate state, derives the initial Season Seed from the terrain seed, generates a complete Earth-like Season Layer, caches separate Terrain/Seasons preview rasters, and reports the Season distribution beside terrain/resource impact. **Use this world** transfers the exact reviewed terrain, dense Season map, priority, recipe, and support tuple to `EditorViewModel` in one acceptance boundary; failure, cancellation, or stale input installs neither authority.

Full-world regeneration captures current terrain/Season revisions, exact catalog/default/priority identity, saved recipe, and dense assignments before worker execution. Same-lattice candidates preserve every Season ID and lock exactly. Changed lattices map each locked source rectangle to its greatest physical-overlap target, use source-centre and stable coordinate ordering only to resolve multiple equal target cells for one source, merge same-ID claims, let strictly greater overlap win, and regenerate all unlocked targets from the reviewed candidate terrain. Equal greatest-overlap claims from different IDs remain explicit blockers until the author chooses a winner. A locked source with no target overlap remains blocked until the author separately permits that drop. The report and every decision stay private until combined terrain/resource/Season source and Candidate revisions are revalidated immediately before one atomic swap.

Focused Slice 6 verification covers exact same-lattice preservation, greatest-overlap movement, same-ID merge, strictly greater winner, unresolved/resolved equal-overlap conflict, explicit out-of-bounds permission, source/Candidate staleness, cancellation, complete new-world generation, exact ViewModel installation, project-identity/history boundaries, and atomic rejection. Full Release verification passes `586/586`; the solution builds with zero warnings and zero errors, and format verification is clean. Native normal/narrow visual and keyboard acceptance remains the Slice 7 gate and is not claimed by this slice.

## Slice 7 implementation evidence

The final product gate adds in-process Avalonia Headless verification over the real native dialogs. **New World** is exercised at `1120 x 800` and `900 x 680`; **Season Generation Preview** at `1480 x 880` and `980 x 700`; and **Locked Season Remap** at `560 x 440`. The tests verify bounds, narrow Current/Candidate switching, accessibility names, validation visibility, preview staleness, Enter activation, Tab traversal, and the default action moving from **Generate** to **Use** only when the exact Candidate is acceptable. Render captures live under `.impeccable/review/season-slice7/`. This gate exposed and fixed static mutable `WorldCanvas` brushes crossing Avalonia UI-thread ownership; shared rendering resources are now immutable.

The maximum-grid diagnostic executes `500 x 500 = 250,000` tiles with all `256` generation definitions enabled. The verified Release run completed owner-thread capture plus generation in `0.814 s` with `39.9 MiB` of current-thread allocations, produced exactly one valid Season value per tile, and stayed beneath deliberately broad `60 s` / `768 MiB` regression ceilings. This guards against retaining a `tile count x definition count` matrix.

Final Release verification passes `588/588`; `dotnet build WorldEditorPixel.sln -c Release --no-restore` completes with zero warnings and zero errors, format verification and `git diff --check` are clean, and runtime-v3 tests verify dense index-to-stable-ID/fallback mapping. The self-contained `win-x64` single executable is `artifacts/publish/seasons/World.Editor.exe` (`103,028,109` bytes, SHA-256 `3AD6572E208497C23533F711E2B317FDE0A12271A8497AEC785D7DC5F846397A`). A hidden bounded startup smoke reached the native main window before only the launched process was stopped. `Launch Tile Editor.cmd` now targets that verified build.
