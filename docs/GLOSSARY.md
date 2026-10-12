# Glossary

## Accepted

**DipsyPaint** — the product name (ADR-0012). Use as one word, camel-cased.
Not affiliated with Microsoft Paint.

**Reference Paint** — Microsoft Paint for Windows 11 (stable 11.2605.x as of
2026-10). The parity target. Distinct from *classic Paint* (Win7-era,
single opaque bitmap, no layers). See ADR-0001.

**Parity** — matching reference Paint's features and behaviour, **excluding all
AI/ML features** (ADR-0002), **printing** (ADR-0010), **HEIC and `.paint`**
(ADR-0019). UI matches its layout and mental model, not its branding (ADR-0012).

**Document** — an open image being edited: an ordered stack of layers sharing one
canvas size (max 16384 px per side), changed only by crop, resize and rotate. See ADR-0005.

**Layer** — a raster plane the size of the canvas, 8-bit RGBA sRGB, composited
into the document. Stored as a flat bitmap behind a pixel-access abstraction.
Distinct from a *selection* (a temporary floating region, see below).

**History step** — one undoable action; stores the before-pixels of the changed
rectangle(s) only. See ADR-0005.

**Project file** — a `.ora` (OpenRaster) file preserving layers and their
properties. Distinct from an *export* (a flattened single-image file such as
PNG or JPEG). See ADR-0006.

**Export** — writing the composited document to a flat format; layers are lost.

**Milestone (M1/M2/M3)** — a releasable increment toward parity: M1 "usable
Paint", M2 "modern Paint", M3 "parity polish". v1.0 = M3 complete. See ADR-0009.

**Addition** — a feature reference Paint lacks, accepted case by case and
tracked in a backlog separate from parity scope (ADR-0011). Distinct from a
*parity item*. Example: magic wand.

**Engine** — the UI-free .NET library containing document, layers, tools,
history and codecs. Distinct from the *shell* (the Avalonia app). ADR-0016.

**Golden image** — a reference PNG a scripted tool sequence must reproduce
within a per-pixel tolerance. ADR-0016.

## Suggested (not yet agreed)

**Command group** — a movable unit of the command bar (e.g. Menu, Tools, Colours) with a stable
id, dockable to a window edge or undocked over the canvas, and saved in the per-user layout
(ADR-0022, ADR-0026). Distinct from a *tool*, which is one entry within the Tools group.

**Undocked group** — a command group placed over the canvas instead of on a window edge
(ADR-0026). Not "floating", which means *floating pixels*.

**Selection** — an outline marking part of the canvas (rectangle, free-form or magic-wand
shape), made with the Select tool (ADR-0025). Moving the outline with Move selection changes no
pixels. Distinct from *floating pixels*.

**Floating pixels** — selected pixels lifted off the layer by Move selected pixels (or a paste),
shown above it while they are moved, stretched or rotated, and composited back when committed.

**Unsaved changes** — history steps made since the document was last saved, opened or
created. Undoing back to the saved point clears them. They trigger the "Save changes?"
prompt (ticket 36) and crash-recovery snapshots (ADR-0013). Code may call this "dirty".

**Resolution (DPI)** — how many pixels per inch other programs should use when printing or placing
the image, stored per document and kept by files (ADR-0024). Metadata only: it never changes the
pixels, and sizes are always entered in pixels. Distinct from *zoom*, which is how large DipsyPaint
shows the image.
