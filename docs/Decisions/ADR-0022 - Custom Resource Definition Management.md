# ADR-0022: Custom Resource Definition Management

- Status: Accepted and implemented
- Date: 2026-08-16
- Extends: [[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]], [[ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019]], and [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]]

## Context

The engine-neutral resource catalog, persistence, runtime export, manual painting, and procedural generator already accept immutable custom `CampaignResourceDefinition` values. The running editor could load and use those definitions but could not create them. A designer therefore needed to hand-author `resource-definitions.json`, which bypassed in-app validation and made the advertised custom-resource workflow incomplete.

Catalog changes are not ordinary occurrence strokes. `CampaignResourceMap` owns an immutable catalog, existing history commands target the current map, generation settings may contain definition-ID overrides, and used occurrences must never become orphaned. The UI needs a protected document boundary rather than direct field mutation.

## Decision

Add **Resources → Custom resources…** and the matching Resources-rail command. The Windows 98 property-workshop dialog edits a temporary custom-definition list; closing or cancelling does not mutate the document.

The manager:

- lists project-owned custom definitions only; the sixteen built-ins remain immutable;
- can create a manual-only default or duplicate any built-in into a new custom ID;
- exposes name, stable ID, Renewable/Finite category, Land/Water/Either medium, distribution shape, symbol ID, color, map priority, default coverage, richness, concentration, four optional bounded ranges, supported preferred/avoided suitability factors, hard normalized-surface exclusions, explicit weights, and custom-terrain include/exclude IDs;
- defaults a new definition to `0%` coverage so merely adding it cannot unexpectedly populate the world;
- offers only generator-supported factor IDs and rejects unsupported typed factors instead of allowing a silent zero-result generator run;
- shows exact usage counts; once used, stable ID and category are disabled, and deletion is blocked until all occurrences of that ID are erased.

**Apply resources** constructs and validates a complete replacement `CampaignResourceCatalog` before touching editor state. The view model then performs one sparse usage-count pass, rejects removal/identity/category violations, filters generation overrides only for deleted unused IDs, builds a replacement map with every still-valid occurrence copied exactly, validates the complete world/resource/settings tuple, and swaps it atomically. Successful catalog replacement marks the document modified and clears shared Undo/Redo because commands created against the previous immutable map/catalog cannot be replayed safely. An equivalent definition set is a no-op and preserves history.

The selected definition becomes the active resource when visible; the category filter widens to **All** when necessary. Existing potential and lock values do not change when editable definition metadata or rules change. Diagnostics refresh against the new rules. Save/reopen writes `resource-definitions.json`; resource generation automatically includes positive-coverage custom definitions; runtime package version 2 exports the custom catalog and occurrences through the existing contracts.

## Consequences

- Designers can complete the custom-resource workflow without editing JSON.
- Built-in identities and rules remain application-owned while duplication provides a safe starting point.
- Used occurrence identity cannot be orphaned, but non-identity rule changes may intentionally make existing manual occurrences out of profile; they remain valid and show warnings.
- Catalog replacement is intentionally not a stroke-level undo operation. Its explicit footer and completion status state that Undo/Redo is cleared.
- The editor performs replacement work only when the user applies the modal manager; ordinary painting and rendering keep their existing sparse/visible-area cost model.

## Verification boundary

Automated coverage proves addition, selection, exact occurrence preservation, history clearing, editable used-definition metadata/rules, used category and deletion rejection without mutation, stale-override filtering, equivalent-set no-op behavior, advanced-rule round trips, unsupported-factor rejection, built-in duplication, and one-pass sparse usage counts. Persistence, procedural generation, and runtime export already have independent custom-catalog coverage.

This decision does not add overview symbols, climate/geology diagnostic overlays, arbitrary scripts, gameplay inventory/economy state, or a combined World/Terrain/Resources property sheet.
