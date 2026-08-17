# Using the Terrain Editor

## Start the editor

To run the current branch, build and start the Avalonia editor from the project root:

```powershell
dotnet run --project src/World.Editor/World.Editor.csproj -c Release
```

For the verified self-contained Windows build, double-click `Launch Tile Editor.cmd`. It targets `artifacts\publish\seasons\World.Editor.exe`; the executable does not require a separately installed .NET runtime. Use the source command above while developing.

## Create an exact campaign grid

Choose **New** or press `Ctrl+N`. Enter world width, world height, and campaign tile size as whole kilometres. Enter sea level, default tile height, and allowed minimum/maximum height in metres.

World width and height must divide exactly by tile size; version 2 never creates partial edge cells. The preview shows the exact grid and total before creation. The default example is:

```text
World: 700 × 700 km
Tile:  5 × 5 km
Grid:  140 × 140 = 19,600 tiles
```

Every untouched terrain tile begins as Unassigned at the default centre height. In **Tile seasons**, choose the complete blank-world default; it starts as unlocked Spring but may be any available built-in or custom Season Definition.

## Choose a blank or generated start

The **Starting world** section is optional. **Blank** keeps every tile untouched for fully manual painting. Generated profiles are:

- **Continental world** — several unequal major landmasses separated by broad connected oceans, with regional bays, peninsulas, smaller fragments, and island arcs;
- **Island** — one compact island surrounded by Sea;
- **Archipelago** — several separated islands;
- **East/West/North/South Coast** — Sea is guaranteed on the named edge; every other boundary follows the seed and generated geography, so land or connected Sea may leave the map there;
- **Sea in center** — a central Sea enclosed by land, with land on every outside edge;
- **Land only** — no Sea, Lake, River, Large River, or water-facing Cliff tiles.

Continental world is the globe-like composition. The seed chooses and mirrors a macro layout, moves the dominant/large/medium/small/microcontinental size roles between regions, bends each mass through several attached lobes, carves unequal ocean-connected bays, and places two restrained offshore arcs. Coast fields are measured in physical kilometres, so changing a `5 km` campaign tile to `10 km` changes sampling resolution rather than doubling the geography. A `2:1` width-to-height world most closely matches an equirectangular atlas; a square `700 × 700 km` world still produces the same hierarchy with more vertical room. Generated land may leave a map edge, but the campaign grid is planar: left and right edges do not wrap or become neighbours.

Choose **Gentle**, **Balanced**, or **Rugged** terrain to control tectonic relief and the erosion response. The generator lays out deterministic crustal provinces, measures its major and detail noise in kilometres, stretches crest detail along convergent/shear boundaries, lowers rifts, then performs bounded slope relaxation and drainage erosion before it places Lakes and Rivers. **Mountain systems** controls one focused ridge, a few ridges, or several ridges retained from that same underlying geology. Mountain tiles follow the strongest narrow crest chains; nearby suitable land becomes grass-toned Hills/foothills instead of a hard rectangular or circular Mountain fill. The canvas and preview shade the continuous slope direction, so the same classification boundary should still read as joined relief. Mountain remains capped at 12% of eligible inland land. Choose **None**, **Light**, **Balanced**, or **Abundant** Lakes and Rivers; compatible tributaries may merge into valid three-exit confluences, but four-way crossings remain prohibited.

For East, West, North, or South Coast, choose **Coast character** independently from water channels:

- **Smooth shelf** keeps broad restrained curves; its compact-world gulf-and-peninsula construction fades into the macro shelf on continental-size maps;
- **Flowing bays and capes** creates one continuous mainland like the curved reference silhouette: an asymmetric gulf and cove, rounded headlands, and a long curved peninsula that narrows toward its tip;
- **Natural mixed coast** uses a compact regional form on ordinary maps, then transitions to broad stochastic shelf bends, heterogeneous gulf/cape/sound/strait regions, irregular nearshore breakup, and sparse island arcs as the map becomes continental;
- **Rugged coast** uses the same large-world hierarchy with stronger nearshore breakup, more landmark opportunities, islands, and supporting shoreline detail.

The generator measures every level in kilometres. At the compact `700 km` reference, the protected-root regional peninsula guarantees a readable gulf/cape form. Between `1,400 km` and `4,200 km`, Smooth/Natural/Rugged fade that authored skeleton out while a slower continental shelf field, stronger two-dimensional nearshore field, distributed macro landmarks, and longer island groups fade in. A `10,000 × 10,000 km` world with `20 km` tiles is therefore a complete `500 × 500 = 250,000`-tile map with broad shelf advance/retreat and many regional shoreline changes, not a stretched 700 km symbol or a row of identical bays. Flowing intentionally keeps its smooth regional cape. The seed also changes the land/water balance and may carry the shelf beyond a non-named boundary. Only the named Sea edge is guaranteed. Other world shapes disable this selector because their complete landmass masks already define their coasts.

