#!/usr/bin/env python3
"""Prepare PRIVATE CI reference/package inputs; only encrypted output can be published."""
import argparse,hashlib,json,tarfile,zipfile,io
from pathlib import Path
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
a=argparse.ArgumentParser()
for k in ('pack','refs','output'):a.add_argument('--'+k,type=Path,required=True)
a.add_argument('--baseline',required=True)
v=a.parse_args()
with zipfile.ZipFile(v.pack) as z:
 names=set(z.namelist());manifest=json.loads(z.read('snapshot-manifest.json'))
 assert manifest['game_version']=='0.26.38.59' and len(manifest['files'])>200
 for f in manifest['files']:
  assert f['path'] in names and hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256']
 assert len(manifest['files'])==len(names)-1
 assert not any('/compiled/' in p or 'CargoHuntersTeammateFlags' in p for p in names)
refs=sorted(p for directory in ('core','interop') for p in (v.refs/directory).glob('*.dll'))
assert len(refs)>60
digest={p.relative_to(v.refs).as_posix():sha(p) for p in refs}
for n in ('interop/Game.dll','interop/Game.Shared.dll','interop/Core.Shared.Utils.dll','core/BepInEx.Core.dll'):
 assert n in digest
data={'schema':1,'game_version':manifest['game_version'],'snapshot_sha256':sha(v.pack),
 'snapshot_files':len(manifest['files']),'source_baseline_commit':v.baseline,'reference_hashes':digest}
v.output.parent.mkdir(parents=True,exist_ok=True)
with tarfile.open(v.output,'w:gz',compresslevel=6) as tar:
 payload=json.dumps(data,sort_keys=True).encode()
 ti=tarfile.TarInfo('build-inputs.json');ti.size=len(payload)
 tar.addfile(ti,io.BytesIO(payload))
 for p in refs:tar.add(p,arcname='references/BepInEx/'+p.relative_to(v.refs).as_posix(),recursive=False)
 tar.add(v.pack,arcname='pack/production.zip',recursive=False)
print(json.dumps({'status':'READY','reference_dlls':len(refs),'pack_files':len(manifest['files']),
 'bundle_size_bytes':v.output.stat().st_size,'sha256':sha(v.output),'baseline':v.baseline}))
