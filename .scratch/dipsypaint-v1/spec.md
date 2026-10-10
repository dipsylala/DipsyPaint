# Spec: DipsyPaint v1.0, a cross-platform equivalent of Microsoft Paint

- **Status:** Ready for planning (no blocking questions). Planning parent; break
  into milestone/work items before implementation.
- **Date:** 2026-10-10
- **Decision record:** `docs/decisions/` (ADR-0001 to ADR-0019), `docs/GLOSSARY.md`

Terms in *italics* are defined in the glossary. *Selection* is still only a
suggested glossary term; until it is agreed, selection behaviour follows
reference Paint.

## Problem Statement

Microsoft Paint is the familiar, simple image editor for Windows users, but it
only runs on Windows. People on macOS and Linux, or people who move between
operating systems, have no equivalent that works like modern Windows 11 Paint
(layers, transparency, opacity, freeform rotation). The existing cross-platform
editors are either much more complex or work differently from Paint. Users who
know Paint's layout and shortcuts must learn something new on every other OS.

## Solution

DipsyPaint is a free, open-source (MIT) desktop image editor for Windows, macOS
and Linux. It reaches *parity* with *reference Paint* (Windows 11 Paint,
11.2605.x). Parity excludes all AI/ML features, printing, HEIC and `.paint`.
Its layout and mental model match reference Paint, and it has its own name,
icon and branding.

Users can draw, add shapes and text, select and transform regions, and work
with *layers*. Layered work is saved as OpenRaster (`.ora`) *project files*
that Krita, MyPaint, Pinta and GIMP can also open. Flat images are opened and
saved in the same formats reference Paint uses. DipsyPaint also offers
*additions* reference Paint lacks, starting with a magic wand and crash
recovery. It sends no data anywhere.

Delivery is in three *milestones*: M1 "usable Paint", M2 "modern Paint",
M3 "parity polish". v1.0 = M3 complete.

## User Stories

**Drawing and editing**

1. As a Paint user on macOS or Linux, I want the same tools reference Paint has, so that I can edit images the way I already know.
2. As a Paint user, I want the toolbar across the top, the canvas in the centre, layers on the right and a status bar at the bottom, so that I can find things without relearning.
3. As a user, I want reference Paint's keyboard shortcuts where practical, so that my muscle memory still works.
4. As a user, I want pencil, brush, eraser, fill and colour picker tools, so that I can do basic drawing and correction (M1).
5. As a user, I want line, rectangle and ellipse shapes with outline and fill, so that I can draw simple diagrams (M1).
6. As a user, I want to crop, resize, rotate in 90° steps and flip the image, so that I can fix photos and screenshots (M1).
7. As a user, I want to zoom and pan the canvas, so that I can work on detail (M1).
8. As a user, I want undo and redo, so that I can recover from mistakes (M1).
9. As a user, I want primary/secondary colours and a palette, so that I can switch colours quickly (M1), and a custom colour editor, so that I can pick any colour (M2).
10. As a user, I want to set the opacity of pencil, brush and eraser strokes, so that I can blend and soften (M2).
11. As a user, I want a text tool, so that I can annotate images (M2).
12. As a user, I want every shape reference Paint offers (polygons, arrows, stars, callouts, heart, lightning), so that I am not missing anything I used in Paint (M2).
13. As a user, I want the artistic brushes (two calligraphy pens, airbrush, oil, crayon, marker, natural pencil, watercolour), so that I can paint expressively (M3).
14. As a user, I want rulers, gridlines, a magnifier and full screen, so that I can work precisely (M3).
15. As a user, I want light and dark mode, so that the app suits my environment.
16. As a pen-tablet user, I want pressure to affect strokes where my OS reports it, so that drawing feels natural. Mouse and keyboard remain the primary input.

**Selection**

