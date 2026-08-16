# ADR-0014: Open Directional Coast Boundaries

- Status: Accepted
- Date: 2026-08-14

## Context

East, West, North, and South Coast presets previously forced every tile on the named edge to Sea and every tile on the opposite edge to land. Reapplying that opposite `forcedLand` line after smoothing guaranteed topology, but it also exposed the rectangular map crop: even when a regional gulf approached a corner, the mainland-side border remained an unbroken straight land wall.

Removing the forced-land assignments alone did not visibly solve the problem. The continental shelf formula still placed its land threshold inside the map at both ends of the along-coast axis for every probed seed. The opposite boundary was technically unforced but remained functionally fixed.

A directional preset should mean that the principal ocean lies on the named side. It should not claim that the map is the complete extent of a continent or that every other boundary is land.

## Decision

Directional Coast forced masks retain only the full named Sea edge. They no longer write any opposite-edge land cells. The named water edge remains the deterministic ocean seed, and final ocean resolution still requires every Sea cell to connect to it.

The continental shelf gains one optional seeded open-boundary retreat. For 30% of seeds the retreat is absent. Other seeds choose the positive or negative along-coast boundary, a centre just inside or outside that boundary, a physical-scale span, and a strength. A broad Gaussian retreat subtracts from the local coast position:

```text
openness = hash01(seed)
retreat = 0                                      when openness < 0.30

strength = smoothstep(0.30, 1, openness)
side = seeded choice of -1 or +1
center = side * lerp(0.98, 1.08, hash01(seed))
sigmaMax = min(0.48, 260 / coastLengthKm)
sigmaMin = min(0.18, sigmaMax)
sigma = clamp(2 * lerp(0.12, 0.20, hash01(seed)),
              sigmaMin,
              sigmaMax)
amplitude = lerp(1.24, 1.84, strength)

Copen(u) = C(u) - amplitude * exp(-0.5 * ((u-center)/sigma)^2)
```

When strong enough, `Copen` moves the shelf beyond the mainland-side corner, allowing connected Sea to occupy part of the opposite edge and the selected top/bottom-equivalent boundary. The Gaussian decays inland along the coast, so the mainland re-enters the map as a broad curved boundary rather than a rectangular cut.

The same retreat is applied to Smooth, Flowing, Natural, and Rugged shelf evaluation before their shared regional skeleton. West, North, and South Coast rotate or reflect the construction through the existing canonical coordinates.

## Consequences

- Only the named Sea edge is a hard directional boundary guarantee.
- The other three boundaries may contain land, connected Sea, or both according to seed and geography.
- Some seeds retain a full mainland-side land edge; others let the coast enter or leave through a corner or along-coast boundary. Neither state is mandatory.
- The map can read as a crop from a larger region instead of a complete continent fitted to a rectangle.
- Exact inputs remain deterministic, and accepted/saved worlds are unchanged until explicitly regenerated.
- Tests flood-fill the mainland from whatever land cells naturally remain on the mainland-side boundary; they no longer rely on an artificial full forced-land line.
- The construction does not model a real continental outline or choose map bounds from generated geography. It only removes the false opposite-edge promise within the existing fixed world dimensions.

Exact formulas are documented in [[../Reference/Campaign World Generation|Campaign World Generation]].

[[ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024]] makes the span clamp valid at the `10,000 km` maximum. Once `sigmaMax` falls below the compact normalized minimum, `sigmaMin` follows it rather than asking `clamp` to use an inverted interval.
