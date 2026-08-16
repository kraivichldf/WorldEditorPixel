# ADR-0020: Preview-First Procedural Resource Generation

- Status: Implemented
- Date: 2026-08-16

## Context

[[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]] through [[ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019]] provide validated resource definitions, sparse occurrences, saved generation settings, terrain queries, manual locks, shared history, persistence, export, and a complete manual editor journey. They deliberately stop before procedural placement.

The next slice must create geographically plausible deposits without treating independent resource coverage as a terrain mix, forcing quotas onto unsuitable cells, replacing manual locks, or mutating the current document before the user reviews the result. It must also avoid reading a live mutable terrain aggregate from a background worker.

> [!NOTE]
> [[ADR-0021 - Reviewed Changed-Lattice Resource Remapping|ADR-0021]] now lifts the current-lattice full-world block described below. Resource-only generation in this ADR remains same-lattice.

## Decision

### Current-lattice generation boundary

Resource generation consumes the open campaign world's existing physical lattice, terrain, centre elevations, Sea/Lake/River topology, and resource catalog. It never changes those inputs. World dimensions and campaign tile size remain editable only through the existing terrain-regeneration workflow.

This slice does not lift the ADR-0019 changed-lattice block. Physical-position lock remapping, merge/drop reporting, and combined terrain/resource regeneration remain a later reviewed boundary.

Before background work starts, the owner thread captures an immutable row-major normalized terrain snapshot through `ICampaignResourceTerrainQuery`. The generator receives that snapshot, a validated catalog, validated settings, the current sparse resource map, and an explicit scope. Equal snapshot values, definitions, settings, seed, lock set, and scope produce equal candidate occurrences and reports independent of catalog evaluation order.

The default resource seed uses a fixed 32-bit mixing function. A terrain generated in the current session derives it from the accepted terrain-generation seed and persists the resulting resource settings. When an older/blank project has no saved terrain seed or resource settings, the editor derives a reproducible fallback from the value-equal world definition plus authoritative row-major terrain contents and labels it as derived from the current world. It never invents an unreported random seed. Randomize explicitly unlocks the seed.

### Scope and replacement

A run targets one of:

- all resources;
- one Renewable or Finite category;
- one stable resource ID;
- an exact non-empty set of stable resource IDs.

[[ADR-0029 - Explicit Resource Generation Selection|ADR-0029]] makes that exact set the native dialog's current operation model. **Included** IDs are the requested replacement scope; **Excluded** IDs keep every current occurrence exactly. Include/Exclude membership is transient and is not a saved generation setting.

Occurrences outside the requested scope are copied unchanged. Inside the scope, every locked occurrence is copied first and remains authoritative even when it is out of profile. Unlocked occurrences in scope are replaced by the generated candidate. Locks count against the upper target; locks above target remain and are reported without creating negative quotas.

Coverage remains independent for every resource:

```text
targetTiles_r = floor(eligibleTiles_r * effectiveCoverage_r / 100)
newBudget_r   = max(0, targetTiles_r - preservedLocks_r)
```

`0%` or disabled means manual-only for that run: unlocked in-scope occurrences are removed while locks remain. Geography may yield fewer than `newBudget_r`, including zero. No candidate below its admission floor is added merely to satisfy a requested percentage.

Sparse, Balanced, and Abundant multiply coverage by `0.60`, `1.00`, and `1.50`, clamped only after multiplication; Custom uses the per-resource values unchanged. Their potential shifts are `-10`, `0`, and `+10`. Poor, Balanced, and Rich add `-15`, `0`, and `+15`, followed by the explicit `-30..+30` richness bias.

Acceptance swaps the candidate resource map and saved settings as one editor document boundary, clears the shared Undo/Redo history, refreshes diagnostics, preserves the terrain and project identity, and marks the document modified. Cancel changes nothing. The result captures terrain/resource revisions, and acceptance rejects a stale candidate.

### Inspectable support fields

Climate and geology are deterministic functions of physical-kilometre tile centres and the saved resource seed. Climate combines profile priors, a north-south temperature tendency, `6.5 C/km` elevation cooling, separate Sea/Lake/River distance influence, prevailing-wind relief exposure, and restrained multi-octave regional noise.

Seeded physical provinces plus coherent boundary fields derive old-crust, volcanic, hydrothermal, sedimentary, fold/shear, rift, granitic, burial, erosion, and competence affinities. Derived renewable/surface fields include moisture, lowland, freshwater, groundwater, biomass, forest capability, open land, ecotone, aquatic productivity, relief, exposed rock, coast transport, and evaporative potential.

Hard medium/range/custom-terrain rules reject invalid cells first. [[ADR-0027 - Hard Resource Surface Exclusions|ADR-0027]] also lets a definition reject exact normalized base surfaces before scoring. Under [[ADR-0028 - Resource Spawn Opportunity Calibration|ADR-0028]], each preferred or avoided tag list is one alternative-cue group. Its cue strength is `0.50 * max(response) + 0.50 * mean(response)`. Preferred groups become `0.12 + 0.88 * cueStrength`; avoided groups become `0.12 + 0.88 * (1 - cueStrength)`. Explicit field and association weights retain their independent exact `0..1` responses. The preferred group, avoided group, and exact factors then use a weighted geometric mean, so one excellent ordinary alternative can carry its group without concealing an explicitly weighted critical weakness:

