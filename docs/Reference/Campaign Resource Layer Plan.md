# Campaign Resource Layer Plan

- Status: Accepted design; manual workspace, preview-first procedural generation, reviewed changed-lattice remapping, and custom-resource definition management implemented
- Date: 2026-08-15
- Boundary: an orthogonal resource layer for the running version-2 editor that remains portable to the later version-3 terrain model

## Goal

Add natural resource potential/deposits to exact campaign tiles. Resources are geographic authoring data, not current inventory, extraction, production, ownership, depletion, or market value.

One tile may contain any number of different resource types, but only one occurrence of a given resource ID. Each occurrence stores a resource-relative potential from `1` through `100`. Resources share the terrain coordinate and editor selection while retaining separate persistence, generation, editing, validation, rendering, and runtime-export authority.

## Current implementation status

- Implemented on 2026-08-15: engine-neutral definitions/catalog, occurrences, sparse map, immutable generation-settings contracts, validated version-2/version-3 terrain-query adapters, hard-rule diagnostics with unevaluated-factor reporting, delta-based resource commands/stroke history, strict sparse authoring persistence, deterministic runtime package version 2 export, and the ADR-0019 native manual workspace.
- The running editor now provides selected-resource category/filter controls, exact potential, independent complete-tile Paint Area, add/update/erase, default locks, fixed-scale heatmap/labels, hover and pinned inspection, hard-rule warnings, lock/unlock/adopt actions, shared terrain/resource Undo/Redo, staged save/reopen, and version-2 export.
- Implemented on 2026-08-16: immutable terrain/resource snapshot capture, deterministic climate/geology support fields, suitability and coherent spatial profiles, preview-first scoped regeneration with preserved locks and stale-candidate rejection, the Windows 98 **Regenerate resources...** dialog with side-by-side current/candidate maps, accepted settings persistence, and shared-history reset on acceptance.
- Implemented on 2026-08-16: soft avoided terrain/support factors with conservative built-in defaults, custom-resource authoring, generation-dialog visibility, honest unsupported-ID reporting, and version-1-to-version-2 definition-sidecar compatibility. See [[../Decisions/ADR-0026 - Soft Avoided Resource Terrain Factors|ADR-0026]].
- Implemented on 2026-08-16: hard normalized-surface exclusions, default Fertile Land/Timber Desert/BarrenRock/Tundra bans, manual warning preservation, custom authoring, and version-1/version-2-to-version-3 definition compatibility. See [[../Decisions/ADR-0027 - Hard Resource Surface Exclusions|ADR-0027]].
- Implemented on 2026-08-16: alternative preferred/avoided cue-group scoring, calibrated Vein admission, and one-tile-minimum coarse-grid region growth so default resources retain spawn opportunity without forced quotas. See [[../Decisions/ADR-0028 - Resource Spawn Opportunity Calibration|ADR-0028]].
- Implemented on 2026-08-16: explicit arbitrary Include/Exclude selection before generation. Included stable IDs replace unlocked occurrences and preserve locks; Excluded stable IDs keep all occurrence authority exact regardless of saved enabled/coverage settings. See [[../Decisions/ADR-0029 - Explicit Resource Generation Selection|ADR-0029]].
- Implemented on 2026-08-16 under [[../Decisions/ADR-0021 - Reviewed Changed-Lattice Resource Remapping|ADR-0021]]: full-world regeneration now captures immutable sparse resource authority, preserves same-lattice values exactly, maps changed-lattice tile centres in physical metres, reports moves/merges/drops and exact locked drops, regenerates unlocked occurrences only from real saved settings, and accepts the exact terrain/resource candidate atomically.
- Implemented on 2026-08-16 under [[../Decisions/ADR-0022 - Custom Resource Definition Management|ADR-0022]]: the native custom-resource manager can create a manual-only definition, duplicate a built-in, edit the complete bounded definition/rule contract, protect used stable IDs/categories, atomically preserve compatible occurrences, remove stale overrides for deleted unused IDs, and feed save/export/generation without JSON editing.
- Not yet implemented: overview symbols, diagnostic field views, or full World/Terrain/Resources generation property pages.

