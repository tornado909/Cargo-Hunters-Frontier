#!/usr/bin/env python3
"""Install universal repair definitions into the source workshop module.
Frontier.Modular recreates compiled/ at every launch; editing compiled is futile.
This script leaves other modules and saves untouched and keeps rollback copies.
"""
from __future__ import annotations
import argparse, datetime, hashlib, importlib.util, json, pathlib, shutil, tempfile

BASE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("kit_catalog", BASE / "generate.py")
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)

def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def encode(data):
    return (json.dumps(data,ensure_ascii=False,separators=(",",":")) + "\n").encode("utf-8")

def create(game,work):
    compiled = read(work/"prepared"/"60_workshop.json")
    strings = read(work/"prepared"/"localization.json")
    ops = [x for x in compiled["operations"] if x["operationId"].startswith("workshop.universal.")]
    if len(ops)!=3:raise ValueError("Expected exactly three prepared operations")
    package = game/"BepInEx"/"plugins"/"FrontierPack"/"modules"/"workshop"
    original = read(package/"operations.json")
    manifest = read(package/"module.json")
    locale = read(package/"localization.json")
    if len(original)!=96 or manifest["id"]!="workshop" or manifest["version"]!="0.6.0":
        raise ValueError("Unexpected source package version: refusing to replace")
    ids={r["operation"]["operationId"] for r in original}
    if any(x["operationId"] in ids for x in ops):
        raise ValueError("Already patched source or partial mix: refusing")
    next_sort=max(r["sort"] for r in original)+1
    dependencies={
        "item_templates": [],
        "shop_templates":[kit.ID],
        "craft_recipes":[kit.ID]  # raw materials are vanilla or other-module IDs, not definition dependencies
    }
    for x in ops:
        row={
          "bucket":"workshop","sort":next_sort,
          "operation":x,"when":[],"needs":dependencies[x["repository"]],
          "defines":x.get("id") if x["op"]=="clone" else (kit.RECIPE_ID if x["repository"]=="craft_recipes" else None)
        }
        next_sort+=1
        original.append(row)
    for entry in [kit.localization_id(kit.NAME_KEY),kit.localization_id(kit.DESCRIPTION_KEY)]:
        if entry in locale:raise ValueError("Localized key already exists: "+entry)
        locale[entry]=strings[entry]
    manifest["files"]["operations.json"]=hashlib.sha256(encode(original)).hexdigest()
    manifest["files"]["localization.json"]=hashlib.sha256(encode(locale)).hexdigest()
    return {"operations.json":encode(original),
            "localization.json":encode(locale),
            "module.json":encode(manifest)}

def stage(game,work,where):
    if kit.game_running():raise RuntimeError("Game is running")
    files=create(game,work)
    root=game if where=="main" else game/"Frontier-Source"/"Reserve-Work"/"Runtime"
    module=root/"BepInEx"/"plugins"/"FrontierPack"/"modules"/"workshop"
    if not module.is_dir():raise FileNotFoundError(module)
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup=work/"backups"/("source-"+where+"-"+stamp)
    backup.mkdir(parents=True)
    for name in files:shutil.copy2(module/name,backup/name)
    try:
        for name,payload in files.items():
            with tempfile.NamedTemporaryFile(mode="wb",dir=module,delete=False) as tmp:
                tmp.write(payload)
                tmp_path=pathlib.Path(tmp.name)
            tmp_path.replace(module/name)
        actual={name:sha(module/name) for name in files}
        for name in files:
            if actual[name]!=hashlib.sha256(files[name]).hexdigest():
                raise RuntimeError("Post-write SHA mismatch "+name)
        print(json.dumps({"status":"MODULE_SOURCE_INSTALLED","scope":where,
            "backup":str(backup),"filesSha256":actual,"item_id":kit.ID},indent=2))
    except:
        for name in files:shutil.copy2(backup/name,module/name)
        raise

def main():
    cli=argparse.ArgumentParser()
    cli.add_argument("--game",required=True)
    cli.add_argument("--workspace",required=True)
    cli.add_argument("--mode",choices=["prepare","install-test","install-main"],required=True)
    args=cli.parse_args()
    game=pathlib.Path(args.game)
    work=pathlib.Path(args.workspace)
    payload=create(game,work)
    prepared=work/"source-prepared"
    prepared.mkdir(parents=True,exist_ok=True)
    for k,v in payload.items():(prepared/k).write_bytes(v)
    print(json.dumps({"status":"SOURCE_PREPARED",
          "files":{k:hashlib.sha256(v).hexdigest() for k,v in payload.items()},
          "operations":len(read(prepared/"operations.json")),
          "localizedCount":len(read(prepared/"localization.json"))}))
    if args.mode!="prepare":
        stage(game,work,"main" if args.mode=="install-main" else "test")

if __name__=="__main__":main()
