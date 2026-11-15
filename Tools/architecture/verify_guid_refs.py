#!/usr/bin/env python3
"""GUID / reference evidence for GUID-preserving moves (no Unity required).

For every entry in migration_manifest.json "moves":
  1. destination exists, source gone (verify_migration.py also checks this)
  2. destination .meta guid == the guid recorded in the manifest (the move kept the GUID)
  3. the guid is unique across all tracked .meta files
  4. every tracked YAML asset (scene/prefab/mat/asset/...) that references the guid is listed (evidence of who depends on it)
  5. no tracked source (.cs/.py/.json/.md, excluding the manifest and history docs) still contains the OLD path
  6. every guid referenced BY a moved YAML asset resolves to a tracked .meta (or is reported as 'external' —
     generated/ignored/vendor — never silently ignored)
Exit code 2 on any violation. Use --report to print the reference table.
"""
from __future__ import annotations
import argparse, json, re, subprocess, sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAN = ROOT / "Assets/_Project/Settings/Architecture/migration_manifest.json"
YAML_EXT = {".unity", ".prefab", ".mat", ".asset", ".terrainlayer", ".controller", ".anim", ".shadervariants", ".lighting", ".physicMaterial"}
TEXT_EXT = {".cs", ".py", ".json", ".md", ".txt"}
HISTORY_DOCS = ("Docs/architecture-", "Docs/migration-", "Docs/props-inventory", "Docs/generated-rebuild")
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")

def tracked() -> list[str]:
    out = subprocess.run(["git", "ls-files", "-z"], cwd=ROOT, capture_output=True, text=True, check=True).stdout
    return [p for p in out.split("\0") if p]

def meta_guid(rel: str):
    m = ROOT / (rel + ".meta")
    if not m.is_file():
        return None
    for line in m.read_text().splitlines():
        if line.startswith("guid:"):
            return line.split(":", 1)[1].strip()
    return None

def main() -> int:
    ap = argparse.ArgumentParser(); ap.add_argument("--report", action="store_true"); ap.add_argument("--manifest", type=Path, default=MAN); args = ap.parse_args()
    moves = json.loads(args.manifest.read_text())["moves"]
    files = tracked()
    errs: list[str] = []

    guid_owner: dict[str, list[str]] = defaultdict(list)
    for f in files:
        if f.endswith(".meta"):
            g = meta_guid(f[:-5])
            if g: guid_owner[g].append(f[:-5])
    dupes = {g: v for g, v in guid_owner.items() if len(v) > 1}
    for g, v in dupes.items(): errs.append(f"duplicate guid {g}: {v}")

    refs: dict[str, set[str]] = defaultdict(set)      # guid -> yaml files referencing it
    yaml_files = [f for f in files if Path(f).suffix in YAML_EXT]
    yaml_guids: dict[str, set[str]] = {}
    for f in yaml_files:
        try: t = (ROOT / f).read_text(errors="replace")
        except OSError: continue
        gs = set(GUID_RE.findall(t)); yaml_guids[f] = gs
        for g in gs: refs[g].add(f)

    text_files = [f for f in files if Path(f).suffix in TEXT_EXT and "Library/" not in f
                  and not f.endswith("migration_manifest.json") and not f.startswith(HISTORY_DOCS)
                  and not f.startswith("Assets/Scenes/")]
    texts = {}
    for f in text_files:
        try: texts[f] = (ROOT / f).read_text(errors="replace")
        except OSError: pass

    rows = []
    for mv in moves:
        to, frm, want = mv["to"], mv["from"], mv.get("guid")
        if not (ROOT / to).is_file(): errs.append(f"missing dest {to}"); continue
        if (ROOT / frm).is_file(): errs.append(f"source still present {frm}")
        got = meta_guid(to)
        if not want: errs.append(f"manifest has no guid for {to}")
        elif got != want: errs.append(f"GUID CHANGED {to}: meta {got} != manifest {want}")
        stale = [f for f, t in texts.items() if frm in t]
        for f in stale: errs.append(f"old path '{frm}' still referenced in {f}")
        users = sorted(refs.get(want or "", ()))
        rows.append((mv["system"], to, want, users))
        # outgoing refs of moved YAML assets
        if Path(to).suffix in YAML_EXT:
            ext = [g for g in yaml_guids.get(to, ()) if g not in guid_owner and not g.startswith("0" * 16)]  # 0000..: Unity built-in resources
            for g in ext: rows.append(("  external-ref", to, g, ["(generated/ignored/vendor; not tracked)"]))

    if args.report:
        for sysn, to, g, users in rows:
            print(f"{sysn:14} {to}\n{'':14} guid={g} referenced_by={users}")
    if errs:
        for e in errs: print("FAIL", e)
        return 2
    print(f"GUID_REFS_OK moves={len(moves)} tracked_metas={len(guid_owner)} yaml_scanned={len(yaml_files)}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
