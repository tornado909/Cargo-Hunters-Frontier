# OBSOLETE — universal repair kit 0.1.1 filter was broken

**Do not use this archive.** Its `ItemsFilter` accidentally required three
conditions simultaneously, including subcategory `102`, which excluded the
head, firearms and most equipment. The native game's "cannot combine" message
was reproduced by a user.

**Replacement:** [Universal Repair Kit 0.1.2 filter fix](https://github.com/tornado909/Cargo-Hunters-Frontier/blob/builds/universal-repair-kit-0.1.2-filter-fix-test/builds/Frontier_UniversalRepairKit_0.1.2_FILTER_FIX_TEST.zip).

This is still an experimental package until actual item use/consumption and
permanent-condition restoration are verified.

---

# Frontier Universal Repair Kit 0.1.1 — TEST

Archive contains a compiled BepInEx sidecar and three modified workshop source JSON files, to be installed identically on host and guest.

**Native startup and repository readback verified** with Cargo Hunters 0.26.38.59 and Frontier 0.6.0. **Actual restoration of maximum condition, transaction/consumption, and two-player synchronization not yet verified.** Do not call it a stable release.

The assembled DLL was built on ASC-AI, not on the player's game PC. No saves, game textures or generated reference binaries are included.

Archive SHA256: `405abf31e74205df77e26e12bf29c68cb016362d35586e1f8b56ef313239de57`.

Source: https://github.com/tornado909/Cargo-Hunters-Frontier/pull/7
