# Verification

## Current automated coverage

The current `dotnet test KingdomWorldEditor.sln -c Release --no-build --no-restore` run executes 433 tests across the active version-2 contract, deterministic editable world generation through ADR-0025, runtime-package export, the retained version-1 implementation, the isolated version-3 Phase 1 domain, and the campaign-resource ADR-0016 through ADR-0029 manual, preview-first generation, changed-lattice remap, custom-definition, terrain-rule, calibrated spawn-opportunity, and explicit Include/Exclude slices. The version-2 and legacy tests cover:

- exact `700 / 5 = 140` axis counts and `19,600` total campaign tiles;
- rejection of partial campaign cells and invalid height definitions;
- sparse full-value tiles, including height-only Unassigned records and implicit-default reset;
- exact height at tile centres, linear midpoint values, bilinear four-centre blending, edge clamping, and boundary continuity;
- nearby-elevation cardinal averaging, 10 m rounding, world-edge omission, and no-neighbour fallback;
- one-stroke undo/redo of both type and centre height;
- centred complete-tile paint-area expansion, edge clipping, negative-radius rejection, and one-command undo/redo of an area footprint;
- derived River/Large River/River Junction cardinal connections; per-type two/three-exit limits; atomic invalid-batch rejection; collision-free two-, three-, and four-branch split templates; and connected-path/split undo/redo;
- River-only render-index consistency across single edits, batches, undo, and redo;
- exact automatic-coast north/east 10% water boundaries, original-material fallback, custom-land identity preservation, nearest-water corner choice, and no-water fallback;
- version-2 save/load equality for Steppe, Desert, Sea, Lake, River, Large River, River Junction, Beach, and Cliff; early `water` normalization; legacy `coastal` to Plains normalization/reporting; and missing-tile-file defaults;
- duplicate, out-of-grid, unknown-type, out-of-range-height, and invalid-river-topology rejection;
- version-1 import, sample ownership averaging, campaign type copy, and unchanged source manifest;
- legacy coordinate/chunk ownership, lazy allocation, height clamping, raise/lower/flatten/smooth behavior, falloff, and sample-stroke history;
- legacy little-endian encoding, malformed chunks, exact sample round trips, and type-only campaign persistence.
- deterministic `.kworld` export, manifest dimensions/orientation/layout/checksum, stable custom terrain indexes, dense row-major default records, exact little-endian bytes, Large River/River Junction/Steppe values and mappings, and ambiguous-extension rejection.

The version-3 Phase 1 tests cover:

- every canonical base surface, sparse base tiles, all-value validation, implicit-default reset, and continuous derived height;
- exact Flat, Rolling, Hills, Mountain, and Cliff threshold boundaries, absolute-elevation Mountain derivation, profile validation, and world-edge clamping;
- automatic Beach/Cliff shore selection across every land surface, explicit per-edge overrides, invalid-edge rejection, and same-edit stale-override cleanup;
- composable Forest + Hills + River + Beach state, River overlays preserving base surface, valid mouths and explicit confluences, segment degree limits, forbidden four-way crossings, and exactly-one shared-edge orientation;
- missing River targets, uphill flow, directed cycles, land-only overlay ownership, removal before water conversion, migration-only unresolved outflow, and valid/invalid regular-large size vocabulary.

The campaign-resource core tests cover:

- exact built-in IDs/default coverages, supported preferred/avoided factors, Fertile Land/Timber hard surface exclusions, custom-definition validation, contradictory-affinity and invalid/duplicate surface rejection, duplicate-ID rejection, and non-mutable catalog/override views;
- occurrence potential `1..100`, same-tile different-resource coexistence, same-tile same-ID rejection, no-op revision stability, empty-tile cleanup, and deterministic `Y/X/ID` enumeration;
- generation-settings defaults, explicit manual-only overrides, unknown/duplicate override rejection, invalid enum/bias rejection, and the `256` active positive-coverage cap with larger manual-only catalogs;
- version-2 and version-3 terrain-query normalization, exact separate Sea/Lake/River distance fields, cardinal-only coast detection, derived terrain-form projection, non-lossy River-feature preservation, and terrain-revision cache invalidation;
- hard-rule diagnostics for unassigned terrain, medium, normalized-surface exclusion, elevation, grade, nearest-water distance, custom-terrain include/exclude mismatches, immutable deterministic issue ordering, unevaluated-factor reporting, and revision-cached occurrence diagnostics over shared resource authority/history;
- strict resource sidecar save/load round trips, missing-file compatibility, duplicate-property/duplicate-record rejection, deterministic save bytes, stale optional-file cleanup, and non-mutation on validation or replacement failure;
- runtime package version-2 resource catalog/index/record manifests, byte-compatible terrain stream preservation, dense zero-count resource indexes, lock-free deterministic occurrence records, definition mismatch rejection, overflow guards, and temporary-package cleanup.
- bounded resource-area queries, selected-resource filtering, stable visible enumeration, and clipped sparse/coordinate traversal;
- editor resource document creation/open invariants, category/selection state, add/update/erase history, pinned lock/adopt/erase actions, warning refresh, same-lattice regeneration preservation, reviewed changed-lattice exact-candidate installation, and stale-candidate non-mutation;
- immutable row-major generation-source capture, terrain/resource revision race rejection, deterministic terrain-seed and world-fallback resource seeds, climate/geology support fields at physical-kilometre scale, alternative preferred/avoided cue-group scoring, calibrated fixed profile admission floors, one-tile-minimum coarse-grid radii, independent-coverage abundance multipliers, lock-preserving scoped replacement, canonical exact Include/Exclude selection, soft avoided-factor placement steering, hard excluded-surface eligible-count/placement enforcement, unsupported-factor lock-only fallback, candidate stale detection, and the explicit 2,000,000-occurrence limit;
- immutable full-world resource-regeneration capture; physical tile-centre mapping across finer, coarser, and smaller grids; maximum-potential/any-lock same-ID merges; exact locked out-of-bounds reporting; saved-settings unlocked regeneration; manual-only all-occurrence remapping; cancellation; and source/candidate revision guards;
- non-timing completion probes for all default resources on the standard `140 × 140` (`19,600` tile) world, meaningful default Copper opportunity on a deterministic Continental world, nonzero positive-target opportunity across all nine non-Blank presets, coarse `20 km` Many Small cardinal growth, and one scoped resource on a `500 × 500` (`250,000` tile) world;
- dispatcher-deferred dual-preview viewport synchronization, latest-value coalescing, current-to-candidate and candidate-to-current application, close/disposal safety, and same-canvas rejection;
- project-level staged terrain/resource round trips, legacy isolation, stale sidecar removal, revision/cancellation guards, rollback, and editor-integrated runtime version-2 export.
- custom-resource addition/selection, built-in duplication including soft and hard terrain rules, advanced-rule round trips, supported-factor/surface enforcement, exact occurrence preservation, used identity/category/deletion protection, stale override cleanup, equivalent-catalog no-op behavior, and one-pass sparse usage counts.

The campaign-generation tests cover:

- blank creation, complete generated grids, height bounds, and immediate repainting through the canonical tile map;
- deterministic equality for equal seeds and changed output for different seeds; deterministic physical-kilometre simplex wavelengths; deterministic tectonic provinces with coherent convergent, divergent, and shear belts; greater ridge continuity along canonical plate-boundary strike than across it; deterministic terrain erosion; and terrain-style relief scaling;
- hierarchical Continental-world output with at least three major unequal landmasses, bounded land share, a broad horizontal ocean crossing, seed-varied macro geography, simultaneous land/Sea map-edge cropping, and physical coastline agreement across `5 km` and `10 km` grids; Island Sea boundaries; multi-component Archipelago land; exact East/West/North/South Coast edge guarantees with bounded seed-varied land/water balance; central Sea with land boundary; and strict Land Only output;
- None hydrology, priority-flood basin Lakes, flow-accumulation Rivers reaching water, generated tributary merges through explicit three-exit River Junctions, physical-distance/accumulation-based Large River downstream reaches, per-type two/three-exit limits, four-way-crossing rejection, and canonical batch validation;
- no stored/generated Coastal values, steep Cliff water adjacency, generated custom land retaining identity on gentle automatic coasts, endpoint-grown Mountain ridge cores with Hill/foothill transitions, localized dry-inland Desert cores, coherent semi-arid Steppe transitions, exact independent Steppe/default/custom eligible-land ratios, unchanged water/drainage/Cliff topology under a custom mix, and invalid-ratio / minimum-grid / maximum-generation-size / invalid-density rejection.
- terrain-style coastal escalation, including a stable reference seed where Rugged produces more Cliff tiles than Gentle.
- directional coastline-character escalation, including deterministic Smooth, Natural, and Rugged geometry, increasing cardinal shoreline complexity, a guaranteed named Sea edge, naturally open non-named boundaries, and complete Sea connectivity;
- the exact maximum `10,000 × 10,000 km`, `20 km` directional Coast grid (`500 × 500 = 250,000` tiles), with valid open-boundary span math, at least `1,500 km` of interior gulf-to-cape relief, at least `900 km` of relief after `1,000 km` band averaging, at least `2,000` cardinal land/water boundary edges, and at least five land components from mainland plus offshore geography.
- deterministic opportunity-based lowland tidal-inlet acceptance, full Sea connectivity to the guaranteed East Coast ocean edge, default-`None` compatibility, strict Land Only suppression, and unknown tidal-inlet setting rejection.

Run:

```powershell
dotnet test KingdomWorldEditor.sln -c Release --no-build --no-restore
```

## Explicit resource Include/Exclude verification on 2026-08-16

- The generation dialog now begins with exact **Included — Regenerate** and **Excluded — Keep** lists. All/Renewable/Finite/Only-selected presets plus individual transfer buttons build an arbitrary mixed set; search and category filtering affect visibility only.
- Generator scope now supports a canonical, distinct, ordinal stable-ID selection with value equality. Empty, invalid, and catalog-unknown selections are rejected before generation, so **Exclude all** remains a safe editing state but cannot launch a meaningless run.
- Included resources preserve locks and replace unlocked occurrences. Excluded resources copy every occurrence exactly even when their saved override is disabled; disabled or `0%` retains its locks-only meaning only when the resource is Included.
- Three focused regressions cover canonical equality/ordering and ID validation plus an end-to-end mixed selection with Included generation, Included disabled removal, report scoping, and exact Excluded preservation. `CampaignResourceGeneratorTests` passed `56/56`; full Release verification passed `433/433`; the Release build completed with zero warnings and zero errors; format verification is clean.
- The Impeccable static UI detector returned no findings for the refactored Win98 dialog. No GUI automation or desktop-control session was used.
- Republished `artifacts/publish/large-world-coasts/World.Editor.exe`; size `102,582,157` bytes, SHA-256 `7CA251C9D4C854478EBDB6115C3A4D7E266374E1292A62635DA9FB7345E49973`. `Launch Tile Editor.cmd` already targets this canonical file.

