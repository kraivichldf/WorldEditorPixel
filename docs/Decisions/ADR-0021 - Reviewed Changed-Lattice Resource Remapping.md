# ADR-0021: Reviewed Changed-Lattice Resource Remapping

- Status: Implemented
- Date: 2026-08-16

## Context

[[ADR-0015 - Preview-First Current World Regeneration|ADR-0015]] allows a reviewed terrain replacement to change world dimensions and campaign tile size. [[ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019]] deliberately blocks that acceptance while resource occurrences exist because keeping the same integer coordinates would silently move them in physical space, while simply discarding them would silently lose authoring authority. [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]] adds deterministic unlocked-resource generation but keeps the same-lattice boundary.

The next safe expansion must preserve physical intent, manual locks, the exact preview-first document boundary, and bounded deterministic behavior. It must explain collisions and out-of-bounds loss before commitment rather than hiding either consequence inside acceptance.

## Decision

### Owner-thread source capture

Opening **Regenerate world...** captures an immutable resource-regeneration source on the owner thread. The source contains the value-equal current world definition, terrain revision, resource revision, resource catalog, saved generation settings, and deterministic `Y/X/resource ID` occurrence entries. Capture verifies the world and resource revisions before and after enumeration.

Terrain generation and resource remapping then run against private candidate state away from the UI thread. They never read the live mutable world or resource map.

### Physical-position mapping

For a source occurrence at tile `(x, y)`, remapping uses the old tile centre in metres:

```text
centreX = (x + 0.5) * oldTileSizeMeters
centreY = (y + 0.5) * oldTileSizeMeters
targetX = floor(centreX / newTileSizeMeters)
targetY = floor(centreY / newTileSizeMeters)
```

If either centre lies outside the replacement world's physical width or height, that source occurrence is an out-of-bounds drop. World-size changes do not normalize or stretch coordinates; a deposit remains at its authored physical position whenever that position still exists.

Several source tiles may collapse into one larger target tile. For the same resource ID, the target stores the highest source potential and is locked when any merged source was locked. Different resource IDs remain independent and may coexist on the target tile. Stable source ordering and explicit comparisons make equal inputs produce equal results.

### Same-lattice and changed-lattice behavior

When world width, world height, and campaign tile size are unchanged, regeneration keeps the existing behavior: every occurrence and saved resource-generation setting is copied exactly to the replacement terrain. No procedural resource run occurs.

When the lattice changes:

- every locked occurrence is remapped before any generated placement;
- if saved resource-generation settings exist, unlocked occurrences are replacement data and are regenerated for **All resources** against the reviewed candidate terrain using those exact settings;
- if no saved resource-generation settings exist, every occurrence is remapped, because inventing a procedural recipe would be less faithful than preserving the current manual layer;
- locked occurrences above a regenerated target remain authoritative under ADR-0020;
- custom resource definitions remain the same catalog instances and participate through the existing generator.

This distinction preserves manual authority while allowing previously generated unlocked deposits to respond to the replacement terrain's new coast, elevation, climate, geology, Lakes, and Rivers.

### Reviewed impact report

The terrain preview also owns an exact candidate resource map and a resource-impact report. The report shows:

- source and final occurrence counts;
- unchanged and physically moved source occurrences;
- same-ID merges;
- out-of-bounds drops;
- locked source, retained, merged, and dropped counts;
- whether unlocked resources were preserved or regenerated;
- regenerated unlocked count and the existing per-resource suitability/shortfall reports;
- exact resource ID and source coordinate for every locked out-of-bounds drop.

The report is visible before **Use this world**. A drop or merge is a warning, not a hidden validation side effect. Cancel discards both terrain and resource candidates.

### Atomic acceptance and stale protection

The dialog result captures the source terrain/resource revisions and candidate terrain/resource revisions. Acceptance rejects the preview if the current document changed, the candidate changed after generation, the target definitions do not match, or the resource catalog identity changed.

Successful acceptance installs the exact reviewed terrain world, resource map, and resource-generation settings as one editor document boundary. It keeps project/import identity, clears the shared Undo/Redo history, refreshes diagnostics and selections, and marks the document modified. It does not regenerate a second time.

## Consequences

- Designers may change world dimensions or campaign tile size without first erasing the resource layer.
- Physical tile centres, not integer grid coordinates or normalized percentages, define resource remapping.
- Manual locks remain protected through coarser-grid merges; locked out-of-bounds loss is always named before acceptance.
- Generated unlocked resources respond to the new terrain when a real saved recipe exists; manual-only documents remain manual-only.
- Preview memory temporarily contains the current document plus candidate terrain and candidate resources, bounded by the existing generated-world and resource-candidate limits.
- Project and runtime file formats do not change; only the accepted sparse coordinates and potentials change.

This decision extends [[ADR-0015 - Preview-First Current World Regeneration|ADR-0015]], [[ADR-0019 - Manual Resource Workspace Vertical Slice|ADR-0019]], and [[ADR-0020 - Preview-First Procedural Resource Generation|ADR-0020]].

## Implementation evidence

Implemented on 2026-08-16 in `CampaignResourceWorldRegenerator`, `NewWorldDialog`, and `EditorViewModel`. Nine focused core tests cover exact same-lattice preservation, physical-centre movement, finer/coarser grids, maximum-potential and lock-preserving merges, exact locked drops, saved-settings unlocked replacement, manual-only remapping, stale candidates, immutable capture, and cancellation. Editor integration tests cover exact candidate installation, project identity/history behavior, and stale non-mutation. After the ADR-0020 dual-preview render-pass correction, the verified repository baseline is `386/386` Release tests with zero build warnings, clean formatting, and the prior clean bounded Impeccable detector pass for the changed dialog/integration targets.
