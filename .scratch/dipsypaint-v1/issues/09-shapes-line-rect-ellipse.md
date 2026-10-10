# 09: Shapes: line, rectangle, ellipse

**What to build:** The user can draw lines, rectangles and ellipses with reference Paint's outline and fill options. Each shape stays adjustable until committed, as in Paint. Spec: user story 5.

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [x] Line, rectangle and ellipse can be drawn
- [x] Outline and fill options match reference Paint
- [x] The shape can be adjusted before it is committed, as in reference Paint
- [x] Golden images per shape and style
- [x] Committing is undoable

Outline/fill offer *No outline/No fill* and *Solid color*; reference Paint's textured styles (crayon, marker, oil, natural pencil, watercolour) need the artistic brushes (tickets 28, 29) and are not yet tracked by a ticket.
