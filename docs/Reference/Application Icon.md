# Application Icon

WorldEditorPixel uses one project-owned terrain-map mark for the Windows executable and the main Avalonia window.

## Visual contract

The icon is a deliberately simple late-1990s pixel-art map:

- green land meeting blue sea;
- one ochre mountain and one cyan river;
- a subtle `2 × 2` campaign-tile grid;
- one dark navy square outline;
- no text, letters, glow, watermark, or opaque background.

The large shapes preserve the editor's terrain, elevation, hydrology, and exact-tile identity at Windows taskbar size. The flat colors and hard edges fit the native Windows 98 presentation defined in [[../Architecture/World Terrain Editor|World Terrain Editor architecture]].

## Files

- `src/World.Editor/Assets/WorldEditorPixel.png` is the transparent `1024 × 1024` RGBA source retained for future edits.
- `src/World.Editor/Assets/WorldEditorPixel.ico` is the shipping Windows icon. It contains `16`, `20`, `24`, `32`, `40`, `48`, `64`, `128`, and `256` pixel RGBA frames.

Frames through `48 × 48` use nearest-neighbour reduction to preserve intentional pixel edges. Larger frames use high-quality reduction. The `.ico` is the executable input; the PNG is not loaded at runtime.

## Build integration

`World.Editor.csproj` declares the ICO as both `ApplicationIcon` and an Avalonia resource. `MainWindow.axaml` uses the same resource as its native window icon. This keeps Explorer, taskbar, executable properties, and the open main window on one visual identity.

The icon is generated into the normal framework-dependent apphost and into self-contained single-file `win-x64` publishes. To verify the shipping boundary, extract the associated icon from `WorldEditorPixel.exe`; do not treat the standalone PNG as proof that the executable was branded.

## Source provenance

The final bitmap was produced with the built-in image-generation tool on 2026-08-23, then deterministically cleaned to true alpha and packed into the multi-resolution ICO. The retained source is original project artwork with no third-party logo or text.
