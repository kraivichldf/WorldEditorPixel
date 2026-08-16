# Campaign World Generation

## Contract

Generation creates a starting arrangement of normal campaign tiles. After creation there is no distinction between a generated tile and a painted tile. The saved project contains `world.json`, `campaign-tiles.json`, and an optional reusable `custom-terrain.json` catalog; the generator never writes a height raster or an additional authority layer.

The desktop New World flow may hold one generated result as a transient read-only preview. Adjusting any input makes that result stale and prevents acceptance until regeneration. **Use this world** transfers the exact preview object to the document; it does not invoke the formulas a second time. The bitmap itself is never serialized and is not part of determinism, history, or terrain authority.

Inputs are:

```text
validated CampaignWorldDefinition
preset
signed Int32 seed
terrain style: Gentle | Balanced | Rugged
mountain density: Sparse | Balanced | Dense
hydrology: None | Light | Balanced | Abundant
directional coastline style: Smooth | Flowing capes | Natural | Rugged
tidal inlets: None | Few | Balanced | Drowned coast
optional inland tile mix: Plains + Forest + Desert + Hills + Mountain + Steppe + positive custom shares = 100%
optional custom land definitions: stable ID + safe base + color + 0..100% independent inland share
```

The default world shape remains `Blank`; the default directional coastline style is `Natural`. Non-directional presets retain that input for reproducibility but do not use it. `Land only` intentionally suppresses Sea, Lake, River, Large River, water-facing Cliff, automatic coast edges, and tidal inlets regardless of water settings so its name is exact. `None` tidal inlets preserves the selected base coastline byte-for-byte. Custom ratios are opt-in. Custom terrain definitions are safe land variants only, so adding a paint-only definition leaves normal generation byte-for-byte compatible.

## Coordinates and deterministic noise

For tile `(x, y)` in a `W × H` grid, normalized centre coordinates are:

```text
nx = 2 * (x + 0.5) / W - 1
ny = 2 * (y + 0.5) / H - 1
```

Generation uses two deterministic noise families. Established coastline masks, aridity, and moisture retain seeded two-dimensional value noise with quintic interpolation in normalized coordinates:

```text
fade(t) = 6t^5 - 15t^4 + 10t^3
```

The hash at every integer lattice point depends only on `(seed, ix, iy)`. Fractal Brownian motion is normalized so octave count does not change its nominal `[-1, 1]` range:

```text
fBm(p; f, o) = sum(i=0..o-1, 0.5^i * N(p * f * 2^i, seed_i))
               / sum(i=0..o-1, 0.5^i)
```

For the Island, Archipelago, and Sea-in-center analytic masks, two independent three-octave fields warp the coordinates:

```text
p' = p + 0.10 * (fBm_x(p; 1.7, 3), fBm_y(p; 1.7, 3))
```

This produces broad coherent bends rather than isolated pixel noise. A lower-frequency five-octave coast field and a weaker higher-frequency three-octave detail field perturb those analytic masks. Continental world and directional Coast use their separate physically scaled constructions below.

Geological warp, macro, detail, seabed, orogeny, and ridge fields instead use two-dimensional simplex gradient noise at physical tile-centre coordinates `pKm`. Each octave declares a characteristic wavelength `lambdaKm`:

```text
q_i = pKm / lambdaKm * lacunarity^i + seededOffset_i
simplexCorner = max(0, 0.5 - dot(delta, delta))^4 * dot(gradientHash, delta)

simplexFBm = sum(persistence^i * simplex(q_i, seed_i))
             / sum(persistence^i)

ridgedFBm = sum(persistence^i * (1 - abs(simplex(q_i, seed_i)))^2)
            / sum(persistence^i)
```

Ordinary output clamps to `[-1, 1]`; ridged output clamps to `[0, 1]`. Current geological fields use lacunarity `2` and persistence `0.5`, except aligned ridge detail uses persistence `0.55` and regional ridges use `0.52`. Fixed seeded coordinate offsets prevent every seed sharing a zero-valued lattice origin. Because `lambdaKm` is a physical distance rather than a fraction of map width, changing grid resolution does not silently turn local texture into continental structure. All seed offsets are fixed constants.

## Preset land masks

`S > 0` means land before cleanup. For an ellipse:

```text
dellipse(p, c, rx, ry) = sqrt(((px-cx)/rx)^2 + ((py-cy)/ry)^2)
```

The presets use these base fields before coast noise:

| Preset | Base field and hard constraint |
|---|---|
| Blank | No tiles are generated. |
| Continental world (`Continent`) | Hierarchical union of five unequal multi-lobe masses plus two three-island arcs; sparse ocean anchors replace an all-Sea boundary. |
| Island | `0.61 - dellipse(p', c, 0.92, 0.84)`; the outside boundary is Sea. |
| Archipelago | Maximum signed score from one centre island and seven seeded ring islands; the outside boundary is Sea. |
| East coast | Canonical directional coast field; east edge is forced Sea while west/north/south follow the generated field. |
| West coast | Canonical directional coast field; west edge is forced Sea while east/north/south follow the generated field. |
| North coast | Canonical directional coast field; north edge is forced Sea while south/east/west follow the generated field. |
| South coast | Canonical directional coast field; south edge is forced Sea while north/east/west follow the generated field. |
| Sea in center | `dellipse(p', c, 0.48, 0.38) - 1`; centre is Sea and every map edge is land. |
| Land only | Every tile is forced to land. |

Island centres and archipelago ellipses receive small deterministic seed offsets.

### Continental world construction

The public/session enum remains `Continent`, while the editor names this preset **Continental world**. It is not the old single noisy ellipse. The seed selects one of three five-anchor macro layouts, mirrors it on each axis, applies a small global translation and per-anchor jitter, then rotates the size roles `1.50`, `1.28`, `1.08`, `0.84`, and `0.66` across those anchors. For aspect ratio `a = worldWidth / worldHeight`:

```text
horizontalRadiusScale = clamp(1.20 / a, 0.76, 0.92)
```

