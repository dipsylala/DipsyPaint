# 06: Colours: primary/secondary, palette, colour picker

**What to build:** The user can choose primary and secondary colours from a palette, swap them, and pick a colour from the canvas with the colour picker tool, as in reference Paint. Tools draw with the active colours. Spec: user story 9 (M1 part).

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [ ] A palette matching reference Paint's default colours is shown
- [ ] Primary/secondary selection and swap behave as in reference Paint
- [ ] The colour picker sets primary (left click) or secondary (right click) from the canvas
- [ ] The pencil draws with primary (left) and secondary (right) colours
- [ ] Engine tests cover picking from known pixels