Remember that tile size is the visible authoring resolution. With `20 km` tiles, the smallest cove, island width, or shoreline step is one complete `20 × 20 km` campaign tile. Use a smaller tile size when you need finer coastal detail, provided the resulting exact grid remains within the `250,000`-tile generation limit.

**Tidal inlets** remains a separate control with **None**, **Few**, **Balanced**, or **Drowned coast**. These settings are opportunity strength, not a required number of canals: the generator considers a bounded number of separated low-coast regions, uses the seed and terrain to accept suitable routes, and may produce fewer or no inlets. Accepted routes bend through low, gentle terrain and remain connected to Sea. At the current tile resolution, each carved Sea tile is a broad estuary or drowned valley—not a narrow constructed canal. Land Only disables hydrology and tidal inlets so its result remains exact. Enter a signed whole-number seed or choose **New random seed**.

Enable **Adjust inland tile ratios** when you want a specific land mix. Set whole-number targets for Plains, Forest, Desert, Hills, Mountain, and Steppe; the displayed total must be exactly `100%`. These percentages apply after Sea, Lake, River, Large River, and steep water-facing Cliff are removed. Automatic coast edges consume no separate terrain category, so eligible base/custom land may extend directly to water. The default custom mix is `40% Plains`, `25% Forest`, `8% Desert`, `13% Hills`, `2% Mountain`, and `12% Steppe`. Mountain, Desert, and Steppe still require suitable geography; if the requested share cannot be placed honestly, the difference becomes Plains. The same definition, controls, ratios, and seed reproduce the same starting arrangement.

Use **Custom tile types…** in the same Starting world section when you want named land variants such as Farmland, Volcanic Hills, or Ancient Forest. Add a name, choose its safe base terrain, enter a `#RRGGBB` color, and set its **Terrain mix**. A share of `0%` makes it available only for manual painting. A positive share is its own portion of the generated eligible-land mix: for example, `30%` Farmland means 30% of eligible land, with the default ratios set to the remaining 70%. Its safe base is a portable texture/data fallback, not the percentage it replaces. Custom types cannot be Sea, Lake, River, Large River, Beach, or Cliff, so water and river rules remain unchanged. When custom land touches water, its inner material/color remains visible and only the outer 10% becomes matching water.

Select **Generate preview**. Directional Coast generation first chooses its seeded land/water balance and shapes the selected coastline character, then applies tectonic relief, optional sea-connected tidal-inlet carving, deterministic erosion, basin drainage, and hierarchical River routing. The dialog then derives a Season Seed from that terrain seed and generates a complete Earth-like Season Layer from the listed first-match priority. New World stays open and shows the resulting campaign silhouette, exact land/water percentages, terrain counts, Large River and confluence counts, tectonic province count, erosion-pass count, grid size, seed, and observed Season distribution. Use the compact **Terrain / Seasons** switch to inspect either candidate raster.

If the result is not satisfactory, change the seed or any other setting and choose **Regenerate preview**. The previous terrain and Season candidate remains visible for comparison but is labeled stale, and **Use this world** stays disabled until both stages finish. When satisfied, choose **Use this world**. The editor transfers that exact reviewed terrain-and-Season result to the normal campaign canvas without generating either layer again. Every tile can immediately be clicked, dragged, undone, saved, and reopened like a hand-painted tile. The preview is temporary dialog state, not a lock. Blank worlds still use **Create blank world** directly; all Season cells receive **Default tile season**, and no Season generation recipe is invented.

Generated worlds need at least `8 × 8` campaign tiles and support at most `250,000` tiles. Blank worlds are not subject to this generation limit.

## Regenerate the current world

Choose **Regenerate** in the toolbar, **Terrain → Regenerate world…**, or press `Ctrl+R`. The command opens the generator without touching the current editable tiles.

- World width, height, campaign tile size, sea/default height, and height limits start with the open world's values but remain editable. Width and height must still divide exactly by tile size, and generated worlds must remain within the displayed tile-count limits.
- The current custom tile definitions and their terrain-mix shares are copied into the regeneration settings. Use **Custom tile types…** there to add or adjust types for the replacement preview.
- If this editor session created or regenerated the current world, the last preset, seed, terrain, Mountain, hydrology, tidal-inlet, coastline, and inland-mix settings are restored. Project files do not store that recipe, so a reopened project starts from generator defaults and tells you so.
- Blank is unavailable because this command generates replacement terrain.