## Resource spawn-opportunity calibration verification on 2026-08-16

- Reproduced the defect on an actual generated `700 × 700 km`, `5 km` Continental world rather than a uniform synthetic query. Copper Ore had `6,895` hard-eligible tiles and requested `482`, but only three cells cleared the old `0.48` Vein floor and generation produced one occurrence.
- Preferred and avoided lists now aggregate alternative ordinary cues with `0.50 * max + 0.50 * mean` before their existing `0.12` soft remap. Exact field/association weights remain independent critical factors. The Vein floor is recalibrated to `0.40`; other profile floors are unchanged.
- Effective region radius is now at least one campaign-tile centre spacing. This prevents a `15 km` Many Small radius on `20 km` tiles from degenerating into isolated local-maximum cells.
- On the original reference, Copper Ore now generates `460/482`. All built-ins with positive targets generated nonzero results across Continent, Island, Archipelago, East/West/North/South Coast, Inland Sea, and Land Only. An eight-seed Continental diagnostic found no missing positive-target built-in. The exact supported `10,000 × 10,000 km`, `20 km` (`500 × 500`) diagnostic likewise produced every applicable default resource without fallback placement.
- Thirteen regression cases cover exact preferred/avoided alternative math, meaningful default Copper opportunity, all non-Blank preset opportunities, the `0.40` Vein floor, and coarse-grid Many Small growth. Full Release verification passed `430/430`; the Release build completed with zero warnings and zero errors; format verification is clean.
- Republished `artifacts/publish/large-world-coasts/World.Editor.exe`; size `102,576,525` bytes, SHA-256 `4140145865624E850F02FC6779C5AB44F0C79D8C9D7784F1E1526B08E52A06BD`. `Launch Tile Editor.cmd` targets this file. No GUI automation or desktop-control session was used.

## Hard resource surface-exclusion verification on 2026-08-16

- Added immutable normalized `ExcludedTerrainSurfaces` hard rules. Unassigned, unknown, and duplicate entries are rejected; existing manual/locked authority is retained and diagnosed with `TerrainSurfaceExcluded`.
- Fertile Land and Timber now hard-exclude Desert, Barren Rock, and Tundra. The generator calculates eligible coverage after these exclusions and cannot create unlocked occurrences on forbidden cells. Their soft avoidance fields still rank allowed land.
- The custom-resource dialog exposes **Add excluded tile** under **Hard terrain rules**. Built-in duplication preserves the list, and the resource-generation dialog shows **Hard excludes** before candidate generation.
- `resource-definitions.json` now writes version 3 with required `excludedTerrainSurfaces`. Version 1 and version 2 remain readable with empty hard-exclusion lists; version-2 avoidance remains intact. Runtime package version 2 is unchanged.
- Four added regression cases cover built-in defaults/immutable validation, manual diagnostic projection, end-to-end generator exclusion, and version-2 compatibility. Full Release verification passed `417/417`; the Release build completed with zero warnings and zero errors; format verification is clean.
- Republished `artifacts/publish/large-world-coasts/World.Editor.exe`; size `102,576,013` bytes, SHA-256 `BA49CE0DCA607BE8EB44BBE813836B5F564FD32FABA458CCD438C8D8462B3640`. No GUI automation or desktop-control session was used.

## Soft avoided resource terrain-factor verification on 2026-08-16

- Added immutable `AvoidedTerrainTags` beside preferred tags. The same supported factor registry drives both; a rule rejects the same ID in both lists. Strong avoidance uses `0.12 + 0.88 * (1 - response)`, so it lowers suitability without changing hard eligibility.
- Added conservative built-in aversions for Fertile Land, Timber, Fresh Water, Grazing, Wild Game, Clay, Sand and Gravel, and Salt. Geological resources retain empty lists where surface aversion would be misleading.
- The custom-resource dialog now provides **Add avoided**, round-trips the list, and preserves it when duplicating a built-in. The generation dialog displays the selected definition's **Prefers** and **Avoids** factors before generation.
- `resource-definitions.json` now writes version 2 with required `avoidedTerrainTags`; version 1 remains readable and defaults that list to empty. Runtime package version 2 is unchanged because authoring rules are not exported.
- Automated cases cover defensive copying, contradiction rejection, supported built-in lists, soft-floor math, unchanged eligibility, end-to-end placement steering, unsupported avoided-ID reporting, manual diagnostic projection, editor round trips, duplication, and version-1/version-2 persistence.
- Final Release build completed with zero warnings and zero errors. Full Release verification passed `413/413`; `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- Republished the launcher target at `artifacts/publish/large-world-coasts/World.Editor.exe`; size `102,570,381` bytes, SHA-256 `D26EB9DB5BF41D8BD4447988A4A584D119DB5799F439ACBFC6E2396294567996`. No GUI automation or desktop-control session was used.

## Built-in Steppe terrain verification on 2026-08-16

- Appended `CampaignTileType.Steppe = 15` without renumbering any existing project/runtime value. Version-2 JSON round trips canonical `steppe`, and `.kworld` stores byte `15` plus the manifest mapping `steppe`.
- The normal classifier places Steppe only after Mountain/Hills and Desert decisions, using the accepted semi-arid thresholds. A standard East Coast reference produced non-empty regional Steppe without water-facing Steppe cells or majority-land takeover.
- The opt-in mix exposes Steppe independently. A Land Only reference requested `30% Steppe` and received the exact integer target while Plains retained its separate target. The balanced editor mix is `40/25/8/13/2/12` for Plains/Forest/Desert/Hills/Mountain/Steppe.
- Manual painting exposes one Steppe selector entry; the canvas uses a distinct olive-gold dry-grass material. Custom terrain can use Steppe as its safe base, and resource terrain queries normalize it to Grassland while climate support remains independent.
- Focused `CampaignMapGeneratorTests` passed `67/67`. Full Release verification passed `409/409`; the Release solution build completed with zero warnings and zero errors; `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly. The self-contained single-file Windows build was republished at `artifacts/publish/large-world-coasts/World.Editor.exe`; size `102,564,237` bytes, SHA-256 `8247663F437A1346B5B62F313835B818BE2030B4C286670F809991ABDD64647F`. The existing launcher targets this file. No GUI automation or desktop-control session was used.

## Scale-hierarchical directional Coast verification on 2026-08-16

- Corrected the open-boundary span formula so `sigmaMin` cannot exceed `sigmaMax`; the previously failing `10,000 km` coast now generates normally.
- Added the ADR-0024 hierarchy: zero-impact compact compatibility below `1,400 km`; a slow macro shelf and amplified two-dimensional nearshore breakup blended in through `4,200 km`; dynamically bounded supporting features; up to four scaled heterogeneous macro landmarks; and up to three longer sparse island arcs. Smooth/Natural/Rugged fade the compact regional skeleton out, while Flowing Capes intentionally retains its authored smooth cape.
- The focused `CampaignMapGeneratorTests` run passed all `64` tests. Full repository verification passed all `402` tests. The Release solution build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- One exact Natural East Coast diagnostic at seed `17,029` produced `250,000` tiles with `73.3%` land, `1,860 km` of interior shoreline relief, `1,110 km` of relief between averaged interior `1,000 km` bands, `2,274` cardinal land/water boundary edges, and nine land components. Seven additional Natural/Rugged/Flowing cases varied the broad shelf, boundary complexity, and island count without repeating one double-bay/hook stamp. Timings were observed only as a diagnostic and are not a product contract.
- Programmatic whole-grid bitmap renders were inspected against the supplied structural reference. The retained Natural/Rugged construction showed broad asymmetric shelf change, irregular meso-scale inlets/headlands, and separated offshore pieces; obvious paired round bays and thin repeated hooks from intermediate attempts were rejected. No visible computer-control session was used.
- The authoring limit remains honest: a `20 km` campaign tile cannot represent a sub-`20 km` cove or island width. Finer coastlines require a smaller exactly dividing tile size while remaining within the `250,000`-tile cap.
- Published a self-contained single-file Windows build at `artifacts/publish/large-world-coasts/World.Editor.exe`; `Launch Tile Editor.cmd` targets it. Size: `102,563,725` bytes. SHA-256: `A7F2D38C17535924D51CE3541D46B73FE0409E361F51D0F55E68D60E733B1987`.

## Hierarchical Continental-world generation verification on 2026-08-16

- Replaced the `Continent` preset's single domain-warped ellipse with five seed-varied unequal multi-lobe masses, ten regional bay cuts, two three-island arcs, physical-kilometre warp/coast/detail fields, and sparse ocean anchors. The editor now labels the compatibility enum value **Continental world**.
- Five new executable cases cover three stable seeds at `700 × 350 km`, at least three major components above 2% of world area, dominant/secondary size hierarchy, `24%..46%` land share, a horizontal Sea run at least 18% of map width, at least 12% seed-to-seed land/water change, simultaneous boundary land and Sea, and at least 86% macro-mask agreement between equivalent `5 km` and `10 km` grids.
- The focused `CampaignMapGeneratorTests` run passed all `63` tests. Full repository verification passed all `401` tests with `dotnet test KingdomWorldEditor.sln -c Release --no-build --no-restore`; the Release build completed with zero warnings and zero errors.
- Programmatic tile renders were inspected for seeds `17,029`, `91,337`, and `902,117` on `140 × 70` and `140 × 140` grids. The first pass exposed five equal cookie-shaped islands; the retained formula was then retuned to a dominant/large/medium/small/microcontinental hierarchy before acceptance. No visible computer-control session was used.
- Published a self-contained single-file Windows build at `artifacts/publish/continental-world/World.Editor.exe`; `Launch Tile Editor.cmd` targeted it at that checkpoint. Size: `102,560,141` bytes. SHA-256: `0D337E7E73F6813A05847C7F2D3AA6AE31BBBB092FDABD7A5A07A3EBCF4E94AA`.

## Custom-resource definition manager verification on 2026-08-16