17. As a user, I want a rectangle *selection* I can move, delete, copy, cut and paste, so that I can rearrange content (M1).
18. As a user, I want free-form selection, select all, invert selection and transparent selection, so that I can isolate irregular regions (M2).
19. As a user, I want to rotate a selection, shape or text to any angle and to skew it, so that I can position content freely (M2).
20. As a user, I want a magic wand that selects by colour similarity, so that I can isolate an object without AI background removal (M2, addition).

**Layers and files**

21. As a user, I want to add, delete, duplicate, merge, hide and reorder layers, so that I can build images non-destructively (M2).
22. As a user, I want to save layered work as a project file, so that I can reopen it with all layers intact (M2).
23. As a Krita, MyPaint, Pinta or GIMP user, I want DipsyPaint project files to open in those apps, so that my work is not locked in.
24. As a user, I want to open and save PNG, JPEG, BMP, GIF and TIFF, so that I can work with common images.
25. As a user, I want to open ICO and WebP files, so that I can edit icons and web images.
26. As a user, I want to save BMP as 24-bit, 256-colour, 16-colour or monochrome, so that I can produce the same outputs reference Paint can.
27. As a user, I want a warning before saving to a format that loses colour or transparency, so that I don't lose detail by accident.
28. As a user, I want to copy and paste images through the OS clipboard, so that I can move content between apps (M1).

**Failure and safety**

29. As a user whose app crashed, I want to be offered recovery of my unsaved document on next launch, so that I don't lose work (M2).
30. As a user working on a very large image, I want a clear message when an operation would exceed available memory, so that the app doesn't crash.
31. As a user who opens a damaged or malicious file, I want a "could not open" message, so that the app stays usable and no half-open document appears.
32. As a user, I want "Report a problem" to open a pre-filled GitHub issue that I review before submitting, so that I can report bugs without anything being sent automatically.
33. As a privacy-conscious user, I want the app to send no telemetry or analytics, so that my usage stays private.

**Distribution**

34. As a user, I want to download DipsyPaint from GitHub Releases for Windows, macOS or Linux (x64 or arm64), so that I can install it on my machine.
35. As a user, I want the app to update itself, so that I get fixes without reinstalling.
36. As a user installing an unsigned build, I want README instructions for getting past SmartScreen and Gatekeeper, so that I can run the app before signing is in place.

## Acceptance Criteria

**Scope and identity**

- AC1. The app contains no AI/ML features, ML models or AI service calls.
- AC2. There is no print command anywhere in the UI.
- AC3. The name "Microsoft", "MS Paint" or Microsoft's Paint icon does not appear in the app name, UI or marketing. The product name is "DipsyPaint".
- AC4. Builds run on Windows, macOS and Linux, for x64 and arm64, within the OS versions .NET 10 and Avalonia 12 support.
- AC5. Each milestone contains the features listed for it in ADR-0009 (as amended by ADR-0010, ADR-0011, ADR-0013 and ADR-0019). M3 is complete only after a parity audit against reference Paint, with the remaining gaps closed or recorded as deviations.
- AC6. An *addition* never blocks a milestone's exit.

**Document model**

- AC7. A *document* is an ordered stack of layers. Each layer stores 8-bit-per-channel RGBA sRGB pixels.
- AC8. Creating, opening or resizing a document beyond 16384 px on either side is refused with a message.
- AC9. M1 documents have exactly one layer; the layers UI appears in M2.
- AC10. Undo and redo restore pixels exactly to their previous state for every *history step*.
- AC11. Undo history memory never exceeds min(1 GB, 25% of RAM). When the cap is reached, the oldest steps are dropped.

**Files**

- AC12. Format support matches this table exactly:

  | Format | Open | Save |
  | --- | --- | --- |
  | PNG | ✓ | ✓ |
  | JPEG (`.jpg` `.jpeg` `.jpe` `.jfif`) | ✓ | ✓ |
  | BMP / DIB | ✓ | ✓ 24-bit, 256-colour, 16-colour, monochrome |
  | GIF | ✓ (first frame) | ✓ |
  | TIFF | ✓ | ✓ |
  | ICO | ✓ | — |
  | WebP | ✓ | — |
  | OpenRaster `.ora` | ✓ | ✓ |
  | HEIC, `.paint` | — | — |

