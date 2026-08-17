# World Editor Context

Canonical domain language for the campaign-scale world editor. These terms keep independent tile authorities distinct and prevent generated classifications from acquiring unintended time or gameplay meaning.

## Language

**Tile Season**:
Exactly one static seasonal classification assigned to a campaign tile. It does not advance with a calendar or change automatically over time.
_Avoid_: Current season, calendar season, season phase

**Season Layer**:
The complete world authority that gives every logical campaign tile exactly one Tile Season, including land, Sea, Lake, River, and Unassigned tiles.
_Avoid_: Terrain type field, sparse resource map, land-only overlay

**Season Definition**:
A named built-in or project-defined classification that may be assigned as a Tile Season.
_Avoid_: Terrain type, biome, climate profile

**Season Catalog**:
The project-owned collection of all built-in and Custom Season Definitions. It has no small product-level count cap; definitions beyond the active generation limit remain available for manual painting and future configuration. The authoring/runtime interchange representation has a technical ceiling of 65,535 definitions.
_Avoid_: Generation priority, four-season-only list, literally unbounded runtime evaluation

**Generation-enabled Season Definition**:
A Season Definition participating in the ordered Season Priority for one generation configuration. At most 256 definitions may be generation-enabled at once; other catalog definitions are manual-paint-only until enabled.
_Avoid_: Deleted season, unavailable season, unlimited active rule

**Manual-paint-only Season Definition**:
A Season Definition that remains visible and selectable in the Seasons Workspace palette but is excluded from Season Priority and procedural generation. Enabling or disabling generation participation is a non-mutating configuration change subject to the 256-definition active limit; existing Tile Seasons remain unchanged until manual editing or acceptance of a new Season Candidate.
_Avoid_: Hidden season, deleted definition, disabled palette entry

**Built-in Season Definition**:
One of the universal Spring, Summer, Autumn, or Winter identities available to every project. Its identity is stable, while a project may change its Season Priority and environmental rule ranges.
_Avoid_: Wet season, dry season, monsoon

**Custom Season Definition**:
A project-owned Season Definition added beyond the four built-ins, such as Monsoon, Wet Season, or Dry Season. It selects one built-in Season Definition as its portable and surface-aware appearance fallback while retaining its own identity, tint, and effect intensity. A newly created custom definition starts manual-paint-only and becomes generation-enabled only after its rule validates and the author explicitly enables it.
_Avoid_: Global default season, custom terrain type

**Season Fallback**:
The built-in Spring, Summer, Autumn, or Winter identity used when a consumer does not recognize a Custom Season Definition.
_Avoid_: Generation fallback, default list entry

**Default Tile Season**:
The project-selected Season Definition written to every tile of a blank/manual world and by the Seasons Workspace's Reset to default tool. It initially equals Spring but may be changed to any valid built-in or custom definition.
_Avoid_: Final generation catch-all, missing tile value, erase

**Season Definition Replacement**:
The required remapping performed before deleting a Custom Season Definition that is referenced by any tile. The editor preselects that definition's built-in Season Fallback, allows another valid Season Definition to be chosen, previews the affected count, and commits the definition deletion and tile replacement atomically.
_Avoid_: Orphaned season ID, silent deletion, implicit data loss

**Season Rule**:
The geographic or environmental ranges associated with one Season Definition during generation, including latitude, elevation, temperature, moisture, water proximity, and terrain inclusion or exclusion. A tile may satisfy more than one Season Rule. Contradictory or incomplete custom rules receive field-level validation and cannot be generation-enabled, but their definitions remain available for manual painting.
_Avoid_: Terrain rule, resource suitability

An empty terrain Include list permits all terrain. A non-empty Include list is a whitelist. Exclude always wins, and validation rejects placing the same terrain identity in both lists.

Custom terrain inherits the filter membership of its built-in base terrain unless the rule explicitly includes or excludes that custom terrain's stable ID. An explicit custom-ID exclusion has final precedence.

