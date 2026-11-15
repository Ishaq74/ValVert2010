#!/usr/bin/env python3
"""Pin the ROOT fileID of the 20 terrain tree-prototype prefabs.

Why: a terrain stores each tree prototype as (prefab GUID, root-GameObject fileID). Regenerated prefabs (ef_*/uns_*)
get fresh random fileIDs, so on a clean clone every prototype loaded null ("Tree prefab at index N is missing").
The GUIDs are already pinned (guid_pins.py); this pins the fileID too and rewrites the prefab YAML to it, so the
COMMITTED terrain assets resolve on any clone without being modified.

  tree_pins.py export  --project <dir>   read both committed TerrainData (needs `pip install UnityPy`), write the pins
  tree_pins.py apply   [--project <dir>] rewrite each prefab's root GameObject fileID to its pin (text only, idempotent)
  tree_pins.py check   [--project <dir>] pins well-formed and every existing prefab has the pinned guid + root fileID
Only IDs and paths are stored: nothing derived from vendor content.
"""
from __future__ import annotations
import argparse, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PINS = "Assets/Settings/Architecture/terrain_tree_pins.json"
TERRAINS = ["Assets/World/Terrain/ValVertTerrain.asset", "Assets/World/Terrain/ValVertTerrainEast.asset"]
# Prototype order of the terrains (ValVertDress.RepairTreePrototypes defines the same order).
NAMES = ["ef_slot_autumnA", "uns_spruce_autumnA", "ef_slot_autumnB", "uns_spruce_autumnB", "ef_slot_green", "uns_spruce_green",
         "uns_bush_a", "uns_bush_c", "fern_b", "fern_c", "laurel_cc0",
         "ef_ash_1", "ef_ash_4", "ef_ash_5", "ef_ash_8", "ef_chestnut_3", "ef_chestnut_4", "ef_chestnut_8", "ef_birch_2", "ef_birch_6"]
DIRS = ["Assets/World/Vegetation/Prefabs", "Assets/World/Prefabs"]
GUID = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.M)
DOC = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)

def prefab_path(project: Path, name: str):
    for d in DIRS:
        p = project / d / f"{name}.prefab"
        if p.is_file() or Path(str(p) + ".meta").is_file(): return p
    return None

def meta_guid(p: Path):
    m = Path(str(p) + ".meta")
    g = GUID.search(m.read_text(errors="replace")) if m.is_file() else None
    return g.group(1) if g else None

def root_file_id(text: str):
    """fileID of the root GameObject: the GameObject owning the Transform/RectTransform whose m_Father is 0."""
    docs = list(DOC.finditer(text))
    for i, d in enumerate(docs):
        if d.group(1) not in ("4", "224"): continue
        body = text[d.end(): docs[i + 1].start() if i + 1 < len(docs) else len(text)]
        if re.search(r"^\s*m_Father: \{fileID: 0\}", body, re.M):
            g = re.search(r"^\s*m_GameObject: \{fileID: (\d+)\}", body, re.M)
            if g: return int(g.group(1))
    return None

def retarget(text: str, new: int) -> str:
    old = root_file_id(text)
    if old is None: raise ValueError("no root transform found")
    if old == new: return text
    return re.sub(r"(?<!\d)%d(?!\d)" % old, str(new), text)

def load(project: Path):
    return json.loads((project / PINS).read_text())

def validate(d: dict) -> list[str]:
    errs, seen = [], set()
    pr = d.get("prototypes", [])
    if [p.get("name") for p in pr] != NAMES: errs.append("prototype names/order differ from NAMES")
    for p in pr:
        if not re.fullmatch(r"[0-9a-f]{32}", str(p.get("guid"))): errs.append(f"bad guid {p.get('name')}")
        if not isinstance(p.get("rootFileId"), int) or p["rootFileId"] <= 0: errs.append(f"bad rootFileId {p.get('name')}")
        if p.get("guid") in seen: errs.append(f"duplicate guid {p.get('guid')}")
        seen.add(p.get("guid"))
    return errs

def check(project: Path) -> list[str]:
    d = load(project); errs = validate(d)
    for p in d["prototypes"]:
        f = prefab_path(project, p["name"])
        if f is None or not f.is_file(): continue            # derived prefab not generated yet: apply will fix it
        g = meta_guid(f)
        if g != p["guid"]: errs.append(f"{p['name']}: guid {g} != pin {p['guid']}")
        r = root_file_id(f.read_text(encoding="utf-8", errors="replace"))
        if r != p["rootFileId"]: errs.append(f"{p['name']}: root fileID {r} != pin {p['rootFileId']}")
    return errs

def apply(project: Path) -> int:
    d = load(project); n = 0; missing = []
    for p in d["prototypes"]:
        f = prefab_path(project, p["name"])
        if f is None or not f.is_file(): missing.append(p["name"]); continue
        t = f.read_text(encoding="utf-8")
        t2 = retarget(t, p["rootFileId"])
        if t2 != t: f.write_text(t2, encoding="utf-8", newline="\n"); n += 1
    print(f"TREE_PINS_APPLIED rewritten={n} missing={len(missing)} {' '.join(missing)}")
    return 1 if missing else 0

def _guidstr(b: bytes) -> str:
    return "".join("%02x" % (((x & 0xF) << 4) | (x >> 4)) for x in b)

def terrain_protos(path: Path):
    import UnityPy
    env = UnityPy.load(str(path)); f = list(env.files.values())[0]
    ext = [_guidstr(e.guid) for e in f.externals]
    td = [o for o in env.objects if o.type.name == "TerrainData"][0].read_typetree()
    return [(ext[p["prefab"]["m_FileID"] - 1], p["prefab"]["m_PathID"]) for p in td["m_DetailDatabase"]["m_TreePrototypes"]]

def export(project: Path) -> int:
    per = [terrain_protos(project / t) for t in TERRAINS]
    if any(x != per[0] for x in per): print("FAIL terrains disagree on prototypes"); return 2
    if len(per[0]) != len(NAMES): print("FAIL prototype count", len(per[0])); return 2
    out = []
    for name, (g, fid) in zip(NAMES, per[0]):
        f = prefab_path(project, name)
        mg = meta_guid(f) if f else None
        if mg and mg != g: print(f"FAIL {name}: prefab guid {mg} != terrain {g}"); return 2
        out.append({"name": name, "guid": g, "rootFileId": fid})
    (project / PINS).write_text(json.dumps({"terrains": TERRAINS, "prototypes": out}, indent=1) + "\n")
    print("TREE_PINS_EXPORTED", len(out)); return 0

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["export", "apply", "check"]); ap.add_argument("--project", type=Path, default=ROOT)
    a = ap.parse_args()
    if a.cmd == "export": return export(a.project)
    if a.cmd == "apply": return apply(a.project)
    errs = check(a.project)
    for e in errs: print("FAIL", e)
    print("TREE_PINS_OK" if not errs else "TREE_PINS_FAIL"); return 1 if errs else 0

if __name__ == "__main__":
    sys.exit(main())