- AC13. Animated GIFs open as their first frame only.
- AC14. Saving to a palette format (≤ 256-colour BMP, GIF) shows a warning first.
- AC15. A saved `.ora` opens in Krita with layer order, names, visibility and opacity intact.
- AC16. A saved `.ora` is valid OpenRaster: it contains `mimetype`, `stack.xml`, per-layer PNGs, `mergedimage.png` and a thumbnail. DipsyPaint-specific data appears only as namespaced extensions.
- AC17. Exporting to a flat format writes the composited image and keeps no layers.

**Untrusted input**

- AC18. An image whose header declares more than 16384 px on a side, or a size beyond the memory budget, is rejected before decoding.
- AC19. An `.ora` file is rejected when it has any of: entry names that are absolute, contain `..`, or contain drive letters; too many entries; total uncompressed size over the cap; layer PNGs larger than the canvas; a `stack.xml` over its size cap.
- AC20. `stack.xml` is parsed with DTD processing disabled. An XXE payload has no effect.
- AC21. Any decode failure shows "could not open". The app does not crash and does not leave a half-open document.

**Recovery and memory (M2)**

- AC22. While a document has unsaved changes, a recovery snapshot (`.ora`) is written to a per-user app-data folder every 2 minutes by default (configurable). The UI thread is not blocked.
- AC23. Snapshots never write to the user's own file.
- AC24. After an unclean exit, the next launch offers restore or discard for each snapshot.
- AC25. A document's snapshot is deleted when the document is cleanly saved or explicitly discarded. Stale snapshots are purged.
- AC26. Large operations (open, new layer, canvas resize, duplicate) are checked against the memory budget beforehand. Anything over budget is refused with a clear message.
- AC27. On out-of-memory, the app attempts an emergency snapshot before exiting.

**Privacy and diagnostics**

- AC28. The app makes no network requests except the update check and the user-initiated "Report a problem" browser hand-off.
- AC29. An unhandled exception writes a local crash log containing the version, the OS and the stack trace. It contains no image data, and file paths and usernames are left out where practical.
- AC30. "Report a problem" opens a pre-filled GitHub issue in the browser and submits nothing itself.

**Performance** (measured on a 2020-era 4-core laptop with integrated GPU)

- AC31. Brush input appears on screen within 16 ms at 4K × 20 layers.
- AC32. Flood fill on a 4K canvas completes within 200 ms.
- AC33. Cold start reaches a usable state within 2 s.
- AC34. Large operations keep the UI thread responsive. There is no file-opening time target.

**Distribution**

- AC35. Releases are published on GitHub Releases. They are unsigned until the signing sprint.
- AC36. Velopack provides installation and auto-update on all three OSes. On Linux the build is an AppImage.
- AC37. The README documents how to get past SmartScreen and Gatekeeper.
- AC38. Third-party notices include ImageSharp's Six Labors Split License.

## Implementation Decisions

- **Stack:** C# on .NET 10, Avalonia UI 12.x with the Fluent theme, SkiaSharp, and SixLabors.ImageSharp. All MIT-compatible (ADR-0004, ADR-0007).
- **Two-part architecture** (ADR-0016):
  - **Engine:** a .NET library with no Avalonia dependency. It contains the document, layers, tools, history, compositing and codecs.
  - **Shell:** the Avalonia app. A thin layer over the *engine* that handles UI, platform integration (file dialogs, clipboard, file associations, HiDPI), updates and crash reporting.
