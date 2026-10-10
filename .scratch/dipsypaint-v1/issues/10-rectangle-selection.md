# 10: Rectangle selection: select, move, delete

**What to build:** The user can draw a rectangle *selection*, move its contents and delete them, following reference Paint's behaviour. (The *selection* glossary term is not yet agreed; follow reference Paint.) Spec: user story 17.

**Blocked by:** 03 (Undo/redo with region-diff history steps), 09 (Shapes): reuses 09's mechanism for floating, not-yet-committed content

**Status:** ready-for-agent

- [ ] A rectangle selection can be drawn, moved and deleted as in reference Paint
- [ ] Move and delete are undoable
- [ ] Golden images for move and delete
- [ ] Shortcuts match reference Paint
