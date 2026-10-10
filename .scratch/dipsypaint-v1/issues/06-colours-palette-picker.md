# 06: Colours: primary/secondary, palette, colour picker

**What to build:** The user can choose primary and secondary colours from a palette, swap them, and pick a colour from the canvas with the colour picker tool, as in reference Paint. Tools draw with the active colours. Spec: user story 9 (M1 part).

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [x] A palette matching reference Paint's default colours is shown
- [x] Primary/secondary selection and swap behave as in reference Paint
- [x] The colour picker sets primary (left click) or secondary (right click) from the canvas
- [x] The pencil draws with primary (left) and secondary (right) colours
- [x] Engine tests cover picking from known pixels
