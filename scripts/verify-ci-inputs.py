#!/usr/bin/env python3
"""Fail-closed verification of decrypted CI inputs (not shipped to Releases)."""
from __future__ import annotations
import argparse,hashlib,json,subprocess,zipfile
from pathlib import Path

def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for chunk in iter(lambda:f.read(1048576),b''):h.update(chunk)
 return h.hexdigest()

a=argparse.ArgumentParser()
a.add_argument('--input-dir',type=Path,default=Path('.ci-private'))
args=a.parse_args()
root=args.input_dir
info=json.loads((root/'build-inputs.json').read_text())
if info.get('schema')!=1 or info.get('game_version')!='0.26.38.59':
 raise SystemExit('Unsupported game version or CI inputs schema')
baseline=info['source_baseline_commit']
if len(baseline)!=40 or any(c not in '0123456789abcdef' for c in baseline):
 raise SystemExit('Invalid source commit pin')
# No release should use new production source that is not the verified DLL snapshot.
scope=['src/Engine','src/Reserve','src/Survivor','reconstruction']
cmd=['git','diff','--quiet',baseline,'HEAD','--']+scope
if subprocess.run(cmd,check=False).returncode!=0:
 raise SystemExit('Unrebuilt production source differs from pinned binary snapshot; release blocked')
refs=root/'references'/'BepInEx'
expected=info['reference_hashes']
if len(expected)<60:raise SystemExit('Incomplete IL2CPP references')
for filename,digest in expected.items():
 p=refs/filename
 if not p.is_file() or sha(p)!=digest:raise SystemExit('IL2CPP ABI reference mismatch: '+filename)
pack=root/'pack'/'production.zip'
if sha(pack)!=info['snapshot_sha256']:
 raise SystemExit('Verified production snapshot hash mismatch')
with zipfile.ZipFile(pack) as z:
 members=z.namelist()
 manifest=json.loads(z.read('snapshot-manifest.json'))
 if manifest['game_version']!=info['game_version'] or len(manifest['files'])!=info['snapshot_files']:
  raise SystemExit('Snapshot metadata mismatch')
 if set(members)!={f['path'] for f in manifest['files']}|{'snapshot-manifest.json'}:
  raise SystemExit('Snapshot files missing/added')
 for f in manifest['files']:
  name=f['path']
  if not name.startswith('BepInEx/plugins/') or '..' in Path(name).parts:
   raise SystemExit('Unsafe snapshot member: '+name)
  raw=z.read(name)
  if len(raw)!=f['size'] or hashlib.sha256(raw).hexdigest()!=f['sha256']:
   raise SystemExit('Snapshot file integrity mismatch: '+name)
 modules=[f for f in manifest['files'] if f['path'].endswith('/module.json')]
 verified=0
 for f in modules:
  module_path=Path(f['path'])
  record=json.loads(z.read(f['path']))
  for rel,digest in record.get('files',{}).items():
   member=str(module_path.parent/rel).replace('\\','/')
   if member not in members or hashlib.sha256(z.read(member)).hexdigest()!=digest:
    raise SystemExit('Frontier module manifest mismatch: '+member)
   verified+=1
 if len(modules)<20 or verified<150:raise SystemExit('Incomplete Frontier plugin pack')
print('PASS game',info['game_version'],'refs',len(expected),'snapshot',len(manifest['files']),'modules',len(modules),'checked',verified)