- Added `9` focused custom-resource editor/view-model tests plus one sparse multi-ID usage-count test. The combined custom-resource/map filter passed all `16` selected tests.
- Automated coverage proves custom addition and selection, built-in duplication, complete advanced-rule round trips, supported-factor enforcement, exact occurrence/lock preservation, editable non-identity rules on used definitions, atomic used-category/deletion rejection, stale generation-override cleanup, equivalent-catalog no-op behavior, and deterministic one-pass usage counts.
- Full repository verification passed all `396` tests with `dotnet test KingdomWorldEditor.sln -c Release --no-build --no-restore`. The Release solution build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- Avalonia XAML compilation validates the new Windows 98 property-workshop dialog and both menu/rail entry points. A fresh in-thread Impeccable finish review returned `disposition: ship`: the established type/material language, left catalog/right grouped form topology, fixed Apply/Cancel footer, text usage locks, empty/disabled/error states, keyboard-native controls, and explicit history consequence match the existing surface contract.
- No visible computer-control walkthrough or screenshot capture was used. The UI conclusion is bounded to compiled XAML plus static native review; correctness comes from executable domain/view-model tests. A later human pass may assess unusual Windows text scaling without changing the data-safety claim.
- Published a self-contained single-file Windows build at `artifacts/publish/custom-resources/World.Editor.exe`; `Launch Tile Editor.cmd` targeted it at that checkpoint. Size: `102,549,901` bytes. SHA-256: `B37BA1A98101462A1E0F2B78778148A87D132122762159E2141B95E6DC525629`.
- A bounded hidden startup smoke observed a real main-window handle (`PID 31892`, handle `2362608`) and then stopped only that launched process.

## Impeccable design-record maintenance on 2026-08-16

- Refreshed `.impeccable/design.json` from the implemented Windows 98 Property Workshop contract while preserving `DESIGN.md`, application source, and project configuration.
- The schema-version-2 sidecar parses successfully, its color metadata matches every `DESIGN.md` frontmatter color key exactly, all seven representative component snippets use supported kinds, and its narrative now carries the design document wording rather than the retired dark-console direction.
- A follow-up Impeccable doctor pass no longer reports the stale-sidecar finding. The project records `"buildPath": "code"`, keeping future Impeccable work direct and implementation-led for this established native editor.
- The Codex marketplace plugin itself remains at `4.1.0`. The supported targeted updater completed the Git checkout but rejected it under its fixed 30-second clone timeout, so it did not replace the installed cache or modify source. The npm `impeccable` package is `3.6.0` and was not installed as a downgrade.

## Reviewed changed-lattice resource remapping verification on 2026-08-16

- Full repository verification passed all `381` tests with `dotnet test KingdomWorldEditor.sln -c Release --no-restore`.
- Focused regenerator and editor regeneration/resource coverage passed all `24` tests with `dotnet test src/World.Tests/World.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~CampaignResourceWorldRegeneratorTests|FullyQualifiedName~EditorViewModelRegenerationTests|FullyQualifiedName~EditorViewModelResourceTests"`.
- `dotnet build KingdomWorldEditor.sln -c Release --no-restore` completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The bounded Impeccable detector returned no findings for `NewWorldDialog.axaml`, its code-behind, and the main-window integration.
- Automated coverage proves exact same-lattice copying; non-normalized physical-centre remapping; finer-grid movement; coarser same-ID maximum-potential/any-lock merges; exact locked out-of-bounds coordinates; saved-settings lock-first unlocked regeneration; no-settings all-occurrence remapping; immutable source independence; cancellation; exact candidate installation; project identity/history behavior; and stale source/candidate rejection without document replacement.
- No native computer-control walkthrough was used for this slice. The Win98 resource-impact well is covered by XAML compilation, bounded detector review, explicit text-state code, and the executable domain/integration tests; a later human visual pass may validate wrapping under unusual system text scaling without changing the correctness claim.
- The self-contained executable was published at `artifacts/publish/changed-lattice-remap/World.Editor.exe`; `Launch Tile Editor.cmd` targets it. SHA-256: `78C9772C2FB4537D9EC294E411086440DDF36CCD63CAEF4B980BE87B95B09250`.
- A bounded hidden startup smoke observed a real main-window handle (`PID 7428`, handle `36703420`) and then stopped only that launched process.

## Resource-generation dual-preview crash correction on 2026-08-16

- Windows Application and .NET Runtime events at `11:48:38` recorded the published editor terminating with `System.InvalidOperationException: Visual was invalidated during the render pass`. The exact stack was `WorldCanvas.Render -> ApplyFitIfPossible -> RaiseViewportChanged -> ResourceGenerationDialog.SyncViewport -> peer WorldCanvas.ApplyViewport -> InvalidateVisual`; a matching crash dump was created at `C:\Users\User\AppData\Local\CrashDumps\World.Editor.exe.12072.dmp`.
- The resource generator, seed, settings, and candidate data were not on the failing stack. The root cause was synchronous peer-canvas invalidation from a viewport fit event raised inside an active Avalonia render pass.
- `WorldCanvasViewportSynchronizer` now queues cross-canvas application through the UI dispatcher, coalesces repeated requests to the latest viewport, supports both directions, and drops pending work after dialog close.
- Five new regression tests prove deferred peer mutation, latest-value coalescing, both synchronization directions, disposal safety, and same-canvas rejection. The focused viewport/resource command passed all `22` tests.
- Full repository verification passed all `386` tests. The Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The corrected self-contained build was published at `artifacts/publish/resource-generation-crash-fix/World.Editor.exe`, and `Launch Tile Editor.cmd` now targets that build. SHA-256: executable `78C9772C2FB4537D9EC294E411086440DDF36CCD63CAEF4B980BE87B95B09250`; managed editor assembly `6A5172ABCEDB91AD0BA5BFCC307A7EE16691DF7D5BD6DFE9ED94FF03E4CC02FA`.
- A bounded hidden startup smoke observed a real main-window handle (`PID 35420`, handle `462066`) and then stopped only that launched process. This proves clean startup of the corrected artifact; the render-pass regression itself is guarded by the new deterministic synchronization tests.

## Preview-first procedural resource generation verification on 2026-08-16

- Full repository verification passed all `370` tests with `dotnet test KingdomWorldEditor.sln -c Release --no-restore`.
- Focused generator/view-model/viewport/resource-document coverage passed all `63` targeted tests with `dotnet test src/World.Tests/World.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CampaignResourceGeneratorTests|FullyQualifiedName~CampaignResourceGenerationViewModelTests|FullyQualifiedName~WorldCanvasViewportTests|FullyQualifiedName~EditorViewModelResourceTests"`.
- `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- `dotnet build KingdomWorldEditor.sln -c Release --no-restore` completed with zero warnings and zero errors after the bounded native-review process was closed.
- A fresh self-contained executable was published at `artifacts/publish/resource-generation/World.Editor.exe`, and `Launch Tile Editor.cmd` now targets that build.
- A bounded hidden Windows startup smoke launched only that canonical published executable, observed a real main-window handle (`PID 36580`, handle `396272`) within eight seconds, then stopped only that launched process. `artifacts/publish/resource-generation-preview` is a historical intermediate artifact and is not the launcher target.
- A bounded native walkthrough created the standard `700 × 700 km`, `5 km` (`140 × 140`, `19,600` tile) world, painted broad Plains bands, opened **Regenerate Resources**, and produced an all-resource candidate containing `36,782` occurrences while the current map stayed at zero. The live run exposed an initial selected-resource/heatmap state mismatch that automated tests had not revealed; initialization and world-derived-seed preservation were corrected before the final build, full test run, publish, and smoke.

## Manual campaign-resource workspace verification on 2026-08-15

- `dotnet build KingdomWorldEditor.sln -c Release --no-restore` completed with zero warnings and zero errors after the final native-inspector correction.
- Full repository verification passed all `317` tests with `dotnet test KingdomWorldEditor.sln -c Release --no-build --no-restore`.
- Focused ViewModel resource tests passed `10/10`; combined resource/regeneration ViewModel tests passed `13/13`.
- Focused visible-area resource map tests passed `10/10`; focused project-coordinator tests passed `10/10`.
- Native Windows acceptance created the default `700 × 700 km`, `5 km` (`140 × 140`, `19,600` tile) blank world, entered Resources, painted locked Clay at `50/100`, zoomed beyond `28 px/tile` to verify the exact map number, pinned the tile to inspect the hard-rule warning and unevaluated factors, unlocked it, and used shared Undo to restore the lock. Terrain surface height and selected-resource authority remained separate inspector facts.
- Native evidence: `.impeccable/review/resources-workspace-confirmed.png` and `.impeccable/review/resources-pinned-diagnostics-confirmed.png`.
- Final static finish/accessibility review then tightened stable-ID visibility, pinned-outline priority, empty occurrence state, footer fit, and selected-warning contrast; focused re-review returned clean. A separate correctness review found and verified the fix for in-flight stroke lifecycle: capture/focus loss now rolls live edits back, and document/history commands cancel the stroke and require a deliberate retry before save/export/replacement.
- Automated coordinator coverage verifies terrain/resource save/reopen and runtime version-2 export. The native pass did not write a user project folder.
- The self-contained Resources-enabled executable was published at `artifacts/publish/manual-resources/World.Editor.exe`; `Launch Tile Editor.cmd` targets it. A bounded hidden Windows startup smoke produced a real main-window handle before only PID `39900`, launched by that check, was stopped.
- Known product accessibility boundary remains keyboard traversal/pinning/stamping on the custom canvas; standard rail/menu controls retain native keyboard semantics. This was already outside the mouse-led map-stamping milestone and remains explicit rather than silently claimed.

## Campaign-resource persistence/export foundation verification on 2026-08-15

- `dotnet build KingdomWorldEditor.sln -c Release --no-restore` completed with zero warnings and zero errors.
- Targeted resource-domain verification passed all `110` focused tests with `dotnet test src/World.Tests/World.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CampaignResource"`.
- Full repository verification passed all `293` tests with `dotnet test KingdomWorldEditor.sln -c Release --no-build --no-restore`.
- `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- Resource persistence verification passed all `39` tests with `dotnet test src/World.Tests/World.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CampaignResourceSerializationTests"`.
- Runtime-package verification passed all `14` tests with `dotnet test src/World.Tests/World.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CampaignWorldRuntimeExporterTests"`.
- This earlier phase validated the ADR-0016 through ADR-0018 resource foundation: definitions/catalog, sparse occurrences, version-neutral terrain queries, hard-rule diagnostics, shared resource history, strict sparse resource sidecar persistence, and deterministic runtime package version 2 export. ADR-0019 now integrates the manual editor/save/export path above; procedural resource generation and regeneration preview remain unclaimed.

