# ADR-0010: Tectonic Erosion and Hierarchical Drainage

- Status: Accepted
- Date: 2026-08-12

Current geological noise, orogeny weights, Mountain suitability, and tributary-budget details are refined by [[ADR-0011 - Physical Terrain Noise and Boundary-Aligned Ridges|ADR-0011]]. This note retains the original tectonic/erosion/drainage boundary; the reference document holds the current formulas.

## Context

The deterministic starting-world generator already produced editable campaign tiles, recognizable preset coastlines, connected Mountain systems, Priority-Flood Lakes, and water-reaching Rivers. Its relief still depended primarily on overlapping noise fields, however, so ranges had no explicit convergent/rift/shear cause and valleys did not respond to drainage before River placement. Rivers were also admitted only as separate paths even when their receiver graph naturally shared a downstream course.

The upgrade must improve geographical structure without creating a second terrain authority, a scientific-simulation promise, a breaking file format, or unbounded creation cost. Preset edge guarantees, seed repeatability, tile ratios, safe custom land, generated-preview review, immediate editability, the 250,000-tile limit, and version-2 River topology must remain intact.

## Decision

Generation builds a deterministic, creation-time tectonic model in physical kilometres:

- four to twelve seeded Voronoi provinces are distributed with a jittered grid;
- each province receives a velocity vector and bounded elevation bias;
- relative boundary motion resolves into convergent uplift, divergent rift, and shear strength;
- a warped Gaussian boundary influence turns straight Voronoi borders into coherent regional belts;
- tectonic structure supplies 75% of the orogeny field and the previous broad regional field supplies 25%.

The tectonic field is transient. It shapes generated centre heights and Mountain suitability but is neither serialized nor editable. Preset masks remain responsible for hard world shape and named edge guarantees; tectonics shape relief inside that geography rather than replacing it. Mountain classification retains only endpoint-grown ridge cores from the suitable field and gives nearby land priority as Hills/foothills, so a percentage target cannot force the full interior of a candidate region into a compact Mountain patch.

After base elevation and tidal-inlet carving, generation runs deterministic erosion before final Lake and River solving:

- mass-conserving thermal relaxation moves bounded height above a terrain-style talus threshold;
- one Priority-Flood receiver/accumulation solve drives a bounded stream-power valley pass;
- a small downstream fraction is deposited on land;
- one final thermal pass removes sharp artifacts;
- every land height remains within the configured sea-level/maximum bounds.

Final hydrology rebuilds Priority-Flood drainage over the eroded terrain. A River candidate may stay separate or merge into the first accepted downstream route on the same receiver path. A merge is accepted only when the tributary prefix is long enough, every new ordinary/Large River segment stays at two exits or fewer, the shared tile stays at three exits or fewer, no lateral contact is introduced, and the canonical map validator accepts the complete result. An exactly-three-exit merge is stored as the existing `RiverJunction` value. Four-way crossings remain forbidden.

The generator remains a deterministic game/editor hybrid, not a full plate-tectonic, climate, sediment, or fluid simulator. Version 2 still stores one undirected route classification and one centre height per tile. Direction, discharge, physical channel width, persistent plate data, climate feedback, and confluence/distributary semantics remain version-3 or downstream-system concerns.

## Consequences

- Mountain candidates form coherent convergent and shear belts; divergent boundaries can form lower rift regions. Stored Mountain tiles express the narrow crest core, while surrounding relief reads as Hills/foothills.
- Thermal and fluvial passes create terrain-shaped slopes and drainage-aligned valleys before Lake/River classification.
- Compatible tributaries can form natural confluences without allowing crossings or arbitrary neighboring contacts.
- Mountain density remains an independent classification control: Sparse, Balanced, and Dense select different amounts of suitable relief rather than changing the tectonic provinces.
- Terrain style now affects both initial relief and erosion response.
- Existing presets, coast guarantees, inland/custom ratios, automatic coast, tile authority, preview acceptance, persistence, runtime export, and immediate editing remain unchanged.
- Equal validated inputs remain deterministic, generation stays bounded by the existing tile limit, and version-2 save/runtime formats do not change.
- Tectonic provinces and erosion intermediates cannot be inspected or edited after creation because only their final ordinary tile heights/types are authoritative.
- Designer-created River Split topology remains useful for intentional distributaries and branching layouts that cannot be inferred from undirected generated drainage.

Exact formulas and thresholds are documented in [[../Reference/Campaign World Generation|Campaign World Generation]]. The broader generated-world contract remains [[ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]].
