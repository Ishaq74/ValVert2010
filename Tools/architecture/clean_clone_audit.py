#!/usr/bin/env python3
"""Clean-clone completeness audit.

Question answered: which assets does the committed scene reference (transitively) that a fresh checkout does NOT contain,
and for each one, how does a fresh clone get it back?

  python3 Tools/architecture/clean_clone_audit.py --tree <checkout-or-clone-dir> [--full <complete-local-project>] [--json out.json] [--strict]

References are followed from Assets/Scenes/ValVert.unity through YAML assets (text GUIDs) AND binary assets (TerrainData,
meshes: 16-byte nibble-swapped GUIDs, found with a vectorised scan), so terrain tree prototypes etc. are covered.
A reference is MISSING when its GUID has no .meta in --tree, or the .meta exists but the asset file does not (a tracked meta of
an ignored binary: Unity deletes such orphan metas on first import and the reference silently breaks).

Classes (how a clone recovers):
  package          Unity package script/material: resolved by UPM at import (needs Library/PackageCache, not a defect)
  rebuild-python   Tools/rebuild producer, byte-proven vs expected_baselines.json     -> rebuild.py rebuild-proven --apply
  rebuild-unity    Unity generator + GUID pins (generated_guid_pins.json)             -> Tools/rebuild/restore_world.py
  vendor           Asset Store EULA content, never committed                           -> documented import (vendor_manifest.json)
  tracked-lfs      must be in the repo (LFS); absent only because it was never committed/ignored  => DEFECT if reported
  frozen-exception already dangling in every checkout (cars/Aldi, FROZEN by owner decision); allow-listed in clean_clone_expected.json
  unresolved       unknown even in --full: new dangling reference => DEFECT

Exit status: 0 = every missing GUID is accounted for by a documented recovery step; 2 = at least one DEFECT
(tracked-lfs / unresolved / unclassified) or, with --strict, anything at all is missing.
"""
from __future__ import annotations
import argparse, collections, fnmatch, json, re, sys
from pathlib import Path

try:
    import numpy as np
except ImportError:  # binary scan degrades to a slow pure-python search
    np = None

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
BUILTIN = re.compile(r"^(0{16}|f{16})")
YAML_EXT = {".unity", ".prefab", ".mat", ".asset", ".controller", ".anim", ".overrideController", ".physicMaterial", ".physicsMaterial2D",
            ".renderTexture", ".flare", ".lighting", ".playable", ".mixer", ".terrainlayer", ".shadervariants", ".preset"}
SCENE = "Assets/Scenes/ValVert.unity"
BIN_MAX = 120 * 1024 * 1024

def meta_guid(meta: Path):
    try:
        for line in meta.read_text(errors="replace").splitlines():
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return None

def index(root: Path, packages: bool = True):
    """guid -> relative asset path; and the set of guids whose asset exists on disk."""
    g2p, present = {}, set()
    roots = [root / "Assets"]
    if packages:
        roots += [root / "Packages", root / "Library" / "PackageCache"]
    for base in roots:
        if not base.is_dir():
            continue
        for m in base.rglob("*.meta"):
            g = meta_guid(m)
            if not g:
                continue
            a = m.with_suffix("")
            rel = a.relative_to(root).as_posix()
            g2p.setdefault(g, rel)
            if a.exists():
                present.add(g)
    return g2p, present

def yaml_refs(path: Path):
    return set(GUID_RE.findall(path.read_text(errors="replace")))

def swapped(g: str) -> bytes:
    return bytes.fromhex("".join(g[i + 1] + g[i] for i in range(0, 32, 2)))

class BinScanner:
    """Finds known GUIDs inside binary serialized assets (external-reference table stores them nibble-swapped)."""
    def __init__(self, guids):
        self.map = {swapped(g): g for g in guids}
        self.pref = {k[:4] for k in self.map}
        self.pref_arr = np.array(sorted(int.from_bytes(p, "little") for p in self.pref), dtype=np.uint32) if np is not None else None
    def scan(self, data: bytes):
        out = set()
        if np is None:
            for k, g in self.map.items():
                if k in data: out.add(g)
            return out
        a = np.frombuffer(data, dtype=np.uint8)
        if a.size < 16: return out
        n = a.size - 15
        w = (a[:n].astype(np.uint32) | (a[1:n + 1].astype(np.uint32) << 8) | (a[2:n + 2].astype(np.uint32) << 16) | (a[3:n + 3].astype(np.uint32) << 24))
        for pos in np.nonzero(np.isin(w, self.pref_arr))[0]:
            g = self.map.get(data[pos:pos + 16])
            if g: out.add(g)
        return out

def closure(tree: Path, g2p, present, universe):
    seen, missing = set(), collections.defaultdict(set)
    scanner = BinScanner(universe) if universe else None
    queue = collections.deque([SCENE]); visited = set()
    while queue:
        rel = queue.popleft()
        if rel in visited: continue
        visited.add(rel)
        p = tree / rel
        if not p.is_file(): continue
        refs = set()
        with p.open("rb") as f: head = f.read(16)
        if head.startswith(b"%YAML"):
            refs = yaml_refs(p)
        elif p.suffix in (".asset", ".terrainlayer") and p.stat().st_size <= BIN_MAX and scanner:
            refs = scanner.scan(p.read_bytes())
        for g in refs:
            if BUILTIN.match(g): continue
            if g in g2p and g in present:
                if g not in seen:
                    seen.add(g); queue.append(g2p[g])
            else:
                missing[g].add(rel)
    return seen, missing