## Confirmed decisions

- Coverage is an independent percentage of tiles eligible for that resource; resource ratios never sum to `100%`.
- Geography may yield fewer occurrences than requested, including zero. Coverage is an upper target, never a quota that forces unsuitable tiles.
- Coverage, richness, and concentration are separate controls. Concentration selects Few large, Balanced, or Many small regions.
- Global abundance offers Sparse, Balanced, Abundant, and Custom; every resource remains manually adjustable.
- `0%` makes a resource manual-only. At most 256 positive-coverage resources may participate in one generation run; the project catalog has no small UI cap.
- Complete generated worlds use Balanced resources by default. Blank worlds start empty and can generate resources later.
- The resource seed derives reproducibly from the world seed by default and may be unlocked without changing terrain.
- Climate and geology profiles are saved settings. Deterministic diagnostic fields are inspectable but are not stored as dense grids.
- Generation runs after terrain, coast, elevation, erosion, Lakes, and Rivers. It never reshapes them to meet resource targets.
- Related resources correlate through shared fields; generated regions have rich cores and weaker edges rather than tile noise.
- Manual painting updates only the selected resource. Manual occurrences lock automatically and may be unlocked.
- Manual out-of-profile placement is allowed with a visible warning. Terrain changes preserve occurrences and refresh warnings rather than deleting data.
- Resource regeneration supports an exact non-empty set of Included stable IDs, with All/Renewable/Finite/Only-selected presets and individual transfers. Excluded resources remain exact; locks above an Included target remain and are reported.
- Accepting resource regeneration is a previewed replacement boundary that clears Undo/Redo. Ordinary resource painting remains delta-based and undoable.
- Full world regeneration remaps locks by physical world position and reports merges, out-of-bounds drops, and suitability changes before acceptance.
- Built-in identity/core rules remain stable. Projects tune generation/display fields or duplicate a built-in into a custom definition.
- Custom definitions use bounded rule controls, never embedded scripts. Project settings persist; named presets save/load explicitly.
- The main editor uses separate Terrain and Resources workspaces. New/Regenerate World uses World, Terrain, and Resources property pages.

## Authority and data model