**Season Distribution Report**:
The preview comparison showing resulting tile counts and percentages for Current and Candidate Season Layers, together with locks, changed tiles, overlaps, and unmatched-before-catch-all counts. Percentages are observed results, never generation quotas. A generation-enabled definition may honestly produce zero tiles; the report distinguishes no environmental matches from matches fully captured by higher-priority rules and never forces placement.
_Avoid_: Required coverage, season ratio target, forced fill

**Season Runtime Payload**:
The exported game-facing season data: one stable Season Definition ID for every logical tile plus the custom-definition manifest and each custom definition's built-in fallback. Generation-only support fields, diagnostic contributions, noise, and preview reports are omitted.
_Avoid_: Baked editor colors, exported temperature cache, incomplete custom IDs

**Season Support Field**:
A deterministic generation-only environmental field, such as temperature, moisture, seasonal intensity, or warming/cooling tendency, used by Season Rules without becoming editable tile authority. Spring and Autumn are distinguished by local seasonal direction rather than temperature alone. Elevation lowers local temperature through a physical lapse-rate model, so alpine tiles may receive a colder seasonal appearance than nearby lowlands without changing terrain authority.
_Avoid_: Tile Season, saved climate layer, terrain height

Season generation consumes terrain, elevation, water topology, geographic coverage, and season settings only. It does not inspect resource occurrences, preventing a circular terrain/resource/season generation dependency. Any later resource-season interaction must declare a one-way dependency explicitly.

**Season Appearance**:
The non-destructive visual composition of a Tile Season with the existing terrain or water material. Authoring mode renders each campaign tile fully as its assigned season by default. An optional Blend Boundaries display toggle may soften presentation edges, but never changes tile identity, painting, hit-testing, persistence, or export. Season Appearance never rewrites terrain type, elevation, River topology, or resources.
_Avoid_: Seasonal terrain conversion, replacement terrain texture

**Season Lock**:
An authoring protection on one Tile Season assignment that prevents procedural regeneration from replacing that tile while leaving manual repainting available after an explicit unlock. When the world lattice changes, locked assignments are remapped by greatest physical-area overlap inside the preview; unlocked assignments are regenerated. The report identifies preserved, conflicted, and dropped locks before acceptance.
_Avoid_: Resource lock, immutable season, terrain lock

An equal-overlap claim from differently locked old assignments is an unresolved lock conflict. It blocks Candidate acceptance until the author explicitly chooses the surviving assignment; no season-ID order or random tie-break may discard a lock.

**Season Regeneration Scope**:
The whole world or a selected rectangular tile area whose unlocked Tile Seasons may be replaced together. Every Season Definition competes through the complete Season Priority inside the scope; definition-by-definition regeneration is not valid.
_Avoid_: Selected-season scope, resource inclusion list

**Season Candidate**:
A temporary, reviewed Season Layer result produced for one Season Regeneration Scope. It does not become authority until explicit acceptance and becomes stale when its settings or source world change.
_Avoid_: Live Season Layer, auto-applied generation

**Terrain-and-Season Candidate**:
The preview-first new-world result in which procedurally generated terrain and a complete generated Season Layer are reviewed and accepted atomically. This term does not imply resource generation. Blank or manual world creation instead initializes every tile to an explicitly selected default Tile Season—initially Spring, but selectable from any available built-in or custom definition—and may generate seasons later.
_Avoid_: Terrain-only generated world, partially initialized season map, post-creation silent generation

**Season Generation Staleness**:
A warning that terrain, elevation, water, Season Rules, priority, coverage, or seed changed after the authoritative Season Layer was generated. Staleness never mutates or invalidates existing Tile Seasons; it only disables acceptance of an older Candidate and offers a fresh preview.
_Avoid_: Automatic season rewrite, invalid tile assignment, destructive refresh

**Seasons Workspace**:
The peer authoring workspace for viewing, painting, locking, configuring, generating, and accepting Tile Seasons on the shared campaign canvas. It does not own terrain or resource changes.
_Avoid_: Terrain season tool, generation-only dialog