## Version-3 Phase 1 verification on 2026-08-10

- `dotnet build KingdomWorldEditor.sln -c Release --no-restore` completed with zero warnings and zero errors, including the unchanged desktop editor.
- All 88 tests passed; this consists of 28 new version-3 core tests plus the unchanged 60-test version-2 and legacy baseline.
- `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- No version-3 serializer, migration, editor control, renderer, executable, or startup behavior is claimed by this phase.

## Version-2 verification on 2026-08-10

- Release compilation completed with zero warnings and zero errors after the tile-authoritative UI replacement.
- All 60 tests passed after adding deterministic Coastal material derivation and Coastal persistence coverage.
- `dotnet format --verify-no-changes` completed cleanly.
- The self-contained `tile-only` and standard `win-x64` executables were republished and each remained healthy through a bounded five-second Windows startup smoke test.
- `Launch Tile Editor.cmd` now resolves to the current `tile-only-current` executable; the previous `tile-only` process was deliberately left untouched because it held an unsaved session.

The initial tile-only verification could not establish a Windows desktop-control session. A later bounded capture for the River correction is recorded below; the broader visible journey remains the manual acceptance path.

## River rendering correction on 2026-08-10

- Removed the full-cell River-water raster that made a campaign tile appear completely flooded.
- River now renders as a narrow connected bank, water core, and highlight over a grass-textured version-2 fallback; isolated tiles remain small source pools.
- A River endpoint with fewer than two River neighbours visually extends to an adjacent Sea or Lake as a mouth.
- The River cursor uses the same connected glyph and the tool copy explicitly says that grass remains visible around the channel.
- A bounded Windows capture of the rebuilt editor with a live five-tile River stroke confirmed that the route no longer uses full-cell cyan water.
- Release compilation completed with zero warnings and zero errors, all 88 tests passed, formatting verification was clean, and the Impeccable mechanical detector returned no findings for the changed UI files.
- The self-contained `tile-only-current` executable was published through an alternate build-output path so two unsaved running editor sessions were not interrupted; it remained healthy through a bounded five-second startup smoke test.

## Campaign-only workflow restoration on 2026-08-10

- Removed the separate generated-raster snapshot pipeline, its editor commands, preview state, domain implementation, and dedicated tests.
- Campaign tile type plus centre height is again the only authored terrain source; automatic slopes, material textures, grid display, and live **Height only** shading remain available.
- Release compilation completed with zero warnings and zero errors, all 88 remaining tests passed, and formatting verification completed cleanly.
- The self-contained `tile-only-current` executable was republished and remained running through a bounded five-second startup smoke test.

## Deterministic editable world generation verification on 2026-08-11

- Added 28 focused generation tests for determinism, every preset's hard geography constraints, height bounds, basin Lakes, water-reaching non-branching Rivers, Coastal/Cliff adjacency, terrain-style escalation, bounded connected Mountain-system coverage control, generation limits, canonical map validation, and immediate repainting.
- All 116 repository tests passed. Release compilation completed with zero warnings and zero errors, and formatting verification completed cleanly.
- The default `700 × 700 km`, `5 km` Island at seed `17,029` produced the exact `140 × 140 = 19,600` editable tile grid with coherent land/Sea separation, basin Lakes, Rivers, and directional shores. Rugged terrain produced more generated Cliff coast than Gentle under the stable reference test.
- The Impeccable mechanical detector returned no findings for the New World flow. The Windows control bridge found the running Avalonia window but could not capture its state in the available nested execution context, so no new full visual-journey claim is made here.
- A clean single-file publish exposed that SkiaSharp's native library had previously been supplied only by stale sidecar files. `IncludeNativeLibrariesForSelfExtract` is now a project-level publish rule, and the native-bundled `tile-only-current` executable remained running through a bounded five-second startup smoke test.

## Desert tile verification on 2026-08-11

- Added `Desert` as an appended version-2 campaign-tile enum value, so existing numeric values and early `water` migration remain unchanged.
- Version-2 persistence now round-trips canonical `"desert"` records. The editor palette renders a warm dry dune-and-stone material distinct from full-tile Beach sand.
- The deterministic lowland classifier uses a separate three-octave aridity field plus distance from Sea/Lake. For the `700 × 700 km`, `5 km`, East Coast reference definition at seed `17,029`, Balanced terrain, Balanced Mountain density, and no hydrology, it produced `1,490` Desert tiles out of `14,909` land tiles (about 10%); Mountain/Hills and direct shores retain higher classification priority.
- Release tests completed with `117` passing tests and formatting verification completed cleanly.
- A self-contained Desert-enabled executable was published at `artifacts/publish/tile-only-desert/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second Windows startup smoke test passed without disturbing the already-open prior editor session.

## Canvas rendering optimization on 2026-08-11

- The procedural surface now samples a pooled dense visible-region snapshot, renders large raster rows in parallel into a pooled BGRA buffer, and reuses a same-size `WriteableBitmap`. The material, Coastal, interpolated-height, grayscale, and elevation-marker formulas are unchanged.
- Middle-drag panning translates the cached raster during pointer movement and performs one exact reraster after release. Grid, border, River, selection, and cursor overlays continue to use the live view transform.
- `CampaignTileMap` now maintains a River-only index. River overlays no longer scan every materialized generated tile on hover, and the single-tile River topology check no longer allocates a temporary dictionary and affected-coordinate set.
- A code-only Release probe used a `1,100 × 800` render target and a generated `140 × 140` Island containing `19,600` materialized tiles and `161` River tiles. Median time on the verification machine was `41.29 ms` for seven forced full raster frames, `3.46 ms` for 30 cached frames, and `4.52 ms` for 30 translated panning frames. These workstation measurements are comparative engineering evidence, not cross-machine product guarantees.
- All 118 tests passed, including the new River-index consistency coverage. Release compilation completed without warnings, formatting verification was clean, and the Impeccable mechanical detector returned no findings for `WorldCanvas`.
- The self-contained optimized executable replaced `artifacts/publish/tile-only-desert/World.Editor.exe`; the existing launcher still targets it, and a bounded five-second hidden Windows startup smoke test passed.

## Nearby elevation helper verification on 2026-08-11

- **Copy centre** reads the exact stored height of the right-click pinned tile into the ordinary active stamp. **Blend around** uses the 10 m-rounded arithmetic mean of valid N/E/S/W neighbour centre heights and excludes missing world-edge directions.
- Three focused domain tests cover four-neighbour averaging and rounding, two-neighbour world-edge behavior, and one-tile fallback. All 121 tests passed in the first implementation test run.
- A bounded code-rendered `1,440 × 1,100` desktop inspection confirmed that both actions remain visible beside the pinned tile, rather than being hidden below the long terrain palette. The helper copy, values, buttons, and surrounding inspector fit without overlap.
- Release verification completed with all 121 tests passing, clean formatting, zero compilation warnings, and no Impeccable detector findings across the changed editor UI files.
- The self-contained executable at `artifacts/publish/tile-only-desert/World.Editor.exe` was republished, remains the launcher target, and passed a bounded five-second hidden Windows startup smoke test.

## Custom inland tile-ratio verification on 2026-08-11

- **Adjust inland tile ratios** now has whole-percentage targets for Plains, Forest, Desert, Hills, Mountain, and Steppe. The six values must total exactly 100%, and the UI caps Mountain at 12%.
- The ratio pool excludes Sea, Lake, River, Coastal, and Cliff. A paired baseline/custom generation test confirms that enabling ratios does not move or reclassify any water, drainage, or shore tile.
- Largest-remainder apportionment makes integer targets sum to the exact inland pool. A Land Only reference with `50% Plains`, `30% Forest`, `0% Desert`, `20% Hills`, and `0% Mountain` produced the exact requested counts. Separate validation coverage rejects an invalid total and a Mountain target above 12%.
- Mountain continues through coherent connected-range selection; Desert remains limited to sufficiently inland lowland. If either candidate set is smaller than its target, Plains receives the shortfall rather than forcing geographically invalid labels.
- A bounded code-rendered `680 × 1,000` New World inspection is stored at `artifacts/ui/16-custom-land-mix-dialog.png`; the disclosure, five controls, constraint copy, and inline `Total: 100% — ready` state were legible without overlap inside the existing scrollable form. The Impeccable detector returned no findings for the changed dialog files.
- All 124 tests passed in Release, formatting verification was clean, and the sequential Release solution build completed with zero warnings and zero errors.
- The self-contained executable at `artifacts/publish/tile-only-desert/World.Editor.exe` was republished. The launcher still targets it, and the process remained healthy through a bounded five-second hidden Windows startup smoke test before only that launched process was stopped.

## Whole-tile paint-area verification on 2026-08-11

- Added `CampaignTileArea`, a bounded row-major selection of complete campaign tiles. Expansion `r` produces a centred `(2r + 1) × (2r + 1)` footprint and clips safely at the world boundary.
- The editor exposes expansion `0…12`, meaning `1 × 1` through `25 × 25` tiles. The live cyan preview uses the exact same clipped footprint as the click and drag writer. Every covered tile receives the active type and centre height; overlapping drag footprints are deduplicated by the existing stroke builder.
- River deliberately remains `1 × 1`, preserving its Manhattan routing and non-branching, at-most-two-exit topology. The paint-area control is disabled while River is selected and its readout reports the fixed route.
- Four focused domain tests cover centred expansion, edge clipping, rejection of invalid negative input, and one-command undo/redo of a `3 × 3` area. Release test run: `128` passed. Release build completed with zero warnings and zero errors, and `dotnet format --verify-no-changes --no-restore` completed cleanly.
- The Impeccable detector returned no findings for the changed canvas, view-model, and left-rail UI files. The self-contained executable at `artifacts/publish/tile-only-desert/World.Editor.exe` was republished; the existing launcher still targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Tidal-inlet generation verification on 2026-08-11

