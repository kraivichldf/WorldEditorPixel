# ADR-0027: Hard Resource Surface Exclusions

- Status: Accepted and implemented
- Date: 2026-08-16
- Extends: [[ADR-0017 - Resource Terrain Queries Diagnostics and History|ADR-0017]], [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]], [[ADR-0022 - Custom Resource Definition Management|ADR-0022]], and [[ADR-0026 - Soft Avoided Resource Terrain Factors|ADR-0026]]

## Context

Soft avoided factors correctly express “uncommon here,” but they deliberately retain a `0.12` floor. A Desert tile with strong freshwater, lowland, geology, or regional evidence could therefore still clear a fixed admission threshold. That is useful for resources with plausible exceptions, but it violates the expected default meaning of Fertile Land and Timber: ordinary generated cropland or forest timber must not appear on a tile whose accepted normalized surface is Desert, Barren Rock, or Tundra.

Increasing negative weights would only disguise the requirement as score math. The generator needs a first-class hard rule that remains portable across the version-2 and version-3 terrain-query adapters.

## Decision

Add immutable `ExcludedTerrainSurfaces` to `CampaignResourceRuleSet`. It is a sorted duplicate-free list of assigned `CampaignResourceSurfaceType` values: Grassland, Forest, Desert, Wetland, Tundra, BarrenRock, Sea, or Lake. Unassigned is rejected because unassigned cells already fail every resource's hard eligibility.

Hard eligibility now evaluates in this order:

1. assigned terrain and Aquatic/medium ownership;
2. exact normalized base-surface exclusion;
3. elevation, grade, and nearest-water ranges;
4. custom-terrain include/exclude rules;
5. soft preferred/avoided and explicit weighted suitability.

Fertile Land and Timber hard-exclude `Desert`, `BarrenRock`, and `Tundra`. Their existing avoided climate/support factors remain because those still shape ranking across otherwise allowed land. Other built-ins retain their prior hard rules; Fresh Water, mineral deposits, and wildlife are not globally forbidden in deserts merely because they may be rare there.

The custom-resource manager adds a selector and **Add excluded tile** under **Hard terrain rules**. The generation dialog shows **Hard excludes** with the selected resource's preferred and avoided factors. Built-ins stay application-owned; duplicating one copies its complete hard and soft rule set.

Manual and locked occurrences remain resource authority. Diagnostics report `TerrainSurfaceExcluded`; they never delete or alter the occurrence. On the next accepted resource regeneration, targeted unlocked occurrences are removed before generation and cannot be recreated on an excluded surface. Targeted locks survive and count against the upper target while continuing to display the warning.

`resource-definitions.json` advances to version 3 with required camel-case `excludedTerrainSurfaces`. Version 1 loads with empty avoided and excluded lists. Version 2 preserves its avoided list and supplies empty hard exclusions. Runtime package version 2 remains unchanged because it exports accepted occurrences, not authoring-generation rules.

## Consequences

- Default generated Fertile Land and Timber cannot occur on Desert, Barren Rock, or Tundra.
- Soft aversion remains available for plausible-but-uncommon geography; hard exclusion is explicit and inspectable.
- Existing old unlocked placements require one regeneration to be replaced. Locked/manual data is never silently destroyed.
- Eligible coverage is calculated after exclusions, so the requested percentage applies only to allowed tiles and may still underfill honestly.
- The normalized surface contract stays independent from the mixed version-2 tile enum. Steppe continues to normalize as Grassland under [[ADR-0025 - Built-in Steppe Terrain|ADR-0025]].

## Verification boundary

Executable coverage proves built-in Desert exclusions, immutable sorted copies, invalid/duplicate exclusion rejection, hard diagnostic reporting, exact eligible-count reduction, end-to-end absence of unlocked generated occurrences on forbidden surfaces, custom-editor and built-in-duplication round trips, deterministic version-3 persistence, required version-3 data, and version-1/version-2 compatibility defaults.

This decision does not add project-level overrides for built-in identities, automatic deletion of manual authority, biome simulation, or a Steppe-specific normalized resource surface.
