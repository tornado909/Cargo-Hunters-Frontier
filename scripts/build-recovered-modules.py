#!/usr/bin/env python3
"""Build all 32 Frontier C# recovery candidates and compare public API/versions.

Produces a diagnostic summary only. Does NOT update the game or release artifacts.
"""
import argparse,hashlib,json,os,subprocess
from pathlib import Path

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

parser=argparse.ArgumentParser()
parser.add_argument("--input-dir",type=Path,default=Path(".ci-private"))
parser.add_argument("--audit",type=Path,default=Path("src/Tools/ApiSurfaceAudit/bin/Release/net8.0/ApiSurfaceAudit.dll"))
parser.add_argument("--report",type=Path,default=Path(".ci-private/recovered-audit.json"))
args=parser.parse_args()
inventory=json.loads(Path("docs/recovered-module-manifest.json").read_text(encoding="utf-8"))
if inventory.get("release_replacement_allowed") is not False:
    raise SystemExit("Release gate not locked")
root=Path.cwd()
ref=args.input_dir.resolve()/"references/BepInEx"
out=[]
for row in inventory["modules"]:
    module=row["assembly"]
    csproj=root/"src/Recovered"/module/(module+".csproj")
    target=root/"src/Recovered"/module/"bin/Release/net6.0"/(module+".dll")
    original=ref/"plugins/FrontierPack"/row["installed_dll"]
    if not csproj.is_file() or not original.is_file():
        raise SystemExit("Missing source/project/reference for "+module)
    if sha(original)!=row["installed_sha256"]:
        raise SystemExit("ABI reference differs from expected "+module)
    command=["dotnet","build",str(csproj),"--configuration","Release",
         "-p:GameReferenceRoot="+str(ref),"--nologo","--verbosity","quiet"]
    build=subprocess.run(command,capture_output=True,text=True,timeout=130)
    if build.returncode or not target.is_file():
        print("BUILD FAILED",module)
        print((build.stdout+"\n"+build.stderr)[-5000:])
        raise SystemExit(1)
    check=subprocess.run(["dotnet",str(args.audit.resolve()),str(original),str(target)],
                         capture_output=True,text=True,timeout=40)
    if check.returncode:
        print("PUBLIC API CHECK FAILED",module,check.stdout[-3200:],check.stderr[-1200:])
        raise SystemExit(2)
    rec=json.loads(check.stdout)
    if not rec.get("public_surface_ok"):
        raise SystemExit("Declared public API changed for "+module)
    rebuilt=sha(target)
    output={"assembly":module,"candidate_sha256":rebuilt,
            "baseline_sha256":row["installed_sha256"],
            "api_compatible":True,"version":rec["rebuilt_version"],
            "exact_binary_match":rebuilt==row["installed_sha256"],
            "extra_nonpublic_type_count":len(rec["new_named_types"])}
    out.append(output)
    print("PASS",module,"source build + declared public API",rec["rebuilt_version"],"sha256",rebuilt[:12],flush=True)
assert len(out)==32
report={"status":"RECOVERED_CANDIDATES_BUILD_AND_PUBLIC_API_PASS",
    "game_version":inventory["game_version"],
    "modules":out,"compiled_count":len(out),
    "public_api_pass":sum(x["api_compatible"] for x in out),
    "exact_binary_matches":sum(x["exact_binary_match"] for x in out),
    "shipping_approved":False}
args.report.parent.mkdir(parents=True,exist_ok=True)
args.report.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print("RECOVERED_BUILD_VERIFIED",len(out),"/32; public APIs matched; binaries NOT approved for game/release")
