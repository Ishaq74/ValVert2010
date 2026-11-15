#!/usr/bin/env python3
"""Fresh clone -> the same world.  One documented command, every step gated.

  python3 Tools/rebuild/restore_world.py --packages-dir <dir with the vendor .unitypackage files> --unity <Unity executable>
        [--grass-pack <path to 'Grass Flowers Pack Free.unitypackage'>] [--skip-unity-import] [--check-only]

Steps (see Docs/clean-clone.md):
  1  python environment preflight + `rebuild.py rebuild-proven --apply`   (byte-proven textures: parking, signs, asphalt)
  2  vendor packages: SHA256-verified against Tools/rebuild/vendor_manifest.json, then imported
       - Unity `-importPackage` (UNS, European Forests): GUIDs come from the package
       - extract-remap (Grass Flowers Pack -> Assets/ThirdParty/...): GUIDs come from the package's own .meta files
  3  vegetation preflight (pack card GUIDs == config)
  4  Unity: ValVertGeneratedAssets.RegenerateAll  (vendor prefabs + field grass), then pins the GUIDs the scene references
  5  gate: clean_clone_audit.py on the resulting tree must report AUDIT_OK (only known-dangling frozen car materials remain)
Exit 0 only if every step and the final audit pass. Large LFS assets (meshes/terrain) come with `git lfs pull`, not from here.
"""
from __future__ import annotations
import argparse, hashlib, json, os, shutil, subprocess, sys, tarfile, tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VENDOR = json.loads((ROOT / "Tools/rebuild/vendor_manifest.json").read_text())

def run(cmd, **kw):
    print("+", " ".join(str(c) for c in cmd), flush=True)
    return subprocess.run([str(c) for c in cmd], cwd=ROOT, **kw)

def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with p.open("rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()

def extract_remap(pkg: Path, remap: list[str], dest_root: Path):
    """Write every asset of a .unitypackage under dest_root using its pathname (prefix remapped) and its own .meta."""
    src, dst = remap
    n = 0
    with tarfile.open(pkg) as tf, tempfile.TemporaryDirectory() as td:
        tf.extractall(td, filter="data")
        for d in Path(td).iterdir():
            pn = d / "pathname"
            if not pn.is_file():
                continue
            rel = pn.read_text().splitlines()[0].strip()
            if rel.startswith(src):
                rel = dst + rel[len(src):]
            if not (d / "asset").is_file():
                continue          # folder entry
            out = dest_root / rel
            out.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(d / "asset", out)
            if (d / "asset.meta").is_file():
                shutil.copy2(d / "asset.meta", str(out) + ".meta")
            n += 1
    print(f"extracted {n} files from {pkg.name}")

def step(msg):
    print(f"\n=== {msg}", flush=True)

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--packages-dir", type=Path)
    ap.add_argument("--grass-pack", type=Path)
    ap.add_argument("--unity", default=os.environ.get("UNITY_PATH"))
    ap.add_argument("--skip-unity-import", action="store_true", help="vendor dirs already imported")
    ap.add_argument("--check-only", action="store_true", help="only run the final audit")
    ap.add_argument("--log-dir", type=Path, default=Path("/tmp/vv_restore"))
    a = ap.parse_args()
    a.log_dir.mkdir(parents=True, exist_ok=True)

    if not a.check_only:
        step("1/5 python-proven binaries")
        if run([sys.executable, "Tools/rebuild/rebuild.py", "preflight"]).returncode != 0: return 11
        if run([sys.executable, "Tools/rebuild/rebuild.py", "rebuild-proven", "--apply"]).returncode != 0: return 12

        step("2/5 vendor packages")
        for pk in VENDOR["packages"]:
            if (ROOT / pk["marker"]).is_dir() and any((ROOT / pk["marker"]).rglob("*.meta")):
                print(f"{pk['id']}: already present ({pk['marker']})"); continue
            f = a.grass_pack if (pk["id"] == "GRASS" and a.grass_pack) else (a.packages_dir / pk["file"] if a.packages_dir else None)
            if not f or not f.is_file():
                print(f"MISSING package {pk['id']}: {pk['name']}\n  expected file: {pk['file']} (sha256 {pk['sha256']})"); return 20
            if sha256(f) != pk["sha256"]:
                print(f"SHA256 MISMATCH for {f}: not the verified package"); return 21
            if pk["method"] == "extract-remap":
                extract_remap(f, pk["remap"], ROOT)
            else:
                if not a.unity: print("--unity required to import " + pk["id"]); return 22
                if a.skip_unity_import: print("skip import " + pk["id"]); continue
                r = run([a.unity, "-batchmode", "-nographics", "-quit", "-projectPath", ROOT, "-importPackage", f, "-logFile", a.log_dir / f"import_{pk['id']}.log"])
                if r.returncode != 0: print(f"import {pk['id']} failed, see {a.log_dir}"); return 23

        step("3/5 vegetation preflight")
        if run([sys.executable, "Tools/vegetation/preflight.py"]).returncode != 0: return 30

        step("4/5 Unity: regenerate generated assets + pin GUIDs")
        if not a.unity: print("--unity required"); return 40
        r = run([a.unity, "-batchmode", "-nographics", "-quit", "-projectPath", ROOT, "-executeMethod", "ValVertGeneratedAssets.RegenerateAll", "-logFile", a.log_dir / "regenerate.log"])
        if r.returncode != 0: print(f"RegenerateAll failed (exit {r.returncode}); see {a.log_dir / 'regenerate.log'}"); return 41

    step("5/5 audit gate")
    r = run([sys.executable, "Tools/architecture/clean_clone_audit.py", "--tree", ROOT])
    return 0 if r.returncode == 0 else 50

if __name__ == "__main__":
    sys.exit(main())
