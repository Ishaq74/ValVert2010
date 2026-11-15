#!/usr/bin/env python3
"""The GUID/ref verifier must pass on the real manifest and FAIL on tampering (anti-tautology)."""
from __future__ import annotations
import json, subprocess, sys, tempfile, unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
V = ROOT / "Tools/architecture/verify_guid_refs.py"
MAN = ROOT / "Assets/_Project/Settings/Architecture/migration_manifest.json"

def run(manifest=None):
    cmd = [sys.executable, str(V)] + (["--manifest", str(manifest)] if manifest else [])
    return subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)

class GuidRefTests(unittest.TestCase):
    def test_real_manifest_ok(self):
        r = run(); self.assertEqual(r.returncode, 0, r.stdout + r.stderr); self.assertIn("GUID_REFS_OK", r.stdout)

    def test_every_move_records_a_guid(self):
        for m in json.loads(MAN.read_text())["moves"]:
            self.assertRegex(m.get("guid", ""), r"^[0-9a-f]{32}$", m["to"])

    def test_sky_resources_colocated(self):
        sky = [m for m in json.loads(MAN.read_text())["moves"] if m["system"] == "World/Sky"]
        names = {Path(m["to"]).name for m in sky}
        self.assertTrue({"VVSkyBlend.shader", "sky.mat", "sky_blend.mat", "sky_overcast.mat"} <= names)
        for m in sky: self.assertTrue(m["to"].startswith("Assets/_Project/Runtime/World/Sky/"))

    def test_tampered_guid_is_caught(self):
        d = json.loads(MAN.read_text()); d["moves"][-1]["guid"] = "0" * 32
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f: json.dump(d, f)
        r = run(f.name); self.assertEqual(r.returncode, 2); self.assertIn("GUID CHANGED", r.stdout)

    def test_stale_old_path_is_caught(self):
        d = json.loads(MAN.read_text()); d["moves"][-1]["from"] = "Assets/_Project/Editor/Generators/ValVertPaths.cs"  # exists -> "source still present"
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f: json.dump(d, f)
        r = run(f.name); self.assertEqual(r.returncode, 2)

if __name__ == "__main__":
    unittest.main()
