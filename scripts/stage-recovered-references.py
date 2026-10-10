#!/usr/bin/env python3
"""Stage only verified Frontier DLL dependencies in PRIVATE CI reference tree.

Requires scripts/verify-ci-inputs.py to have passed before running.
No assets are published by this script.
"""
from pathlib import Path
from zipfile import ZipFile
import hashlib
import json

root=Path(".ci-private")
inventory=json.loads(Path("docs/recovered-module-manifest.json").read_text(encoding="utf-8"))
info=json.loads((root/"build-inputs.json").read_text(encoding="utf-8"))
assert inventory["schema"]==1 and len(inventory["modules"])==32
assert inventory["game_version"]==info["game_version"]=="0.26.38.59"
pack=root/"pack/production.zip"
output=root/"references/BepInEx/plugins/FrontierPack"
count=0
with ZipFile(pack) as z:
    for row in inventory["modules"]:
        rel=row["installed_dll"]
        archive_path="BepInEx/plugins/FrontierPack/"+rel
        if ".." in Path(rel).parts or not archive_path.endswith(row["assembly"]+".dll"):
            raise SystemExit("Unsafe source dependency "+rel)
        blob=z.read(archive_path)
        if hashlib.sha256(blob).hexdigest()!=row["installed_sha256"]:
            raise SystemExit("Inconsistent source recovery target: "+archive_path)
        file=output/rel
        file.parent.mkdir(parents=True,exist_ok=True)
        file.write_bytes(blob)
        count+=1
assert count==32
print("PASS: staged 32 SHA256-pinned production plugin DLLs as private compile-time references only")