Each continental mass is the maximum signed score from six oriented ellipses: a core, two axial shoulders, two asymmetric branches, and a narrower bent peninsula. Two edge-crossing ellipse fields are subtracted from that mass before it joins the other masses, producing unequal connected embayments rather than closed inland holes. Two minor arcs of three islands are placed at deterministic ocean positions with maximum clearance from the continental cores.

Let `Dkm` be the shorter world dimension and `pKm` the physical tile-centre position. Two independent `0.065`-amplitude simplex fields with wavelength `max(40, 0.62 Dkm)` warp the normalized evaluation point. The resulting hierarchical signed-distance score receives:

```text
+ 0.055 * simplexFBm(pKm, max(28, 0.34 Dkm), 3)
+ 0.120 * simplexFBm(pKm, max(16, 0.15 Dkm), 4)
+ 0.030 * simplexFBm(pKm, max(8, 0.055 Dkm), 3)
- 0.025
```

The first field changes regional outline, the second shapes the coastline, and the third supplies restrained detail. All wavelengths are physical kilometres; the campaign tile size changes sampling resolution, not the intended geographic scale.

Continental world fixes only seven ocean anchors: four corners, the north/south midpoint, and one seeded side opening. Other boundary cells use the generated field, so a large mass may leave the map as cropped geography instead of acquiring an artificial one-tile Sea frame. Ocean resolution still flood-fills from every boundary water cell and fills enclosed mask holes as land. Left/right boundaries are not topologically wrapped.

### Directional coastline construction

All four directional presets are rotated or reflected into one canonical coordinate system: `v` points from mainland toward the named Sea edge and `u` runs along the coast. East uses `(v, u) = (nx, ny)`, West `(-nx, ny)`, North `(-ny, nx)`, and South `(ny, nx)`. The initial coast position is:

```text
scaleAlong = max(0.35, coastLengthKm / 700)
largeBlend = smoothstep(1400, 4200, coastLengthKm)
macroScale = max(0.35, coastLengthKm / 2800)
Bseed = lerp(-0.45, 0.15, hash01(seed))

Cbase(u) = 0.52 + Bseed
     + Ab * fBm((0.37, 1.55 * u * scaleAlong); 4 octaves)
     + Ad * fBm((1.83, 4.20 * u * scaleAlong); 3 octaves)
     + 1.40 * Ab * largeBlend
       * fBm((2.71, 0.58 * u * macroScale); 3 octaves)

S0(v, u) = Copen(u) - v
          + An * lerp(1.00, 1.55, largeBlend)
            * nearshore(S0)
            * fBm((v * scaleAcross, u * scaleAlong); 2.2, 3)

nearshore(s) = 1 - smoothstep(0.04, 0.34, abs(s))
```

`Bseed` is a uniform deterministic continental advance/retreat shared by all directional Coast characters for that seed. Its `0.60` normalized range changes the broad pre-feature land share by about 30 percentage points; regional geography, landmarks, smoothing, islands, and tidal inlets then affect the exact final percentage. Only the named Sea edge is fixed. Below `1,400 km`, the added macro term and amplification are exactly zero. Between `1,400` and `4,200 km` they blend in; at the `10,000 km` maximum the macro field supplies a separate continental wavelength while the stronger two-dimensional nearshore field breaks that shelf into regional coves and headlands. Distance falloff still prevents fine detail from contaminating deep mainland or open ocean. Flowing Capes uses the same macro field with fixed amplitude `0.16` rather than `1.40Ab`.

#### Open map boundaries

The directional preset describes the principal ocean orientation, not a complete continent fitted inside a rectangle. The mainland-side edge is therefore not forced land. A seed may also carry the shelf entirely outside one positive or negative along-coast boundary:

```text
o = hash01(seed)
Ropen(u) = 0                                                when o < 0.30

strength = smoothstep(0.30, 1, o)
side = seeded {-1, +1}
center = side * lerp(0.98, 1.08, hash01(seed))
sigmaMax = min(0.48, 260 / coastLengthKm)
sigmaMin = min(0.18, sigmaMax)
sigma = clamp(2 * lerp(0.12, 0.20, hash01(seed)),
              sigmaMin,
              sigmaMax)
amplitude = lerp(1.24, 1.84, strength)

Ropen(u) = amplitude * exp(-0.5 * ((u-center)/sigma)^2)
Copen(u) = Cbase(u) - Ropen(u)
```

The broad Gaussian can move the shelf past the mainland-side corner near the chosen boundary, allowing external Sea to enter the map there. It decays toward the interior of the along-coast axis, so the mainland returns as a curved geographic boundary. `sigmaMin = min(0.18, sigmaMax)` matters at continental scale: it prevents the compact-world normalized minimum exceeding the physical upper span. Seeds below the openness threshold retain `Copen = Cbase`; a full land edge may occur naturally but is never enforced. The construction is disabled below `90 km` of coast length. Flowing applies the same `Ropen` subtraction to `Cflow`. West, North, and South rotate or reflect this result through the canonical coordinates.

#### Regional geographic skeleton

The base curve is only a continental shelf. Compact directional worlds also need a readable two-dimensional regional form rather than remaining the graph of one function `v = C(u)`. On worlds whose shorter dimension is at least `90 km`, the compact construction composes one bay-and-attached-peninsula skeleton in physical kilometres. Flowing Capes retains that skeleton at every supported size. Smooth, Natural, and Rugged instead fade it out as continental hierarchy becomes available:

```text
regionalVisibility = 1 - smoothstep(1400, 4200, min(widthKm, heightKm))
S = lerp(Ssupporting, Sregional, regionalVisibility)
```

At `4,200 km` and above, Natural and Rugged identity comes from macro shelf displacement, distributed landmark regions, supporting bays/headlands, nearshore breakup, and island arcs—not one enlarged geometric symbol.

Let `J = lerp(0.88, 1.12, hash01(seed))` and let style scale `q` be `0.76`, `1.00`, `1.04`, or `1.18` for Smooth, Flowing, Natural, or Rugged. The ordinary reference dimensions are:

