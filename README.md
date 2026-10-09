# Cargo Hunters Modded — Frontier

Private source repository for the Cargo Hunters Frontier plugin pack.

## Project status
Source migration is in progress. The files imported from the existing development
workspace are **not** automatically production-ready or identical to every DLL
currently installed. The existing game install and saves are the reference baseline.

## Rules
- Builds take place on GitHub Actions or on a separate build host, **not** the game PC.
- Never commit user saves, Steam tokens, private profiles, binaries extracted from
  Cargo Hunters, game assets, game-generated caches, or crash dumps.
- Commit source and tests in reviewable groups. Never replace a working game DLL
  without a verified artifact, rollback package and explicit acceptance.
- Multiplayer must keep exact pack signatures in sync across host and guest.
- Teammate Flags is maintained separately:
  https://github.com/QueenDekim/Cargo-Hunters-Teammate-Flags

## Current tracked work
1. Survivor equipment durability: investigate and isolate actual Condition decreases
   before applying any Survivor-only protection for both host and guest.
2. Nanorepair: distinguish healing actual body-part damage from repairing item durability;
   verify skill progression and translated display name.
3. Universal 5,000-credit condition-restoration kit with vanilla authoritative
   RestoreConditionOperation; no save file edits.
4. Maintain and improve multiplayer regression tests and pack fingerprint parity.

## Building
The source projects target game-specific IL2CPP interop assemblies. Those
proprietary generated reference assemblies are intentionally excluded from Git.
Do not claim a binary plugin build succeeded until the separate build host or CI
has access to authorized matching game references. The initial GitHub workflow
performs source/repository checks and independent pure-code tests; native/game
acceptance is a separate step.
