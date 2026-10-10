# 20: Text tool

**What to build:** The user can add text with reference Paint's font, size, style and colour options. Text becomes pixels when committed. Re-editing after commit is out of scope. Spec: user story 11, Out of Scope.

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [ ] The text box, font, size, style and colour options behave as in reference Paint
- [ ] Committing writes pixels to the active layer and is undoable
- [ ] Golden images for committed text (with font-rendering tolerance across OSes)
- [ ] Text strings and UI come from `.resx`
