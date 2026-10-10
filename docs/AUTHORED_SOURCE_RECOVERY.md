# Authored Frontier source inventory

Source recovery on 2026-10-11 uses original C# files from the game development workspace and saved source archives, not reconstructed binaries.

SHA256 of the source-only transfer: f1b79b084a68eb15ecb580f23cda4d410f2d2dd6ada708bd54355927999bcc83.

174 source entries are recorded in docs/source-archive-manifest.json: 95 newly imported and 79 byte-identical originals that were already in GitHub. A separate 196-file source-only archive was verified during transfer.

Existing authored copies: src/Engine (14 files), src/Reserve (29 files), and historical src/Survivor (36 files). Newly preserved sources: src/Candidates/SurvivorCoop (32), history/authored/0.2.0 (15), history/authored/0.3.0 (19), history/authored/elite-rigos-patch (5), history/authored/coop-shelter-patch (5), and history/authored/reserve-v10.9.0 (19).

The authenticated developer sources for Engine, Reserve and SurvivorCoop compile on ASC-AI with version-matched references. Their results do not match the installed runtime DLLs byte-for-byte. These are source candidates, NOT approved replacements for the shipping plugins.

The stable v0.1.0 Release remains unchanged. CI must verify provenance and compile candidates separately. A source-built replacement may enter Release only after parity and in-game acceptance, including multiplayer.

Several later archives, including Frontier_v10.8.12_Source.zip, exist in the Project Library, but raw bytes of those Project files could not be retrieved in this run. They are excluded from the verified source set until obtained.