**Season Priority**:
The explicit top-to-bottom order of generation-enabled Season Definitions used during generation. The first matching Season Rule assigns the tile. The final entry is an unconditional generation catch-all and may be either built-in or custom; a custom catch-all still retains its built-in Season Fallback.
_Avoid_: Display order, percentage weight, random priority

The default built-in priority is **Winter → Spring → Autumn → Summer**. This is a rule-specificity order: Winter captures cold and alpine conditions, Spring and Autumn distinguish warming from cooling moderate conditions, and Summer is the warm/tropical unconditional catch-all.

**Season Snapshot**:
A deterministic, seed-derived global orbital phase used to generate static Tile Seasons without storing a month, date, or calendar. Comparable northern and southern latitudes receive opposite seasonal phases, while equatorial classification is governed mainly by temperature, moisture, and matching custom rules rather than forced four-season bands. Ocean influence, elevation, and coherent physical-scale variation may gently shift local seasonal expression, but tiles never receive independent random phases. It does not advance or mutate tiles after generation.
_Avoid_: Running calendar, user-selected month, seasonal simulation, world time

**Season Seed**:
The saved reproducibility identity for Season Snapshot phase and procedural seasonal variation. It is independent after creation even when its initial value is derived from terrain generation or stable world content.
_Avoid_: Unsaved terrain seed, random-every-time seed, resource seed

**Season Axial Tilt**:
The generation setting controlling hemispheric seasonal strength without creating a calendar. It defaults to Earth's 23.44 degrees and is exposed as an Advanced setting for fictional worlds.
_Avoid_: Month, animation speed, terrain rotation

**Season Climate Controls**:
The collapsed Advanced generation settings for lapse rate, maritime strength and radius, moisture influence, rain shadow, and coherent regional variation. They use Earth-like defaults and are not required for the ordinary Generate-and-preview workflow.
_Avoid_: Mandatory expert setup, hidden hard-coded climate, per-tile randomness

**Season Coverage**:
The generation-only geographic interpretation that maps campaign tile centres to latitude and longitude. It does not change terrain coordinates, tile adjacency, River topology, or world dimensions.
_Avoid_: Map projection, terrain topology, world resize

**Whole-globe Coverage**:
A Season Coverage spanning the north pole to the south pole, with periodic longitude for seamless seasonal fields.
_Avoid_: Wrapped campaign map, spherical terrain

**Regional Coverage**:
A Season Coverage centred on an explicit latitude from -90 to +90 degrees whose north-south span follows the campaign map's physical height using Earth-scale latitude distance. The UI provides Equator, Northern/Southern Mid-Latitude, and Northern/Southern Polar presets without replacing the exact numeric value. A regional window that would cross either pole is invalid and must be repositioned or changed to Whole-globe Coverage; it is never silently clamped or folded.
_Avoid_: Cropped globe topology, normalized full-world latitude

## Flagged ambiguities

**Season**:
Use **Tile Season** for the per-tile value and **Season Definition** for a selectable built-in or custom identity. Do not use the unqualified term when the distinction matters.

## Example dialogue