Choose **Generate preview** and keep adjusting until satisfied. The open definition, tiles, resources, seasons, project path, dirty state, and undo history remain untouched while the dialog is open. The scrollable **Layer impact** well beneath the preview is part of the commit review. Its Resources section reports:

- With the same world width, height, and campaign tile size, every resource coordinate, potential, lock, and saved resource-generation setting stays exact.
- With a changed lattice, the editor maps each protected occurrence from its old tile centre in physical metres into the replacement grid. It reports moved occurrences, same-ID merges, out-of-bounds drops, and final counts before acceptance. A merge keeps the highest potential and remains locked when any merged source was locked.
- If saved resource-generation settings exist, locked occurrences are remapped first and old unlocked occurrences are regenerated against the replacement terrain using those exact settings. Without saved settings, every in-bounds occurrence is remapped and no procedural recipe is invented.
- Every locked out-of-bounds drop is named by stable resource ID and source coordinate. Changing any terrain input keeps the old report visible but stale and disables **Use this world** until another preview completes.

Its Seasons section reports:

- With the same width, height, and campaign tile size, every Season ID and lock stays exact. The terrain may change underneath that independent authority.
- With a changed lattice, only locked source assignments are remapped. The editor intersects each old tile rectangle with target cells in physical metres and chooses the target with greatest overlap. Unlocked target tiles are regenerated from the reviewed candidate terrain and current saved Season recipe; when no recipe exists, settings derive reproducibly from the candidate terrain seed.
- Same-ID locks may merge. A strictly greater different-ID overlap wins and the smaller claim is reported displaced. Equal greatest-overlap claims from different IDs block acceptance until **Resolve locked Season blockers…** chooses the winner for that target.
- A locked source rectangle with no overlap is named and blocks acceptance until the same review dialog explicitly permits the drop. This permission is separate from conflict winners and applies only to the shown candidate definition. Until an equal-overlap conflict has a winner, its target cell is rendered magenta and omitted from the observed Season percentages; the preview never presents one claimant as a silent winner.
- After decisions, the dialog rebuilds only the Season candidate against the unchanged terrain preview. Any terrain input change stales both impact reports and requires a complete fresh preview.

**Use this world** installs the exact reviewed terrain, resource, and Season candidates together, keeps the current project folder/import safety boundary, clears the shared undo/redo history, and marks the document modified. It remains disabled for stale candidates, unresolved Season conflicts, or unpermitted locked Season drops. **Cancel** leaves the current world exactly as it was. Physical positions are not normalized or stretched when world dimensions change.

Regeneration does not create a saved generated mode or lock the result. Every accepted built-in or custom tile is immediately paintable and saves through the normal version-2 project files. See [[../Decisions/ADR-0015 - Preview-First Current World Regeneration|ADR-0015]].

## Export for game development

Keep using **Save World** for editable project folders. When a world is ready for a downstream importer, choose **File → Export Runtime Data…**, the toolbar **Export** action, or `Ctrl+E`, then save a `.kworld` file.

The exported package is runtime format version 3. It contains a JSON manifest, a dense binary copy of every campaign tile, a dense per-tile resource index, compact resource occurrence records, and one dense two-byte Season catalog index per tile. It includes exact metre scale, grid size, coordinate direction, terrain/custom mappings, signed centre height, resource IDs/potentials, and the canonical Season ID/fallback/appearance catalog. Authoring locks, rules, support diagnostics, and generation settings are deliberately omitted. Export does not mark the project saved or change its folder because it is a derived handoff artifact.

Use `.kworld` as an input to a Unity editor importer, Unreal import plugin, or build step. Convert it into native engine assets during import; do not repeatedly decompress the package during gameplay. The terrain, resource, Season record layouts and validation sequence are documented in [[../Reference/Runtime World Package|Runtime World Package]].

## Build the active stamp

The left rail contains the complete authoring model:

1. Open the **Terrain type** selector and choose Unassigned, Plains, Steppe, Desert, Forest, Hills, Mountain, Sea, Lake, River, Large River, Beach, or Cliff. Each option shows its color, name, and description; the selected explanation remains visible below the selector. Coastal is absent because adjacency now derives it for every non-water type.
2. Set **Tile elevation** in whole metres. The arrow buttons move in `10 m` steps (`10, 20, 30, 40…`), and you may type another whole-metre value directly.
3. Set **Paint area** when you want a larger edit: expansion `0` paints `1 × 1`, `1` paints `3 × 3`, through `12` for `25 × 25` complete tiles. The footprint is centred on the pointer and clips at the world edge.
4. Confirm the **Active stamp** summary.

