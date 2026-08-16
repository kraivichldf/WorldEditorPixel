# ADR-0029: Explicit Resource Generation Selection

- Status: Accepted and implemented
- Date: 2026-08-16
- Extends: [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]] and [[ADR-0022 - Custom Resource Definition Management|ADR-0022]]

## Context

The first resource-regeneration dialog offered only **All resources**, **One category**, or **Selected resource**. That was sufficient for the generator contract but did not let an author prepare an arbitrary mixed set before starting an expensive run. The separate per-resource **Enabled** setting was also easy to mistake for exclusion even though disabled resources inside the active scope deliberately lose unlocked occurrences and retain only locks.

The editor needs an explicit, reviewable operation boundary: which resource occurrence layers will be replaced, and which layers must remain untouched. This boundary is different from saved generation settings such as coverage or enabled/manual-only state.

## Decision

### Exact transient selection

Resource generation accepts an exact non-empty set of stable resource IDs in addition to its retained all/category/single-ID core scopes. The selection is canonicalized to distinct ordinal ID order, validated against the complete built-in/custom catalog, and compared by value for stale-candidate detection. It is transient operation intent and is not written into `resource-generation.json`.

The native dialog exposes two lists before generation:

- **Included — Regenerate**: locked occurrences are preserved, unlocked occurrences are removed and replaced by the candidate;
- **Excluded — Keep**: every current occurrence is copied exactly, regardless of that resource's enabled or coverage setting.

The dialog starts with every catalog definition included, preserving the previous default. **All**, **Renewable**, **Finite**, **Only selected**, and **Exclude all** provide quick set construction, while the transfer buttons support arbitrary mixtures. Category filtering and name/ID search change only what is visible; they never change membership. Custom resources participate through the same stable-ID selection.

An empty Included list is a valid editing state but not a valid generation request. **Generate candidate** explains that at least one resource must be included. Reports are created only for Included resources; an Excluded resource shown in the preview is identified as unchanged.

### Enabled remains generation behavior

**Generate new occurrences when included** retains the existing saved setting semantics:

- on: generate up to the resource's independent eligible coverage target;
- off, or coverage `0%`: remove unlocked Included occurrences and keep locks only;
- Excluded: do not inspect that setting for occurrence replacement; keep the current occurrence layer exact.

Overrides edited for an Excluded resource may still be saved when the reviewed candidate is accepted, so they are ready for a later run in which that resource is Included. Editing any override or changing Include/Exclude membership makes the visible candidate stale and disables **Use resources** until regeneration.

## Consequences

- Authors can regenerate a deliberate mixed subset without repeated one-resource runs.
- Exclusion is non-destructive and no longer overloaded onto the destructive manual-only setting.
- Existing locked-authority, independent-coverage, cancellation, stale-result, acceptance, persistence, and runtime-export contracts remain unchanged.
- Selection order and duplicate input IDs cannot affect deterministic output or reports.
- The dialog becomes wider to keep both lists readable while preserving the synchronized Current/Candidate comparison.

## Verification boundary

Executable tests cover canonical ordering/value equality, empty/invalid/unknown selection rejection, Included disabled removal, Included generation, report scoping, and exact preservation of an Excluded occurrence even when its saved override is disabled. The Win98 dialog builds with native automation names and keyboard-focusable controls; the Impeccable static UI detector reports no findings.
