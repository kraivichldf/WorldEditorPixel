# ADR-0023: Hierarchical Continental World Generation

- Status: Accepted
- Date: 2026-08-16

## Context

The `Continent` preset was one domain-warped ellipse with smaller noise added to its boundary. It could produce a valid editable landmass, but it could not read as a world map: the output had one similarly rounded body, no hierarchy between continents, no broad ocean basins between several major masses, and an artificial all-water frame.

The desired reference is structural rather than literal. A generated result should contain several unequal continental masses, large connected ocean basins, regional bays and peninsulas, small offshore arcs, and seed-dependent geography. It must not trace Earth or persist a second geographic authority. The accepted campaign tile, deterministic preview, physical-scale noise, drainage, and editable-result contracts remain unchanged.

## Decision

Keep the serialized/API enum value `CampaignMapGenerationPreset.Continent` for compatibility, but present it as **Continental world**. Replace only that preset's old single-ellipse mask with a deterministic hierarchical profile:

1. Choose one of three macro layouts, mirror it independently on each axis, apply a small global offset, and jitter five stable anchor regions from the signed seed.
2. Assign the anchors one dominant, one large, one medium, one small, and one microcontinental scale. The scale sequence is `1.50, 1.28, 1.08, 0.84, 0.66`, rotated by the seed so no map position always owns the largest mass.
3. Build each mass as the union of six precomputed oriented ellipses: a cratonic core, two axial shoulders, two asymmetric regional branches, and one narrower bent peninsula. Subtract two edge-crossing oriented ellipses to form unequal ocean-connected embayments.
4. Place two three-island arcs at deterministic high-clearance ocean locations. These are minor features and do not replace the major continental hierarchy.
5. Bend the profile with two physical-kilometre simplex warp fields, then add separate regional, coastline, and detail fields. Their wavelengths are fractions of the shorter world dimension with fixed lower bounds, so changing campaign tile size does not change macro geography.
6. Keep only sparse ocean anchors fixed: four corners, the north/south midpoint, and one seeded side opening. Every other boundary cell is generated. A continent may therefore leave the map as cropped geography instead of being surrounded by a mandatory one-tile Sea frame.
7. Run the established two majority passes, component cleanup, boundary-seeded ocean flood fill, elevation, erosion, hydrology, terrain classification, and exact preview-first acceptance afterward.

For world aspect ratio `a = width / height`, normalized horizontal lobe radii use:

```text
horizontalScale = clamp(1.20 / a, 0.76, 0.92)
```

For physical tile-centre coordinates `pKm` and shorter dimension `Dkm`, the signed land score after analytic lobe/bay evaluation is perturbed by:

```text
warpX = 0.065 * simplexFBm(pKm, max(40, 0.62 * Dkm), 3)
warpY = 0.065 * simplexFBm(pKm, max(40, 0.62 * Dkm), 3)

S = hierarchicalProfile(p + warp)
  + 0.055 * simplexFBm(pKm, max(28, 0.34 * Dkm), 3)
  + 0.120 * simplexFBm(pKm, max(16, 0.15 * Dkm), 4)
  + 0.030 * simplexFBm(pKm, max(8,  0.055 * Dkm), 3)
  - 0.025
```

`S > 0` is land before cleanup. Lobe sine/cosine values are precomputed once per generation, and the per-tile evaluator uses allocation-free loops.

## Consequences

- **Continental world** now produces several recognizably unequal landmasses rather than one ellipse or equal cookie islands.
- Broad connected ocean basins, bays, peninsulas, cropped edge geography, and small island arcs are part of the preset's measurable contract.
- The seed changes the layout family, mirrors, size ownership, branches, bays, coast fields, and ocean opening while equal inputs remain exact.
- A `2:1` world best matches an equirectangular world-map composition; square worlds retain the same hierarchy but naturally allocate more vertical space to the masses.
- This remains a planar campaign map. Land on the left and right edges is visually cropped but those edges do not wrap or become adjacent. True spherical/wrapping topology would require a separate world-definition and neighbour contract.
- The profile is fictional and does not reproduce the outline of Earth or any supplied reference map.
- The generated result remains ordinary editable version-2 campaign tiles. No continent IDs, lobe geometry, seed, or mask are persisted after acceptance.

The exact current formulas are recorded in [[../Reference/Campaign World Generation|Campaign World Generation]]. The preview and tile-authority boundary remains [[ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]].
