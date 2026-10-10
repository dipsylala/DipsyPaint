# 04: New/Open/Save for PNG, JPEG, BMP (24-bit)

**What to build:** The user can create a new document, open PNG, JPEG (`.jpg` `.jpeg` `.jpe` `.jfif`) and BMP/DIB files, and save as PNG, JPEG or 24-bit BMP using native file dialogs. All encoding and decoding goes through ImageSharp, with pixels crossing to Skia at a single conversion point. Untrusted flat images are checked before decoding. Spec: AC8, AC12 (partial), AC17, AC18, AC21, ADR-0008, ADR-0014, ADR-0019.

**Blocked by:** 02 (Pencil tool + golden-image harness): opening a file needs the layer pixel model 02 creates

**Status:** ready-for-agent

- [ ] PNG, JPEG and 24-bit BMP open and save; round-trips preserve pixels (within JPEG loss)
- [ ] Saving writes the composited image; exports keep no layers
- [ ] Creating or opening a document larger than 16384 px on either side is refused with a message
- [ ] Image dimensions are read from the header before decoding; over-limit or over-memory-budget images are rejected before decode
- [ ] Corrupt, truncated or malicious samples show "could not open". No crash, no half-open document
- [ ] A malicious-sample corpus for flat formats runs in CI
- [ ] The ImageSharp/Skia boundary is a single conversion point
- [ ] Unresolved (non-blocking): the memory-budget value is not yet quantified; implement it as a configurable value with a documented placeholder
