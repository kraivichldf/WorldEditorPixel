# World Editor Context

Canonical domain language for the campaign-scale world editor. Terrain, resources, and season availability are separate tile authorities.

## Language

**Season Occurrence**:
A statement that one Season Definition can occur on one campaign tile. A tile contains at most one occurrence of a given Season Definition and may contain occurrences for several different definitions.
_Avoid_: Current season, assigned season, calendar state

**Tile Season Set**:
The unordered collection of Season Occurrences on one campaign tile. The set describes which seasons can exist there; it does not choose which one is active now.
_Avoid_: Tile Season, season slot, season timeline

**Season Layer**:
The world authority containing every tile's Season Occurrences. It is independent from terrain types, resource occurrences, months, clocks, and weather simulation.
_Avoid_: Current-season map, biome map, calendar layer

**Season Definition**:
A stable built-in or project-defined seasonal identity that may occur on many tiles.
_Avoid_: Terrain type, resource type, month

**Built-in Season Definition**:
One of Spring, Summer, Fall, or Winter. Built-ins have stable identities and cannot be removed.
_Avoid_: Calendar quarter, fixed three-month period

**Custom Season Definition**:
A project-owned seasonal identity such as Monsoon, Wet Season, or Dry Season. It retains its own identity while declaring a built-in fallback for consumers that do not recognize it.
_Avoid_: Custom terrain, renamed built-in season

**Season Catalog**:
The project-owned collection of built-in and Custom Season Definitions available for painting, generation, inspection, persistence, and export.
_Avoid_: Generation result, Tile Season Set

**Season Fallback**:
The built-in Spring, Summer, Fall, or Winter identity used by a consumer that does not recognize a Custom Season Definition.
_Avoid_: Generation catch-all, default tile season

**Season Lock**:
An authoring protection on one Season Occurrence. Regeneration preserves that exact tile-and-definition membership without locking other seasons on the tile.
_Avoid_: Tile lock, terrain lock, immutable season set

**Season Rule**:
The geographic and environmental conditions under which one Season Definition can occur. Rules are evaluated independently, so several definitions may match and coexist on the same tile.
_Avoid_: First-match rule, exclusive season band, priority winner

**Season Generation Selection**:
The explicit set of Season Definitions to regenerate. Unselected definitions remain unchanged, while selected unlocked occurrences may be added or removed according to their rules.
_Avoid_: Season Priority, global winner list

**Season Candidate**:
A temporary reviewed Season Layer produced for one generation selection and spatial scope. It does not become authority until explicit acceptance.
_Avoid_: Live season data, auto-applied result

**Season Distribution Report**:
A comparison showing how many tiles contain each Season Definition in Current and Candidate data, including additions, removals, locks, and no-match results. Percentages are per-definition tile coverage and do not need to sum to 100 percent.
_Avoid_: Exclusive terrain ratio, forced season quota

**Season Support Field**:
A deterministic generation-only environmental field such as temperature, moisture, water influence, or seasonal range. It helps rules decide occurrence membership without becoming editable tile authority.
_Avoid_: Saved climate layer, season occurrence, terrain height

**Season Appearance**:
The non-destructive visualization of one selected Season Definition over terrain on tiles where its occurrence exists. It never rewrites terrain, resources, or other Season Occurrences.
_Avoid_: Seasonal terrain conversion, single categorical world overlay

**Season Generation Staleness**:
A warning that terrain, rules, catalog, scope, or settings changed after a Candidate was generated. Staleness disables acceptance but never mutates current Season Occurrences.
_Avoid_: Automatic refresh, invalid current data

**Season Seed**:
The reproducibility identity for deterministic environmental variation used by season generation. It does not represent a date or advancing phase.
_Avoid_: Month, current year, runtime clock

**Season Coverage**:
The generation-only interpretation that maps campaign tile centres to Earth-like latitude and longitude. It does not alter terrain coordinates, adjacency, or world dimensions.
_Avoid_: Map projection, terrain wrapping

**Whole-globe Coverage**:
A Season Coverage spanning north pole to south pole with periodic longitude for coherent environmental fields.
_Avoid_: Wrapped campaign topology, spherical terrain

**Regional Coverage**:
A Season Coverage centred on an explicit latitude whose north-south span follows the campaign map's physical height.
_Avoid_: Full-globe map, normalized tile percentage

**Runtime JSON Export**:
A one-way, single-file UTF-8 representation of accepted terrain, Resource occurrences, and Season Occurrences for a game-engine importer. It is derived runtime data and never editable project authority.
_Avoid_: Project save, raw heightmap, engine-native asset

## Flagged ambiguities

**Season**:
Use **Season Definition** for an identity, **Season Occurrence** for one tile membership, and **Tile Season Set** for all memberships on one tile. The unqualified term does not mean a current calendar state.

**Priority**:
Season generation has no first-match priority or catch-all winner. Definition order is presentation only; every selected rule is evaluated independently.

## Example dialogue

> **Developer:** Which season is this tile currently in?\
> **Domain expert:** The editor does not store a current season. This tile's Season Set contains Spring, Summer, and Fall.
>
> **Developer:** Can another tile also contain Winter?\
> **Domain expert:** Yes. Its set may contain Spring, Summer, Fall, and Winter at the same time.
>
> **Developer:** If Winter matches, does it replace Fall?\
> **Domain expert:** No. Each definition is evaluated independently, so both occurrences may exist.
>
> **Developer:** Does a Winter lock protect the whole tile?\
> **Domain expert:** No. It preserves only the Winter occurrence; generation may still add or remove other unlocked occurrences.
>
> **Developer:** Do occurrence percentages need to total 100 percent?\
> **Domain expert:** No. Coverage is reported independently for each definition because the same tile can count toward several seasons.
>
> **Developer:** Does Spring mean a fixed three-month period?\
> **Domain expert:** No. It is a seasonal identity that can occur there; months and time progression are outside this layer.
