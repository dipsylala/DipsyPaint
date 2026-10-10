# 18: OpenRaster open/save

**What to build:** The user can save layered work as an OpenRaster (`.ora`) *project file* and reopen it with all layers intact. Other apps (Krita, MyPaint, Pinta, GIMP) can open the files. Untrusted `.ora` files are validated. Spec: AC15, AC16, AC19 to AC21, ADR-0006, ADR-0014.

**Blocked by:** 17 (Layers panel), 04 (New/Open/Save for PNG, JPEG, BMP)

**Status:** ready-for-agent

- [ ] A saved `.ora` contains `mimetype`, `stack.xml`, per-layer PNGs, `mergedimage.png` and a thumbnail
- [ ] DipsyPaint-specific data appears only as namespaced extensions in `stack.xml`
- [ ] Reopening restores layer order, names, visibility and opacity exactly
- [ ] Manual check: the file opens in Krita with layers intact
- [ ] Rejected: zip-slip entry names (absolute, `..`, drive letters), too many entries, total uncompressed size over the cap, oversized `stack.xml`, layer PNGs larger than the canvas
- [ ] `stack.xml` is parsed with DTD processing disabled; an XXE sample has no effect
- [ ] All rejections show "could not open" with no crash and no half-open document; the samples are in the CI corpus
