#!/usr/bin/env python3
"""Remove references that are dangling INSIDE the Asset Store packages themselves (local, git-ignored vendor files only).

European Forests' URP materials ship with leftovers of another pipeline version: texture slots pointing at textures that are
not in the package, and `AssetVersion`-style MonoBehaviour sub-objects whose script GUID does not exist in this URP version.
Unity already treats them as null/missing at runtime; this script makes that explicit so the clean-clone audit has nothing to
allow-list. It only touches *.mat files under the vendor roots, only for the GUIDs listed in vendor_manifest.json
("sanitizeDanglingGuids"), and is idempotent. Nothing here is committed (the vendor files are ignored).

  sanitize_vendor.py [--project <dir>] [--check]
"""
from __future__ import annotations
import argparse, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOC = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+)")

def sanitize_text(text: str, guids: set[str]) -> str:
    heads = list(DOC.finditer(text))
    out = text
    if heads:
        pieces = [text[: heads[0].start()]]
        for i, h in enumerate(heads):
            body = text[h.start(): heads[i + 1].start() if i + 1 < len(heads) else len(text)]
            if h.group(1) == "114" and any(re.search(r"m_Script: \{fileID: \d+, guid: %s, type: \d\}" % g, body) for g in guids):
                continue                                   # MonoBehaviour whose script does not exist: drop the sub-object
            pieces.append(body)
        out = "".join(pieces)
    for g in guids:
        out = re.sub(r"\{fileID: \d+, guid: %s, type: \d\}" % g, "{fileID: 0}", out)
    return out

def run(project: Path, check: bool = False) -> int:
    vm = json.loads((project / "Tools/rebuild/vendor_manifest.json").read_text())
    guids = set(vm.get("sanitizeDanglingGuids", []))
    changed = 0
    for pat in vm.get("sanitizeRoots", []):
        for f in sorted((project / pat).rglob("*.mat")):
            t = f.read_text(encoding="utf-8", errors="replace")
            t2 = sanitize_text(t, guids)
            if t2 != t:
                changed += 1
                if not check: f.write_text(t2, encoding="utf-8", newline="\n")
    print(f"VENDOR_SANITIZE {'would change' if check else 'changed'}={changed} guids={len(guids)}")
    return 1 if (check and changed) else 0

if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--project", type=Path, default=ROOT); ap.add_argument("--check", action="store_true")
    a = ap.parse_args(); sys.exit(run(a.project, a.check))
