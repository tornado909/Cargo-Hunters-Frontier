#!/usr/bin/env python3
"""Create a BepInEx-rooted install ZIP from a verified pack and three source builds."""
from __future__ import annotations
import argparse,hashlib,json,re,zipfile
from datetime import datetime,timezone
from pathlib import Path

SOURCES={
 'FrontierSurvivorExitGuard/Frontier.SurvivorExitGuard.dll':
  'src/Plugins/SurvivorExitGuard/bin/Release/net6.0/Frontier.SurvivorExitGuard.dll',
 'FrontierSurvivorWearGuard/Frontier.SurvivorWearGuard.dll':
  'src/Plugins/SurvivorWearGuard/bin/Release/net6.0/Frontier.SurvivorWearGuard.dll',
 'FrontierUniversalRepairKit/Frontier.UniversalRepairKit.dll':
  'src/Plugins/UniversalRepairKit/bin/Release/net6.0/Frontier.UniversalRepairKit.dll',
}
def sha(data):return hashlib.sha256(data).hexdigest()
a=argparse.ArgumentParser()
a.add_argument('--input-dir',type=Path,default=Path('.ci-private'))
a.add_argument('--out-dir',type=Path,default=Path('dist'))
a.add_argument('--version',required=True)
a.add_argument('--commit',required=True)
args=a.parse_args()
if not re.fullmatch(r'(v[0-9A-Za-z._-]+|manual-[0-9a-f]{7,40})',args.version):
 raise SystemExit('Unsafe version string')
info=json.loads((args.input_dir/'build-inputs.json').read_text())
with zipfile.ZipFile(args.input_dir/'pack'/'production.zip') as initial:
 file_manifest=json.loads(initial.read('snapshot-manifest.json'))
 inputs={f['path']:initial.read(f['path']) for f in file_manifest['files']}
expected={'BepInEx/plugins/'+rel:'%s'%path for rel,path in SOURCES.items()}
if not set(expected).issubset(inputs):
 raise SystemExit('Production snapshot lacks one of the three source-build plugins')
replacements={}
for location,project_output in expected.items():
 p=Path(project_output)
 if not p.is_file():raise SystemExit('Built DLL missing: '+str(p))
 value=p.read_bytes()
 if len(value)<3000 or value[:2]!=b'MZ':
  raise SystemExit('Invalid compiled plugin assembly: '+str(p))
 inputs[location]=value
 replacements[location]=sha(value)
expected_dirs=('BepInEx/plugins/FrontierPack/',
 'BepInEx/plugins/FrontierSurvivorExitGuard/',
 'BepInEx/plugins/FrontierSurvivorWearGuard/',
 'BepInEx/plugins/FrontierUniversalRepairKit/')
if not all(any(p.startswith(prefix) for p in inputs) for prefix in expected_dirs):
 raise SystemExit('Missing core plugin folders')
if any('CargoHuntersTeammateFlags' in p for p in inputs):
 raise SystemExit('Teammate Flags must remain a separate project')
prebuilt_dlls=sorted(p for p in inputs if p.endswith('.dll') and p not in replacements)
if len(prebuilt_dlls)!=32:
 raise SystemExit('Unexpected prebuilt DLL count %d'%len(prebuilt_dlls))
args.out_dir.mkdir(parents=True,exist_ok=True)
release_name=f'Cargo-Hunters-Frontier-{args.version}.zip'
zip_path=args.out_dir/release_name
with zipfile.ZipFile(zip_path,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as archive:
 for name,content in sorted(inputs.items()):
  if not name.startswith('BepInEx/plugins/') or '..' in Path(name).parts:
   raise SystemExit('Refusing non-BepInEx package entry: '+name)
  entry=zipfile.ZipInfo(name,date_time=(2026,10,11,0,0,0))
  entry.compress_type=zipfile.ZIP_DEFLATED
  entry.external_attr=(0o644<<16)
  archive.writestr(entry,content,compress_type=zipfile.ZIP_DEFLATED,compresslevel=6)
# Re-read release ZIP and verify exact expected file bytes.
with zipfile.ZipFile(zip_path) as package:
 assert set(package.namelist())==set(inputs)
 for name,value in inputs.items():
  if sha(package.read(name))!=sha(value):raise SystemExit('Written archive mismatch: '+name)
details={
 'format':1,'version':args.version,'git_commit':args.commit,'game_version':info['game_version'],
 'source_baseline_commit':info['source_baseline_commit'],
 'source_compiled_dlls':replacements,
 'included_verified_prebuilt_dlls':len(prebuilt_dlls),
 'included_prebuilt_names':prebuilt_dlls,
 'snapshot_sha256':info['snapshot_sha256'],'archive_sha256':sha(zip_path.read_bytes()),
 'archive_size_bytes':zip_path.stat().st_size,'total_package_files':len(inputs),
 'note':'3 DLLs compiled in Actions; remaining 32 DLLs are byte-verified production snapshot binaries, not claimed to be rebuilt',
 'build_timestamp_utc':datetime.now(timezone.utc).isoformat()
}
manifest_path=args.out_dir/'BUILD-INFO.json'
manifest_path.write_text(json.dumps(details,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
sha_file=args.out_dir/'SHA256SUMS.txt'
sha_file.write_text(
 sha(zip_path.read_bytes())+'  '+release_name+'\n'+
 sha(manifest_path.read_bytes())+'  BUILD-INFO.json\n',encoding='utf-8')
print(json.dumps({'status':'PACKAGE_VERIFIED','zip':str(zip_path),'files':len(inputs),
 'built':len(replacements),'verified_prebuilt':len(prebuilt_dlls),
 'bytes':zip_path.stat().st_size,'sha256':details['archive_sha256']}))