def load_json(p: Path, default):
    return json.loads(p.read_text()) if p.is_file() else default

def classify(path, g, ctx) -> str:
    if g in ctx["expected_dangling"]:
        return "frozen-exception"
    if path is None:
        return "unresolved"
    if path.startswith(("Packages/", "Library/PackageCache/")):
        return "package"
    for v in ctx["vendor_globs"]:
        if fnmatch.fnmatch(path, v):
            return "vendor"
    if path in ctx["rebuild_python"]:
        return "rebuild-python"
    if path in ctx["pins"]:
        return "rebuild-unity"
    if any(fnmatch.fnmatch(path, pat) for pat in ctx["lfs_globs"]) or path in ctx["tracked_paths"]:
        return "tracked-lfs"
    return "unclassified"

def path_for_guid(g: str, g2p: dict, full_g2p: dict, pinned_paths: dict) -> str | None:
    """A missing generated asset has no .meta to index; its pinned GUID still names it."""
    return g2p.get(g) or full_g2p.get(g) or pinned_paths.get(g)

def incomplete_count(classes: dict[str, list]) -> int:
    """Missing assets that are not the explicitly frozen car-material exceptions."""
    return sum(len(items) for kind, items in classes.items() if kind != "frozen-exception")

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--tree", "--clean", dest="tree", type=Path, required=True)
    ap.add_argument("--full", type=Path, help="complete local project used only to NAME unresolved GUIDs")
    ap.add_argument("--json", type=Path)
    ap.add_argument("--strict", action="store_true")
    ap.add_argument("--require-restored", action="store_true", help="fail unless only the documented frozen exceptions remain missing")
    ap.add_argument("--list", type=int, default=5)
    ap.add_argument("--tracked-list", type=Path, help="file with `git ls-files` of the intended commit (classifies tracked-lfs)")
    a = ap.parse_args()
    tree = a.tree.resolve()
    g2p, present = index(tree)
    full_g2p = index(a.full.resolve())[0] if a.full else {}
    pins = load_json(ROOT / "Assets/Settings/Architecture/generated_guid_pins.json", {"assets": {}})["assets"]
    pinned_paths = {guid: path for path, guid in pins.items()}
    universe = set(g2p) | set(full_g2p) | set(pinned_paths)
    seen, missing = closure(tree, g2p, present, universe)

    rebuild = load_json(ROOT / "Tools/rebuild/manifest.json", {"targets": {}})["targets"]
    rebuild_python = {o["path"] for t in rebuild.values() if t.get("status") == "proven" for o in t.get("outputs", [])}
    vendor = load_json(ROOT / "Tools/rebuild/vendor_manifest.json", {"globs": []})["globs"]
    exp = load_json(HERE / "clean_clone_expected.json", {"frozenExceptions": {}})
    attrs = (ROOT / ".gitattributes").read_text().splitlines() if (ROOT / ".gitattributes").is_file() else []
    lfs_globs = [l.split()[0] for l in attrs if "filter=lfs" in l and l.strip() and not l.startswith("#")]
    tracked = set(a.tracked_list.read_text().split("\n")) if a.tracked_list else set()
    ctx = dict(expected_dangling=set(exp["frozenExceptions"]), vendor_globs=vendor, rebuild_python=rebuild_python, pins=set(pins),
               lfs_globs=lfs_globs, tracked_paths=tracked)

    classes: dict[str, list] = collections.defaultdict(list)
    for g, refs in sorted(missing.items()):
        path = path_for_guid(g, g2p, full_g2p, pinned_paths)
        classes[classify(path, g, ctx)].append({"guid": g, "path": path, "referrers": sorted(refs)[:3]})
    print(f"tree={tree}\nreferenced_and_present={len(seen)} missing_guids={len(missing)}")
    order = ["rebuild-python", "rebuild-unity", "vendor", "package", "frozen-exception", "tracked-lfs", "unresolved", "unclassified"]
    for c in order + [k for k in classes if k not in order]:
        items = classes.get(c, [])
        if not items: continue
        fam = collections.Counter()
        for it in items:
            p = it["path"] or "?"
            d, _, n = p.rpartition("/")
            fam[d + "/" + re.sub(r"\d+", "#", n)] += 1
        print(f"  {len(items):5d}  {c}")
        for f, n in fam.most_common(a.list): print(f"           {n:5d}  {f}")
    defects = sum(len(classes.get(c, [])) for c in ("tracked-lfs", "unresolved", "unclassified"))
    incomplete = incomplete_count(classes)
    if a.json:
        a.json.write_text(json.dumps({"missing": classes, "present": len(seen)}, indent=1))
    if defects or (a.strict and missing) or (a.require_restored and incomplete):
        print(f"AUDIT_FAIL defects={defects} missing={len(missing)} incomplete={incomplete}"); return 2
    print(f"AUDIT_OK missing_but_recoverable={len(missing)}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