The **Map number** readout repeats the active stored centre height in metres. Turn **Elevation numbers** on or off from the toolbar or **View** menu. When enabled, each readable campaign tile shows its whole-metre centre value directly on the map; the labels hide automatically while zoomed out so they do not become illegible noise.

### Create and paint a custom land type

Open **Terrain → Custom tile types…** or the **Custom tile types…** button below the terrain selector. The manager is also where New World defines custom types before generation.

1. Choose **Add type**, then give it a name and color.
2. Choose its safe base: Plains, Steppe, Desert, Forest, Hills, or Mountain. The base preserves portable data and material texture; it does not control the share’s allocation.
3. Leave Terrain mix at `0%` for a manual-only type, or give it an independent percentage for the next generated world. In **Set inland terrain mix**, make the six default percentages plus every positive custom percentage equal `100%`.
4. Choose **Apply types**, select the new name in **Terrain type**, set elevation, and paint normally.

Once a custom type is painted, its base is locked and it cannot be deleted until every tile using it is repainted. You may still change its visible name, color, or future Terrain mix percentage. A custom type fills the entire selected tile or Paint Area footprint exactly like any other land type; it is not an overlay, a river, or a water transition.

### Use a nearby elevation

Right-click a tile to pin it, then use the helper in **Pinned tile**:

- **Copy centre** puts the pinned tile's exact stored centre height into the active stamp. Use this when changing type without changing elevation.
- **Blend around** averages the valid north, east, south, and west neighbour centre heights, rounds the result to the nearest `10 m`, and puts that value into the active stamp. Missing world-edge neighbours are ignored; diagonal tiles are not sampled.

The helper does not paint, alter the pinned tile, dirty the project, or create undo history. Review the resulting **Tile elevation**, then click or drag normally. On a one-tile world, **Blend around** rounds that pinned tile's own height because no neighbours exist.

Unassigned clears the classification but still stamps the selected centre height. To return a tile to the fully implicit default, stamp Unassigned using the world's default height.

Steppe is explicit full-tile semi-arid grassland: drier and more open than Plains, but not true Desert. Desert is an explicit full-tile dry sand-and-stone lowland; it is not a thin beach band. Both keep their chosen centre height and interpolate normally with neighbouring tiles. Sea is open salt water; Lake is enclosed inland water. Beach and Cliff are explicit full-tile sand and rock types. Any of these non-water materials automatically receives a water-facing edge as described below.

## Paint complete tiles

- Left-click stamps the selected centred footprint of complete cells. The default expansion `0` is one complete cell.
- Left-drag stamps the selected footprint at every crossed tile centre, including centres crossed between fast pointer events.
- The cyan rectangle is the exact clipped footprint that will be stamped.
- One drag is one undo entry for both type and height.
- Press `Escape` during a drag to restore every tile touched by that active stroke.

Paint area is a whole-tile selection extent, not a sample brush or falloff: every included tile receives the same selected type and centre height. The type color always belongs to the whole cell. The optional centre number shows the stored tile elevation in metres, while the surrounding cell brightness shows the derived slope. Height does not make the tile flat: the editor automatically blends toward neighbouring centre heights, producing a continuous surface across every tile boundary.

Material patterns make the map easier to read: Plains looks grassy, Desert has dry dune-and-stone variation, Forest has canopy variation, Hills show ridges, Mountain and Cliff show rock, Sea/Lake/River show water movement, and Beach shows sand grain. An automatic coast preserves that exact original pattern inside the tile. These patterns are deterministic display details, not extra saved fields. They fade away when zoomed out and disappear in **Height only** mode.

## Paint campaign resources

Choose **Resources** in the workspace strip or **Resources → Resources workspace**. The canvas keeps the same pan, zoom, grid, hover coordinate, and pinned tile while the left rail changes to resource tools. Switching workspaces does not edit data.

### Create a custom resource

Choose **Resources → Custom resources…** or the **Custom resources…** button in the Resources rail.

