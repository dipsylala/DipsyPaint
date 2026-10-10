# 22: Magic wand (addition)

**What to build:** The user can select a region by colour similarity with a tolerance control. This is an *addition* beyond parity, standing in for AI background removal. Spec: user story 20, ADR-0011.

**Blocked by:** 21 (Free-form selection, select all, invert, transparent selection), 08 (Fill tool)

**Status:** ready-for-agent

- [ ] Clicking selects the contiguous region within the colour tolerance
- [ ] A tolerance control is available
- [ ] Scanline-based; no recursion
- [ ] The result is a normal selection (move, delete, copy, invert all work)
- [ ] Golden images for selections at several tolerances