- Added **Tidal inlets** to New World with `None`, `Few`, `Balanced`, and `Drowned coast`. The default is `None`, so untouched existing generation inputs retain their prior output.
- The inlet pass begins from the already-resolved ocean, chooses lowland single-edge shore mouths and bounded inland targets, and uses a deterministic low-ground A* route. It reruns ocean resolution afterward, so every generated inlet remains Sea-connected and the normal Coastal/Cliff, height, Lake, and River passes see the final coast.
- At that release, the reference `700 × 700 km`, `5 km` East Coast at seed `17,029` and **Drowned coast** produced additional Sea tiles farther inland than the same `None` run while retaining Sea on the named east edge and land on the west edge. Repeated generation was identical. The later opportunity-based inlet and open-boundary decisions supersede the fixed seed/count and west-edge assumptions.
- Release tests completed with `132` passing tests. Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The Impeccable detector returned no findings for the changed New World dialog files. The self-contained executable at `artifacts/publish/tile-only-desert/World.Editor.exe` was republished; `Launch Tile Editor.cmd` still targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Tile-type selector verification on 2026-08-12

- Replaced the permanently expanded terrain-type palette with one native **Terrain type** selector. The popup retains every tile type's swatch, name, and description, and the selected description remains visible beneath the control before painting.
- Added a name-only fallback string for a type option, so standard ComboBox and accessibility presentation never expose the record's implementation text.
- Release compilation completed with zero warnings and zero errors. All `132` existing tests passed and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The Impeccable detector returned no findings for the changed selector files. An existing editor session was running from the prior publish target, so it was left untouched. The new self-contained executable was published at `artifacts/publish/tile-only-selector/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Safe custom land tile-type verification on 2026-08-12

- Added a versioned optional custom terrain catalog, validated safe-base tile IDs, and palette/render support for up to twelve named, colored land variants. A painted custom type rejects removal or a base change, and a tile rejects an unknown or base-mismatched custom ID.
- Version-2 roundtrip coverage proves the catalog and a per-tile custom ID survive save/reopen. Existing worlds without `custom-terrain.json` remain canonical and keep their ordinary base types.
- Deterministic-generation coverage proves custom types receive their own largest-remainder portions of one combined inland mix, repeated runs retain the same ordered entries, and a custom Mountain type can receive its independent allocation even when ordinary Mountain is zero. Water, River, Coastal, Cliff, and other topology outputs remain outside this allocation.

## Independent custom terrain-mix verification on 2026-08-12

- Positive custom terrain percentages now share the same exact inland `100%` budget as Plains, Forest, Desert, Hills, Mountain, and Steppe; their safe base is only a serialized fallback and material foundation.
- Coverage rejects custom shares without a configured inland mix, rejects an overfilled default/custom total, rejects custom shares above the entire inland pool even when their bases differ, and proves deterministic independent Plains and Mountain custom allocations.
- Verification: `dotnet build KingdomWorldEditor.sln --no-restore`, `dotnet test src/World.Tests/World.Tests.csproj --no-build`, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` passed with `137` tests.
- The desktop manager is reachable from **Terrain**, the left rail, and **New World**. Its base lock/deletion lock protects painted data; `0%` reads as paint-only and positive shares explicitly describe an independent portion of the inland mix.
- Release tests completed with `135` passing tests. `dotnet build KingdomWorldEditor.sln -c Release --no-restore` completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The Impeccable detector returned no findings for the changed menu, rail, canvas, view-model, and dialog files. The self-contained executable was published at `artifacts/publish/tile-only-custom-types/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Natural directional-coast verification on 2026-08-12

- Replaced the former one-dimensional directional coast threshold with a canonical two-dimensional, kilometre-scaled field. The broad shelf now composes seeded bays, headlands/peninsulas, nearshore variation, and optional offshore island groups before the existing smoothing and ocean-resolution passes.
- Added **Coast character** to New World for East, West, North, and South Coast only. **Smooth shelf**, **Natural mixed coast**, and **Rugged coast** control shoreline complexity independently from **Tidal inlets**; Natural is the default. Non-directional presets disable the selector because their own masks define the complete landmass.
- The stable `700 × 700 km`, `5 km`, East Coast, seed `17,029` test proves cardinal land/water boundary complexity increases from Smooth through Natural to Rugged. A second test proves Natural is deterministic, keeps the east edge Sea and west edge land, and leaves every Sea tile connected to the east-edge ocean. Invalid style values are rejected.
- Verification: `dotnet build KingdomWorldEditor.sln --no-restore` completed with zero warnings and errors, `dotnet test src/World.Tests/World.Tests.csproj --no-build` passed all `140` tests, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` was clean.
- The Impeccable detector returned no findings for the changed New World dialog and generation-status UI. The self-contained executable was published at `artifacts/publish/natural-coast/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Characteristic coastal-landmark verification on 2026-08-12

- Natural and Rugged directional coasts now rotate through four deterministic landmark systems: a deep gulf with reinforced cape jaws, a four-lobe hooked cape, a shallow sound behind three barrier islands, and a channel beside one major offshore island with satellites. Smaller independent bays, peninsulas, and island groups remain supporting detail rather than the primary silhouette.
- Landmark geometry evaluates rotated ellipses in physical kilometres. Natural targets two systems per `700 km`; Rugged targets 3.2; Smooth deliberately has none. Counts remain bounded and seed-repeatable, and the starting type rotates so consecutive systems do not collapse into one repeated motif.
- The stable Natural `700 × 700 km`, `5 km`, East Coast at seed `17,029` must now contain at least `100 km` of coast-normal shoreline excursion and at least one separated offshore land component. Existing tests still prove increasing Smooth/Natural/Rugged boundary complexity, determinism, named edge guarantees, and complete ocean connectivity.
- Verification passed all `141` tests, a zero-warning build, and clean formatting. The self-contained executable was published at `artifacts/publish/characteristic-coast/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Runtime world-package export verification on 2026-08-12

- Added **Export Runtime Data…** under File, `Ctrl+E`, and a quiet toolbar **Export** action. Save remains the primary authoring action; export does not alter project identity or dirty state.
- `.kworld` is a deterministic ZIP package containing a self-describing `manifest.json` and dense `tiles.bin`. Every row-major tile is exactly four bytes: normalized type, indexed custom identity or `255`, and little-endian signed centre height in metres.
- Automated coverage verifies world dimensions, north-west/Y-south orientation, record layout, byte length, SHA-256, stable custom index, implicit-default output, exact negative/positive height bytes, identical repeated package bytes, and rejection of an ambiguous `.raw` extension.
- Verification passed all `144` tests, a zero-warning build, and clean formatting. The Impeccable detector returned no findings for the File/toolbar export workflow. The self-contained executable was published at `artifacts/publish/runtime-export/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Flowing bay-and-cape coastline verification on 2026-08-12

- Added **Flowing bays and capes** as a distinct directional Coast character based on the supplied reference silhouette. It uses overlapping smooth Gaussian mainland lobes for a rounded headland, deep bay, shoulder, and lower cove, then unions one physical-kilometre cubic-Bézier peninsula whose radius tapers continuously toward its tip.
- The profile deliberately remains one connected mainland and does not require offshore islands. Seed variation mirrors/shifts the regional composition and adjusts scale without replacing it with uniform high-frequency noise. At that release, forced masks guaranteed both named Sea and opposite land edges; [[../Decisions/ADR-0014 - Open Directional Coast Boundaries|ADR-0014]] later removed the opposite-edge guarantee while preserving complete Sea connectivity.
- At the stable `700 × 700 km`, `5 km`, East Coast, seed `17,029` reference, automated coverage requires repeated generation equality, exactly one land component, water-side retreat on both sides of the cape, at least `225 km` between deepest bay and farthest cape, and valid east/west edges.
- Verification passed all `145` tests with a zero-warning build and clean formatting. The Impeccable UI detector returned no findings for the new selector and status copy. The self-contained executable was published at `artifacts/publish/flowing-capes/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Large River verification on 2026-08-12

- Added `LargeRiver = 13` without changing any existing version-2 numeric value. Project JSON writes `largeRiver`; `.kworld` exports value `13` and includes `largeRiver` in its manifest mapping.
- River and Large River use one indexed route topology. Mixed cardinal neighbours connect, switching width does not split the route, and a mixed-width third or fourth exit is rejected atomically by editing and loading.
- The canvas retains grass beneath both route classes. River uses the established narrow ribbon; Large River uses a broader bank-and-water corridor with visible ground on both sides. Both keep a `1 × 1` route footprint and symbolic rather than literal-kilometre preview widths.
- Generated routes widen only when a route reaches `100 km`, the remaining downstream reach is `30–80 km`, the start lies at least 60% along the path, and flow accumulation reaches `1.10 ×` the channel-head threshold. The stable Balanced-hydrology coverage produces Large River tiles that remain in a water-reaching non-branching component.
- The version-3 Phase 1 domain now preserves `RiverSize.Regular` and `RiverSize.Large` and rejects unknown size values, so a later v2 migration need not erase the new category.
- Verification passed all `149` tests with a zero-warning build and clean formatting. The Impeccable UI detector returned no findings for the palette, route guidance, generator copy, and canvas changes. The self-contained executable was published at `artifacts/publish/large-river/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## River Split verification on 2026-08-12

- Added persisted/exported `RiverJunction = 14`. Normal River and Large River tiles remain limited to two cardinal exits; River Junction permits three; every four-exit tile remains invalid.
- `CampaignRiverSplitBuilder` accepts only a normal/Large root with zero or one River neighbour. Auto continues away from an existing incoming side; an isolated root requires North, East, South, or West. Bounds overflow, Sea/Lake replacement, existing River overlap, and unintended outside River contact reject before mutation.
- Two branches create one Y and two leaves; three create two cascading Ys and three leaves; four create three cascading Ys and four leaves. Branch leaves preserve the root River/Large class, all new cells copy its centre height, and the full footprint is one undoable command.
- Focused coverage proves automatic two-way split plus undo/redo, an explicit-direction isolated three-way split, collision rejection without revision change, a four-way requested result whose every physical tile stays below four exits, canonical `riverJunction` save/reopen, and `.kworld` value/manifest preservation.
- Verification passed all `155` tests with a zero-warning Release build and clean formatting. The Impeccable UI detector returned no findings for the pinned controls, view model, handler, and river canvas. The self-contained executable was published at `artifacts/publish/river-splits/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Automatic original-material coast verification on 2026-08-12

