# ADR-0021: Clipboard image formats per OS; Windows output through Avalonia's bitmap

- **Status:** Accepted
- **Date:** 2026-10-11
- **Amends:** [ADR-0008](0008-codecs-imagesharp.md) (clipboard data through ImageSharp)
- **Related:** [ADR-0014](0014-untrusted-file-handling.md)

## Context

ADR-0008 puts clipboard image data through ImageSharp. Avalonia 12's clipboard API
(`IClipboard`, `DataTransfer`, `DataFormat`) carries raw bytes under platform
format names, or an Avalonia `Bitmap` under `DataFormat.Bitmap`, which each
backend converts itself (Skia encode/decode or GDI). Checked against the Avalonia
12.1.4 sources:

- **Windows:** the names `PNG`, `image/png`, `CF_DIB`, `CF_DIBV5` and `CF_BITMAP` are
  reserved for `DataFormat.Bitmap`. Bytes placed under them are never served to
  other apps; bytes can be read from them.
- **macOS:** `public.png` on the pasteboard reaches the app only as an Avalonia
  `Bitmap` that Skia has already decoded. Writing bytes as `public.png` works, and
  other types such as `public.tiff` can be read as bytes.
- **Linux (X11):** `image/png` bytes can be read and written.

Options: write `DataFormat.Bitmap` everywhere (Avalonia encodes and decodes); add
native clipboard code per OS; use ImageSharp wherever Avalonia passes bytes through,
and Avalonia's bitmap only where it does not.

## Decision

- **Reading is always ImageSharp-decoded** from bytes. ImageSharp parses only the
  format the data is labelled with, and the declared size is checked before decoding
  (ADR-0014). Windows reads `PNG`, `image/png`, `CF_DIB` and `CF_DIBV5`; macOS reads
  `public.tiff`; Linux reads `image/png`.
- **Windows exception, reading:** an image that another app offers only as
  `CF_BITMAP` is taken as the pixels Avalonia gets from GDI, with no codec involved,
  then checked against the size limits.
- **Writing:** ImageSharp PNG as `public.png` (macOS) or `image/png` (Linux). On
  Windows, an Avalonia `Bitmap` holding the raw pixels, which Avalonia offers as
  PNG (Skia-encoded), `CF_DIB`, `CF_DIBV5` and `CF_BITMAP`.
- **Every OS** also gets DipsyPaint's own ImageSharp PNG under an application format
  (`DipsyPaint.Png`). Copy and paste between DipsyPaint windows is then lossless,
  including alpha, and DipsyPaint reads this format first.
- No native clipboard code.

## Consequences

- On Windows, other apps receive PNG data that Skia encoded, not ImageSharp. This
  is our own pixels, so there is no untrusted input involved.
- macOS images offered only as `public.png` cannot be pasted. Most apps also offer
  `public.tiff`.
- Avalonia 12.1.4 renders `CF_BITMAP` as a 1-bit monochrome bitmap (it uses
  `CreateCompatibleBitmap` on a memory DC). Apps that read `CF_BITMAP` in preference
  to PNG or DIB get a black-and-white image. This is an upstream bug to report.
- On X11, copied images disappear when DipsyPaint exits unless a clipboard manager
  keeps them. On Windows the clipboard is flushed after each copy, so the image
  outlives the app, as in Paint.
- Wayland behaviour through Avalonia is unverified.
