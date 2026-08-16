# ADR-0005: Water and River Tile Topology

- Status: Accepted
- Date: 2026-08-10
- Amended: 2026-08-12 — add the interoperable Large River route class and explicit multi-Y River Split topology

## Context

The tile-authoritative surface needs distinct sea, lake, shore, and river classifications without restoring a separate brush or sub-tile authoring layer. River painting must make connected routes automatically. Designers also need deliberate two-, three-, and four-branch river splits, but those branches must not become ambiguous four-way crossings or accidental contacts with another route.

## Decision

The active `CampaignTileType` palette contains Sea, Lake, River, Large River, Beach, and Cliff in addition to the existing land and Unassigned types. The old generic Water value remains a read-time compatibility alias only and normalizes to Sea. Large River is appended as numeric value `13`, preserving every earlier version-2 numeric value. `RiverJunction` is appended as value `14`; it is persisted and exported but created only by the River Split tool, not offered as a direct paint choice.

Beach and Cliff are explicit shore classifications usable beside Sea or Lake. The editor neither generates them nor enforces adjacency in this milestone.

River connectivity is derived from orthogonally adjacent River, Large River, or River Junction tiles. Normal and Large River segments may have at most two N/E/S/W exits. A River Junction may have at most three exits and represents an intentional Y. Four exits are invalid for every river type. This permits sources, endpoints, straight sections, bends, loops, narrow-to-large transitions, and explicit branching without permitting an arbitrary cross.

The River Split tool starts from a pinned normal or Large River tile with zero or one river neighbour. From an endpoint, **Auto** continues away from the incoming side; from an isolated source the designer chooses North, East, South, or West. Its fixed templates are:

- two branches: one three-exit Y junction and two leaf segments;
- three branches: two cascaded Y junctions and three leaf segments;
- four branches: three cascaded Y junctions and four leaf segments.

Every proposed tile copies the root centre height; branch leaves preserve the root's normal/Large class. Before mutation, the builder rejects world-edge overflow, existing river tiles in its footprint, replacement of Sea/Lake, and cardinal contact with any river outside the root and proposed footprint. The complete result applies as one undoable command. A later ordinary edit may reduce a persisted junction below three exits, but no edit may exceed that type's maximum.

River drag interpolation is four-connected. Single edits and batches validate all changed coordinates and their cardinal neighbours against the hypothetical final state before mutation. Rejected UI stamps are skipped and reported; invalid persisted topology refuses to load. The normal paint tool cannot directly create a third exit because it never changes a segment into River Junction.

Connectivity is not serialized. A consumer derives the same connection flags from the saved tile types.

## Consequences

- Water meaning is explicit: Sea and Lake no longer share one active generic type.
- Each River, Large River, or River Junction remains one complete campaign-tile **classification** with one centre height, preserving [[ADR-0004 - Tile-Authoritative Campaign Surface|ADR-0004]]. This does not mean that water visually fills the cell; [[ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006]] renders connected ribbons over visible ground.
- River paths remain deterministic and visually connected without spline control points or hidden edge data.
- Undo, redo, cancellation, and loading require atomic multi-tile application so validation never observes an invalid intermediate arrangement.
- Beach and Cliff can currently be placed away from water; shoreline validation or generation is future work.
- Flow direction, river order, continuous physical width, discharge, navigability, bridges, hydrologically directed confluences/distributaries, automatic delta generation, and sea/lake hydrology remain outside this decision. The two stored size classes are campaign-scale semantic categories, not metre widths; River Junction stores topology intent only.

Automatic direction-aware 10% water edges for every non-water terrain and procedural material rendering are defined by [[ADR-0006 - Procedural Materials and Directional Coasts|ADR-0006]]. Beach and Cliff remain explicit full-tile classifications.

The accepted but not product-integrated [[ADR-0007 - Layered Campaign Tile Taxonomy v3|ADR-0007]] moves River to a directed overlay. Its current Phase 1 model represents confluences with one outflow; preserving version-2 River Split distributaries requires a future directed multi-outflow extension before migration can be canonical.
