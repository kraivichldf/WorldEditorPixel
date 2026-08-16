# ADR-0025: Built-in Steppe Terrain

- Status: Accepted
- Date: 2026-08-16

## Context

The active version-2 terrain palette distinguishes wet lowland, wooded land, exposed relief, and true desert, but it has no shared semantic for the broad semi-arid grassland between Plains and Desert. A custom color named “Steppe” is insufficient: it cannot participate in the standard climate classifier, has no portable runtime value, and makes every project redefine a common biome independently.

Steppe must remain a campaign-tile classification. It must not become a sub-tile texture mask, infer elevation, reshape coastlines, or introduce a second biome authority. Existing project and runtime numeric values must remain stable.

## Decision

Add `CampaignTileType.Steppe` as the appended byte value `15`. No existing enum member is renumbered. The canonical project string is `steppe`, and runtime manifests expose the mapping `{ value: 15, name: "steppe" }`.

Steppe means semi-arid grassland: drier and more open than Plains, but not true Desert. It remains one complete tile with the same independent signed whole-metre centre height as every other version-2 type. It receives an olive-gold dry-grass material with stable world-space broad, fine, and restrained directional grass variation. Automatic Sea/Lake edges, slope interpolation, area painting, undo/redo, and River topology work without a Steppe-specific exception.

For the normal classifier, Mountain and Hills/form decisions still run first. Let `A` be the existing aridity value, `M` the existing moisture value, and `Dwater` the cardinal distance to Sea or Lake:

```text
Desert when Dwater >= 4 and A >= 0.68
Steppe when Dwater >= 2 and A >= 0.52 and M < 0.53
Forest when M >= 0.53
Plains otherwise
```

The opt-in inland mix gains an independent whole-number `SteppePercent`. The default balanced mix becomes:

```text
40% Plains + 25% Forest + 8% Desert + 13% Hills
+ 2% Mountain + 12% Steppe = 100%
```

Existing source callers that construct the previous five-value mix default Steppe to `0%`, preserving their requested output. Largest-remainder allocation includes Steppe as a sixth built-in category. After Mountain foothills, custom types, and Desert are reserved, Steppe receives the highest-aridity unassigned cells satisfying:

```text
Dwater >= 2
cardinal grade < 0.04
normalized elevation < 0.34
```

Unsatisfied constrained Steppe share remains Plains, matching the existing honest-target rule for unsuitable Mountain and Desert geography.

Custom terrain may use Steppe as its safe portable base. The version-2 resource adapter normalizes Steppe to `Grassland`; climate and geology support fields remain responsible for resource suitability, so the resource schema does not gain a redundant Steppe surface value. The accepted version-3 migration maps Steppe to `Grassland`, where a future biome/climate layer may retain finer ecological meaning.

## Consequences

- Designers can paint, generate, ratio-control, save, reopen, and export Steppe without creating a project-local type first.
- Steppe forms coherent dry transition regions instead of random per-cell scatter and does not replace relief forms or shoreline ownership.
- Version-2 project readers and runtime importers must recognize the new string/value. Older consumers must reject unknown value `15` rather than silently reinterpret it.
- Existing numeric values `0..14`, old projects, custom identities, resource records, and terrain heights remain unchanged.
- Steppe is still a version-2 combined classification. [[ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]] remains the long-term separation of base surface, terrain form, networks, shore, and later biome data.

The executable formulas and compatibility tests are recorded in [[../Reference/Campaign World Generation|Campaign World Generation]], [[../Reference/World File Format|World File Format]], [[../Reference/Runtime World Package|Runtime World Package]], and [[../Testing/Verification|Verification]].