```text
Campaign coordinate (x, y)
|- terrain tile and centre height        existing authority
|- River/shore data                      existing or v3 authority
`- zero or more resource occurrences     new authority
```

The in-memory aggregate presents one logical campaign tile. Storage remains layer-specific so resource-only edits do not materialize or rewrite terrain records.

### Resource definition

```text
id                       stable lowercase ID
name                     designer-facing name
category                 Renewable | Finite
symbolId                 bundled portable symbol ID
color                    #RRGGBB
mapPriority              positive display priority
distributionProfile      Field | Vein | Basin | SurfaceDeposit | Aquatic
coveragePercent          0..100 of eligible tiles
richness                 Poor | Balanced | Rich plus optional -30..+30 bias
concentration            FewLarge | Balanced | ManySmall
allowedTerrainRules      built-in/base rules
preferredTerrainTags     supported soft positive terrain/support factors
avoidedTerrainTags       supported soft negative terrain/support factors
excludedTerrainSurfaces  hard normalized base-surface exclusions
customTerrainIncludes    optional stable custom terrain IDs
customTerrainExcludes    optional stable custom terrain IDs
elevationRangeMeters     optional inclusive range
slopeRange               optional grade range
waterDistanceRangeKm     optional preferred/allowed range
regionScaleKm            validated physical-kilometre range
fieldWeights             bounded environmental weights
associationWeights       bounded positive/negative shared-field weights
```

Custom resources may edit all fields while unused. Once used, stable ID and Renewable/Finite category are locked; deletion is blocked until all occurrences are removed. Rule edits affect later generation and warnings, never existing occurrence values.

### Resource occurrence

```text
x, y                     campaign coordinate
resourceId               one stable definition ID
potential                byte, 1..100
locked                   authoring-only boolean
```

Potential `0` is not stored; erasing removes the occurrence. Locks are excluded from runtime data.

### Saved generation settings

```text
schemaVersion
resourceSeed
seedDerivedFromWorld
abundancePreset          Sparse | Balanced | Abundant | Custom
climateProfile           AutoMixed | Tropical | Temperate | Continental | Arid | Cold
geologyProfile           AutoMixed | AncientCraton | VolcanicArc | SedimentaryBasins | FoldBelt | YoungRift
resourceOverrides        sparse settings keyed by resource ID
```

Equal terrain, definitions, settings, seed, and lock set reproduce the same unlocked result.

## Default catalog and ratios

Catalog membership does not guarantee presence. Every built-in starts at resource-relative Balanced richness.

| Resource | Category | Profile | Eligible coverage | Concentration | Main suitability |
|---|---|---|---:|---|---|
| Fertile Land | Renewable | Field | 45% | Few large | Moist lowland, low grade, floodplain/freshwater access |
| Timber | Renewable | Field | 65% | Few large | Forest-capable climate, biomass, moderate relief |
| Fresh Water | Renewable | Field | 35% | Balanced | Rainfall, groundwater basin, River/Lake proximity on land |
| Fish | Renewable | Aquatic | 55% | Few large | Sea/Lake productivity, coast/lake conditions, nutrient runoff |
| Grazing | Renewable | Field | 50% | Few large | Open land, moderate moisture/temperature and grade |
| Wild Game | Renewable | Field | 35% | Many small | Biomass, habitat ecotones, freshwater access |
| Stone | Finite | SurfaceDeposit | 40% | Balanced | Exposed relief, erosion, competent bedrock |
| Clay | Finite | SurfaceDeposit | 25% | Balanced | Fine sediment, low basins, River/Lake proximity |
| Sand and Gravel | Finite | SurfaceDeposit | 35% | Many small | Coast/River transport, outwash and arid exposure |
| Salt | Finite | Basin | 12% | Few large | Arid closed basin or evaporative coast |
| Iron Ore | Finite | Vein | 10% | Balanced | Mineral belts, old crust/fold belt and erosion exposure |
| Copper Ore | Finite | Vein | 7% | Balanced | Volcanic arc, rift and hydrothermal fields |
| Tin Ore | Finite | Vein | 4% | Few large | Granitic/mineralized province and mountain margin |
| Coal | Finite | Basin | 8% | Few large | Sedimentary basin, ancient biomass proxy and burial |
| Gold | Finite | Vein | 2% | Many small | Rare hydrothermal/shear mineralization |
| Silver | Finite | Vein | 3% | Many small | Hydrothermal fields associated with base-metal belts |

Optional packs/custom definitions cover oil, gas, uranium, rare earths, setting-specific crops, and fantasy materials.

| Abundance | Coverage multiplier | Potential shift |
|---|---:|---:|
| Sparse | `0.60` | `-10` |
| Balanced | `1.00` | `0` |
| Abundant | `1.50` | `+10` |
| Custom | per resource | per resource |

Coverage clamps to `0..100`; potential clamps to `1..100`. Potential is not comparable economically across resource types.

## Deterministic generation

### 1. Complete terrain first

The generator consumes physical-kilometre position, surface/type, custom terrain base/ID, centre elevation, derived grade/form, coast/water adjacency, Lakes, and Rivers. It never changes those inputs.

### 2. Build inspectable support fields

All fields use physical-kilometre coordinates and the saved resource seed. Changing campaign tile size changes sampling resolution, not intended feature scale.

Temperature uses climate base, a north-south tendency, physical elevation cooling, and restrained regional noise:

```text
temperatureC = profileBaseC
             + latitudeSpanC * (0.5 - normalizedY)
             - 6.5 * max(0, elevationAboveSeaKm)
             + 2.5 * regionalTemperatureNoise
```

Moisture combines profile moisture, distance transforms from Sea/Lake/River, orographic exposure/rain shadow, and kilometre-scaled noise:

```text
moisture = clamp01(
    profileMoisture
  + oceanLakeInfluence(distanceKm)
  + riverInfluence(distanceKm)
  + regionalMoistureNoise
  - rainShadow)