1. Choose **Add resource** for a safe `0%` manual-only definition, or select a built-in template and choose **Duplicate as custom**. Built-ins themselves cannot be edited.
2. Set the designer name and portable stable ID, then choose Renewable/Finite category, Land/Water/Either medium, distribution shape, symbol ID, map color, and map priority.
3. Set default coverage, richness, and concentration. Coverage belongs only to this resource and never joins a 100% terrain-style ratio. Keep `0%` for paint-only use; a positive value makes it eligible for **Regenerate resources…**.
4. Optionally enable inclusive elevation, grade, water-distance, or physical region-scale ranges. Use **Add preferred** for soft positive terrain/support influences and **Add avoided** for soft negative influences. Avoided factors reduce generation suitability but do not ban a tile. Under **Hard terrain rules**, choose a normalized surface and use **Add excluded tile** when generation must never place that resource on the surface. Field and association weights use `factor-id=weight`, one per line, from `-10` through `10`; the buttons insert only supported factor IDs. A factor cannot be both preferred and avoided.
5. Optionally enter portable custom-terrain IDs in the include or exclude lists. Current IDs are shown as copyable help, while a future valid portable ID may be authored before its terrain type exists.
6. Choose **Apply resources**. The editor validates the complete catalog first, preserves every compatible occurrence exactly, removes generation overrides only for definitions you actually deleted, selects the applied definition, marks the project modified, and clears Undo/Redo because the immutable catalog/map authority changed. **Cancel** changes nothing.

Once a custom resource is painted, its stable ID and Renewable/Finite category are locked and it cannot be deleted until every occurrence of that ID is erased. You may still change its name, color, generation defaults, medium, shape, ranges, and factors; existing potential/lock values remain exact and any new terrain mismatch appears as a warning rather than deleting data. Saving writes `resource-definitions.json`, runtime export includes the custom catalog, and the generation dialog lists the custom definition alongside built-ins.

1. Filter the catalog by **All**, **Renewable**, or **Finite**, then choose one resource. The selector shows its portable color, name, category, and stable ID.
2. Set **Potential** from `1` through `100`. Potential is relative to that resource; it is not inventory, production, or economic value.
3. Set the independent resource **Paint Area**: expansion `0` is `1 × 1`, through `12` for `25 × 25` complete tiles.
4. Choose **Add / update** or **Erase selected**. Add/update writes only the selected resource ID and exact potential. Erase removes only that ID. Other resources and all terrain, height, River, and shore data remain unchanged.
5. Leave **Lock manual edits** enabled when the occurrence must survive later resource regeneration. You may unlock it from the pinned inspector.

Left-click or drag to apply the complete clipped footprint. One drag is one entry in the same Undo/Redo history as terrain edits; `Escape` restores an in-progress stroke. A tile may contain several different resource IDs, but never two occurrences of the same ID.

The Resources canvas mutes rather than hides terrain and draws the selected resource on a fixed `1..100` heatmap. Zoom to at least `28 px/tile` to see exact potential numbers in cells; hover always reports the selected occurrence exactly. Right-click a tile to list every occurrence in stable-ID order. The pinned list can **Use selected**, **Erase**, **Lock**, or **Unlock** one occurrence and shows hard-rule warnings plus factors the current diagnostic layer cannot yet evaluate. Manual out-of-profile placement remains valid; a warning never deletes or changes it.

## Regenerate resources

Choose **Resources -> Regenerate resources...**, press `Ctrl+Shift+R`, or use the left-rail button while a world is open. The dialog works against the current terrain and resource layer without changing the document until you accept a candidate.

1. Build the operation set before generating. **Included — Regenerate** resources will replace their unlocked occurrences; **Excluded — Keep** resources remain exactly unchanged. Use **All**, **Renewable**, **Finite**, **Only selected**, **Exclude all**, or the transfer buttons to create any mixed set. Filter/search changes only the visible rows, never membership. At least one resource must remain Included.
2. Select a resource in either list to see its **Prefers**, **Avoids**, and **Hard excludes** rules and edit its generation participation, independent coverage, richness, richness bias, concentration, and map priority override. Built-in rule lists are application-owned; duplicate a built-in under **Custom resources...** to customize them.
3. **Generate new occurrences when included** is separate from Include/Exclude membership. Turn it off only when an Included resource should lose unlocked occurrences and retain locks. An Excluded resource stays unchanged regardless; edited settings are kept for a future Included run after acceptance.
4. Leave **Use world-derived seed** enabled to keep the resolved resource seed reproducible for the accepted world, or turn it off and type/randomize a manual seed.
5. Set global **Abundance**, **Climate**, and **Geology**. These bias support fields; they do not rewrite terrain, Rivers, Lakes, or coasts.
6. Choose **Generate candidate**. The dialog captures an immutable row-major terrain/resource snapshot first, then builds the candidate away from the UI thread.
7. Compare **Current map** and **Candidate map** with the same pan/zoom. Pick any resource in **Preview resource** to inspect one heatmap and one report. Excluded resources report that their current entries stayed unchanged.
8. If you change Include/Exclude membership, seed, profile, or overrides, the old candidate stays visible but becomes stale. **Use resources** stays disabled until you regenerate.
9. Choose **Use resources** only when satisfied. That replaces only the Included unlocked resource occurrences, preserves locks, installs the reviewed settings, clears shared Undo/Redo, and keeps the terrain plus project identity.