- Removed Coastal from the terrain selector, generator output, canonical project writer, and `.kworld` type table. Reserved enum value `11` remains read-only so older `coastal` JSON can normalize to Plains at the same centre height; the load result reports the count and the editor marks the project modified.
- `GetAutomaticCoastSurfaceMaterial` now applies to every non-water tile. The outer `0.10` of each cardinal Sea/Lake-facing edge resolves to matching water; all other positions resolve to `Original`. There is no automatic sand band.
- The raster resolves coast water before custom hue, then applies the original built-in/custom appearance only inside. Custom land therefore retains its ID, color, and base texture across the inner 90% of a one-sided coast. Beach and Cliff retain sand/rock respectively; River fallback ground and Unassigned also participate.
- Generation retains normal/custom land on gentle shores, excludes only steep water-facing Cliff from terrain-mix allocation, and never emits Coastal. A focused 100%-Farmland East Coast proves generated custom identity can reach water without replacing coastline, River, height, or steep Cliff authority.
- Release verification passed all `158` tests with zero build warnings and clean formatting. The Impeccable detector returned no findings for the palette/help, inspector, New World copy, and raster changes. The self-contained executable was published at `artifacts/publish/automatic-coast/World.Editor.exe`; `Launch Tile Editor.cmd` now targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Reviewable generation preview verification on 2026-08-12

- Generated presets now run inside New World and render the actual temporary `CampaignWorld` in a bounded `512 px` terrain-color and height-shaded preview. The dialog also reports the exact grid, terrain counts, and seed.
- The dialog remains open after generation. Every definition, generator, ratio, custom-type, height, and seed input invalidates acceptance while retaining the old image as a labeled comparison. **Use this world** is enabled only for the current result; it transfers that exact world and generation result to `EditorViewModel` without a second generator call. Blank still creates directly.
- Closing during generation cancels acceptance of the in-flight result. The preview bitmap is transient, is disposed with the dialog, and never enters save data, undo history, or terrain authority.
- Release build completed with zero warnings and zero errors, all `158` tests passed, and formatting was clean. The Impeccable detector returned no findings for the New World dialog, preview renderer, result handoff, or main-window change. The self-contained executable was published at `artifacts/publish/generation-preview/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Tectonic erosion and hierarchical drainage verification on 2026-08-12

- Added a deterministic physical-kilometre Voronoi tectonic model with four to twelve seeded provinces. Relative province motion produces coherent convergent uplift, divergent rifts, and shear belts; tectonic structure supplies most of the orogeny field while a smaller regional-noise contribution prevents sterile geometry.
- Added terrain-style erosion before final hydrology. Mass-conserving thermal relaxation acts only above the selected talus threshold; a Priority-Flood receiver/accumulation pass supplies bounded stream-power carving and downstream deposition; one final relaxation pass removes sharp artifacts. Generated land remains inside configured height bounds.
- River candidates may now merge into the first accepted downstream route on the same drainage path. A tributary must contribute a bounded independent prefix, may create only one exactly-three-exit `RiverJunction`, and is rejected when it introduces lateral contact or a four-way crossing. The existing River Split tool remains the designer-controlled split/distributary path.
- Four focused regressions prove deterministic coherent plate-boundary fields, deterministic erosion that carves a flowing slope, geological relief scaling across terrain styles, and generated tributary merging through canonical River Junction topology. Existing density coverage continues to distinguish Sparse, Balanced, and Dense connected Mountain output under the 12% cap.
- Release verification passed all `162` tests with zero build warnings/errors and clean formatting. The Impeccable detector returned no findings for the changed generated-world summary/status UI.
- A Release median probe on the verification machine measured `63.90 ms` for the standard `140 × 140 = 19,600`-tile East Coast and `2,393.11 ms` for the maximum `500 × 500 = 250,000`-tile East Coast, both at seed `17,029` with Balanced terrain, Mountains, and hydrology. These are comparative workstation measurements, not cross-machine guarantees.
- The self-contained executable was published at `artifacts/publish/geologic-generation/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Seed-varied directional Coast balance verification on 2026-08-12

- Removed the effectively fixed broad Coast ratio. Every East/West/North/South Coast seed now derives one bounded normalized mainland advance/retreat in `[-0.38, 0.14]` before coastline character, landmarks, smoothing, and inlets are applied.
- The seed changes the final land/water balance while generation remains exactly repeatable. At that release, the hard directional constraints were full Sea on the named edge and full non-water land on the opposite edge; the latter was superseded by [[../Decisions/ADR-0014 - Open Directional Coast Boundaries|ADR-0014]].
- The original focused 12-seed `400 × 320 km`, `5 km` East Coast regression observed more than 15 percentage points of land-share spread and bounded every sample between 35% and 90% land. Current coverage keeps the named Sea edge exact while requiring the unforced opposite-edge water count to vary across seeds.
- New World now reports explicit land and water percentages beside their exact tile counts so a designer can judge the ratio before choosing **Use this world**.
- Release verification passed all `163` tests with zero build warnings/errors and clean formatting. The Impeccable UI detector returned no findings for the updated preview summary. The self-contained executable was published at `artifacts/publish/seeded-coast-balance/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Ridge-core terrain and relief presentation verification on 2026-08-12

- Replaced quota-exhausting Mountain flood fill with endpoint-only ridge-chain growth. Candidate suitability now includes local crest prominence; a new segment must touch exactly one selected tile and extend an existing endpoint. This prevents loops and compact interior fill while keeping Sparse/Balanced/Dense system-count escalation deterministic.
- Ordinary classification makes suitable Mountain-adjacent land Hills/foothills. Broad high land requires real grade or ridged relief instead of elevation alone before becoming Hills. Explicit terrain mixes retain exact requested counts but reserve available foothill candidates first within the unchanged Hill target.
- Updated Hills from isolated orange-brown to muted grass/rock `#8B8A62`. Both New World preview and main canvas now apply bounded northwest hillshade from the continuous bilinear height gradient beneath terrain colors.
- The stable `700 × 700 km`, `5 km`, East Coast, seed `17,029`, Balanced reference changed from a `484`-tile compact Mountain selection with `439` cells having at least three Mountain neighbours to a `57`-tile crest network whose maximum cardinal Mountain degree is `2`; exposed suitable neighbors are Hills.
- Added a focused regression requiring non-empty Mountain output, fewer than 20% interior blob cells, and Hill classification around every exposed non-water/non-River/non-Cliff Mountain edge. Release verification passed all `164` tests with zero build warnings/errors and clean formatting. The Impeccable UI detector returned no findings for the palette, preview relief, or main canvas hillshade. The self-contained executable was published at `artifacts/publish/realistic-relief/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Physical terrain noise and boundary-aligned ridge verification on 2026-08-13

- Added deterministic two-dimensional simplex gradient noise with explicit physical-kilometre wavelengths, bounded normalized ordinary/ridged output, fixed seed offsets, and wavelength-relative octave sampling. Province warp, boundary texture, aligned ridge, seabed, macro, detail, regional ridge, and regional orogeny fields now use it; established coast and climate fields retain their separate contracts.
- Canonical province-pair orientation supplies a stable tangent on both sides of one plate boundary. The aligned ridge field applies restrained long-wave domain warp, keeps the full along-strike wavelength, and compresses the cross-strike wavelength by `3.4`. A focused regression samples strong boundaries and proves average adjacent variation is lower along strike than across it.
- Regional/aligned ridge blending is calculated once per transient tectonic cell and reused by height construction, Mountain selection, Hill scoring, and final classification. This avoids repeatedly evaluating the same four-octave physical field. Mountain suitability is now invariant across density choices; Sparse/Balanced/Dense retain increasing coverage from the same geology, and independently grown systems cannot block one another's seed.
- Tributary discovery includes lower-order accumulation thresholds. A merge that only extends a one-neighbour River head may grow the network but does not consume a complete route target until it produces a separate route or actual three-exit confluence. The existing crossing and degree validators remain authoritative.
- A generated `700 × 700 km`, `5 km`, East Coast seed `17,029` diagnostic produced `28` narrow Mountain-core tiles, `847` Hills/foothill tiles, `398` River tiles, and `4` valid River Junctions. The relief probe is stored at `artifacts/ui/17-physical-noise-relief-probe.bmp`; it is a classification/hillshade diagnostic rather than a desktop UI screenshot.
- Release verification passed all `166` tests. The sequential Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly. No UI files changed in this pass.
- The self-contained executable was published at `artifacts/publish/physical-terrain-noise/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only that launched process was stopped.

## Regional geographic Coast skeleton verification on 2026-08-13

- Reframed the old directional curve as a continental shelf rather than the complete coast. Every directional Coast character on a sufficiently large world now subtracts two unequal physical-kilometre bay ellipses and unions a variable-width cubic-Bézier peninsula from a protected inland root. The result can contain a long mainland projection with Sea on both along-coast flanks instead of only a noisy land/water wall.
- The root extends past the maximum bay cut plus a five-tile safety margin. The peninsula uses smooth root-to-neck, neck-to-body, and body-to-tip radius transitions with one restrained two-octave physical simplex perturbation per tile. The closest-point test remains bounded to 28 curve segments.
- Supporting coast landmarks and generic bays are composed before the protected regional root; offshore island groups follow it. Natural and Rugged therefore retain separated islands without allowing a later water subtraction to sever the mandatory peninsula.
- Expanded directional seed balance to `[-0.45, 0.15]`. Offshore groups now begin approximately `82–164 km` from their local shelf before profile scaling, reducing accidental union with the regional projection.
- Mountain-system seed spacing is invariant across density choices. Dense preserves the cumulative Balanced target for its first two systems before growing the third system, so the same geology remains coverage-monotonic after the changed land mask.
- Added four structural regressions, one per Coast character, that flood-fill mainland from its naturally present mainland-side boundary cells and prove a connected projection beyond 75% of map width with water immediately beyond both flanks. The stable `700 × 700 km`, `5 km`, East Coast seed `17,029` probe reports one mainland component for Smooth and Flowing; Natural and Rugged may additionally retain separated islands according to their landmark composition.
- The four-character silhouette probe is stored at `artifacts/ui/18-geographic-coast-skeleton-probe.bmp`. It is a land/Sea topology diagnostic ordered Smooth, Flowing, Natural, Rugged from left to right, not a desktop UI screenshot.
- Release verification passed all `170` tests. The sequential Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly. No UI files changed; the selected Impeccable workflow explicitly excludes core generation algorithms.
- The self-contained executable was published at `artifacts/publish/regional-coast-geography/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only PID `7756`, launched by that check, was stopped.

## Opportunity-based tidal-inlet verification on 2026-08-14

- Replaced inlet target counts with maximum separated opportunity regions. A considered region consumes its opportunity even when its deterministic roll fails, so neighboring shore cells cannot retry until a quota is filled. Mouth ranking now includes the positive inland grade as a valley-opening term.
- Every candidate must pass a profile-dependent mouth threshold and deterministic probability roll. Target reach is seeded within the profile range instead of always preferring maximum depth. The completed route must pass an average elevation, average grade, and forward-progress suitability threshold.
- A* still gives elevation and grade most of its cost, but now follows a bounded sinusoidal corridor between endpoints with restrained physical-kilometre valley variation. Mouth widening is reduced to zero, one, or two route steps and rejects lateral cells above the profile elevation limit or grade `0.045`.
- A four-seed regression proves that **Few** yields zero or one inlet component and that **Balanced** and **Drowned coast** can each produce zero or nonzero results without exceeding three components at the `140 × 140` reference. The existing deterministic, forced-edge, Sea-connectivity, `None` compatibility, and Land Only tests remain authoritative.
- The eight-seed diagnostic observed **Few** at zero or one accepted component, **Balanced** at zero through three, and **Drowned coast** at zero through three. Seed `17,029` accepted none even at Drowned, demonstrating that the setting is no longer a forced carve. Seed `91,337` supplied a visual comparison with `0`, `1`, `3`, and `3` added components for None, Few, Balanced, and Drowned respectively.
- The four-profile silhouette probe is stored at `artifacts/ui/19-opportunity-based-tidal-inlets-probe.bmp`, ordered None, Few, Balanced, Drowned from left to right. It is a topology diagnostic, not a desktop UI screenshot.
- Release verification passed all `171` tests. The sequential Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The self-contained executable was published at `artifacts/publish/opportunity-tidal-inlets/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only PID `35880`, launched by that check, was stopped.