- **Pixel-access abstraction** (ADR-0005): tools and other engine code touch layer pixels only through this abstraction, never raw bitmaps. That keeps a later move to tiled storage contained in the storage layer. Code review enforces it.
- **Layer storage:** one flat bitmap per layer, premultiplied RGBA in memory (Skia's native format). Converted to straight alpha on export. No colour management (ADR-0005).
- **M1 design for M2 features** (ADR-0009): the tool and brush architecture
  supports opacity and multiple layers from M1, even though M1 exposes
  neither, so M2 doesn't need a rework.
- **History:** each *history step* stores the before-pixels of the changed rectangle(s) only (ADR-0005). The memory cap is applied per AC11 (ADR-0018).
- **Codec boundary** (ADR-0008, ADR-0019): ImageSharp performs all encoding and decoding. This covers flat files, the PNGs inside `.ora` files and clipboard image data. SkiaSharp is used only for rendering, compositing and pixel operations. Pixels cross between them through a single conversion point.
- **Project format** (ADR-0006): OpenRaster `.ora`. App-specific data is stored in namespaced extension attributes/elements in `stack.xml`. Exposed layer properties stay within what OpenRaster can represent.
- **Programmatic tools** (ADR-0016): tools take input events as data, so they can be driven without a UI for tests and replay.
- **Compositor** (ADR-0018): caches the composite of the layers below and above the active layer and redraws only dirty rectangles.
- **Flood fill and magic wand:** scanline algorithm, no recursion (ADR-0018). The magic wand is colour-tolerance flood selection (ADR-0011).
- **Additions backlog** (ADR-0011): *additions* are tracked in a backlog
  separate from milestone parity scope. Parity items take priority.
- **Recovery** (ADR-0013): snapshots are written in the `.ora` format. The pixel copy happens synchronously; compression and writing happen on a background thread.
- **Memory budget** (ADR-0013): checked before large allocations. Out-of-memory is caught as a last resort.
- **Untrusted-input limits** (ADR-0014): as AC18 to AC21.
- **Localisation default:** all UI strings live in `.resx` resources from M1. English only at launch.
- **Accessibility default:** every tool and command can be reached by keyboard, and controls carry Avalonia automation names. No formal WCAG commitment.
- **Icons** (ADR-0012): Fluent UI System Icons (MIT). Bespoke icons only for tools the set lacks.
- **Publishing** (ADR-0018): ReadyToRun or NativeAOT. Which one depends on the AOT spike.
- **Distribution** (ADR-0015): Velopack packages from GitHub Releases. AppImage on Linux. Flathub is optional later.

## Testing Decisions

The agreed boundaries come from ADR-0016, ADR-0014 and ADR-0018. Tests check external behaviour (pixels, files, visible UI state), never internal structure.

| Boundary | Verifies |
| --- | --- |
| **Engine public API** (unit tests) | Tool effects, history (undo/redo restores exact pixels, cap enforcement), layer operations, document limits (16384 px, memory budget), format table (AC12 to AC17), `.ora` structure and round-trip |
| **Golden images** | A scripted tool sequence, run through the engine, produces output matching a reference PNG within a per-pixel tolerance. Failures emit a diff image as a CI artefact. Covers rendering of tools, brushes, shapes, text and compositing |
| **Malicious-sample corpus** (engine API) | AC18 to AC21: oversized headers, zip-slip names, zip bombs, oversized `stack.xml`, XXE payloads, layer PNGs larger than the canvas, truncated/corrupt files. All end in a clean "could not open" |
| **Avalonia.Headless UI smoke tests** (shell) | The main window loads, tools can be selected, open/save flows and dialogs work, the recovery prompt appears after a simulated unclean exit, the warning appears before a lossy save, there is no print command |
| **BenchmarkDotNet suites** | AC31 to AC33 trends. Regressions are flagged in CI but don't block merges. Each milestone release also gets a manual check on reference hardware |

- **CI:** a GitHub Actions matrix (Windows, macOS, Linux) runs on every PR.
- **What a good test is:** it drives the engine or shell through its public surface and asserts on observable results. It doesn't assert on private state or call order. Golden tests must tolerate small Skia differences between platforms.
- **Golden-image upkeep:** an update workflow must be documented, and tolerance thresholds tuned to avoid flaky tests.
- **Interop check:** AC15 (opening in Krita) is a manual check per milestone; no automated Krita boundary has been agreed.
- **Prior art:** none. The repository has no code yet.

## Out of Scope

- All AI/ML features, including ML-based background removal (ADR-0002). Permanent.
- Printing (ADR-0010).
- HEIC/AVIF and Microsoft `.paint` files (ADR-0019, ADR-0006).
- Saving ICO and WebP (ADR-0019).
- Mobile/tablet builds. A web build is a stretch goal only, not a v1 commitment (ADR-0003).
- Animated GIF editing (ADR-0008).
- 16-bit, wide-gamut or ICC colour-managed editing (ADR-0005).
- Tiled layer storage. The abstraction allows it later (ADR-0005).
- Telemetry, analytics and automatic crash upload (ADR-0017).
- Fuzzing and sandboxed decoding (ADR-0014).
- Code signing and notarisation. Deferred to a future sprint (ADR-0015).
- Languages other than English at launch.
- Re-editable text after commit. Text becomes pixels (ADR-0006 limit).

## Further Notes

- The repository currently contains only `docs/`. There is no code, and it is not a git repository.
- Reference Paint's save-format list comes from a single third-party source. The M3 parity audit should confirm it.
- "Dipsy" is also a Teletubbies character name, which is a third-party trademark. Avoid branding that evokes the character (ADR-0012). No trademark search has been done.
- Signing options for the future sprint: SignPath Foundation (free for open source, but ImageSharp's Split License may disqualify the project), the Microsoft Store, or a purchased OV certificate. The maintainer is not eligible for Azure Trusted Signing. Apple notarisation costs $99/yr.
- Recovery snapshots store user image data in app-data. Note this in the privacy docs.
- Update integrity relies on GitHub account security plus Velopack package
  hashes over HTTPS. The release account needs 2FA and restricted release
  permissions (ADR-0015).

## Unresolved Questions

None block implementation. Where the behaviour of an individual tool isn't specified here, the parity rule settles it: copy reference Paint's behaviour. These remain open:

1. **NativeAOT compatibility** of ImageSharp, SkiaSharp and Avalonia 12 (spike). If AOT fails, use ReadyToRun. Only AC33 depends on the outcome.
2. **SignPath eligibility** given ImageSharp's licence. Relevant only to the signing sprint.
3. **Memory-budget value** for refusing large operations (AC26). It isn't quantified; the undo cap is. To be tuned during M1/M2.
4. **Golden-image tolerance thresholds.** To be tuned once tests exist.
5. **Signing trigger.** Deferred to a future sprint, with no trigger set.
6. **Layer blend modes.** ADR-0006 mentions blend mode among the layer
   properties that might be exposed, but no decision says whether DipsyPaint
   offers them. The parity rule decides it: expose them only if reference
   Paint does, and only modes OpenRaster supports. Not blocking for M1, which
   has no layers UI. Check during M2.

## References

- `docs/decisions/README.md`: ADR index, deferred defaults, spikes
- `docs/GLOSSARY.md`
- ADR-0001 feature baseline · ADR-0002 no AI · ADR-0003 platforms · ADR-0004 stack · ADR-0005 document model · ADR-0006 OpenRaster · ADR-0007 MIT · ADR-0008 ImageSharp codecs · ADR-0009 milestones · ADR-0010 no printing · ADR-0011 additions · ADR-0012 UI/identity · ADR-0013 recovery · ADR-0014 untrusted files · ADR-0015 distribution · ADR-0016 testing · ADR-0017 no telemetry · ADR-0018 performance · ADR-0019 file formats
- https://learn.microsoft.com/en-us/windows-insider/release-notes/apps/paint
- https://www.digitalcitizen.life/paint-windows-11/
- https://signpath.org/terms