Locked manual occurrences always survive Included generation and count against the upper target. Coverage stays independent per resource and may produce fewer tiles than requested when terrain suitability runs out. A preferred or avoided list is a group of alternative ordinary cues: one strong cue can carry the group, while agreement between several cues strengthens it. Use an explicit positive field/association weight when a factor must remain independently critical. Medium, range, normalized-surface exclusions, and custom-terrain rules are hard bans. Include Fertile Land or Timber when regenerating to remove their old unlocked Desert/BarrenRock/Tundra occurrences; locked ones remain with a hard-rule warning until manually unlocked or erased. `0%` or disabled makes an Included resource manual-only for that run, so unlocked occurrences are removed while locks remain. Excluding it instead preserves both locked and unlocked occurrences exactly.

The generator never invents unsuitable fallback cells. A report can still show zero when the resource is disabled/manual-only, has no hard-eligible terrain in that world, or every eligible cell remains below its fixed admission floor. On coarse grids, generated region radius is clamped to at least one complete campaign tile so a valid region can grow through cardinal neighbours instead of becoming trapped at a sub-tile core.

Resource-only regeneration still uses the current lattice and never changes terrain. Changed dimensions or campaign tile size belong to **Regenerate world...**, whose combined preview remaps locks and either regenerates saved unlocked resources or preserves manual-only occurrences as described above. See [[../Decisions/ADR-0021 - Reviewed Changed-Lattice Resource Remapping|ADR-0021]].

## Paint tile seasons

Choose **Seasons** in the workspace strip or **Seasons → Seasons workspace**. The canvas keeps the same terrain, pan, zoom, grid, hover coordinate, and pinned tile. Seasons are static classifications: every logical tile has exactly one ID, but there is no month, date, clock, or automatic progression.

1. Search by name or stable ID, then choose Spring, Summer, Autumn, Winter, or a custom definition. The selector states Built-in/Custom and Generated/Manual-only.
2. Choose **Paint** to assign the selected ID, **Reset** to restore the project default (initially Spring), or **Lock/Unlock** to change only regeneration protection while preserving the assigned ID.
3. Leave **Lock manual edits** enabled when painted IDs must survive future Season regeneration. Reset always returns the tile to the default unlocked.
4. Set the independent Season **Paint Area**: expansion `0` is `1 × 1`, through `12` for `25 × 25` complete tiles. The footprint clips at world edges.
5. Left-click or drag. One drag is one entry in the same Undo/Redo history as terrain and Resources; `Escape` restores an in-progress stroke.

Season color fills each complete tile over the unchanged terrain presentation. **Blend boundaries** mixes only the displayed edge colors; the saved ID and exported index stay exact. At `28 px/tile` or closer, **Season labels** shows an outlined abbreviation and appends `L` for a lock. Right-click to pin a tile and inspect exact name/ID, built-in/custom fallback, lock, terrain/elevation context, retained rule summary, and accepted-recipe availability. Pinned **Lock** and **Unlock** are ordinary undoable edits.

### Create and order custom seasons

Choose **Seasons → Manage seasons...** or the left-rail button.

1. Choose **Add custom** for a new manual-only definition, or **Duplicate** to start from an existing definition. Set name, portable stable ID, Spring/Summer/Autumn/Winter fallback, map color, tint, and effect intensity. A new draft ID is editable until **Apply seasons**; existing applied IDs are immutable.
2. Enter optional inclusive ranges as `min..max` for latitude, elevation, temperature, moisture, seasonal intensity/tendency, and Sea/Lake/River distance. Terrain Include is a whitelist, Exclude wins, and custom terrain uses comma-separated stable IDs.
3. Enable definitions that should participate in generation and use **Move up/Move down**. First match wins. The last enabled row is labelled **Catch-all** and always completes the map; its configured rule is retained for use if you later move it upward.
4. Built-in IDs, names, and fallbacks are protected, but their project appearance, rule, enabled state, and priority are editable. New custom definitions begin manual-only.
5. To delete a custom definition referenced by tiles, the project default, or priority, choose an explicit remaining replacement. Tile locks are preserved and the catalog/default/priority/map replacement applies atomically. An effective apply clears obsolete Undo/Redo; an equivalent apply is a no-op.