```

Seeded physical Voronoi provinces and boundary fields derive old-crust affinity, volcanic/hydrothermal activity, sedimentary accommodation, fold/shear mineralization, rifting, erosion exposure, and material competence. The Geology profile changes priors/weights; it does not stamp resources directly.

Derived diagnostics include fertility, biomass, groundwater, aquatic productivity, sediment energy, evaporative potential, and mineral-family potential. They are recomputed lazily rather than serialized as dense authority.

### 3. Evaluate eligibility and suitability

Hard rules reject invalid media such as Fish on land. Normalized base-surface exclusions then reject explicitly forbidden Grassland/Forest/Desert/Wetland/Tundra/BarrenRock/Sea/Lake cells. Custom-terrain include/exclude rules refine custom land, and elevation, grade, and distance ranges remain explicit constraints. Manual or locked out-of-profile authority is diagnosed rather than silently deleted; scoped regeneration removes only unlocked in-scope occurrences before replacement.

Accepted factors become smooth normalized responses `f_k` in `0..1`. A preferred or avoided tag list represents alternative ordinary cues rather than simultaneous requirements. Each list first becomes one group strength and one soft response:

```text
cueStrength       = 0.50 * max(f_k) + 0.50 * mean(f_k)
preferredResponse = 0.12 + 0.88 * cueStrength
avoidedResponse   = 0.12 + 0.88 * (1 - cueStrength)
```

The floor keeps ordinary preference or aversion soft. Explicit field and association weights retain independent exact responses. The non-empty preferred group, non-empty avoided group, and every explicit factor then enter a weighted geometric mean; this lets one strong alternative cue carry its group without letting it hide an explicitly weighted critical weak factor:

```text
suitability_r = hardMask_r
              * pow(product(pow(max(epsilon, response_k), abs(weight_k))),
                    1 / sum(abs(weight_k)))
```

Associations consume shared geology/climate family fields, never already-generated occurrence order. Generation order therefore cannot change results.

### 4. Form coherent regions without forcing quotas

```text
targetTiles_r = floor(eligibleTiles_r * effectiveCoverage_r / 100)
```

Candidates below the resource-specific suitability floor remain unavailable. The fixed Field/Vein/Basin/SurfaceDeposit/Aquatic floors are `0.30/0.40/0.42/0.38/0.30`. Local maxima become deterministic seed candidates with physical-kilometre Poisson separation. Concentration changes seed spacing and region size. The effective radius is at least one campaign-tile centre spacing so coarse grids can grow through cardinal neighbours. Stable hashes resolve exact ties.

- **Field:** broad connected climate/surface regions.
- **Vein:** narrow anisotropic belts following mineralization/shear direction.
- **Basin:** unequal sedimentary/drainage-shaped regions.
- **SurfaceDeposit:** smaller exposed or transported patches constrained by grade, erosion, coast, and Rivers.
- **Aquatic:** connected Sea/Lake productivity regions.

Growth stops at the upper target or when qualified candidates are exhausted.

### 5. Produce coherent potential

```text
n = clamp01((suitability - admissionThreshold) / (1 - admissionThreshold))
core = exp(-(distanceToCoreKm / regionRadiusKm)^2)
raw = 0.70 * n + 0.25 * core + 0.05 * detailNoise
potential = clamp(
    round(100 * raw)
  + abundanceShift
  + namedRichnessShift
  + richnessBias,
  1,
  100)
