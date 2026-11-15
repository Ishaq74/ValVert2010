#!/usr/bin/env python3
"""Fresh clone -> the same world.  One documented command, every step gated.

  python3 Tools/rebuild/restore_world.py --packages-dir <dir with vendor .unitypackage files> --unity <Unity executable>
        --vv-work <dir containing clean/> [--grass-pack <path>] [--derive-inputs-from-terrain]
        [--staging <dir>] [--allow-unpinned-env] [--check-only]

Steps (see Docs/clean-clone.md):
  1  keep baseline-valid Python outputs, or preflight + `rebuild.py rebuild-proven --apply`
  2  vendor packages: SHA256-verified against Tools/rebuild/vendor_manifest.json, then EXTRACTED (GUIDs come from the package;
       oversized unused items such as UNS_HDRI.hdr are excluded: they OOM Unity's import on 16 GB). Unity imports them in step 4.
  3  vegetation preflight (pack card GUIDs == config) + check/derive VV_WORK/clean masks from committed terrain
  4  Unity: ValVertGeneratedAssets.RegenerateAll  (vendor prefabs + field grass), then pins the GUIDs the scene references
  5  gate: clean_clone_audit.py on the resulting tree must report AUDIT_OK (only the frozen-exception car materials remain)
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

def vendor_package_candidates(pk: dict, packages_dir: Path | None, grass_pack: Path | None = None):
    """Return pinned archive choices, preferring the filtered export over a full archive."""
    if pk["id"] == "GRASS" and grass_pack is not None:
        return [(grass_pack, pk["sha256"])]
    if packages_dir is None:
        return []
    choices = [(packages_dir / pk["file"], pk["sha256"])]
    choices.extend((packages_dir / alt["file"], alt["sha256"]) for alt in pk.get("alternatives", []))
    return choices

def required_world_inputs(work_dir: Path) -> list[Path]:
    """External source masks/labels consumed by grass and flower generation."""
    return [work_dir / "clean" / name for name in (
        "veg_excl.png", "canopy_orig.png", "veg_meadow.png", "woods_meshes.json", "label_v3.raw",
    )]

def proven_outputs_already_valid(root: Path = ROOT) -> bool:
    """Avoid re-encoding already restored outputs on hosts with different image codecs."""
    sys.path.insert(0, str(ROOT / "Tools/rebuild"))
    from lib import load_manifest, validate_against_baselines
    manifest = load_manifest()
    ok = True
    for target in manifest["targets"].values():
        if target.get("status") != "proven": continue
        for output in target.get("outputs", []):
            rel = output["path"]
            valid, reason = validate_against_baselines(rel, root / rel)
            print(("OK   " if valid else "NEEDS REBUILD"), rel, reason)
            ok = ok and valid
    return ok

FOLDER_META = ("fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")

def extract_remap(pkg: Path, remap, dest_root: Path, exclude=()):
    """Write every asset of a .unitypackage under dest_root using its pathname (optional prefix remap) and its own .meta.
    A package entry without asset.meta gets a minimal meta carrying the package's GUID (the entry's directory name), so
    references to it stay valid; Unity completes the importer settings on first import."""
    import fnmatch
    src, dst = remap if remap else ("", "")
    n = skipped = 0
    with tarfile.open(pkg) as tf, tempfile.TemporaryDirectory() as td:
        tf.extractall(td, filter="data")
        for d in Path(td).iterdir():
            pn = d / "pathname"
            if not pn.is_file():
                continue
            # Unity package pathnames are UTF-8. Use an explicit encoding: on Windows,
            # Path.read_text() otherwise uses the active ANSI code page and mangles
            # names containing an en dash or accented characters.
            rel = pn.read_text(encoding="utf-8").splitlines()[0].strip()
            if src and rel.startswith(src):
                rel = dst + rel[len(src):]
            if any(fnmatch.fnmatch(rel, pat) or fnmatch.fnmatch(Path(rel).name, pat) for pat in exclude):
                skipped += 1; continue
            out = dest_root / rel
            meta_dst = Path(str(out) + ".meta")
            if not (d / "asset").is_file():                      # folder entry
                out.mkdir(parents=True, exist_ok=True)
                if (d / "asset.meta").is_file(): shutil.copy2(d / "asset.meta", meta_dst)
                else: meta_dst.write_text(FOLDER_META.format(g=d.name))
                continue
            out.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(d / "asset", out)
            if (d / "asset.meta").is_file(): shutil.copy2(d / "asset.meta", meta_dst)
            else: meta_dst.write_text(f"fileFormatVersion: 2\nguid: {d.name}\n")
            n += 1
    print(f"extracted {n} files from {pkg.name} (excluded {skipped})")

def step(msg):
    print(f"\n=== {msg}", flush=True)

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--packages-dir", type=Path)
    ap.add_argument("--grass-pack", type=Path)
    ap.add_argument("--unity", default=os.environ.get("UNITY_PATH"))
    ap.add_argument("--vv-work", type=Path, help="directory containing the external clean/ generator inputs (defaults to VV_WORK or /workspace/valvert)")
    ap.add_argument("--derive-inputs-from-terrain", action="store_true", help="if VV_WORK/clean inputs are absent, derive fallback masks/labels from the committed terrain alphamap")
    ap.add_argument("--staging", type=Path, help="temporary staging directory for Python rebuild outputs")
    ap.add_argument("--allow-unpinned-env", action="store_true", help="skip Python/font preflight; baseline SHA checks still gate writes")
    ap.add_argument("--check-only", action="store_true", help="only run the final audit")
    ap.add_argument("--log-dir", type=Path, help="Unity log directory (default: system temp directory)")
    a = ap.parse_args()
    a.packages_dir = a.packages_dir.resolve() if a.packages_dir else None
    a.grass_pack = a.grass_pack.resolve() if a.grass_pack else None
    a.staging = a.staging.resolve() if a.staging else Path(tempfile.gettempdir()) / "valvert-rebuild-staging"
    a.log_dir = a.log_dir.resolve() if a.log_dir else Path(tempfile.gettempdir()) / "vv_restore"
    a.vv_work = (a.vv_work or Path(os.environ.get("VV_WORK", "/workspace/valvert"))).resolve()
    a.log_dir.mkdir(parents=True, exist_ok=True)

    if not a.check_only:
        step("1/5 python-proven binaries")
        if proven_outputs_already_valid():
            print("all proven outputs already match their pinned baselines; keeping them")
        else:
            env_args = ["--allow-unpinned-env"] if a.allow_unpinned_env else []
            if run([sys.executable, "Tools/rebuild/rebuild.py", "preflight", *env_args]).returncode != 0: return 11
            if run([sys.executable, "Tools/rebuild/rebuild.py", "rebuild-proven", "--apply", "--staging", a.staging, *env_args]).returncode != 0: return 12

        step("2/5 vendor packages")
        for pk in VENDOR["packages"]:
            mk = ROOT / pk["marker"]
            if mk.is_file() or (mk.is_dir() and any(mk.rglob("*.meta"))):
                print(f"{pk['id']}: already present ({pk['marker']})"); continue
            candidates = vendor_package_candidates(pk, a.packages_dir, a.grass_pack)
            if not candidates:
                print(f"MISSING package {pk['id']}: {pk['name']}")
                for name, digest in [(pk["file"], pk["sha256"]), *[(x["file"], x["sha256"]) for x in pk.get("alternatives", [])]]:
                    print(f"  expected file: {name} (sha256 {digest})")
                return 20
            f = None; mismatches = []
            for candidate, expected in candidates:
                if not candidate.is_file(): continue
                actual = sha256(candidate)
                if actual == expected:
                    f = candidate; break
                mismatches.append((candidate, actual, expected))
            if f is None:
                if mismatches:
                    for candidate, actual, expected in mismatches:
                        print(f"SHA256 MISMATCH for {candidate}: {actual} != {expected}")
                    return 21
                print(f"MISSING package {pk['id']}: no recognized archive found")
                for candidate, digest in candidates: print(f"  expected: {candidate} (sha256 {digest})")
                return 20
            extract_remap(f, pk.get("remap"), ROOT, pk.get("exclude", ()))

        step("2b/5 vendor-internal dangling references (local, ignored files only)")
        if run([sys.executable, "Tools/rebuild/sanitize_vendor.py"]).returncode != 0: return 25

        step("3/5 vegetation preflight")
        if run([sys.executable, "Tools/vegetation/preflight.py"]).returncode != 0: return 30

        step("3b/5 external vegetation source inputs")
        missing = [p for p in required_world_inputs(a.vv_work) if not p.is_file()]
        unity_env = os.environ.copy(); unity_env["VV_WORK"] = str(a.vv_work)
        if missing and a.derive_inputs_from_terrain:
            if not a.unity: print("--unity required to derive vegetation inputs"); return 40
            print("Original clean/ data absent; deriving fallback masks and labels from the committed terrain alphamap.")
            r = run([a.unity, "-batchmode", "-nographics", "-quit", "-projectPath", ROOT,
                     "-executeMethod", "ValVertVegetationSourceFallback.GenerateFromTerrainBatch",
                     "-logFile", a.log_dir / "derive-inputs.log"], env=unity_env)
            if r.returncode != 0:
                print(f"terrain-derived input generation failed (exit {r.returncode}); see {a.log_dir / 'derive-inputs.log'}")
                return 32
            missing = [p for p in required_world_inputs(a.vv_work) if not p.is_file()]
        if missing:
            print(f"MISSING local inputs under {a.vv_work / 'clean'}:")
            for p in missing: print(f"  {p.name}")
            print("Restore the original clean/ folder, pass --vv-work, or explicitly opt in to --derive-inputs-from-terrain.")
            return 31

        step("4/5 Unity: regenerate generated assets + pin GUIDs")
        if not a.unity: print("--unity required"); return 40
        r = run([a.unity, "-batchmode", "-nographics", "-quit", "-projectPath", ROOT, "-executeMethod", "ValVertGeneratedAssets.RegenerateAll", "-logFile", a.log_dir / "regenerate.log"], env=unity_env)
        if r.returncode != 0: print(f"RegenerateAll failed (exit {r.returncode}); see {a.log_dir / 'regenerate.log'}"); return 41

        step("4b/5 terrain tree prototypes: pin prefab root fileIDs, then verify")
        if run([sys.executable, "Tools/architecture/tree_pins.py", "apply"]).returncode != 0: return 42
        r = run([a.unity, "-batchmode", "-nographics", "-quit", "-projectPath", ROOT, "-executeMethod", "ValVertGeneratedAssets.ReportTreesBatch", "-logFile", a.log_dir / "trees.log"], env=unity_env)
        if r.returncode != 0: print(f"terrain tree prototypes still broken (exit {r.returncode}); see {a.log_dir / 'trees.log'}"); return 43

    step("5/5 audit gate (only frozen car exceptions may remain)")
    r = run([sys.executable, "Tools/architecture/clean_clone_audit.py", "--tree", ROOT, "--require-restored"])
    return 0 if r.returncode == 0 else 50

if __name__ == "__main__":
    sys.exit(main())