### Generate and review tile seasons

Choose **Seasons -> Generate seasons...**, press `Ctrl+Shift+G`, or use the left-rail button. Generation uses the current terrain, Season catalog, and explicit first-match priority. It never reads resources and does not change terrain or resource authority.

1. Choose **All tiles** or **Rectangle**. For a rectangle, enter inclusive tile bounds or drag a rectangle directly on either read-only preview map.
2. Keep **Derive from terrain** for a reproducible initial Season Seed, enter an explicit signed seed, or choose **Randomize**. The accepted Season Seed is saved independently from the terrain generator.
3. Choose **Whole globe** for north-to-south planetary latitude or **Regional** and enter the region's centre latitude. Set axial tilt; open **Advanced climate** only when tuning the accepted physical support parameters.
4. Review the read-only priority summary. First match wins and the final enabled definition is the unconditional Catch-all. Change rules or ordering through **Manage seasons...**, then reopen or regenerate the preview.
5. Press **Generate**. The current document stays unchanged while the Candidate is calculated. You can cancel the run or close the dialog without applying partial work.
6. Compare synchronized **Current — unchanged** and **Candidate — not applied** maps. Choose a report Season to inspect coverage, environmental matches, wins, generated unlocked tiles, shadowed matches, locks, changed-to counts, zero-result reasons, and warnings.
7. Pan, zoom, switch Current/Candidate at a narrow window, or toggle Grid, Labels, and Blend without invalidating the Candidate. Changing an in-dialog generation input or scope keeps the old preview visible as **Previous result — settings changed** and disables **Use seasons** until you generate again. The modal fixes catalog/priority and source authority for its lifetime; close it before **Manage seasons...**, which discards that dialog's Candidate. Unexpected source drift is rejected before acceptance.
8. Choose **Use seasons** only after reviewing a current result. Acceptance installs that exact Candidate and recipe, marks the project modified, and clears shared Undo/Redo. Locked tiles and tiles outside a rectangle remain exact.

After acceptance, right-click a tile in the Seasons workspace. The pinned inspector adds generated latitude, temperature, moisture, intensity/tendency, rain shadow, exact Sea/Lake/River distances, the current rules' winning, shadowed, and higher-priority matches, authority agreement, and source/input current-or-stale state. Reopening a project with a saved recipe rebuilds the immutable support/fingerprint cache and re-evaluates rules on demand; it does not regenerate or rewrite the Season map.

This command regenerates only the existing lattice. New World handles atomic generated terrain-and-Season creation, while **Regenerate world...** owns reviewed changed-grid lock remapping as described above.

## Use the automatic coast

Paint any non-water tile directly beside Sea or Lake. No Coastal choice is required. The edge automatically faces every immediate N/E/S/W water neighbour:

```text
original terrain/custom material | inner 90% | outer 10% matching water | Sea or Lake
```

The percentages are edge depth for a typical coast with water on one side. When water touches multiple sides, each side receives its own 10%-deep transition and corners follow the nearest water-facing edge; total original-material area can therefore be below 90% on a multi-sided coast. Removing cardinal water restores the entire original material. Diagonal water alone does not create a coast. The inspector keeps the original type name and appends `automatic Sea coast`, `automatic Lake coast`, or both.

There is no automatic sand strip. Paint **Beach** when the whole campaign tile should be sand; it will still receive the same outer 10% matching-water edge. Cliff, Hills, Forest, Mountain, Unassigned, River fallback ground, and custom land behave the same way.

## Route a river

Choose **River** for a narrow channel or **Large River** for a broad major-river corridor, set its centre elevation, then drag. Both types deliberately ignore Paint Area and always use a `1 × 1` footprint, so the editor can turn diagonal pointer motion into one contiguous N/E/S/W tile path. Orthogonally adjacent River and Large River tiles connect to each other and to tool-created River Junctions without a separate connector tool.

Large River still leaves ground visible on both sides. Its broader preview communicates category and map readability; it does not mean a fixed percentage of a 2 km or 5 km tile becomes literal water in the downstream game terrain.