```

This yields rich cores, weaker edges, and restrained local variation.

### 6. Preserve explicit locks

Resource-only regeneration applies locks first. Over-target locks remain and suppress unnecessary new placement. Full-world regeneration maps old tile centres into the replacement grid by physical metres. Identical IDs merging into one tile retain the highest potential and remain locked when any source was locked. The preview lists exact locked out-of-bounds drops before acceptance. When saved settings exist, old unlocked occurrences are regenerated against the replacement terrain; without saved settings, every in-bounds occurrence remaps and no recipe is invented.

## UX and map display

### Future combined New and Regenerate World flow

The current shipped slice deliberately uses separate **Regenerate resources...** and **Custom resources...** dialogs. A later combined Win98 property sheet may add **World**, **Terrain**, and **Resources** pages. Its Resources page would contain enablement, seed, abundance, climate, geology, a searchable per-resource settings table, an entry to the shipped custom-resource manager, active-generation count, and preset save/load.

The existing terrain preview now includes a bounded textual **Resource impact** well for ADR-0021 changed-lattice consequences. A later combined property sheet may still add dedicated **Terrain** and **Resources** tabs, Overview, diagnostic fields, and preset save/load.

### Main editor

Primary **Terrain** and **Resources** workspace tabs share the canvas, zoom, pan, and pinned coordinate. The implemented Resources rail contains **Custom resources...**, resource/category filter, potential `1..100`, Paint Area, Add/Update, Erase selected, default-on Lock manual edits, and pinned Use/Erase/Lock/Unlock. **Regenerate resources...** opens the preview-first resource-only dialog.

Painting changes only the selected occurrence. Other resources and all terrain/height/network data remain unchanged.

### Map

- Terrain view remains uncluttered.
- Resources view mutes terrain without hiding it.
- Overview displays at most three symbols ranked by `potential × mapPriority`, then `+N`.
- Selecting one resource shows a fixed `1..100` heatmap.
- Selected-resource values appear around `28 px/tile`; hover/inspector always show exact values.
- Bundled symbol IDs plus color remain portable; short labels distinguish reused symbols.
- Lock and out-of-profile warning states use visible badges plus explanatory text, not color alone.
- Diagnostic overlays explain contributing fields and final suitability.

### Resource regeneration

Current and Candidate maps are side by side with synchronized pan and zoom. Before generation, two explicit lists distinguish **Included — Regenerate** from **Excluded — Keep**; category filtering and search affect visibility only. Cross-canvas viewport requests are dispatcher-deferred and coalesced to the latest value because an initial fit notification may originate during a render pass; neither canvas may synchronously invalidate its peer from that stack. Per-resource reporting includes eligible count, requested/actual coverage, occurrence count, mean/maximum potential, region count, preserved/over-target locks, warnings, and zero/shortfall explanation. Excluded resources report unchanged authority rather than a generated report. Resource-only regeneration remains same-lattice. **Regenerate world...** now owns the changed-lattice preview and reports physical moves, same-ID merges, out-of-bounds drops, lock retention, regenerated unlocked counts, and new-terrain shortfalls beside its terrain image.

Cancel changes nothing. Acceptance replaces only the requested unlocked scope and clears Undo/Redo.

## Authoring persistence

```text
MyWorld/
|-- world.json
|-- campaign-tiles.json
|-- resource-definitions.json    optional
|-- resource-generation.json     optional
`-- resource-tiles.json          optional
```

Representative sparse tile record:

```json
{
  "x": 18,
  "y": 9,
  "resources": [
    { "id": "iron-ore", "potential": 72, "locked": true },
    { "id": "fresh-water", "potential": 41, "locked": false }
  ]
}
```

Records sort by coordinate and stable resource ID. Missing resource files mean no occurrences and keep older projects compatible; built-ins remain available. Writers use temporary files and atomic replacement. Readers reject duplicate resource IDs per tile, unknown IDs, invalid potential, invalid coordinates, malformed definitions/settings, and duplicate records. Environmental mismatch is a warning, not corruption.

## Runtime export

The next `.kworld` package version adds:

```text
manifest.json
tiles.bin
resource-index.bin
resource-records.bin
```

`resource-index.bin` contains one eight-byte entry per tile in row-major order:

```text
uint32 firstRecordIndex
uint16 recordCount
uint16 reserved = 0
```

`resource-records.bin` contains sorted four-byte records:

```text
uint16 resourceCatalogIndex
uint8  potential
uint8  reserved = 0
```

