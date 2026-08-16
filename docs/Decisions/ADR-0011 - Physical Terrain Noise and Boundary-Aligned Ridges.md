# ADR-0011: Physical Terrain Noise and Boundary-Aligned Ridges

- Status: Accepted
- Date: 2026-08-13

## Context

[[ADR-0010 - Tectonic Erosion and Hierarchical Drainage|ADR-0010]] introduced transient crustal provinces and erosion, but the supporting macro, detail, and ridge fields still sampled interpolated value noise in normalized map coordinates. The same frequency therefore represented a different number of kilometres when world dimensions changed. Its isotropic ridged field could also produce rounded patches that did not follow the strike of a convergent or shear boundary.

The editor needs more geographically legible generated relief without adding a persistent geology layer, changing the tile/save contract, weakening deterministic previews, or turning Mountain density into arbitrary paint coverage.

## Decision

Geological fields use deterministic two-dimensional simplex gradient noise sampled from tile-centre positions in physical kilometres. Every field declares a characteristic wavelength in kilometres, with bounds derived from tile size and the shorter world dimension. Seeded coordinate offsets prevent a shared lattice-origin artifact; normalized octave weights keep ordinary fractal output in `[-1, 1]` and ridged output in `[0, 1]`.

The tectonic model canonicalizes each nearest-province pair by stable province ID before deriving its boundary normal and tangent. This makes boundary orientation continuous on both sides of the same Voronoi edge. A restrained long-wave domain warp bends the ridge field. Sampling along the canonical tangent while multiplying the cross-boundary coordinate by `3.4` creates longer correlation along strike and shorter correlation across strike. The generator blends that aligned ridge with a regional physical-kilometre ridge according to boundary, convergent, and shear strength.

Physical simplex fields now drive province warp, boundary texture, boundary-aligned ridges, seabed relief, continental macro relief, land detail, regional ridges, and regional orogeny. Existing analytic coastline masks, coast-character formulas, aridity, moisture, and visual material texture keep their current deterministic fields because they have separate established contracts and do not define geological relief.

Mountain suitability uses one stable geological threshold. Sparse, Balanced, and Dense control only the retained coverage and number of separated ridge systems. Systems grow independently from endpoints so one system cannot consume or obstruct another system's seed. This keeps density monotonic without changing the underlying geology.

Drainage candidate search retains lower-accumulation tributary scales. Extending the upstream head of an accepted River no longer consumes a complete River-system slot; a merged candidate consumes that slot only when it creates an actual three-exit junction. This preserves the hierarchical drainage guarantee when a physical-noise change moves channel heads.

## Consequences

- A wavelength has approximately the same campaign-scale meaning on different valid grid resolutions and aspect ratios.
- Major ridges are statistically smoother along plate-boundary strike than across it, reducing circular Mountain/Hill stamps.
- Small detail cannot silently become continental structure merely because world dimensions change.
- Equal definition, settings, and seed still produce identical ordered tile data.
- Coast topology, automatic shore material, editor authority, version-2 save data, and `.kworld` runtime data do not change.
- The generator remains a bounded game-oriented approximation, not a scientific plate, erosion, or climate simulator.
- Geological output changes for existing seeds because the former normalized value-noise relief is intentionally replaced. Generated tiles already accepted into a document remain unchanged.

Exact wavelengths and blend formulas are documented in [[../Reference/Campaign World Generation|Campaign World Generation]].
