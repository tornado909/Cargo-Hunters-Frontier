#!/usr/bin/env python3
"""Verify preserved Frontier authored-source SHA-256 provenance, without game refs."""
from __future__ import annotations
import hashlib,json,pathlib,re,sys
root=pathlib.Path(__file__).resolve().parent.parent
manifest=json.loads((root/"docs/source-archive-manifest.json").read_text(encoding="utf-8"))
if manifest.get("schema")!=1:raise SystemExit("Unsupported authored source manifest")
entries=manifest["entries"]
if len(entries)<170:raise SystemExit("Source archive inventory unexpectedly incomplete")
counts={"imported":0,"existing":0}
paths=set()
for item in entries:
 name=item["path"]
 rel=pathlib.PurePosixPath(name)
 if rel.is_absolute() or ".." in rel.parts or name in paths:
  raise SystemExit("Duplicate or unsafe source path: "+name)
 paths.add(name)
 if pathlib.PurePosixPath(name).suffix not in (".cs",".csproj",".props",".targets"):
  raise SystemExit("Non-source file in original inventory: "+name)
 file=root/name
 if not file.is_file():
  raise SystemExit("Source missing: "+name)
 data=file.read_bytes()
 if hashlib.sha256(data).hexdigest()!=item["sha256"] or len(data)!=item["size"]:
  raise SystemExit("Changed authored-source bytes without provenance update: "+name)
 if re.search(rb"(-----BEGIN PRIVATE KEY-----|ghp_[A-Za-z0-9]{28,}|github_pat_[A-Za-z0-9_]{25,})",data):
  raise SystemExit("Potential credential present: "+name)
 if item["newly_imported"] and not name.startswith(("history/authored/","src/Candidates/")):
  raise SystemExit("Unexpected source destination: "+name)
 counts["imported" if item["newly_imported"] else "existing"]+=1
for root_dir in (root/"history/authored",root/"src/Candidates/SurvivorCoop"):
 for file in root_dir.rglob("*"):
  if file.is_file() and file.suffix.lower() in (".dll",".zip",".exe",".torrent",".pdb",".key",".pem",".dmp"):
   raise SystemExit("Forbidden original-source asset: "+str(file))
assert counts=={"imported":95,"existing":79},counts
print("PASS original source snapshot",manifest["source_snapshot_sha256"][:16],
      counts["imported"],"historical/candidate files,",
      counts["existing"],"existing authored sources,",
      len(entries),"SHA256-verified entries")
