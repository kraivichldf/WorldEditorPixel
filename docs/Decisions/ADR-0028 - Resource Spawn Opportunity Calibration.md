# ADR-0028: Resource Spawn Opportunity Calibration

- Status: Accepted and implemented
- Date: 2026-08-16
- Extends: [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]], [[ADR-0026 - Soft Avoided Resource Terrain Factors|ADR-0026]], and [[ADR-0027 - Hard Resource Surface Exclusions|ADR-0027]]

## Context

The first resource generator treated every preferred tag as a separate term in the weighted geometric mean. That made a list such as Copper Ore's `hydrothermal`, `rift`, and `volcanic` cues behave like three simultaneous requirements even though the authoring contract presents them as ordinary alternative evidence. On the standard deterministic `700 × 700 km`, `5 km` Continental world, Copper Ore had `6,895` hard-eligible land tiles and a target of `482`, but only three cells cleared the Vein admission floor and just one occurrence was generated.

The same audit exposed a separate coarse-grid aliasing problem. A Many Small SurfaceDeposit uses a nominal `15 km` radius. On a `20 km` campaign grid that radius was clamped to only `0.75` tile, so cardinal growth could never leave a local-maximum core. This made high-coverage resources appear nearly absent on the supported `10,000 × 10,000 km`, `500 × 500` grid even when many cells were qualified.

Lowering all admission floors, forcing the requested coverage, or stamping random fallback cells would hide both defects and weaken geographical meaning. The response model and discrete physical-scale boundary need explicit calibration.

## Decision

### Alternative ordinary cues

Preferred and avoided tag lists are each one group of alternative ordinary cues. For normalized cue responses `f_i`:

```text
cueStrength       = 0.50 * max(f_i) + 0.50 * mean(f_i)
preferredResponse = 0.12 + 0.88 * cueStrength
avoidedResponse   = 0.12 + 0.88 * (1 - cueStrength)
```

Half peak response allows one strong geographical cue to carry the group. Half mean response still rewards several agreeing cues and prevents one incidental maximum from completely determining suitability. Each non-empty preferred or avoided group contributes once with magnitude `1` to the outer weighted geometric mean.

Explicit field and association weights remain independent exact `0..1` factors. Their positive weights can still require a critical signal, negative weights still invert it, and their magnitudes remain author-controlled. Hard medium, normalized-surface, range, and custom-terrain rules are unchanged.

Unsupported IDs preserve the existing honest behavior: the affected resource produces locks only and reports every unsupported factor rather than evaluating a partial group.

### Admission calibration

The fixed Vein admission floor changes from `0.48` to `0.40`. Field, Basin, SurfaceDeposit, and Aquatic remain `0.30`, `0.42`, `0.38`, and `0.30`. This is a fixed response-scale calibration, not a percentile and not a quota. Cells below the floor remain unavailable.

### Coarse-grid region radius

The effective physical region radius is now:

```text
regionRadiusKm = max(campaignTileSizeKm, baseRadiusKm * concentrationMultiplier)
```

The prior lower bound was `0.75 * campaignTileSizeKm`. Requiring one full tile-centre spacing lets a qualified core reach cardinal neighbours on coarse grids. It does not enlarge normal regions when the authored/default physical radius is already larger, and growth still stops at the qualified boundary, physical radius, or upper target.

## Consequences

- Default geological resources retain spatial character but no longer lose practically all spawn opportunity because several alternative cues do not peak on the same tile.
- Strong avoided evidence still penalizes a tile when any listed aversion is present, while agreement between several aversions strengthens that penalty.
- Common resources remain bounded by their independent coverage targets; the change cannot make them exceed the requested upper target.
- Geography can still produce an honest shortfall or zero when no hard-eligible cell clears the calibrated floor. No unsuitable fallback placement is introduced.
- Accepting a new regeneration may produce a different deterministic candidate than older builds. Existing saved/manual occurrences remain unchanged until the user explicitly accepts a preview.
- Many Small regions on `20 km` tiles can grow across cardinal cells instead of degenerating into isolated one-cell local maxima.

## Verification boundary

Executable tests cover the exact alternative preferred/avoided cue math, unchanged exact negative-weight behavior, the `0.40` Vein floor, meaningful Copper opportunity on the standard Continental reference, nonzero opportunity for every built-in with a positive eligible target across all nine non-Blank world presets, and coarse `20 km` Many Small region growth beyond local cores.

Diagnostic calibration also exercised eight Continental seeds without a missing positive-target built-in and the exact maximum `10,000 × 10,000 km`, `20 km` grid. These diagnostics informed the fixed constants; timing and exact occurrence counts are not serialized product contracts.
