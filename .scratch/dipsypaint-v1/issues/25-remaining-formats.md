# 25: Remaining formats + lossy-save warning

**What to build:** The user can open and save GIF and TIFF, open ICO and WebP, and save BMP as 256-colour, 16-colour or monochrome. A warning appears before saving to a format that loses colour or transparency. Spec: AC12 to AC14, ADR-0019.

**Blocked by:** 04 (New/Open/Save for PNG, JPEG, BMP)

**Status:** ready-for-agent

- [ ] Format support matches the ADR-0019 table exactly (no ICO/WebP save, no HEIC, no `.paint`)
- [ ] Animated GIFs open as their first frame only
- [ ] Low-colour BMP and GIF saves use palette quantisation
- [ ] A warning appears before saving to a palette format (≤ 256-colour BMP, GIF)
- [ ] Malicious samples for the new formats are added to the CI corpus
