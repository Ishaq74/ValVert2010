#!/usr/bin/env python3
from __future__ import annotations
import copy, json, subprocess, sys, unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PRE = ROOT / "Tools/vegetation/preflight.py"
CFG = ROOT / "Assets/_Project/Settings/World/Vegetation/grass_field_config.json"
sys.path.insert(0, str(ROOT / "Tools/vegetation"))
import preflight as PF  # noqa

def cfg():
    return json.loads(CFG.read_text())

class GrassConfigTests(unittest.TestCase):
    def test_config_is_source_under_settings(self):
        self.assertTrue(CFG.is_file())
        self.assertIn("/Settings/World/Vegetation/", str(CFG))

    def test_schema2_pack_cutout_authentic_package(self):
        c = cfg()
        self.assertEqual(c["schemaVersion"], "2")
        self.assertEqual(c["materialMode"], "pack_cutout")
        self.assertEqual(c["seed"], 5)
        self.assertEqual(c["chunkSize"], 32.0)
        self.assertEqual(c["pack"]["assetStoreId"], "138810")
        self.assertEqual(c["pack"]["packageSha256"], "b2278e2c7ea8ca005f6958af3e810ab554b04bea0adf4714320e3a00cb50c750")
        self.assertEqual([x["file"].split("/")[-1] for x in c["pack"]["cards"]], ["grass01.tga", "grass02.tga"])
        self.assertNotIn("legacyAlbedoPath", c)

    def test_pack_and_derived_materials_are_private(self):
        c = cfg()
        self.assertTrue(c["pack"]["localRoot"].startswith("Assets/ThirdParty/"))
        self.assertTrue(c["materialFolder"].startswith("Assets/ThirdParty/"))
        for p in (c["pack"]["localRoot"] + "/Textures/x.tga", c["materialFolder"] + "/grass_pack_0.mat"):
            r = subprocess.run(["git", "check-ignore", "-q", p], cwd=ROOT)
            self.assertEqual(r.returncode, 0, p + " must be gitignored")
        tracked = subprocess.run(["git", "ls-files", "Assets/ThirdParty"], cwd=ROOT, capture_output=True, text=True).stdout.split()
        self.assertTrue(all(t.endswith(".gitkeep") or t.endswith("ThirdParty.meta") for t in tracked), tracked)

    def test_preflight_bare_clone_ok(self):
        r = subprocess.run([sys.executable, str(PRE), "--allow-missing-pack"], cwd=ROOT, capture_output=True, text=True)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)

    def test_legacy_mode_rejected(self):
        c = cfg(); c["materialMode"] = "legacy_homemade"
        self.assertTrue(any("pack_cutout" in e for e in PF.check(c, True)))

    def test_bad_cards_and_ranges_flagged(self):
        c = cfg(); c["pack"]["cards"] = []
        self.assertTrue(any("cards" in e for e in PF.check(c, True)))
        c = cfg(); c["cardHeightPark"] = [0.5, 0.2]
        self.assertTrue(any("cardHeightPark" in e for e in PF.check(c, True)))
        c = cfg(); c["materialFolder"] = "Assets/ValVert/Mats"
        self.assertTrue(any("materialFolder" in e for e in PF.check(c, True)))
        c = cfg(); c["pack"]["assetStoreId"] = "1"
        self.assertTrue(any("assetStoreId" in e for e in PF.check(c, True)))

    def test_guid_mismatch_detected_when_pack_present(self):
        c = cfg()
        f = ROOT / c["pack"]["localRoot"] / c["pack"]["cards"][0]["file"]
        if not f.is_file():
            self.skipTest("pack not imported in this clone (private)")
        c["pack"]["cards"][0]["guid"] = "0" * 32
        self.assertTrue(any("GUID mismatch" in e for e in PF.check(c)))
        self.assertEqual(PF.check(cfg()), [])

    def test_woods_delegates_and_cs_uses_pack_config(self):
        woods = (ROOT / "Assets/_Project/Editor/Generators/ValVertWoods.cs").read_text()
        self.assertIn("ValVertGrassField.Build", woods)
        field = (ROOT / "Assets/_Project/Editor/Generators/Vegetation/ValVertGrassField.cs").read_text()
        for s in ("Settings/World/Vegetation/grass_field_config.json", "ExpectedPackId", "FinitePositive", "GUID mismatch", "ExpectedSchema = \"2\""):
            self.assertIn(s, field)
        self.assertNotIn("legacy_homemade\")", field)
        self.assertNotIn("grass_blades", field)

if __name__ == "__main__":
    unittest.main()
