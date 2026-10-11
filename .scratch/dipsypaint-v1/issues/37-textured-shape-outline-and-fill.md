# 37: Textured shape outline and fill styles

**What to build:** Besides no outline and solid colour, shapes can be outlined and filled with the artistic textures reference Paint offers (crayon, marker, oil, natural pencil, watercolour). These reuse the artistic brushes' rendering. Applies to every shape, while adjusting before commit too. Spec: user story 12 (outline/fill options), user story 13.

**Blocked by:** 24 (All remaining shapes), 28 (Artistic brushes I), 29 (Artistic brushes II)

**Status:** ready-for-agent

- [x] The outline and fill options list the same styles as reference Paint
- [x] Each textured style renders like the matching artistic brush, along the outline and across the fill
- [x] Changing the style while adjusting a shape before commit updates the shape, as in reference Paint
- [x] Golden images per style on at least one closed and one open shape; randomised textures are seeded so they are deterministic