```text
reachKm = clamp(238qJ, <= 0.42 * acrossKm)       // Flowing uses 225J
sweepKm = clamp(118qJ, <= 0.28 * alongKm)        // Flowing uses 145J
bayDepthKm = clamp(105qJ, <= 0.22 * acrossKm)
baySpanKm  = clamp(118qJ, <= 0.24 * alongKm)

rootRadiusKm = 66qJ                              // Flowing uses 58J
neckRadiusKm = 31qJ                              // Flowing uses 27J
bodyRadiusKm = 47qJ                              // Flowing uses 37J
tipRadiusKm  = 20qJ                              // Flowing uses 13J
```

Two slightly rotated ellipses are subtracted from the compact signed land field on opposite sides of the root. Their centers are separated by `0.78rootRadius + 0.56baySpan`; one uses `1.08` depth and `1.12` span while the other uses `0.92` depth and `0.88` span. During the large-world transition, one seeded primary gulf retains full depth and grows to `1.30` span, while the secondary inlet blends toward `0.18` depth and `0.42` span. The curve bends away from the primary gulf and its sweep blends toward `0.62` of the enlarged reference. This avoids a mirrored double-bay symbol before the standard skeleton reaches zero visibility.

The peninsula centreline is a cubic Bézier curve:

```text
B(t) = (1-t)^3 P0 + 3(1-t)^2t P1 + 3(1-t)t^2 P2 + t^3 P3

P0 = (protected inland root, rootAlong)
P1 = (coastAnchor + 0.14reach, rootAlong - 0.10direction*sweep)
P2 = (coastAnchor + 0.68reach, rootAlong + 0.40direction*sweep)
P3 = (coastAnchor + reach,     rootAlong + direction*sweep)
```

`P0` is moved inland far enough to cross the deepest bay subtraction plus five campaign tiles of safety. This is a topology guarantee, not a visual overlap: the peninsula remains cardinally connected to the mainland after whole-tile classification. For the closest point `t` on 28 bounded physical curve segments, the radius is:

```text
r(t) = smooth(root, neck, t / 0.24)                  when t <= 0.24
r(t) = smooth(neck, body, (t - 0.24) / 0.40)         when 0.24 < t <= 0.64
r(t) = smooth(body, tip, (t - 0.64) / 0.36)          otherwise

Sregional = max(Ssupporting, (r(t) + detailKm - distanceKm) / (0.5acrossKm))
```

`detailKm` is one restrained two-octave simplex sample per tile with wavelength `max(22 km, 1.3bodyRadius)` and amplitude capped at `6 km` on compact worlds. Its transition target uses three octaves, wavelength `max(32 km, 0.62bodyRadius)`, and amplitude `min(42 km, 0.20bodyRadius)`. Supporting landmarks, generic bays, and small peninsulas are evaluated first; the regional bay pair and peninsula follow so a later subtraction cannot sever its root. Offshore island groups are added last.

#### Flowing bays and capes

This profile supplies a dedicated continuous continental shelf rather than another density setting for the landmark system. After the directional preset is converted to canonical `(v,u)` coordinates, the seed may mirror the profile along the coast. Let:

```text
G(u; c, s) = exp(-0.5 * ((u - c) / s)^2)
b = 0.90 + 0.20 * hash01(seed)

CflowBase(u) = 0.27 + Bseed
         + 0.035 * fBm((0.41, u * scaleAlong); 1.10, 3)
         + 0.24b * G(u; -0.70, 0.21)   // rounded upper headland
         - 0.34b * G(u; -0.18, 0.27)   // broad deep bay
         + 0.13  * G(u;  0.31, 0.15)   // cape shoulder
         - 0.23  * G(u;  0.67, 0.17)   // lower cove/retreat

Cflow(u) = CflowBase(u) - Ropen(u)
Sflow(v,u) = Cflow(u) - v + nearshoreDetail
```

The Gaussian lobes have continuous derivatives and overlap across regional distances, producing a flowing S-shaped shelf instead of visible repeated ellipse bites. The shared regional skeleton then carves the opposing bay pair and unions its protected-root Bézier peninsula. Flowing uses a `225 km` reference reach, `145 km` sweep, `58 km` root, `27 km` neck, `37 km` body, and `13 km` tip. It remains one connected mainland and does not require offshore islands.

Two majority passes still resolve this field to complete campaign tiles. A `5 km` tile world therefore preserves the regional silhouette but exposes an editable whole-tile edge, not a hidden sub-tile vector coastline.

#### Landmark systems

Natural and Rugged coasts first place a small number of mutually distinct, seeded landmark systems. The starting landmark kind is seed-selected and later systems rotate through all four kinds instead of repeatedly choosing the same shape:

1. **Major gulf** — one deep, slightly rotated water ellipse cuts inland while smaller positive land ellipses reinforce capes on both jaws. This creates one readable embayment with a protected interior, not a circular bite.
2. **Hooked cape** — four overlapping land ellipses follow a curved centreline:

   ```text
   v(t) = C(u(t)) + reach * t
   u(t) = u0 + direction * curve * t^2
   t in {0.25, 0.50, 0.75, 1.00}
   ```

   Their taper and increasing physical rotation form a connected neck, projecting peninsula, and hooked end.
3. **Barrier sound** — a long shallow water ellipse cuts a coast-parallel sound; three spaced, elongated land ellipses sit seaward of it as barrier islands, retaining ocean entrances between them.
4. **Offshore strait** — a narrow water ellipse preserves a channel beside one large rotated offshore island, with two smaller satellites at opposite ends.