## Open directional Coast boundary verification on 2026-08-14

- Removed the full mainland-side `forcedLand` line from East, West, North, and South Coast masks. The full named Sea edge remains forced and remains the external-ocean seed.
- Added one optional seeded broad Gaussian shelf retreat. Thirty percent of seeds keep the base shelf; other seeds choose an along-coast side, centre, span, and `1.24–1.84` normalized retreat amplitude. This can move the shelf outside a map corner while decaying into a curved mainland return.
- Updated the four-orientation topology regression to use a stable open-boundary seed and require the named edge to be entirely Sea while the opposite edge contains both land and connected Sea. The 12-seed balance regression now requires differing opposite-edge water counts instead of reasserting a full land wall.
- The 45-seed/four-style diagnostic changed from zero open mainland-side boundaries after removing only the force flag to 90 seed/style combinations after the shelf retreat. The four-style seed `6` comparison is stored at `artifacts/ui/20-natural-open-coast-boundary-probe.bmp`, ordered Smooth, Flowing, Natural, Rugged. It is a land/Sea topology diagnostic, not a desktop UI screenshot.
- Flowing coverage now permits complete Sea-only rows at the open along-coast boundary while still requiring one connected mainland, at least half the along-coast rows to contain land, a `225 km` coast-normal excursion, a mainland-attached peninsula, the full named Sea edge, and complete ocean connectivity.
- The changed land mask moved downstream terrain and drainage opportunities for the old fixed regression seed. Stable reference seeds now preserve the original strong contracts: seed `3` gives strict Sparse `<` Balanced `<` Dense Mountain coverage with unchanged Sea count, and West Coast seed `1` produces an explicit valid three-exit River Junction.
- Release verification passed all `171` tests. The sequential Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The self-contained executable was published at `artifacts/publish/open-coast-boundaries/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke test passed before only PID `36804`, launched by that check, was stopped.

## Windows 98 workstation and numeric elevation verification on 2026-08-14

- Replaced the modern dark-shell styling with the approved Windows 98 Property Workshop system: system-gray work surfaces, navy captions, square raised/sunken controls, compact Tahoma-era typography, a deep sunken map, and segmented status wells. The New World, custom-type, and choice dialogs inherit the same control language.
- Removed the topographic elevation-colour square from terrain rasterization. Stored centre heights now render as invariant whole-metre numbers in a separate overlay with white text and a four-direction dark outline.
- Added **Elevation numbers** as a default-on view option in both the toolbar and **View** menu. The option affects presentation only: toggling it does not mutate tiles, interpolation, dirty state, or undo history and does not rebuild the cached terrain bitmap.
- Numeric labels enumerate only visible tiles, reuse a bounded formatted-text cache, skip values that cannot fit, and auto-hide below `28 px/tile`. A real `140 × 140` / `19,600`-tile blank world showed no labels at the `5.28 px/tile` fit view and showed readable values at `29.60 px/tile`; painting a seven-tile diagonal at `120 m` updated each affected number immediately.
- The inspected native build is captured at `.impeccable/review/desktop-elevation-numbers.png`. The fresh finish reviewer scored the stale design record, native-platform guidance, and early label-clutter findings **Resolved** and returned **Pass**.
- Release verification passed all `171` tests. The sequential Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- The self-contained executable was published at `artifacts/publish/win98-elevation-numbers/World.Editor.exe`; `Launch Tile Editor.cmd` targets it, and a bounded five-second hidden startup smoke passed before only PID `36076`, launched by that check, was stopped.

## Manual acceptance path

1. Open **New**, keep **Blank**, and confirm the default `700 × 700 km` world with `5 km` tiles previews `140 × 140 · 19,600 tiles` and creates an untouched grid.
2. Open **New** again, choose **Island**, Balanced terrain and hydrology, and seed `17,029`; choose **Generate preview** and confirm New World stays open while the preview shows Sea around a coherent landmass plus the grid, terrain counts, and seed. Confirm the existing editable document has not changed.
3. Change only the seed and confirm the old preview remains visible but is labeled stale and **Use this world** is disabled. Choose **Regenerate preview**, confirm the map changes, then choose **Use this world** and confirm the exact reviewed result opens. Recreate the original seed and settings to confirm the original map; check East Coast and Sea in Center for their named guaranteed edges, then check Land Only contains no water or shore tiles.
4. Repaint one generated tile as Plains at `100 m` and confirm it changes immediately without accepting, unlocking, or switching modes.
5. Select Mountain at `1,000 m` and click an adjacent tile; confirm the whole neighboring cell changes type and shows rock texture.
6. Set **Paint Area** expansion to `1`; confirm the readout is `3 × 3 tiles`, the cyan preview is a centred three-by-three square, and one click assigns all nine complete cells the selected type and height.
7. Move the same `3 × 3` preview to a world corner and confirm it clips to the valid cells rather than extending off-world.
8. Drag the `3 × 3` footprint across several cells, undo once, and confirm all of the unique tiles changed by that drag restore both fields; redo once to reapply them.
9. Turn **Elevation numbers** on, zoom to at least `28 px/tile`, and confirm each readable tile shows its stored whole-metre centre value with a high-contrast outline. Turn the option off and confirm all map numbers disappear without changing terrain, stored heights, derived slopes, dirty state, or undo history; zoom back out and confirm labels automatically hide even while the option remains enabled.
10. Move across their boundary and confirm **Surface here** changes continuously, reading about `550 m` halfway between the two centres.
11. Toggle **Height only** and confirm the same slope remains visible without type hues or material texture; elevation numbers remain an independent view option.
12. Paint adjacent Sea and Lake tiles and confirm their wave textures and hues differ; place Beach and Cliff beside them and confirm their inner sand/rock material remains while the outer 10% matches the adjacent water.
13. Paint Plains, Steppe, Forest, Hills, and a custom land type directly south of Sea. Confirm Steppe has distinct olive-gold dry-grass texture and Coastal is absent from the selector; each north edge reads as 10% Sea and the inner 90% retains that tile's original material/color. Replace Sea with Lake and confirm only the edge changes to Lake.
14. Put water on two sides of one land tile and confirm both sides receive 10%-deep water while the remaining interior retains its original material. Remove cardinal water and confirm the full original material returns; diagonal water alone must not create a coast.
15. Select River and confirm Paint Area is disabled with a `1 × 1 route` readout, then drag diagonally. Confirm the result is one contiguous N/E/S/W path with a narrow water ribbon, visible grass around it, and channels meeting at shared tile edges. End a River beside Sea or Lake and confirm the ribbon reaches that water-facing edge.
16. Attempt to add a third branch to the middle of a River path; confirm the cursor is red and crossed, the tile is skipped, and the status reports a blocked crossing.
17. Save, close, reopen `world.json`, and confirm the same original terrain/custom values, automatic coast transitions, River connections, and slope.
18. Generate the default `700 × 700 km` East Coast with seed `17,029` at Sparse, Balanced, then Dense Mountain density. Confirm the coastline remains fixed while Mountain tiles form one, a few, then several connected range systems; confirm Dense remains below 12% of eligible land and broad high plateaus remain Hills.
19. Generate the same East Coast with Balanced terrain, Balanced Mountain density, and **None** hydrology. Confirm Desert forms limited dry interior cores, Steppe forms a broader semi-arid transition toward Plains, neither is water-facing, and neither replaces Hills or Mountain; repaint one Steppe and one Desert cell to verify both are ordinary complete tiles with independent centre heights.
20. Confirm reopening has no generated/manual mode: the saved generated tiles remain ordinary version-2 tiles and can still be repainted.
21. Open a version-1 project, confirm **Converted · unsaved**, and verify the editor refuses the original folder as the save destination.
22. Right-click a tile surrounded by known heights. Confirm **Copy centre** adopts its exact stored height, **Blend around** adopts the nearest-`10 m` mean of only its valid N/E/S/W neighbours, and neither action changes a tile until the next paint stroke.
23. Open **New**, choose East Coast, enable **Adjust inland tile ratios**, and confirm the default `40 + 25 + 8 + 13 + 2 + 12 = 100%` state is ready, including the independent Steppe control. Change one value and confirm the total asks for 100%; restore 100%, generate, and confirm water/steep-Cliff placement still follows East Coast while eligible base/custom types follow the requested mix up to gentle water edges. Try `0% Mountain` and confirm no ordinary Mountain tiles are generated; set a visible Steppe share and confirm it does not consume the Desert or Plains target.
24. Open **New**, choose **East Coast** and **Drowned coast** with seed `17,029`, then generate. Confirm the coast may remain unchanged because no suitable opportunity is forced. Change to seed `91,337` and regenerate; confirm a few separated broad Sea-connected drowned valleys bend through low terrain, their surrounding land retains normal/custom material with automatic 10% water edges or becomes Cliff when steep, and the east edge remains Sea. The west edge may contain land, connected Sea, or both. Recreate with **None** to confirm the normal uncut coastline returns. Treat each channel as a campaign-scale estuary, not a narrow canal.
25. Open **Terrain → Custom tile types…**, add `Farmland` with base **Plains**, color `#91A85A`, and `0%` share. Apply it, select **Farmland** in Terrain type, then stamp a tile and confirm the entire tile uses its color while retaining Plains grass texture and normal elevation behavior. Reopen the manager and confirm its base/delete controls are locked until that tile is repainted.
26. Open **New**, add `Farmland` on Plains with a nonzero share, choose **Land only**, and generate twice with the same seed. Confirm Farmland receives its own requested part of the combined inland mix and repeats in the same locations; confirm a `0%` type appears in the palette but not generated output. Try to add a Sea/River base and confirm the manager does not offer it.
27. Open **New**, choose **East Coast**, seed `6`, and **None** tidal inlets. Generate Smooth shelf, Flowing bays and capes, Natural mixed coast, then Rugged coast. Confirm the named east edge remains entirely Sea while the west and one along-coast boundary may naturally contain both land and connected Sea—there must be no forced straight land wall. In every result, find the large asymmetric bay region and follow its long curved peninsula back to the mainland; confirm Sea is visible on both sides of its seaward body. Natural should add coherent landmarks and occasional offshore islands; Rugged should add visibly more shoreline complexity without turning the boundary into tile-sized static. Recreate Natural with the same seed and confirm the same map.
28. With a modified world open, choose **File → Export Runtime Data…** and save `test.kworld`. Confirm the status reports terrain records plus resource occurrences while the title still shows the world as modified. Open the package as ZIP and confirm it contains exactly `tiles.bin`, `resource-index.bin`, `resource-records.bin`, and `manifest.json`; confirm `tiles.bin` is `tileCount × 4` bytes, the resource index is `tileCount × 8` bytes, and the manifest counts/checksums match.
29. Open **New**, choose **East Coast**, **Flowing bays and capes**, seed `17,029`, and **None** tidal inlets. Generate and confirm one continuous mainland forms an unequal gulf and cove plus a long cape that leaves the protected inland root, narrows, widens through its body, then tapers toward its end, with Sea visibly returning on both sides. Confirm no offshore island is required and the east edge remains Sea. Accept Sea-only along-coast rows and a partly open west boundary as intended generated geography.
30. Select **River**, paint three connected cells, switch to **Large River**, and continue the same path. Confirm the centreline stays connected, the new reach becomes visibly broader without filling its cells, Paint Area remains disabled, and changing one middle cell between River sizes does not split the route. Attempt a third mixed-width branch and confirm it is blocked.
31. Generate a sufficiently large Continent with **Balanced** hydrology. Confirm ordinary upstream River cells can transition into rarer Large River cells downstream toward Sea or Lake; hover both classes to verify their distinct stored types, then save/reopen and export `.kworld` to confirm the distinction survives.
32. Paint a River or Large River endpoint, right-click it, choose **4** branches with **Auto**, and create the split. Confirm the result uses three visible Y junctions, four separated leaves, and no four-way tile. Undo once to remove the whole footprint, redo once to restore it, then save/reopen and confirm the junction geometry survives. Pin an isolated River and confirm **Auto** asks for an explicit cardinal direction.
33. Open an older version-2 project containing stored `coastal` tiles. Confirm the status reports how many became Plains, the project is marked modified, centre heights are unchanged, and automatic water edges appear from current adjacency. Save and confirm `campaign-tiles.json` no longer contains `coastal`.
34. Generate the standard East Coast at seed `17,029` with Balanced terrain and hydrology. Confirm the preview summary reports tectonic provinces and erosion passes, Mountain/Hill relief forms regional belts rather than uniform scatter, River routes follow low terrain, and any generated confluence is an explicit three-exit River Junction with no four-way crossing. Change the seed, regenerate, then restore `17,029` and confirm the original result returns exactly.
35. Generate the same directional Coast with several random seeds. Confirm both preview percentages and the visible mainland advance/retreat change meaningfully, while only the named water edge remains exact. Confirm some seeds retain a land-heavy opposite edge while others let connected Sea enter it or a top/bottom-equivalent boundary. Restore a previous seed and confirm its percentage and tile arrangement return exactly.
36. Generate East Coast seed `17,029` with Balanced terrain and Mountain systems. Confirm gray Mountain tiles read as narrow connected crest chains rather than solid blobs, exposed suitable neighbors transition through muted grass-toned Hills, and slope-facing light/dark relief continues across type boundaries in both the preview and main canvas.
37. Generate the same physical `700 × 700 km` world at two valid campaign tile sizes and compare the major relief scale. Confirm continental undulation and range spacing retain campaign-scale proportions rather than doubling with tile count; within each result, confirm primary crest chains tend to follow convergent/shear belts as long arcs while smaller Hills and erosion supply the local variation.
38. With a modified saved world open, add or adjust a safe custom land type, then choose **Regenerate**, **Terrain → Regenerate world…**, or press `Ctrl+R`. Confirm all seven definition fields start from the current world but remain editable, Blank is absent, the current custom catalog is present, and cancelling leaves the definition, tiles, and undo history unchanged. Change the dimensions or campaign tile size to another exactly divisible generated grid, generate a non-Blank preview, then change another input to confirm acceptance becomes stale. Regenerate and choose **Use this world**. Confirm the exact reviewed definition and tiles replace the map, the existing project path/name and import boundary remain intact, the document is marked modified, and obsolete undo/redo history is empty.
39. Open **Resources**, choose one stable resource ID, set potential and independent Paint Area, then add/update across complete tiles. Confirm terrain/height stay unchanged, the fixed heatmap shows exact numbers at readable zoom, and erase removes only that ID. Right-click a populated tile and verify every occurrence, lock text, warning, and unevaluated factors; exercise Use selected, Lock/Unlock, shared Undo/Redo, save/reopen, and version-2 export. Accept a same-lattice terrain preview and confirm every resource stays at its exact coordinate/potential/lock.
40. Keep at least one locked and one unlocked resource occurrence, then open **Regenerate world...** and change campaign tile size to another exactly divisible grid. Generate the preview and confirm the scrollable **Resource impact** well reports moved, merged, dropped, locked-retained, replaced-unlocked, regenerated-unlocked, and final counts as applicable. Shrink one axis past a locked source and confirm its stable ID and old coordinate are named before acceptance. Change another terrain input and confirm the old impact remains visible but stale while **Use this world** is disabled. Regenerate, accept, and confirm the exact reviewed terrain/resource result replaces the document together, the project path remains, and shared Undo/Redo is empty. Repeat with no saved resource-generation settings and confirm all in-bounds occurrences remap without inventing generated deposits.
41. Open **New**, enter `10,000 × 10,000 km` and `20 km` campaign tiles, choose **East Coast**, **Natural mixed coast**, seed `17,029`, **None** hydrology, and generate. Confirm the preview reports `500 × 500 · 250,000 tiles`, the east edge remains Sea, the coast contains broad shelf advance/retreat plus irregular smaller bays/headlands and separated offshore pieces, and no repeated paired-round-bay/hooked-cape stamp dominates the map. Compare Rugged for stronger detail and Flowing for the intentionally smoother authored cape. Remember that every visible shoreline step is a complete `20 km` tile.