The manifest stores the built-in/custom catalog and Renewable/Finite category. Locks and editor symbols are not runtime occurrence state. Catalog indexes support up to 65,535 definitions. Stable order, checksums, and ZIP timestamps make equal worlds byte-identical.

## Performance and validation boundaries

- Maximum world generation remains 250,000 tiles; maximum active generated resources is 256.
- Generation runs off the UI thread, is cancellable, and processes one resource workspace at a time rather than allocating `tileCount × resourceCount` objects.
- Shared distance, climate, terrain, and geology fields reuse pooled dense buffers during one run.
- Diagnostics are lazy and cached by world revision/settings. Persistence remains sparse and export streams bounded buffers.
- Rendering enumerates visible tiles only, caps overview symbols at three, and culls numbers below the readability threshold.
- Determinism tests cover seed stability, evaluation-order independence, physical-scale invariance, resource constraints, zero results, lock conflicts, and coherent potential.
- Editing tests cover layer isolation, one-ID-per-tile, range validation, warnings, lock/unlock, scoped regeneration, history, and remapping.
- Serialization/export tests cover missing-file compatibility, strict roundtrip, stable order, binary offsets/counts/bytes, catalog indexes, and checksums.
- Native acceptance covers property pages, filters, keyboard navigation, painting, lock/warning states, heatmaps, stale preview, side-by-side comparison, Cancel, acceptance, save/reopen, and export.

## Implementation sequence

1. Accept the resource ADRs and freeze built-in IDs plus the version-2/version-3 terrain-query seam. ✅ Implemented on 2026-08-15.
2. Implement engine-neutral definitions, occurrences, sparse map, settings, validation, hard-rule diagnostics, and delta commands with tests. ✅ Implemented on 2026-08-15.
3. Freeze the authoring schemas and runtime package version while implementing atomic authoring persistence and deterministic runtime export. ✅ Implemented on 2026-08-15.
4. Ship the manual Resources workspace, inspector, selected-resource heatmap renderer, painting, locks, warnings, history, save/reopen, and export vertical slice. ✅ Implemented on 2026-08-15.
5. Implement climate/geology support fields, suitability, associations, five spatial profiles, default catalog, and physical-scale tests. ✅ Implemented on 2026-08-16.
6. Add the resource-regeneration dialog with dual preview, explicit Include/Exclude selection, seed/profile controls, per-resource overrides, stale-candidate rejection, and side-by-side current/candidate comparison. ✅ Implemented on 2026-08-16; arbitrary stable-ID selection refined under ADR-0029.
7. Complete performance probes at 19,600 and 250,000 tiles, native accessibility/visual review, docs, self-contained publish, launcher update, and bounded smoke. ✅ Implemented for the preview-first regeneration slice on 2026-08-16.
8. Lift the populated changed-lattice block through deterministic physical-centre remapping, saved-settings unlocked regeneration, exact locked-drop reporting, stale-safe atomic terrain/resource acceptance, focused tests, docs, and publish. ✅ Implemented under ADR-0021 on 2026-08-16.

Do not publish partial resource files from a UI that cannot inspect, edit, validate, and roundtrip them. The shipped manual release completes: paint -> inspect/warn -> lock/unlock -> undo/redo -> save -> reopen -> export -> verify identical resource authority. Preview-first resource generation adds: configure -> generate candidate -> compare -> accept/cancel -> scoped regenerate -> save -> reopen -> export. Full-world regeneration now adds: change grid -> generate terrain/resource candidate -> review moves/merges/drops/locks -> accept/cancel atomically.

## Outside this feature

- ownership, buildings, workers, extraction, production chains, stockpiles, markets, trade, depletion/replenishment simulation, and AI valuation;
- roads, settlements, tactical meshes/instances, and engine-specific assets;
- guaranteed balanced starts or equal access to strategic resources;
- arbitrary generation scripts and dense persisted geology/climate grids.

See [[../Architecture/World Terrain Editor|World Terrain Editor architecture]], [[Campaign Tile Taxonomy v3|Campaign Tile Taxonomy v3]], [[Campaign World Generation|Campaign world generation]], and [[Runtime World Package|Runtime world package]].