A normal River or Large River tile may have at most two exits across the combined route. If an ordinary stamp would create a third exit at the target or one of its neighbours, the editor skips that coordinate. The cursor becomes a red crossed rectangle when the hovered stamp is invalid, and the status bar reports how many unique crossing coordinates were blocked when the drag ends. Other valid coordinates in the same drag remain painted and undo together.

### Split a river into branches

Use the dedicated split action when one route should become two, three, or four separated branches:

1. Paint the source route up to an endpoint. An endpoint has exactly one River/Large River neighbour; an isolated River tile is also accepted.
2. Right-click that normal River or Large River tile to pin it.
3. In **Pinned tile → River split**, choose `2`, `3`, or `4` branches.
4. Keep **Direction: Auto** for an endpoint. Auto continues away from its existing neighbour. For an isolated root, choose North, East, South, or West.
5. Choose **Create 2/3/4-branch split**.

Two branches use one Y junction. Three branches use two cascaded Ys; four use three. No tile receives four exits, so the branches do not form a cross. The new leaf tiles keep the pinned route's River or Large River class and every new tile copies its centre height. The complete split is one undo entry.

The action refuses to replace Sea/Lake, extend beyond the world, overlap an existing River, or touch an outside route beside its footprint. If it is blocked, choose a more open endpoint or another valid direction. Generated drainage can merge compatible tributaries into three-exit confluences, while this action remains the designer-controlled way to add two-, three-, or four-branch distributary/split shapes through cascaded Y junctions.

## Navigate and inspect slopes

- Rotate the wheel to zoom around the pointer.
- Hold the middle mouse button and drag to pan.
- Press `F` or choose **Fit world** to show the full extent.
- Toggle **Tile grid** without changing data.
- Toggle **Elevation numbers** to show or hide stored centre heights. Even when enabled, labels appear only after zooming in to at least `28 px/tile`; hiding labels never changes tile data or slopes.
- Toggle **Height only** to compare relief without type hues.
- Move over the map to inspect tile coordinate, type, stored centre height, derived surface height, and world position.
- Right-click to pin a tile while inspecting elsewhere or to target **Copy centre** and **Blend around**.

Stored centre height and **Surface here** differ whenever the pointer is away from that centre and neighboring heights differ. That difference is the automatic slope, not hidden authored data.

## Save and reopen

Choose **Save** or press `Ctrl+S`. The first save asks for a project folder. **Save As** always asks for a folder and confirms before replacing an existing version-2 project.

A current project keeps version-2 terrain plus orthogonal resource and Season sidecars:

```text
world.json
campaign-tiles.json
custom-terrain.json (only when custom types exist)
resource-definitions.json (only when custom resources exist)
resource-generation.json (only when settings exist)
resource-tiles.json (only when occurrences exist)
season-definitions.json
season-generation.json (only after accepting Season generation)
season-layer.bin
```

Version 2 terrain saves generated tile values, not the terrain creation profile or seed. Resource generation settings remain optional authority. Season definitions/priority/default and every dense ID/lock always round-trip; the Season generation recipe remains optional and is never invented. Save stages terrain, resources, and Seasons together, reloads the complete candidate for validation, and marks the editor clean only after the nine-file managed commit succeeds. An older project with no Season files opens as a clean unlocked Spring layer; its first ordinary save adds the complete Season sidecars.

Choose **Open** or press `Ctrl+O`, then select `world.json`. Unsaved changes prompt before New, Open, Exit, or window close.

## Import a version-1 world

Open the old `world.json` normally. The editor:

1. strictly loads the old metadata, campaign types, and height chunks;
2. copies each campaign type;
3. averages the samples owned by each campaign cell into one centre height;
4. marks the converted document unsaved;
5. requires a different save folder.

The version-1 source folder is never modified by opening or converting it. Save the converted version to a new folder, then keep or archive the old project independently.

## Recovery behavior

Validation and file errors appear without discarding the current world. A failed combined save leaves the document dirty and attempts to restore every managed terrain/resource/Season file. Unsupported versions, duplicate tile/resource records, unknown terrain/resource/Season IDs, partial or corrupt Season sidecars, invalid custom definitions, potential outside `1..100`, out-of-range heights, invalid river topology, invalid grid dimensions, and out-of-world coordinates refuse to load instead of inventing replacement data. Terrain suitability mismatch is valid resource authority and appears as a warning. Older version-2 `coastal` records are the terrain exception: they load as Plains at the same centre height, the document is marked modified, and the status asks you to save the normalized project. Automatic coast adjacency itself is derived and never invalidates a project.

See [[../Reference/World File Format|World File Format]] for the importer contract.
