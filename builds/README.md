# Universal Repair Kit 0.1.2 — RED BAR FIX (EXPERIMENTAL)

The previous Universal Repair Kit v0.1.1 could restore current/yellow condition,
but the red segment remained. Server wrote SetMaxValue in a postfix AFTER the
native operation had already constructed ItemDto. A log "MAX_RESTORED" was
not actual client/persistent acceptance.

This test DLL moves SetMaxValue(PerfectValue) into a matching
ConditionComponent.Repair(float) POSTFIX within the native authoritative
RestoreConditionOperation, BEFORE its resulting ItemDto serialization.
It logs both remaining NotPerfectMaxValue and kit consumption rather than
claiming red restored solely on the setter call.

**Status: BUILT/NOT GAMEPLAY VERIFIED.** Installation into the main running game
is intentionally deferred until that game is closed. Use the existing 0.1.2
native category filter package and keep original kit item ID and shop/recipe.
Only the sidecar DLL changes in this ZIP.

ZIP SHA-256: 5ca8c4f69c2432fb272222f4ef38ce22bfdf56a08ecef5f090af35029731834c

Code: https://github.com/tornado909/Cargo-Hunters-Frontier/pull/7
