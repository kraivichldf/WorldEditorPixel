# ADR-0024: Scale-Hierarchical Directional Coasts

- Status: Accepted
- Date: 2026-08-16

## Context

Directional Coast generation was tuned around the `700 × 700 km`, `5 km` reference world. At the valid maximum `10,000 × 10,000 km` definition with `20 km` campaign tiles, three faults became visible:

1. the open-boundary span clamp could receive a minimum greater than its maximum and throw before preview completed;
2. one regional bay-and-cape construction became an obvious geometric stamp when enlarged, while fixed feature caps left most of the 10,000 km shoreline under-described;
3. short-wavelength shelf noise repeated along the coast without a distinct continental, regional, and nearshore hierarchy.

Adding more tile-scale noise would only make the edge busy. The desired result is a fictional but map-like crop: broad shelf advance and retreat, asymmetric regional bays and headlands, irregular meso-scale shoreline, and sparse offshore island arcs. It must remain deterministic, bounded, rotatable to every directional preset, composed from complete campaign tiles, and editable immediately after preview acceptance.

## Decision

Keep the canonical directional coordinate system and named Sea-edge guarantee, but make Coast detail explicitly scale-hierarchical.

For coastline length `Lkm`, define:

```text
g = smoothstep(1400, 4200, Lkm)
macroScale = max(0.35, Lkm / 2800)

Cmacro(u) = 1.40 * Ab * g
            * fBm((2.71, u * macroScale); frequency 0.58, 3 octaves)

AnEffective = An * lerp(1.00, 1.55, g)
```

`Cmacro` is added to the existing broad/detail shelf curve for Smooth, Natural, and Rugged. Flowing Capes uses the same field with fixed amplitude `0.16`. The extra two-dimensional nearshore amplitude uses `AnEffective`, so continental worlds gain irregular coves and headlands without contaminating deep mainland or open ocean.

The explicit regional Bézier skeleton remains authoritative for compact maps and for the deliberately stylized **Flowing Capes** character. For Smooth, Natural, and Rugged its visibility is:

```text
regionalVisibility = 1 - smoothstep(1400, 4200, min(widthKm, heightKm))
```

The standard skeleton therefore fades out before continental scale instead of becoming a repeated round-bay/hooked-cape symbol. During its transition, the primary gulf remains full size, the secondary inlet becomes shallower and narrower, the cape bends away from the primary gulf, and large radii receive kilometre-scale boundary perturbation. Flowing Capes retains its continuous shelf plus one protected-root regional cape at every supported size because that smooth authored silhouette is the purpose of the character.

Natural and Rugged supporting geography scales by coastline length:

```text
dynamicMaximum = clamp(ceil(Lkm / 520), compactMaximum, hardMaximum)

hardMaximum landmarks / bays / peninsulas / islandGroups = 18 / 26 / 22 / 14
```

Up to four evenly distributed landmarks become macro landmarks. Their size multiplier blends toward `clamp(sqrt(Lkm / 700), 1, 4)`. Up to three evenly distributed offshore groups become island arcs; their distance/spread multiplier blends toward `clamp(0.75sqrt(Lkm / 700), 1, 3)`, with additional independently jittered islands. Large water and land primitives blend from the compact two-octave boundary perturbation to rougher three-octave boundaries. Other features retain compact physical sizes, so a 10,000 km coast contains several scales rather than twenty identical giant shapes.

The open-boundary span is made valid for every supported coastline length:

```text
sigmaMax = min(0.48, 260 / Lkm)
sigmaMin = min(0.18, sigmaMax)
sigma = clamp(rawSigma, sigmaMin, sigmaMax)
```

At very large `Lkm`, `sigmaMin == sigmaMax`; the retreat remains a bounded physical edge opening rather than throwing because a normalized compact-world minimum exceeded the maximum.

## Consequences

- The exact `10,000 × 10,000 km`, `20 km` case completes as a `500 × 500 = 250,000`-tile candidate.
- Natural and Rugged large worlds use stochastic shelf, landmark, and island hierarchy instead of the compact paired-bay/curved-cape stamp. Flowing Capes remains intentionally smoother and more constructed.
- Seeded geography remains fictional and planar. The method does not trace a supplied map, reconstruct plate history, or make left/right edges adjacent.
- A `20 km` campaign tile is still the minimum shoreline step. The generator can form broad bays, capes, sounds, and island chains, but it cannot honestly draw a cove narrower than one complete `20 × 20 km` tile.
- Equal definition, options, and seed still produce exact ordered tile data. Accepted output remains ordinary editable version-2 campaign tiles; no coast curve or generation metadata is persisted.
- Existing compact-world output and its tidal-inlet opportunities remain unchanged where the large-world blend is zero.

Exact formulas and measurable tests are recorded in [[../Reference/Campaign World Generation|Campaign World Generation]] and [[../Testing/Verification|Verification]]. This decision refines [[ADR-0012 - Regional Geographic Coast Skeletons|ADR-0012]] and corrects the large-world span boundary in [[ADR-0014 - Open Directional Coast Boundaries|ADR-0014]].
