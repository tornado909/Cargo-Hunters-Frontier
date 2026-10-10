#!/usr/bin/env python3
"""Read-only audit for non-shipping Frontier binary-derived C# candidates."""
from __future__ import annotations
from pathlib import Path, PurePosixPath
import hashlib
import json
import re
root=Path(__file__).resolve().parent.parent
base=root/"src/Recovered"
manifest=json.loads((root/"docs/recovered-module-manifest.json").read_text(encoding="utf-8"))
if manifest.get("schema")!=1 or manifest.get("game_version")!="0.26.38.59":
 raise SystemExit("Wrong recovered-source schema/game version")
if manifest.get("release_replacement_allowed") is not False:
 raise SystemExit("Recovered sources are not approved shipping replacements")
modules=manifest.get("modules",[])
if len(modules)!=32:raise SystemExit(f"Expected 32 DLL source candidates, found {len(modules)}")
names=set()
checks=0
for module in modules:
 name=module["assembly"]
 if not re.fullmatch(r"Frontier\.[a-zA-Z0-9]+",name) or name in names:
  raise SystemExit("Invalid/repeated recovered assembly name")
 names.add(name)
 if module.get("kind")!="binary-derived-compilable-candidate" or module.get("production_replacement_approved") is not False:
  raise SystemExit("Incorrect provenance for "+name)
 if not re.fullmatch(r"[0-9a-f]{64}",module["installed_sha256"]):
  raise SystemExit("Original DLL SHA256 invalid for "+name)
 if module["source_file_count"] != len(module["source_sha256"]):
  raise SystemExit("File manifest count mismatch "+name)
 csprojs=0
 seen=set()
 for rel,sha in module["source_sha256"].items():
  path=PurePosixPath(rel)
  if path.is_absolute() or ".." in path.parts or rel in seen:
   raise SystemExit("Unsafe/duplicate source name: "+rel)
  seen.add(rel)
  if path.suffix not in (".cs",".csproj"):
   raise SystemExit("Source-only import violation: "+rel)
  if path.suffix==".csproj":csprojs+=1
  p=base/name/path
  if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest()!=sha:
   raise SystemExit("SHA mismatch or missing source: "+str(p))
  data=p.read_bytes()
  if any(x in data for x in (b"-----BEGIN PRIVATE KEY-----",b"/home/ascento",b"C:\\Users\\ascento")):
   raise SystemExit("Sensitive/private path in source "+rel)
  checks+=1
 if csprojs!=1:raise SystemExit("Need exactly one .csproj for "+name)
 if not module["installed_dll"].endswith(name+".dll"):
  raise SystemExit("Inconsistent runtime DLL name")
for f in base.rglob("*"):
 if not f.is_file():continue
 if any(part.lower() in ("bin","obj") for part in f.relative_to(base).parts):continue  # local dotnet outputs, ignored by Git
 if f.suffix in (".dll",".zip",".pdb",".exe",".pem",".key"):
  raise SystemExit("Generated/private assembly in public source: "+str(f))
 if f.suffix not in (".cs",".csproj"):
  raise SystemExit("Untracked metadata in recovered source: "+str(f))
 root_relative=f.relative_to(base)
 if not any(root_relative.parts[0]==m["assembly"] and root_relative.relative_to(m["assembly"]).as_posix() in m["source_sha256"] for m in modules):
  raise SystemExit("Unregistered source file: "+str(f))
print(f"PASS: {len(modules)} recovered source candidates, {checks} verified files; not approved for production substitution")