The executable domain tests are authoritative for exact values; this path checks that the same operations are reachable and legible in the desktop UI.

## Public repository publication on 2026-08-17

- The canonical public source repository is [kraivichldf/WorldEditorPixel](https://github.com/kraivichldf/WorldEditorPixel).
- The initial publication includes the application source, tests, Obsidian-compatible documentation vault, and checked-in design/review evidence.
- `.gitignore` excludes `artifacts/`, `bin/`, `obj/`, Visual Studio/JetBrains state, user settings, and temporary files. Published executables and local build outputs are intentionally rebuilt from source rather than committed.
- The publication baseline is the `433/433` Release test suite plus a clean solution format check.

## Standalone repository README verification on 2026-08-17

- `README.md` is a self-contained GitHub landing page. It contains no Obsidian syntax, `docs/` navigation, ADR link bundle, or dependency on documentation-vault context.
- Product capabilities, requirements, build/run commands, controls, height/type semantics, architecture, persistence, repository source layout, and current limits remain explained directly in the README.

## Full-definition preview-first regeneration verification on 2026-08-14

- Added **Regenerate** to the toolbar, **Terrain → Regenerate world…**, and `Ctrl+R`. The command is disabled until a world is open.
- Regeneration initializes the current dimensions, campaign tile size, sea/default/minimum/maximum elevations, and current custom land catalog. All seven definition fields remain editable; exact grid validation and generation size limits run before preview. Blank remains removed so replacement still requires reviewed generated terrain.
- The dialog restores the most recently accepted generator recipe only while it exists in the current editor process. Reopened projects use clearly labeled defaults because generator provenance is not persisted or invented.
- Preview generation remains temporary and stale-safe: changing an input disables **Use this world** until a new preview is generated. Cancelling preserves the current tile map and undo history. Acceptance installs the exact reviewed temporary world, preserves the current project/import identity, marks the document modified, and clears obsolete undo/redo history.
- Three focused `EditorViewModelRegenerationTests` cover identity preservation/history clearing, changed-definition acceptance with identity preservation, and absence of invented saved generator settings. Release verification passed all `174` tests; the Release build completed with zero warnings and zero errors, and `dotnet format KingdomWorldEditor.sln --verify-no-changes --no-restore` completed cleanly.
- A real native journey used a `700 × 700 km`, `5 km` world (`140 × 140`, `19,600` tiles), generated and accepted a Continent preview, and confirmed the replacement status contract. The inspected dialog is captured at `.impeccable/review/desktop-regenerate-world.png`; the independent finish review returned **Pass** with no material findings.
- The self-contained executable was published at `artifacts/publish/preview-regenerate-custom-types/World.Editor.exe`; `Launch Tile Editor.cmd` targets this build. A bounded five-second hidden startup smoke passed with a real main-window handle before only PID `27348`, launched by that check, was stopped.

## Historical desktop evidence

The version-1 application completed a real create, sculpt, save, undo, redo, close, and reopen journey on Windows on 2026-08-07. That evidence remains useful for the retained legacy loader but is not evidence for the new tile-only pointer flow.
