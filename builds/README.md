# Universal Repair Kit 0.1.5 — native save/reload verified

This supersedes previous universal repair kit 0.1.1/0.1.2 artifacts.

Built on **ASC-AI** using the matching Cargo Hunters IL2CPP references. Game PC was used for an isolated game test and packaging only, not compilation.

Actual isolated game acceptance:
- Existing damaged head `ConditionComponent.Value=0.196754`, maximum `5`.
- `RestoreConditionOperation` on a genuine purchased one-charge universal repair kit: `CanExecute=true`, `operationAccepted=true`.
- Server returned SuccessItemOperationResponse; client showed current/max **5/5**, kit was consumed.
- Native `ExitToMainMenuClickedEventArgs` returned to lobby and `OfflineAccountContextPersistenceService` confirmed successful save.
- `offline.save` changed in the isolated profile; after reentering shelter the head still read **5/5**, the consumed kit remained absent.
- Installed to the *main game* without altering its save; BepInEx loaded Universal Repair Kit 0.1.5 with no errors.

**Not verified:** two-computer multiplayer repair and all categories of equipment. Every peer should have matching plugin set and catalog source modules. Do not call this a multiplayer acceptance.

This ZIP includes 3 validated workshop module source JSON files (with their SHA256 contract), the Universal Repair Kit 0.1.5 sidecar DLL and documentation/manifest. **No saves or private credentials**.

ZIP SHA-256: `9f37a0b91405905d381e28f45d1f25a9f5407c3855fd2f17885c873b11c882f6`.

Source: https://github.com/tornado909/Cargo-Hunters-Frontier/pull/7