```text
suitability_r = hardMask_r
              * pow(product(pow(max(epsilon, response_k), abs(weight_k))),
                    1 / sum(abs(weight_k)))
```

Negative explicit weights invert their response. Associations consume shared support fields rather than already-generated occurrences, so resource evaluation order cannot affect output.

The supported factor-ID vocabulary is code-owned and inspectable. Every built-in preferred or avoided tag must resolve. A custom definition that names an unsupported preferred tag, avoided tag, field weight, or association does not silently ignore it: the resource produces locks only and its report lists the unsupported IDs. The custom-definition UI must offer only supported IDs.

### Coherent spatial profiles and potential

Candidate ranking combines suitability with a seeded kilometre-scale profile field. Deterministic local maxima become spatially separated region cores. Four-connected priority growth follows the requested distribution:

- **Field** uses broad climate/surface regions;
- **Vein** uses narrow anisotropic ridges aligned to shared mineral boundaries;
- **Basin** uses unequal low-frequency depressions and sedimentary accommodation;
- **SurfaceDeposit** uses smaller erosion/transport patches;
- **Aquatic** grows only through connected Sea/Lake cells.

`FewLarge`, `Balanced`, and `ManySmall` alter physical core spacing and region radius, not per-cell randomness. Growth stops at the upper target, the qualified-candidate boundary, or the physical region boundary.

When a definition has no explicit region-scale range, the physical defaults are `80 km` for Field, `35 km` for Vein, `65 km` for Basin, `25 km` for SurfaceDeposit, and `70 km` for Aquatic. FewLarge, Balanced, and ManySmall multiply those radii by `1.60`, `1.00`, and `0.60`. The effective radius is never less than one campaign-tile centre spacing, so coarse grids can grow through cardinal neighbours. The fixed suitability admission floors are respectively `0.30`, `0.40`, `0.42`, `0.38`, and `0.30`; they are never percentile thresholds.

Generated potential has rich cores and weaker edges:

```text
n         = clamp01((suitability - admissionFloor) / (1 - admissionFloor))
core      = exp(-(distanceToCoreKm / regionRadiusKm)^2)
raw       = 0.70 * n + 0.25 * core + 0.05 * restrainedDetail
potential = clamp(round(100 * raw) + richnessShift, 1, 100)
```

Richness and abundance shifts are resource-relative and never compare economic value between resource types.

The generator refuses a candidate above `2,000,000` total occurrences and explains that the user must narrow scope or lower independent coverage. This explicit memory boundary prevents the theoretical `250,000 tiles x 256 resources` configuration from attempting tens of millions of dictionary entries while keeping the accepted catalog and active-resource limits unchanged.

### Preview-first native workflow

The Resources workspace exposes **Regenerate resources...**. Its Windows 98 property-workshop dialog provides:

- explicit Included/Excluded resource lists, seed/link state, abundance, climate, and geology controls;
- a searchable resource selector with enabled, coverage, richness, bias, and concentration overrides;
- current and candidate selected-resource maps with shared view state;
- per-resource eligible/requested/actual coverage, occurrence and region counts, mean/maximum potential, preserved/over-target locks, and an explicit shortfall reason.

Generation is cancellable and runs away from the UI thread after snapshot capture. Any settings/scope/catalog change marks the visible candidate stale and disables **Use resources** until regeneration completes. The old candidate remains visible for comparison. Custom definitions already present in the project catalog use the same controls and generator; custom-definition creation remains a separate catalog-authoring task.

Inside the Included set, disabled or `0%` remains locks-only replacement behavior. Excluded resources are copied unchanged regardless of that setting. See [[ADR-0029 - Explicit Resource Generation Selection|ADR-0029]] for the UI and exact-selection boundary.

## Consequences

- Generated resources remain a peer authority and never become terrain types.
- Independent ratios, geography-limited shortfalls, and lock conflicts are visible instead of hidden behind forced quotas.
- Manual resource editing stays lightweight and undoable; accepted regeneration stays an explicit replacement boundary.
- Immutable capture makes background generation safe while keeping version-2/version-3 terrain adapters live and owner-thread oriented.
- The same generator supports built-ins and bounded custom definitions without scripts.
- New/Regenerate World Resources property pages, named presets, diagnostic-field overlays, overview symbols, custom-definition authoring, and changed-lattice remapping remain follow-up surfaces unless explicitly added to this slice.

This decision extends [[ADR-0016 - Orthogonal Campaign Resource Layer|ADR-0016]], [[ADR-0017 - Resource Terrain Queries Diagnostics and History|ADR-0017]], and [[ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019]].

## Render-pass synchronization correction

On 2026-08-16, Windows crash evidence identified an unhandled Avalonia exception when the dual preview first fitted its canvases: `WorldCanvas.Render` raised a viewport event, the dialog synchronously applied it to the peer canvas, and `InvalidateVisual` rejected mutation during the active render pass. The generator, settings, seed, and candidate data were absent from the failing stack.

`WorldCanvasViewportSynchronizer` now dispatcher-defers every cross-canvas application and coalesces rapid requests to the latest viewport. Closing the dialog cancels pending mutation. This preserves shared pan/zoom while making the render boundary explicit; five regression tests cover deferred application, coalescing, both directions, disposal, and invalid same-canvas construction.
