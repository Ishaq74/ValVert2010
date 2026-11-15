#!/usr/bin/env python3
"""Preflight for the Val Vert grass field SOURCE config (no Unity required).

Schema 2: materialMode must be pack_cutout; cards come from the authentic Asset Store package 138810
(imported only into the gitignored Assets/ThirdParty/). In a bare clone the pack is absent:
use --allow-missing-pack (config shape is still fully validated). With the pack present, every card
file must exist and its .meta guid must equal the configured guid.
Optional --package <file.unitypackage> verifies the package SHA256 recorded in the config.
"""
from __future__ import annotations
import argparse, hashlib, json, math, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CFG = ROOT / "Assets/_Project/Settings/World/Vegetation/grass_field_config.json"
EXPECTED_PACK_ID = "138810"
EXPECTED_SCHEMA = "2"

def load():
    return json.loads(CFG.read_text())

def finite_positive(v) -> bool:
    try:
        f = float(v)
    except (TypeError, ValueError):
        return False
    return math.isfinite(f) and f > 0

def meta_guid(p: Path):
    m = Path(str(p) + ".meta")
    if not m.is_file():
        return None
    for line in m.read_text().splitlines():
        if line.startswith("guid:"):
            return line.split(":", 1)[1].strip()
    return None

def check(cfg: dict, allow_missing_pack: bool = False, package: Path | None = None) -> list[str]:
    errs: list[str] = []
    if cfg.get("schemaVersion") != EXPECTED_SCHEMA:
        errs.append(f"schemaVersion {cfg.get('schemaVersion')!r} != {EXPECTED_SCHEMA}")
    for key in ("chunkSize", "parkSpacing", "woodsSpacing"):
        if not finite_positive(cfg.get(key)):
            errs.append(f"{key} must be finite > 0")
    for key in ("cardHeightPark", "cardHeightMeadow", "cardWidthFactor"):
        v = cfg.get(key)
        if not (isinstance(v, list) and len(v) == 2 and all(finite_positive(x) for x in v) and v[1] >= v[0]):
            errs.append(f"{key} must be [min,max] finite > 0")
    if cfg.get("materialMode") != "pack_cutout":
        errs.append(f"materialMode must be pack_cutout (legacy homemade removed), got {cfg.get('materialMode')!r}")
    if "legacyAlbedoPath" in cfg:
        errs.append("legacyAlbedoPath must not exist any more")
    folder = cfg.get("materialFolder") or ""
    if not folder.startswith("Assets/ThirdParty/"):
        errs.append("materialFolder must be under ignored Assets/ThirdParty/ (pack-derived materials are never committed)")
    pack = cfg.get("pack")
    if not isinstance(pack, dict):
        errs.append("pack slot missing")
        return errs
    if pack.get("assetStoreId") != EXPECTED_PACK_ID:
        errs.append(f"pack.assetStoreId must be {EXPECTED_PACK_ID}")
    if not (pack.get("localRoot") or "").startswith("Assets/ThirdParty/"):
        errs.append("pack.localRoot must be under ignored Assets/ThirdParty/")
    cards = pack.get("cards")
    if not isinstance(cards, list) or not cards:
        errs.append("pack.cards must be a non-empty list")
        cards = []
    for c in cards:
        for k in ("file", "guid"):
            if not c.get(k):
                errs.append(f"card missing {k}: {c}")
        for k in ("weightMeadow", "weightPark"):
            if not isinstance(c.get(k), (int, float)) or c[k] < 0:
                errs.append(f"card {c.get('file')}: {k} must be >= 0")
        f = ROOT / (pack.get("localRoot") or "") / (c.get("file") or "")
        if f.is_file():
            g = meta_guid(f)
            if g != c.get("guid"):
                errs.append(f"card GUID mismatch {c.get('file')}: meta {g} != config {c.get('guid')}")
        elif not allow_missing_pack:
            errs.append(f"pack card absent: {f.relative_to(ROOT)} (import the package into ignored ThirdParty, or --allow-missing-pack)")
    for key in ("weightMeadow", "weightPark"):
        if cards and sum(float(c.get(key, 0) or 0) for c in cards) <= 0:
            errs.append(f"sum of {key} must be > 0")
    if package is not None:
        h = hashlib.sha256(package.read_bytes()).hexdigest()
        if h != pack.get("packageSha256"):
            errs.append(f"package sha256 {h} != config {pack.get('packageSha256')}")
    return errs

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--allow-missing-pack", action="store_true", help="bare clone: pack files are gitignored")
    ap.add_argument("--package", type=Path, help="verify .unitypackage SHA256 against config")
    args = ap.parse_args()
    cfg = load()
    errs = check(cfg, allow_missing_pack=args.allow_missing_pack, package=args.package)
    if errs:
        for e in errs:
            print("FAIL", e)
        return 2
    print("OK materialMode=", cfg["materialMode"], "cards=", len(cfg["pack"]["cards"]), "pack.status=", cfg["pack"].get("status"))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
