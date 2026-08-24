# ADR-0034: Render-First Generation Busy State

- **Status:** Accepted
- **Date:** 2026-08-24
- **Owners:** WorldEditorPixel

## Context

[[ADR-0020 - Preview-First Procedural Resource Generation|Resource generation]] and [[ADR-0030 - Preview-First Campaign Season Occurrences|Season generation]] correctly run their generators away from the UI thread, but each native dialog captured the complete immutable source before entering its busy state. That owner-thread capture scans every campaign tile and may also build normalized water-distance fields or sort sparse occurrences.

On a large world, pressing **Generate candidate** therefore blocked the Avalonia dispatcher while the progress bar was still hidden, the Generate button was still enabled, and no render pass could explain the wait. Worlds without a saved recipe could perform the same capture while resolving the initial derived seed before the dialog opened. Switching an explicit seed back to a derived seed could also capture synchronously from the settings form.

Moving capture to `Task.Run` is not valid: the terrain-query adapters are live owner-thread projections. The worker must receive an immutable captured source rather than read the mutable editor document.

## Decision

Resource and Season generation use one explicit execution boundary:

1. Cheap form and scope validation may run immediately.
2. The operation creates its cancellation source, disables settings and Generate, exposes the indeterminate progress bar, and updates the textual generation state.
3. The handler yields its continuation at Avalonia `DispatcherPriority.Background`. Queued layout and Render work therefore completes before capture begins.
4. After the yield, the handler rechecks cancellation and captures the immutable terrain/layer source on the owner thread with the operation token.
5. Derived seeds are resolved from that same capture. Selecting a derived seed never performs a hidden capture from a settings-change event.
6. The deterministic generator runs on a worker and publishes only its completed Candidate back to the UI continuation.
7. Closing during the render yield cancels before the first terrain read. Validation failures restore the non-busy state and remain inside the dialog.

The busy transition updates only loading text, buttons, input availability, and progress visibility. It does not invalidate both preview canvases or rescan report summaries merely to show loading state.

When a world has no saved Resource or Season recipe, initial derived-seed preparation remains an owner-thread operation but is wrapped by the Main Window busy state and the same render-before-work dispatcher boundary. Its prior status text is restored before the modal opens.

## Consequences

- A designer sees and can understand the wait before any full-grid capture starts.
- Owner-thread query safety, immutable generation inputs, deterministic seeds, preview-first authority, and worker-backed generation remain unchanged.
- Capture is cancellable before it starts and between its row checks; closing during the initial render turn performs no terrain read.
- The owner thread is still occupied while the immutable snapshot is copied. This decision makes that bounded preparation honest and ordered; chunked cooperative capture would require a separate domain API and is not implied here.
- Headless native regressions prove that a Render-priority dispatcher sentinel runs, the progress bar is visible and arranged, and Generate is disabled before the first injected terrain-query read for both dialogs.

## Rejected alternatives

### Move live terrain queries to the worker

Rejected because it would read mutable owner-thread editor state from a background thread and violate the capture contract.

### Use only `Task.Yield`

Rejected because the intent is specifically to let Avalonia layout and Render work run before continuation. The dispatcher-priority boundary makes that ordering explicit and testable.

### Show busy after capture

Rejected because this is the original defect: the most expensive synchronous preparation remains unexplained and unrendered.
