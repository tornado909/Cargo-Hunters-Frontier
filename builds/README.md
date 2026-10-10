# Universal Repair Kit — filter fix 0.1.2 (TEST)

**This supersedes the previous 0.1.1 test ZIP**. Previous combined three native target filters, including a globally mandatory subcategory 102: normal body parts such as head (category 13, subcategory 32) were rejected as "cannot combine."

- Source module `workshop` keeps 99 operations; repairs `workshop.universal.00100` to a **single native category matcher** covering normal weapons, body parts, armor, backpacks, shields.
- Same kit ID, 5,000-credit price, recipe and 0.1.1 sidecar DLL as before.
- Compiled `item_templates`, `shop_templates`, `craft_recipes` verified in native isolated game; no BepInEx errors.
- Real item use, kit consumption and restoration of permanently decreased max condition **not yet verified**. Treat as test only.
- All multiplayer peers must install identical FrontierPack source files and sidecar; back up module first.

**ZIP SHA256:** `640324a564a17f7884362a548d4430a3a73a8f75febe0e79b96e75ce7abb106d`

Code: https://github.com/tornado909/Cargo-Hunters-Frontier/pull/7