> **Developer:** Does this tile move from Winter to Spring when time advances?\
> **Domain expert:** No. Its Tile Season is statically assigned to the Winter Season Definition. Calendar-driven seasons are outside this feature.\
> **Developer:** Can another project define Monsoon?\
> **Domain expert:** Yes. Spring, Summer, Autumn, and Winter are the only built-ins; Monsoon can be a custom Season Definition with one of them as its Season Fallback.
>
> **Developer:** What if a high tropical tile matches both Winter and Summer?\
> **Domain expert:** The first matching definition in Season Priority wins. If Winter is above Summer, that tile receives Winter; the final fallback ensures no tile remains unassigned.
>
> **Developer:** Does generated temperature become another value I paint on every tile?\
> **Domain expert:** No. It is a Season Support Field used to assign the static Tile Season, not another editable layer.
>
> **Developer:** Does painting Winter convert Forest into a snow terrain type?\
> **Domain expert:** No. Forest remains authoritative terrain; Winter changes only its Season Appearance.
>
> **Developer:** Will regeneration erase a deliberate custom-season tile?\
> **Domain expert:** Not while its Season Lock is set; unlocked assignments remain replaceable generation authority.
>
> **Developer:** Can I regenerate only Winter without considering Spring or Summer?\
> **Domain expert:** No. Regeneration is spatial because every tile receives the first matching definition from the complete Season Priority.
>
> **Developer:** Does clicking Generate immediately overwrite the current seasons?\
> **Domain expert:** No. It creates a Season Candidate; only explicit acceptance replaces the eligible current assignments.
>
> **Developer:** Do I edit Winter from the Terrain palette?\
> **Domain expert:** No. Tile Seasons belong to the Seasons Workspace even though their appearance is composed with terrain on the shared canvas.
>
> **Developer:** Will the generated seasons change next month?\
> **Domain expert:** No. The Season Snapshot influences generation once; the resulting Tile Seasons remain static until a user edits or regenerates them.
>
> **Developer:** Does an ocean tile have no season?\
> **Domain expert:** It still has exactly one Tile Season. Its surface may render that season differently, but the Season Layer remains complete.
>
> **Developer:** Can we reproduce those seasons after reopening the project?\
> **Domain expert:** Yes. The saved Season Seed reproduces the same Snapshot and spatial variation independently of transient terrain-generation state.
>
> **Developer:** Does Whole-globe Coverage connect terrain across the left and right edges?\
> **Domain expert:** No. It makes seasonal generation seamless across longitude, but it does not change campaign-tile adjacency or terrain topology.
>
> **Developer:** Can a project contain more than 256 custom seasons?\
> **Domain expert:** The Season Catalog has no small product cap, but at most 256 definitions may be generation-enabled in one ordered run. Additional definitions remain available for manual painting.
>
> **Developer:** Does manual-only mean I cannot paint that season?\
> **Domain expert:** No. It remains visible in the Seasons palette and can be painted normally; it simply does not compete during generation.
>
> **Developer:** Must the final generation catch-all be Spring, Summer, Autumn, or Winter?\
> **Domain expert:** No. It may be a custom definition such as Wet Season; that custom identity still declares a built-in Season Fallback for portability and surface-aware rendering.
>
> **Developer:** If I disable Winter generation, are existing Winter tiles immediately removed?\
> **Domain expert:** No. Generation configuration never mutates the authoritative Season Layer; only manual edits or explicit acceptance of a Season Candidate can replace assignments.
>
> **Developer:** If northern temperate tiles resemble Winter, should southern temperate tiles also be Winter?\
> **Domain expert:** No. Comparable hemispheres use opposite Snapshot phases. Near the equator, environmental conditions and custom Wet or Dry rules matter more than forcing astronomical season names.
>
> **Developer:** Can neighboring tiles independently roll different times of year?\
> **Domain expert:** No. One coherent global orbital phase drives the Snapshot. Geography may shift local expression gently, but procedural variation cannot become per-tile seasonal randomness.
>
> **Developer:** Can I require exactly 30 percent Winter?\
> **Domain expert:** No. The preview reports Winter's resulting percentage, but realistic environmental rules and priority determine the distribution without a forced quota.
>
> **Developer:** Does raising a mountain immediately replace its Tile Season?\
> **Domain expert:** No. The Season Layer remains authoritative. The editor marks its generation inputs as changed and offers a new preview without silently rewriting the tile.
>
> **Developer:** Are all worlds forced to use Earth's seasonal strength?\
> **Domain expert:** Earth-like generation defaults to 23.44 degrees of axial tilt, while an Advanced setting allows a fictional world to use a different tilt without adding a calendar.
>
> **Developer:** Can generated terrain be accepted while its generated seasons fail or remain missing?\
> **Domain expert:** No. New-world preview treats generated terrain and its complete Season Layer as one candidate and accepts both atomically. A deliberately blank/manual world is initialized from a selected default Tile Season instead.
>
> **Developer:** Must a blank world begin as Spring everywhere?\
> **Domain expert:** Spring is only the initial selector value. The author may initialize all tiles from any Season Definition available to that project.
>
> **Developer:** What does erasing a season tile mean if every tile must have one?\
> **Domain expert:** There is no absent value. Reset to default writes the project's Default Tile Season.
>
> **Developer:** If two tiles have the same mild temperature, how can one be Spring and the other Autumn?\
> **Domain expert:** Generation also evaluates seasonal direction: warming conditions support Spring, while cooling conditions support Autumn.
>
> **Developer:** Does a snowy high mountain require converting Mountain terrain into Winter terrain?\
> **Domain expert:** No. Elevation lowers the generated temperature support field, which may assign a Winter Tile Season while Mountain remains the terrain authority.
>
> **Developer:** Must every user configure atmospheric constants before generating seasons?\
> **Domain expert:** No. The normal workflow uses realistic defaults; detailed physical controls are available in a collapsed Advanced section.
>
> **Developer:** Why is the default list not calendar order?\
> **Domain expert:** Season Priority is a first-match rule chain, not a timeline. The most specific cold and transitional rules come first, while Summer safely completes unmatched warm or tropical tiles.
>
> **Developer:** Does adding Monsoon immediately change the next generation result?\
> **Domain expert:** No. A new custom definition is paintable immediately but starts manual-paint-only until its rule is valid and explicitly enabled.
>
> **Developer:** What happens if I delete a custom season already painted on tiles?\
> **Domain expert:** Deletion requires an explicit replacement mapping, initially set to the custom season's built-in fallback, and the remap is committed atomically with deletion.
>
> **Developer:** Does a soft visual boundary mean one tile stores two seasons?\
> **Domain expert:** No. Full-tile categorical rendering is the authoring default. Optional boundary blending is presentation-only; every tile still stores exactly one Tile Season.
>
> **Developer:** How do I place a regional map on Earth-like latitude?\
> **Domain expert:** Enter its exact centre latitude or start from an Equator, Mid-Latitude, or Polar preset; physical map height determines the north-south span.
>
> **Developer:** What if the regional span extends beyond 90 degrees north?\
> **Domain expert:** The editor rejects that coverage with an actionable message. Clamping would distort geography, so the author must move the centre or use Whole-globe Coverage.
>
> **Developer:** Does the game export need every temperature and moisture value used by the generator?\
> **Domain expert:** No. Runtime data contains the authoritative season ID per tile and portable custom-definition metadata; support fields and diagnostics remain editor-only.
>
> **Developer:** What survives if tile size changes?\
> **Domain expert:** The preview remaps locked seasons by greatest physical overlap, regenerates unlocked tiles, and reports every preserved, conflicted, or dropped lock before acceptance.
>
> **Developer:** Which lock wins an equal-overlap conflict?\
> **Domain expert:** Neither wins silently. The conflict blocks acceptance until the author explicitly resolves it.
>
> **Developer:** Must every enabled season appear at least once?\
> **Domain expert:** No. Zero is a valid result when geography or priority provides no winning tiles, and the preview explains the cause rather than forcing a placement.
>
> **Developer:** Can an unfinished Monsoon rule enter generation?\
> **Domain expert:** No. Field-level validation explains the conflict and blocks generation enablement, while Monsoon remains available for manual painting.
>
> **Developer:** Does an empty terrain Include list match nothing?\
> **Domain expert:** No. Empty means unrestricted; a populated list is a whitelist, and Exclude always takes precedence.
>
> **Developer:** Must every existing season rule be edited when I add Savanna terrain based on Ground?\
> **Domain expert:** No. Savanna inherits Ground's membership unless its stable custom ID is explicitly included or excluded.
>
> **Developer:** Can an Iron or Timber occurrence change which season generation assigns?\
> **Domain expert:** No. Season generation does not consume the resource layer. A future resource-season relationship must be introduced as an explicit one-way dependency rather than a cycle.