All ellipse distances are evaluated after converting coordinate deltas to physical kilometres, so rotation and aspect remain meaningful on non-square worlds. Water fields subtract a smooth signed influence. Land fields use `max(currentScore, landmarkScore)`, preventing several overlapping island/cape fields from inflating the whole coast. Smooth coast places no landmark systems; Natural targets `2.0` and Rugged `3.2` systems per `700 km`. Compact worlds remain bounded to six. Above `1,400 km`, the dynamic bound is `clamp(ceil(coastLengthKm / 520), 6, 18)`. Up to `clamp(round(coastLengthKm / 2500), 1, 4)` distributed systems become macro landmarks; their size blends toward `clamp(sqrt(coastLengthKm / 700), 1, 4)`. Larger landmark ellipses also blend from compact two-octave boundary perturbation to rougher three-octave edges, preventing a large gulf or island from revealing a perfect ellipse.

Smaller supporting coastal features are seeded ellipses measured in kilometres rather than tile counts. For ellipse distance `d`, a bay subtracts waterward influence and a peninsula adds landward influence:

```text
bay(v,u)       = 1.18 * rv * (1 - smoothstep(0, 1, d))^1.35
peninsula(v,u) = 1.14 * rv * (1 - smoothstep(0, 1, d))^1.25

S1 = S0 - sum(bay) + sum(peninsula)
```

Bay depth starts near `58 km`; peninsula reach starts near `48 km`. Seeded size jitter, the coastline profile scale, a minimum of three campaign tiles, and a bounded fraction of the physical world dimension keep features visible without letting one field consume the map. Along-coast spans are wider than their depth/reach, producing bays and headlands instead of circular holes or blobs.

Offshore groups place two to four irregular ellipses approximately `82–164 km` from the local shelf before profile scaling. Each island gets independent position, radius, aspect, and two-octave boundary perturbation. Its signed score combines through `max(S1, islandScore)`, so it creates real separated land rather than merely pushing the mainland coast outward. On coasts above `1,400 km`, up to `clamp(round(coastLengthKm / 3500), 1, 3)` distributed groups become island arcs. Distance and along-coast spread blend toward `clamp(0.75sqrt(coastLengthKm / 700), 1, 3)`; their count gains `round(1.5(scale - 1))` independently jittered islands and radius grows only by `sqrt(scale)`. The result is a long sparse chain rather than several identical giant islands.

The following table applies to the generic Smooth, Natural, and Rugged construction; Flowing bays and capes bypasses these independent feature counts and uses its dedicated profile above.

| Coast character | `Ab` | `Ad` | `An` | bays / 700 km | peninsulas / 700 km | island groups / 700 km | landmarks / 700 km | feature scale |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Smooth | 0.09 | 0.016 | 0.007 | 1.2 | 0.7 | 0 | 0 | 0.72 |
| Natural | 0.14 | 0.032 | 0.015 | 1.6 | 1.1 | 0.7 | 2.0 | 1.00 |
| Rugged | 0.18 | 0.046 | 0.026 | 2.6 | 2.0 | 1.5 | 3.2 | 1.22 |

Feature counts scale with physical coastline length, are deterministically distributed into jittered slots, and are bounded to prevent pathological work. Above `1,400 km`, the dynamic bounds are `clamp(ceil(coastLengthKm / 520), compactMaximum, hardMaximum)` with compact/hard maxima of `9/26` bays, `8/22` peninsulas, `5/14` island groups, and `6/18` landmarks. `Natural` is the New World default. Smooth → Natural → Rugged increases measured cardinal land/water boundary complexity at the stable `700 × 700 km`, `5 km`, seed `17,029` reference. Every style at that compact reference has a cardinally mainland-connected projection beyond 75% of map width with Sea on both along-coast flanks. At continental scale, Smooth/Natural/Rugged no longer guarantee that one constructed projection; Natural and Rugged instead retain measured broad relief, shoreline complexity, and separated offshore land. The forced mask guarantees only the named Sea edge; all other boundaries use the resolved generated field. The later majority and connected-component passes remove single-tile noise, while ocean resolution ensures that only water connected to the guaranteed ocean is classified as Sea.

Two 3 × 3 majority passes remove one-tile salt-and-pepper artifacts while hard preset constraints are reapplied after every pass. For a directional Coast that hard constraint is only its named Sea edge. Tiny disconnected land components are removed except for seeded archipelago islands. Water is flood-filled from the preset's guaranteed Sea seed: boundary water for external-ocean presets and the centre for `Sea in center`. Enclosed mask holes are filled as land; connected water reaching any unforced boundary remains part of the named ocean. Inland Lake selection is handled by drainage rather than misclassifying noise holes as ocean.

## Optional tidal inlets

`Few`, `Balanced`, and `Drowned coast` offer campaign-scale, Sea-connected drowned-valley opportunities after the initial ocean is resolved and before final elevation, drainage, and shore classification. They are not quotas. A seed and coast may accept fewer than the setting maximum, including zero. `None` does not enter this pass and remains byte-for-byte identical to the unmodified base coast. A full `Sea` tile is the only available water resolution in version 2, so at a `5 km` tile size an inlet represents a broad estuary or drowned valley, not a narrow constructed canal. A future narrow canal needs its own overlay/network rather than a row of full Sea tiles.

The pass considers an initial shoreline land cell with exactly one cardinal Sea neighbour as a possible mouth. Let `riseGrade` be only the positive height rise into its first inland neighbour:

```text
valleyOpening = 1 - clamp(riseGrade / 0.05, 0, 1)
mouthScore = 0.52 * (1 - elevationFactor)
           + 0.28 * estuaryNoise
           + 0.20 * valleyOpening
```

Mouths are ranked by this score. A considered opportunity marks a region within `1.5 * maximumReach`, even when its deterministic roll fails, so adjacent shoreline cells cannot repeatedly retry until the requested count is filled. At most `maximumCount` separated regions are considered. For minimum profile score `m`, profile chance `p`, and deterministic `roll`:

```text
strength = smoothstep(m, 0.92, mouthScore)
acceptChance = p * lerp(0.45, 1, strength)
accept when mouthScore >= m and roll <= acceptChance
```

For an accepted mouth opportunity, the target reach is seeded inside the permitted range instead of always favoring the farthest possible cell:

```text
desiredReach = lerp(minimumReach, maximumReach, 0.18 + 0.64 * hash01(seed, mouth))
reachFit = 1 - clamp(abs(progress - desiredReach) / reachRange, 0, 1)

targetScore = 0.38 * reachFit
            + 0.37 * (1 - elevationFactor)
            + 0.15 * targetNoise
            - 0.25 * (lateralOffset / maximumLateralOffset)
```

An A* route joins the mouth to that target through land. Elevation and grade remain dominant. A seeded curved corridor joins both endpoints, while restrained physical-kilometre simplex variation prevents equal-cost routes from becoming ruler-straight:

```text
desiredLateral(t) = targetLateral * t + direction * bendAmplitude * sin(pi * t)

stepCost = 1
         + 3.8 * elevationFactor
         + 2.4 * clamp(grade / 0.06, 0, 1)
         + (1.0 when the step is not inward)
         + 0.50 * lateralOffset / maximumReach
         + 0.70 * corridorDeviation / bendAmplitude
         + 0.40 * valleyVariation
```

The completed route must also pass a terrain suitability test:

```text
routeSuitability = 0.50 * (1 - averageElevation)
                 + 0.30 * (1 - clamp(averageGrade / 0.05, 0, 1))
                 + 0.20 * forwardStepFraction
```

For `shortestTiles = min(width, height)`, `densityScale = clamp(shortestTiles / 48, 1, 4)`, and `baseReach = clamp(shortestTiles / 8, 5, 28)`, the profiles are:

| Setting | Maximum opportunities | Minimum reach | Maximum reach | Chance `p` | Minimum mouth | Minimum route | Widened steps | Maximum widening elevation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Few | `max(1, (densityScale+1)/2)` | `max(3, baseReach/3)` | `max(5, baseReach/2)` | 0.34 | 0.66 | 0.70 | 0 | 0.24 |
| Balanced | `min(3, densityScale+1)` | `max(4, baseReach/3)` | `max(6, 3baseReach/4)` | 0.50 | 0.60 | 0.63 | 1 | 0.30 |
| Drowned coast | `min(5, densityScale+1)` | `max(5, baseReach/3)` | `min(shortestTiles/5, baseReach+4)` | 0.68 | 0.53 | 0.56 | 2 | 0.38 |

A lateral mouth-widening cell must additionally have grade at most `0.045`; incompatible high cells remain land. Routes that cannot meet the minimum reach, touch forced land, crowd an accepted inlet, or satisfy the route threshold are skipped without replacement. Every carved cell is then flood-filled from the existing ocean again, so no inland hole is mislabeled as Sea. The final Sea mask feeds normal height, Lakes, Rivers, steep Cliff classification, and automatic coast derivation; all stored results remain ordinary editable tiles.

## Tectonic provinces and elevation

The creation pipeline builds a transient Voronoi tectonic field in physical kilometres. It is not saved and never becomes another editable layer. Let world dimensions be `worldWidthKm × worldHeightKm`:

```text
provinceCount = clamp(round(sqrt(worldWidthKm * worldHeightKm) / 180) + 2, 4, 12)
boundaryWidthKm = clamp(0.055 * min(worldWidthKm, worldHeightKm),
                        max(1.5 * tileSizeKm, 12),
                        45)
```

Province centres use a seeded jittered grid. Each receives a deterministic velocity vector and elevation bias. Let `shortKm = min(worldWidthKm, worldHeightKm)` and `tileKm` be the campaign tile size. Province sampling coordinates use a three-octave physical-simplex warp:

```text
provinceWarpWavelength = max(6tileKm, min(260 km, 0.36shortKm))
provinceWarpAmplitude  = max(0.2tileKm, min(4tileKm, 0.028shortKm))
```

This bends the Voronoi borders into regional arcs without letting warp scale grow without limit. The nearest province pair is canonicalized by stable province ID before orientation is calculated, so the same edge uses the same direction from both sides. For squared distances `d1²`, `d2²`, centre separation `dc`, canonical boundary normal `n`, tangent `t = (-ny, nx)`, and canonical relative velocity `dv`:

```text
distanceToBoundary = abs(d2² - d1²) / (2 * dc)
B = exp(-(distanceToBoundary / boundaryWidthKm)²)

normalMotion = dot(dv, n) / 2
C = sqrt(clamp(normalMotion, 0, 1))
Dv = sqrt(clamp(-normalMotion, 0, 1))
Sh = sqrt(clamp(abs(dot(dv, t)) / 2, 0, 1))

T_uplift = clamp(B * boundaryTexture * (0.82C + 0.18Sh), 0, 1)
T_rift   = clamp(B * Dv, 0, 1)
T_shear  = clamp(B * Sh, 0, 1)
```

`boundaryTexture` is a bounded seeded physical-simplex multiplier in `[0.88, 1]` with wavelength `max(5tileKm, min(100 km, 0.11shortKm))`.

Boundary-aligned ridge coordinates use a separate restrained domain warp. Let:

```text
lambdaRange = max(10tileKm, min(150 km, 0.18shortKm))
w = 0.10lambdaRange * simplexFBm(pKm; 2.2lambdaRange, 2 octaves)
along  = dot(pKm + w, t)
across = dot(pKm + w, n)
Rboundary = ridgedFBm((along, 3.4across); lambdaRange, 3 octaves)

lambdaRegional = max(8tileKm, min(115 km, 0.14shortKm))
Rregional = ridgedFBm(pKm; lambdaRegional, 4 octaves)
Aactive = clamp(T_uplift + 0.45T_shear + 0.15B, 0, 1)
Rblend = smoothstep(0.08, 0.58, B) * (0.35 + 0.65Aactive)
R = lerp(Rregional, Rboundary, Rblend)
```

Multiplying only the cross-boundary coordinate makes its effective wavelength shorter across strike while preserving the longer along-strike wavelength. The broad and fine ordinary land fields use:

```text
macro wavelength  = max(12tileKm, min(320 km, 0.32shortKm))
detail wavelength = max(3tileKm,  min(55 km,  0.055shortKm))
```

