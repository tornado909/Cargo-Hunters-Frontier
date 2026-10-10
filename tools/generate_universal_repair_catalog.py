#!/usr/bin/env python3
"""Generate or deploy Frontier Universal Repair Kit catalogs.

No game/DLL compilation. Reads raw *matching* game catalog source and the
installed Frontier workshop manifest; patches only the existing workshop
module and localization mapping, preserving all other module operations.
"""
from __future__ import annotations
import argparse, copy, datetime, hashlib, json, pathlib, shutil, subprocess, tempfile, uuid

ID = str(uuid.uuid5(uuid.NAMESPACE_URL, "frontier.cargohunters/universal-repair-kit/v1"))
RECIPE_ID = str(uuid.uuid5(uuid.NAMESPACE_URL, "frontier.cargohunters/universal-repair-recipe/v1"))
STOCK_KIT_ID = "0a92b724-46a9-45b9-9837-67f3b2aecf1d"
SHOP_ID = "381e554d-0ab7-4111-9e8c-3710bd05d086"
WORKSHOP_ID = "b8690aac-4720-42b2-98ce-42a31a915757"
CREDIT_ID = "cb567810-cc82-424f-893f-299c704ffb12"
MATERIAL_A = "53f524ce-b36e-52f9-a13c-edab8a187f25"
MATERIAL_B = "5283349a-7e5a-46d0-b242-1bdf9b313ce7"
MANIFEST = "60_workshop.json"
LOCALIZATION = "localization.json"
NAME_KEY = "frontier.universal.repair.name"
DESCRIPTION_KEY = "frontier.universal.repair.description"

def localization_id(key: str) -> str:
    v = int.from_bytes(hashlib.sha256(key.encode("utf-8")).digest()[:8], "big") & ((1 << 63)-1)
    return str(v)

