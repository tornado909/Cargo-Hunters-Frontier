# Cargo Hunters Frontier — instructions for automation agents

These rules apply to all work in this repository, including child source projects, local build scripts and CI changes.

## Authorized build and test locations

**Allowlist:** GitHub-hosted Actions runners and the owner's authorized Windows machine, **ASCENTO-PC**. Work on the Windows machine must use a separate workspace and the existing isolated test game:

- Source checkout / generated build files: a subdirectory of \`%USERPROFILE%\FrontierKitWork\`.
- Isolated test game: \`D:\Games\Cargo Hunters Modded\Frontier-Source\Reserve-Work\Runtime\`.
- Production game: \`D:\Games\Cargo Hunters Modded\` — read-only by default.

**Do not access, copy files to, compile on, or run experiments on ASC-AI or any other host for this project.** Finding a build environment on an unapproved host is not authorization to use it. If the permitted targets are unavailable, report the blocker rather than silently switching infrastructure.

## Isolation and integrity

- The test runtime uses its own Unity player profile and FreeTP save directory. Do not redirect it to production saves.
- Before replacing a test DLL or module manifest, confirm that CargoHunters.exe is not running; back up the original bytes and record SHA-256 hashes. Restore the test installation after acceptance checks unless the owner directs otherwise.
- When recompiling module DLLs, update their \`module.json\` SHA-256 entries in **the test copy only**, or module loading will stop before the catalog is composed.
- Never edit the live game, production plugin folder, save files, Steam-related data or game executable without separate explicit user authorization.
- Never commit game-proprietary interop/reference DLLs, local logs, saves, private tokens, Steam credentials or downloaded game installers into this repository.

## Source provenance and releases

- Keep **original authored source** separate from **binary-derived recovered source**. Neither category may be mislabeled.
- Source compilation and CLR public API parity are useful checks, but **not** sufficient to approve runtime compatibility. Test menu load, gameplay, survival, repair, saving and multiplayer as relevant.
- Existing stable GitHub Releases must keep their verified binaries until each replacement passes gameplay acceptance.
- Source-built test ZIPs must be unmistakably marked as experimental; release workflows must not silently replace installed production DLLs with reconstructed candidates.

GitHub Actions remains the standard CI/release service. Local ASCENTO-PC builds and isolated smoke tests are authorized for development and troubleshooting.