Let `I = smoothstep(0, 0.78, max(S, 0))` be coast-to-interior strength, `M` the macro field mapped to `[0,1]`, `D` detail mapped to `[0,1]`, `g` terrain-style strength (`0.35`, `0.65`, or `0.95`), and `Pbias` the blended province elevation bias. Regional orogeny uses wavelength `max(16tileKm, min(420 km, 0.52shortKm))`:

```text
Oregional = 0.68 * provinceNoise + 0.32R
Oactive = clamp(T_uplift + 0.35T_shear, 0, 1)
Otectonic = clamp(0.06 + 0.82T_uplift + 0.18T_shear + 0.10B
                  + 0.18Rboundary * Oactive + 0.16max(0, Pbias), 0, 1)
O = clamp(0.18Oregional + 0.82Otectonic, 0, 1)

L = I^1.20 * (0.055 + 0.11M + 0.025gD + 0.035Pbias)
Q = smoothstep(0.20, 0.72, O)^1.20
U = I * Q * (0.12 + g(0.26 + 0.24R) + 0.28T_uplift)
F = I * T_rift * (0.025 + 0.040g)

E = clamp(0.015 + L + U + C_coast - F, 0.01, 0.96)
height = seaLevel + E * (maximumHeight - seaLevel)
```

`L` supplies ordinary continental lowland and province-scale bias. `U` concentrates major relief along convergent/shear belts while retaining a smaller seeded regional contribution. `F` lowers divergent rifts. `C_coast` is a separate ridged coastal contribution capped at `0.16g(1-I)`, so a subset of shores can rise as campaign-scale escarpments without forcing every coast flat or mountainous.

Sea depth still grows with negative land score and a low-frequency seabed field, then clamps inside the configured minimum/sea-level interval.

## Deterministic erosion

Erosion modifies only the transient generated height array and runs before final depression/Lake/River solving. Thermal relaxation processes each east/south land pair once per iteration and transfers equal material from the higher tile to the lower tile only above the selected talus grade:

```text
talusHeight = tileSizeMeters * talusGrade
thermalTransfer = max(abs(h1 - h2) - talusHeight, 0) * transferFraction
```

After the initial thermal passes, Priority-Flood supplies a receiver graph and flow accumulation. Let `Amax` be maximum accumulation, `availableHeight = maximumHeight - seaLevel`, and `grade` be the larger of raw and priority-filled downstream grade:

```text
Alog = log(1 + accumulation) / log(1 + Amax)
streamPower = Alog^0.52 * clamp(grade / 0.12, 0, 1)^0.85
erosionMeters = availableHeight * fluvialStrength * streamPower
```

The downstream land tile receives a bounded deposition fraction `0.08 + 0.12(1 - Alog)`. One final thermal pass uses 75% of the normal transfer fraction. Land remains at least one metre above Sea and no tile exceeds the configured maximum. The profiles are:

| Terrain style | Thermal passes | Talus grade | Transfer | Fluvial strength | Reported total passes |
|---|---:|---:|---:|---:|---:|
| Gentle | 2 | 0.075 | 0.16 | 0.006 | 3 |
| Balanced | 3 | 0.105 | 0.14 | 0.012 | 4 |
| Rugged | 3 | 0.145 | 0.11 | 0.018 | 4 |

The reported pass count includes thermal passes plus the fluvial pass. All final values are rounded once, away from zero, and clamped to the world's signed `Int16` height range.

## Depression filling and lakes

Hydrology uses four-neighbour movement because campaign Rivers must cross shared N/E/S/W tile edges. A priority flood starts from every Sea/Lake tile; when no water exists, boundary tiles act only as drainage outlets. For a cell `v` first reached from downstream cell `r(v)`:

```text
filled(v) = max(rawHeight(v), filled(r(v)))
depressionDepth(v) = filled(v) - rawHeight(v)
```

This constructs an acyclic receiver tree and removes artificial local pits for routing. Connected inland depression components are ranked by maximum depth, area, and distance from Sea. Components must exceed the configured minimum depth, remain below the per-map lake-area cap, and stay separated from existing water. Light, Balanced, and Abundant hydrology increase the accepted basin count and maximum area. Accepted Lake tiles use the basin spill elevation, then drainage is recomputed with those Lakes as sinks.

## Flow accumulation and rivers

Every land tile contributes one unit of catchment. Cells are processed from highest filled elevation toward their receiver:

```text
A(v) = 1 + sum(A(u)) for every upstream u where r(u) = v
```

A channel head begins where accumulation first crosses the hydrology threshold. The candidate search samples threshold scales `1.00`, `0.68`, `0.46`, `0.30`, and `0.20`, allowing lower-order tributaries to enter the same bounded ranking instead of retaining only trunk channels. Each route repeatedly follows `r(v)` until reaching Sea or Lake. Candidates are ranked by catchment, route length, and source-to-mouth relief. A route may remain separate or merge into the first accepted downstream route it reaches. It is accepted only when:

- it is at least the preset minimum length;
- it reaches water;
- its own new shape gives every normal/Large River tile at most two cardinal neighbours;
- a separate route neither intersects nor cardinally touches an accepted River route;
- a merging tributary has at least `max(3, minimumRouteLength / 2)` new cells before the merge;
- a merge creates at most three exits on the shared tile and introduces no other lateral River contact;
- adding it passes the canonical `CampaignTileMap` topology validator.

When the shared tile reaches exactly three exits it is stored as `RiverJunction`; its Large River flag is cleared because the version-2 enum stores one route class per tile. A candidate that only extends the one-neighbour head of an accepted route may add upstream length but does not consume a complete River-system target until a real three-exit confluence or separate water-reaching route is admitted. Four-exit crossings are always rejected. This creates hierarchical confluences only where two candidates share the same drainage path; independent basins may remain separate. River centre heights use the priority-filled profile, which is non-increasing toward the receiver; equal-height reaches are allowed because whole-metre storage cannot represent every sub-metre gradient on multi-kilometre tiles.

### Large downstream reaches

