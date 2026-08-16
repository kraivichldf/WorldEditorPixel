# ADR-0026: Soft Avoided Resource Terrain Factors

- Status: Accepted and implemented
- Date: 2026-08-16
- Extends: [[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]], [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]], and [[ADR-0022 - Custom Resource Definition Management|ADR-0022]]

> [!NOTE]
> [[ADR-0027 - Hard Resource Surface Exclusions|ADR-0027]] adds an explicit hard normalized-surface rule for cases such as Fertile Land or Timber on Desert. The soft rule in this decision remains appropriate for ordinary preferences.
>
> [[ADR-0028 - Resource Spawn Opportunity Calibration|ADR-0028]] later groups each preferred or avoided list as alternative ordinary cues before the outer geometric mean. The `0.12` soft floor and exact explicit-weight behavior remain unchanged.

## Context

Resource definitions could name soft preferred terrain/support factors and exact positive or negative weights, but they had no discoverable way to say that a resource should usually avoid a condition. Treating every aversion as a hard tile exclusion would create artificial empty regions, hide geographical shortfalls, and incorrectly ban rare but plausible deposits. Asking authors to encode ordinary aversion as a negative expert weight was possible but obscure and could collapse suitability to the epsilon floor.

The resource layer also serves both the running version-2 tile taxonomy and the normalized version-3 terrain query. The rule therefore needs to use the existing portable support-factor vocabulary rather than couple resource definitions to one editor enum. Steppe continues to normalize through the Grassland/climate contract frozen by [[ADR-0025 - Built-in Steppe Terrain|ADR-0025]].

## Decision

Add an immutable, ordinally sorted `AvoidedTerrainTags` list to `CampaignResourceRuleSet`. It accepts the same code-owned `CampaignResourceSupportFieldIds` vocabulary as preferred tags. A factor cannot be both preferred and avoided in one rule set.

Hard eligibility remains unchanged: medium, inclusive ranges, unassigned terrain, Aquatic water ownership, and custom-terrain include/exclude rules are the only bans. For a normalized factor response `f` in `0..1`:

```text
preferredResponse = 0.12 + 0.88 * f
avoidedResponse   = 0.12 + 0.88 * (1 - f)
```

Both enter the existing weighted geometric mean with magnitude `1`. The `0.12` floor lets an aversion lower ranking and cross an admission floor without making the tile structurally invalid. Explicit negative field/association weights remain the stronger expert control and continue to invert the exact response without the soft floor.

The built-in catalog uses conservative surface/climate aversions only where the relationship is clear:

| Resource | Avoided factors |
|---|---|
| Fertile Land | `arid`, `exposed-rock` |
| Timber | `arid`, `open-land` |
| Fresh Water | `arid` |
| Grazing | `forest`, `relief` |
| Wild Game | `arid`, `exposed-rock` |
| Clay | `exposed-rock`, `relief` |
| Sand and Gravel | `forest` |
| Salt | `freshwater`, `moist` |

Fish, Stone, ores, Coal, Gold, and Silver keep empty avoidance lists because their existing water or geological evidence is more authoritative than a blanket surface aversion.

The custom-resource manager adds **Add avoided** beside **Add preferred** and validates both against the supported registry. The resource-generation dialog shows the selected definition's **Prefers** and **Avoids** lists before candidate generation. Built-ins remain immutable; duplicating one copies both lists for editing.

Unsupported avoided IDs follow the existing honest failure rule: the affected resource produces preserved locks only, its report lists every unsupported ID, and no factor is silently ignored. Manual occurrence diagnostics identify avoided tags as generator-only unevaluated data; they do not mislabel a soft aversion as a hard warning.

`resource-definitions.json` advances from version 1 to version 2 and adds required `avoidedTerrainTags` to every rule record. The reader accepts version 1 by supplying an empty list; every new save writes version 2. Runtime package version 2 does not change because authoring rules are deliberately omitted from runtime export.

## Consequences

- Generation can steer resources away from unsuitable terrain without forcing a categorical ban.
- Users can inspect and customize the rule without writing a negative-weight expression.
- Built-in biological/surface resources gain clearer spatial character; deep geological resources do not inherit arbitrary surface prejudices.
- Coverage remains an upper target. A strong aversion may cause an honest shortfall when too few candidates clear the fixed admission floor.
- Version-1 custom-resource projects remain readable, while older applications correctly reject the new version-2 definition sidecar instead of misreading it.

## Verification boundary

Executable coverage proves defensive copying and preferred/avoided conflict rejection, supported built-in factors, soft `0.12` aversion at a maximum response, continued hard eligibility, end-to-end placement away from otherwise equivalent avoided cells, unsupported-ID lock-only reporting, diagnostic projection, custom-editor round trips and built-in duplication, deterministic save/load, required version-2 data, and version-1 empty-list compatibility.

This decision does not add hard exclusions for built-in terrain, resource inventory/economy state, biome authority, persisted support fields, or a Steppe-specific resource surface enum.