def h(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def read_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def serial(obj):
    return (json.dumps(obj, ensure_ascii=False, separators=(",",":")) + "\n").encode("utf-8")

def find_component(item, tag: int):
    matches=[(idx,c) for idx,c in enumerate(item["_components"]) if c.get("$t")==tag]
    if len(matches)!=1:
        raise ValueError(f"Expected exactly one component tag={tag}, got {len(matches)}")
    return matches[0]

def op(kind, repository, suffix, *, activation="active", **kw):
    return dict(op=kind, repository=repository, activation=activation,
                requires=[], operationId=f"workshop.universal.{suffix}", **kw)

def make_patch(main: pathlib.Path, game: pathlib.Path):
    base=game/"Frontier-Source"/"Reserve-Work"/"EngineOptimization"/"Inputs"
    itemfile=base/"item_templates.json"
    shopfile=base/"shop_templates.json"
    craftfile=base/"craft_recipes.json"
    if not all(f.exists() for f in (itemfile,shopfile,craftfile)):
        raise FileNotFoundError("Matching raw item/shop/recipe fixture not found")
    items=read_json(itemfile)
    stock=[x for x in items if x.get("_id")==STOCK_KIT_ID]
    if len(stock)!=1:raise ValueError("Original Weapon_repair_kit does not match expected ID")
    kit=stock[0]
    indices={t:find_component(kit,t)[0] for t in (26276,4373,51833,49658,28063)}
    price=kit["_components"][indices[51833]]["_data"]["Price"]["Items"]
    if not len(price)==1 or price[0]["ItemTemplateId"]!=CREDIT_ID:
        raise ValueError("Unexpected repair kit currency")
    orig_filter=kit["_components"][indices[49658]]["_data"]["ItemsFilter"]
    if orig_filter.get("$t")!=43446:
        raise ValueError("Original repair filter ABI changed")
    existing={x["_id"] for x in items}
    manifest=read_json(main/MANIFEST)
    locale=read_json(main/LOCALIZATION)
    if manifest.get("id")!="workshop" or manifest.get("schema")!=1:
        raise ValueError("Unexpected Frontier workshop manifest schema")
    previous=[x for x in manifest["operations"] if x.get("operationId","").startswith("workshop.universal.")]
    updating_existing=bool(previous)
    if previous:
        expect={"workshop.universal.00100","workshop.universal.00101","workshop.universal.00102"}
        if len(previous)!=3 or {x.get("operationId") for x in previous}!=expect or ID not in str(previous):
            raise ValueError("Existing partial or foreign repair operations detected")
        # Reconcile a prior fully installed version instead of declaring it
        # up-to-date: compiled/ is regenerated from module source on startup.
        manifest["operations"]=[x for x in manifest["operations"]
            if not x.get("operationId","").startswith("workshop.universal.")]
    if ID in existing or RECIPE_ID in {x["Id"] for x in read_json(craftfile)}:
        raise ValueError("Repair template or recipe ID collision")
    shop=read_json(shopfile)
    if sum(x.get("Id")==SHOP_ID for x in shop)!=1:
        raise ValueError("Starter shop ID mismatch")
    name_id=localization_id(NAME_KEY)
    desc_id=localization_id(DESCRIPTION_KEY)
    for code,expected_key in ((name_id,NAME_KEY),(desc_id,DESCRIPTION_KEY)):
        existing_locale=locale.get(code)
        if existing_locale is not None:
            if not updating_existing or existing_locale.get("key")!=expected_key:
                raise ValueError("Localization ID collision")
            del locale[code]
    # The native root IItemsFilter uses the stock Weapon_repair_kit
    # category policy. A previous version incorrectly AND-ed three
    # filters together, including subcategory 102, so ordinary head
    # parts (category=13, subcategory=32) and guns could NEVER match.
    # Keep precisely one stock-compatible category matcher. The
    # authoritative native repair operation still validates a target.
    filter_obj={
        "$t":43446,"_filterPolicy":0,
        "_filtersList":[
            {"$t":39169,"_filterPolicy":1,
             "_itemCategoryIds":[1,11,9,7,6,13,31,5]}
        ]
    }
    def change(path,value):return {"op":"set","path":path,"value":value}
    item_edits=[
        change("/_id",ID),
        change(f"/_components/{indices[26276]}/Name","Frontier/universal.repair"),
        change(f"/_components/{indices[4373]}/_data/LocalizedName/TableEntryReference",int(name_id)),
        change(f"/_components/{indices[4373]}/_data/LocalizedShortName/TableEntryReference",int(name_id)),
        change(f"/_components/{indices[4373]}/_data/LocalizedDescription/TableEntryReference",int(desc_id)),
        change(f"/_components/{indices[51833]}/_data/Price/Items/0/Count",5000),
        change(f"/_components/{indices[49658]}/_data/RestorePower",100.0),
        change(f"/_components/{indices[49658]}/_data/RestoreDuration",6.0),
        change(f"/_components/{indices[49658]}/_data/ItemsFilter",filter_obj),
        change(f"/_components/{indices[28063]}/_data/MaxDurability",1.0)
    ]
    item_op=op("clone","item_templates","00100",activation="catalog",
               key="_id",source=STOCK_KIT_ID,id=ID,edits=item_edits)
    offer={
        "Id":"Frontier_universal_repair_kit",
        "Count":3,"BuyFromShopPriceCoefficient":1.0,"Chance":1.0,
        "UnlockRequirement":{"$t":32,"_filterPolicy":1,"_minAccountLevel":1},
        "Item":{"$t":23772,"ItemItemplateId":ID,"MinDurability":1,"MaxDurability":1},
        "PositionViewPriority":60
    }
    shop_op=op("edit","shop_templates","00101",key="Id",source=SHOP_ID,
               edits=[{"op":"append","path":"/PricelistTemplate/Commodities","value":offer}])
    recipe={
        "Id":RECIPE_ID,"EditorName":"Frontier/universal.repair",
        "CraftDuration":90,"OrderNumber":955,"ShowInModuleScreen":True,
        "Inputs":[
            {"ItemTemplateId":MATERIAL_A,"Count":2},
            {"ItemTemplateId":MATERIAL_B,"Count":4}
        ],
        "Outputs":[{"ItemTemplateId":ID,"Count":1}],
        "MainBuiltLeveledShelterModule":{"ShelterModuleFoundationId":WORKSHOP_ID,"MinLevel":1},
        "UnlockRequirement":{"$t":20492,"_filterPolicy":1,
             "_shelterModuleFoundationId":WORKSHOP_ID,"_minModuleLevel":1},
        "LocalizedName":{"TableReference":"Items","TableEntryReference":int(name_id)},
        "LocalizedDescription":{"TableReference":"Items","TableEntryReference":int(desc_id)}
    }
    craft_op=op("append","craft_recipes","00102",activation="catalog",value=recipe)
    manifest["operations"].extend([item_op,shop_op,craft_op])
    locale[name_id]={"ru":"Универсальный ремкомплект","en":"Universal Repair Kit","key":NAME_KEY}
    locale[desc_id]={
        "ru":"Восстанавливает текущую и максимальную прочность оружия, брони и частей тела. Одно применение.",
        "en":"Restores current and maximum condition of weapons, armor and body parts. Single use.",
        "key":DESCRIPTION_KEY
    }
    if len({x.get("operationId") for x in manifest["operations"]})!=len(manifest["operations"]):
        raise ValueError("Duplicate workshop operation IDs")
    return manifest,locale,updating_existing

def game_running()->bool:
    r=subprocess.run(["tasklist.exe","/FI","IMAGENAME eq CargoHunters.exe"],
                     capture_output=True,text=True,check=True)
    return "CargoHunters.exe" in r.stdout

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--mode",choices=["prepare","install-test","install-main"],required=True)
    p.add_argument("--game",required=True)
    p.add_argument("--workspace",required=True)
    args=p.parse_args()
    game=pathlib.Path(args.game)
    work=pathlib.Path(args.workspace)
    master=game/"BepInEx"/"plugins"/"FrontierPack"/"compiled"
    if not master.is_dir():raise FileNotFoundError(master)
    manifest,locale,already=make_patch(master,game)
    output=work/"prepared"
    output.mkdir(parents=True,exist_ok=True)
    (output/MANIFEST).write_bytes(serial(manifest))
    (output/LOCALIZATION).write_bytes(serial(locale))
    print(json.dumps({"status":"PREPARED","itemId":ID,"recipeId":RECIPE_ID,
       "operations":len(manifest["operations"]),"alreadyPatched":already,
       "files":{x:h((output/x).read_bytes()) for x in [MANIFEST,LOCALIZATION]}},indent=2))
    if args.mode=="prepare":return
    if game_running():raise RuntimeError("CargoHunters.exe running: cannot install")
    root=game if args.mode=="install-main" else game/"Frontier-Source"/"Reserve-Work"/"Runtime"
    dst=root/"BepInEx"/"plugins"/"FrontierPack"/"compiled"
    if not dst.is_dir():raise FileNotFoundError(dst)
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup=work/"backups"/(args.mode+"-"+stamp)
    backup.mkdir(parents=True,exist_ok=False)
    for name in (MANIFEST,LOCALIZATION):
        shutil.copy2(dst/name,backup/name)
    try:
        for name in (MANIFEST,LOCALIZATION):
            with tempfile.NamedTemporaryFile(mode="wb",dir=dst,delete=False) as tmp:
                tmp.write((output/name).read_bytes())
                temp_name=tmp.name
            pathlib.Path(temp_name).replace(dst/name)
        print(json.dumps({"status":"INSTALLED","scope":args.mode,"backup":str(backup),
          "dstHashes":{x:h((dst/x).read_bytes()) for x in (MANIFEST,LOCALIZATION)}}))
    except Exception:
        for name in (MANIFEST,LOCALIZATION):
            shutil.copy2(backup/name,dst/name)
        raise

if __name__=="__main__":main()