An accepted route remains ordinary River unless its physical source-to-mouth length reaches `100 km`. For a qualifying route, the generator searches for a Large River start only in the downstream part:

```text
minimum route length       = ceil(100 km / tileSize)
earliest large start       = ceil(0.60 * routeTileCount)
minimum large reach        = ceil(30 km / tileSize)
maximum large reach        = ceil(80 km / tileSize)
required accumulation      = 1.10 * channelHeadThreshold
```

The first downstream cell satisfying the accumulation requirement becomes Large River and every later route cell through the mouth retains that class. If the remaining path cannot preserve the minimum reach or no cell meets the accumulated-flow requirement, the full path stays ordinary River. This makes major reaches rare and downstream-focused rather than randomly widening isolated tiles. River, Large River, and River Junction remain one validated topology network; their preview widths are symbolic campaign categories, not literal kilometre widths.

## Terrain and shore classification

After Sea, Lake, and River are known, each remaining land tile separately computes maximum land-neighbour grade and maximum water-neighbour grade:

```text
landGrade  = max(abs(height - landNeighbourHeight)) / campaignTileSizeMeters
waterGrade = max(abs(height - waterNeighbourHeight)) / campaignTileSizeMeters
```

Classification order is:

1. Water-facing land is `Cliff` when `waterGrade` reaches `0.06`. On a 5 km tile this requires at least 300 m of centre-to-centre rise, so the label represents a coastal escarpment rather than a small local bluff. Gentler water-facing land retains its normal base/custom classification; coast is not stored.
2. Mountain selection is a map-level ridge-system pass, not an independent per-tile height threshold. It uses the tectonic-weighted orogeny field `O`, ridged relief `R`, and local crest prominence `K` measured above the mean valid cardinal land-neighbour height:

```text
K = clamp((height - meanCardinalLandHeight) / (0.04 * tileSizeMeters), 0, 1)
P = 0.38O + 0.25R + 0.17E + 0.14K
  + 0.06 * clamp(grade / 0.12, 0, 1)
```

   An inland, non-River, non-water-facing cell is a geological candidate when `E >= 0.35` or grade reaches `0.070`. An elevation-only candidate must also reach `O >= 0.52`. These suitability thresholds do not change with Mountain density; density controls how much of the same geology is retained. Every density uses the Dense profile's maximum system count when calculating seed separation, so increasing density does not relocate the first systems merely because their count changed. The highest-scoring separated candidates seed the selected number of systems. Each system grows against its own endpoint frontier before the next system begins, so a second seed cannot prematurely block the first ridge. Without an explicit land mix, Dense grows its first two systems to the same cumulative target used by Balanced, then adds the third system toward the Dense target. Growth may accept a cardinal candidate only when it touches exactly one selected tile and that selected tile is a ridge endpoint with fewer than two Mountain neighbours. This builds independent non-looping crest chains instead of exhausting the requested quota into compact painted blobs. The target remains an upper bound when the candidate field cannot extend another valid ridge segment.

| Mountain density | Target inland coverage | Systems |
|---|---:|---:|
| Sparse (default) | 1.8% | 1 |
| Balanced | 5.0% | 2 |
| Dense | 9.0% | 3 |

   Terrain style multiplies target coverage by `0.60`, `1.00`, or `1.20` for Gentle, Balanced, or Rugged terrain. A hard cap of 12% of eligible inland tiles prevents a map from becoming a cheap 50% Mountain fill. Both are limits, not a demand to paint unsuitable tiles.
3. Every suitable non-Mountain tile one cardinal step from a Mountain core becomes `Hills`; a suitable tile two steps away receives a weaker foothill preference. Other land becomes Hills at `landGrade >= 0.04`, or at elevation factor `E >= 0.24` only when grade reaches `0.02` or ridged relief reaches `0.52`. This prevents a broad smooth plateau from turning into a single flat-colored Hill stamp. Water depth does not turn an otherwise gentle coast into Hills.
4. Remaining lowlands can become `Desert` through a separate dry-region field. Only cells at least four cardinal tiles from Sea or Lake are eligible, so direct water-facing land remains a Plains/Steppe/Forest/Hills/custom material unless it is a steep Cliff.
5. Drier non-Desert lowlands at least two cardinal tiles from water can become `Steppe`, provided moisture remains below the Forest threshold. Other lowlands become Forest or Plains from deterministic moisture.

For aridity, let `A` be a seeded three-octave fBm field mapped to `[0, 1]`, `D = 1 - exp(-distanceToWater / 12)`, and `E` be normalized elevation. The lowland rule is:

```text
aridity = 0.54A + 0.34D + 0.12(1 - E)
Desert when distanceToWater >= 4 and aridity >= 0.68
Steppe when distanceToWater >= 2 and aridity >= 0.52 and moisture < 0.53
```

The low frequency keeps Desert and Steppe in coherent interior regions rather than a per-tile scatter. The stricter Desert threshold reserves true arid cores; the lower Steppe threshold forms a semi-arid transition, while distance terms keep immediate coastal grass out of both categories. This is a deterministic palette-classification heuristic, not a physical climate simulation. Moisture combines a seeded four-octave field, exponential distance-to-water decay, latitude moderation, and a high-elevation drying term. The classification changes material type only; centre elevation remains the authoritative generated value.

### Optional custom inland tile ratios

When **Adjust inland tile ratios** is enabled, the six percentages describe the eligible land pool `N`. Sea, Lake, River, and steep water-facing Cliff are excluded before `N` is counted. Gentle water-facing land remains eligible because its automatic 10% water edge is derived presentation, not a terrain category. The percentages are whole numbers, must total exactly 100%, and Mountain is limited to 12%. The editor's balanced starting mix is `40% Plains`, `25% Forest`, `8% Desert`, `13% Hills`, `2% Mountain`, and `12% Steppe`.

Integer target counts use largest-remainder apportionment rather than six independent rounding operations:

```text
base_i = floor(N * percent_i / 100)
remaining = N - sum(base_i)
```

