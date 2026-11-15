#!/usr/bin/env python3
"""GUID pins for GENERATED (git-ignored) assets the committed scene/terrain reference.

  guid_pins.py export --project <full-project-dir>   # (re)write generated_guid_pins.json from the project's .metas
  guid_pins.py check  [--project <dir>]              # pins well-formed; with --project: every pinned asset exists with that GUID

The pins file is the contract between the committed scene (which stores GUIDs) and the regeneration step
(ValVertGeneratedAssets.RegenerateAll rewrites each regenerated asset's .meta guid back to its pin).
Only GUIDs/paths are stored — nothing derived from vendor content.
"""
from __future__ import annotations
import argparse, fnmatch, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PINS = ROOT / "Assets/Settings/Architecture/generated_guid_pins.json"
PATTERNS = [
    "Assets/Generated/grass_*.asset",                 # field grass chunks (ValVertGrassField, seed 5)
    "Assets/World/Prefabs/ef_*.prefab",               # European Forests variants (vendor-derived, ignored)
    "Assets/World/Prefabs/uns_*.prefab",              # Ultimate Nature Starter variants (vendor-derived, ignored)
    "Assets/ThirdParty/ValVertGrass/grass_pack_*.mat",  # pack-derived grass materials (ignored)
    "Assets/Generated/flowers_*.asset",               # field flower chunks (ValVertFlowerField, seed 11)
    "Assets/ThirdParty/ValVertGrass/flower_pack.mat",  # pack-derived flower material (ignored)
]
GUID = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.M)

def meta_guid(meta: Path):
    m = GUID.search(meta.read_text(errors="replace")) if meta.is_file() else None
    return m.group(1) if m else None

def referenced_guids(project: Path) -> set:
    """GUIDs the committed scene reaches (transitively, YAML + binary assets) inside the complete project."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import clean_clone_audit as A
    g2p, present = A.index(project)
    seen, missing = A.closure(project, g2p, present, set(g2p))
    return seen | set(missing)

def export(project: Path) -> dict:
    out = {}
    ref = referenced_guids(project)   # unreferenced stale leftovers are NOT pinned (regeneration would never recreate them)
    for pat in PATTERNS:
        for p in sorted(project.glob(pat)):
            g = meta_guid(Path(str(p) + ".meta"))
            if g and g in ref: out[p.relative_to(project).as_posix()] = g
    return out

def load() -> dict:
    return json.loads(PINS.read_text())

def validate(d: dict) -> list[str]:
    errs = []
    a = d.get("assets")
    if not isinstance(a, dict) or not a: return ["no assets"]
    seen = {}
    for path, g in a.items():
        if not re.fullmatch(r"[0-9a-f]{32}", str(g)): errs.append(f"bad guid for {path}")
        if not any(fnmatch.fnmatch(path, p) for p in PATTERNS): errs.append(f"{path} matches no pin pattern")
        if g in seen: errs.append(f"guid {g} pinned twice: {seen[g]} and {path}")
        seen[g] = path
    return errs

def main() -> int:
    ap = argparse.ArgumentParser(); ap.add_argument("cmd", choices=["export", "check"]); ap.add_argument("--project", type=Path)
    a = ap.parse_args()
    if a.cmd == "export":
        if not a.project: ap.error("--project required")
        assets = export(a.project.resolve())
        PINS.write_text(json.dumps({"schema": 1, "note": "path -> guid the committed scene/terrain reference; regenerated assets get these GUIDs back (ValVertGeneratedAssets)",
                                    "patterns": PATTERNS, "assets": assets}, indent=1) + "\n")
        print(f"exported {len(assets)} pins"); return 0
    d = load(); errs = validate(d)
    if a.project:
        for path, g in d["assets"].items():
            got = meta_guid(Path(a.project) / (path + ".meta"))
            if got != g: errs.append(f"{path}: project guid {got} != pin {g}")
    for e in errs[:30]: print("FAIL", e)
    if errs: return 2
    print(f"PINS_OK {len(d['assets'])}"); return 0

if __name__ == "__main__":
    sys.exit(main())
