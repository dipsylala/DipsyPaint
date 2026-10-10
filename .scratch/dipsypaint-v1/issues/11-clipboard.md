# 11: Copy/cut/paste with the OS clipboard

**What to build:** The user can copy, cut and paste a selection within DipsyPaint and exchange images with other apps through the OS clipboard. Clipboard image data goes through ImageSharp. Spec: user story 28, ADR-0008.

**Blocked by:** 10 (Rectangle selection), 04 (New/Open/Save for PNG, JPEG, BMP)

**Status:** ready-for-agent

- [ ] Copy, cut and paste work within the app, with reference Paint's shortcuts
- [ ] An image copied in DipsyPaint pastes into another app, and vice versa, on Windows, macOS and Linux
- [ ] Pasted content arrives as a movable selection, as in reference Paint
- [ ] A pasted image larger than the canvas behaves as in reference Paint
- [ ] Clipboard decode failures show a message and don't crash