The remaining cells are assigned one at a time to the types with the largest fractional remainder, with stable type order as the tie-break. This guarantees that the six integer targets add back to `N`.

The existing coherent Mountain-system pass receives the apportioned Mountain target instead of its profile coverage; the selected profile still controls whether one, a few, or several connected systems are seeded and which cells are geographically suitable. Desert then reserves the highest-aridity cells satisfying `distanceToWater >= 4`, `grade < 0.04`, and `E < 0.24`. Steppe takes the next highest-aridity unassigned cells satisfying `distanceToWater >= 2`, `grade < 0.04`, and `E < 0.34`. From the remaining cells, Hills takes the highest relief score and Forest takes the highest moisture score:

```text
mountainProximity = 1.00 at one tile, 0.55 at two tiles, else 0
hillScore = 0.55 * mountainProximity
          + 0.20 * clamp(grade / 0.12, 0, 1)
          + 0.15E + 0.10R
moisture = 0.50 * moistureNoise
         + 0.30 * exp(-distanceToWater / 10)
         + 0.20 * (1 - abs(ny))
         - 0.18E
```

On a no-water Land Only map, the water-influence term is exactly zero. Stable row-major tile index breaks equal-score ties. With an explicit mix, its Hill target reserves available Mountain-adjacent foothills before custom terrain and the geographically constrained Desert and Steppe passes; any remaining Hill target then takes the next relief-ranked unassigned land. Exact category targets remain unchanged. Plains is the final remainder.

Mountain, Desert, or Steppe may produce fewer cells than requested when the suitable candidate set is too small. The generator never creates a shoreline Desert/Steppe or labels unsuitable cells Mountain merely to force a number; every unmet constrained share remains Plains. Thus the control is a deterministic target, not a promise that can override geography.

Normal generated shores keep their selected base/custom land type. Rendering derives matching Sea/Lake over the outer 10% of each water-facing edge, with no automatic sand. `Cliff` is reserved for a genuinely steep campaign-scale transition. Explicit full-tile Beach remains available for manual painting and receives the same 10% water edge.

### Optional custom land types

A custom type has a stable lower-case ID, a designer name and `#RRGGBB` color, one safe base (`Plains`, `Steppe`, `Desert`, `Forest`, `Hills`, or `Mountain`), and a whole-number share from `0` through `100`. At most twelve definitions may exist and their combined positive shares may not exceed `100%`. A zero share is paint-only. The base remains the serialized fallback and material foundation; it never limits the custom category to a matching normal-terrain population.

For a generated map with any positive custom share, the six default percentages and all positive custom shares must total exactly `100%` of the eligible land pool. A single stable largest-remainder allocation gives every category an exact integer target. Regular Mountain targets are selected first from connected orogeny candidates; custom categories are then ranked over the remaining eligible land with their own seeded low-frequency fBm fields, followed by normal Desert, Steppe, Hills, and Forest selection. This keeps a custom type’s share independent from its base while preserving water, steep Cliff, River, and height authority.

Custom terrain is part of the land allocation, not a final identity pass over matching base tiles. Sea, Lake, River, and steep water-facing Cliff are excluded before mix allocation. Regular Mountain targets are reserved first, then custom categories choose from the remaining eligible tiles, including gentle water-facing land. Custom terrain therefore cannot create water, alter coastline geometry, touch River topology, or replace a steep Cliff, but it can retain its identity/color inside an automatic coast.

For an eligible land pool of `N` cells, let `p_i` cover the six default categories followed by positive custom definitions. The values must sum to `100`. The generator computes exact integer targets using one largest-remainder allocation:

```text
base_i = floor(N * p_i / 100)
remaining = N - sum(base_i)
```

The remaining target cells go to the largest fractional remainders, breaking exact ties by default category order (`Plains`, `Forest`, `Desert`, `Hills`, `Mountain`, `Steppe`) and then stable custom ID. Each custom definition ranks its still-unassigned eligible land candidates with two seed-stable fields derived from the world seed and a deterministic character hash of its ID:

```text
macro  = (fBm(p; 1.05, 3, seed + idSeed) + 1) / 2
detail = (fBm(p; 2.80, 2, seed + 31 * idSeed + 7127) + 1) / 2
score  = 0.78 * macro + 0.22 * detail
```

The highest-ranked cells receive the custom ID and its safe base as the stored portable `Type`; row-major index breaks equal scores. The low-frequency macro field makes each generated custom type read as a coherent terrain region instead of cheap per-tile scatter. Centre height stays untouched.

## Editability and persistence

The ordered generation result is applied once with `CampaignTileMap.SetTiles`. The same topology and height validation used by painting and file loading therefore validates the complete generated map. The editor then clears history, marks the new world unsaved, and enables the normal tile stamp immediately.

Preset, seed, terrain style, mountain-system profile, hydrology amount, tidal-inlet amount, and optional inland ratios are not serialized as generation history. The generated tile values are the data. Custom terrain definitions and their shares are serialized because they remain available to paint and reuse, but generation-versus-manual provenance is not. To reproduce a starting world, record the preset, seed, terrain style, mountain-system profile, hydrology amount, tidal-inlet amount, optional inland ratios, custom definitions, and world definition before editing.

See [[../Decisions/ADR-0008 - Deterministic Editable Campaign World Generation|ADR-0008]], [[../Decisions/ADR-0010 - Tectonic Erosion and Hierarchical Drainage|ADR-0010]], [[../Decisions/ADR-0011 - Physical Terrain Noise and Boundary-Aligned Ridges|ADR-0011]], [[../Decisions/ADR-0012 - Regional Geographic Coast Skeletons|ADR-0012]], [[../Decisions/ADR-0013 - Opportunity-Based Tidal Inlets|ADR-0013]], [[../Decisions/ADR-0014 - Open Directional Coast Boundaries|ADR-0014]], [[../Decisions/ADR-0024 - Scale-Hierarchical Directional Coasts|ADR-0024]], [[../Decisions/ADR-0025 - Built-in Steppe Terrain|ADR-0025]], and [[World File Format|World File Format]].
