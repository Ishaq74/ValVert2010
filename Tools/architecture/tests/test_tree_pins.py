import json, sys, tempfile, unittest
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "Tools/architecture"))
import tree_pins as T

PREFAB = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &111
GameObject:
  m_Component:
  - component: {fileID: 222}
  m_Name: root
--- !u!4 &222
Transform:
  m_GameObject: {fileID: 111}
  m_Father: {fileID: 0}
  m_Children:
  - {fileID: 444}
--- !u!1 &333
GameObject:
  m_Component:
  - component: {fileID: 444}
--- !u!4 &444
Transform:
  m_GameObject: {fileID: 333}
  m_Father: {fileID: 222}
"""

class TreePins(unittest.TestCase):
    def test_root_detection_ignores_children(self):
        self.assertEqual(T.root_file_id(PREFAB), 111)

    def test_retarget_rewrites_every_reference_and_is_idempotent(self):
        out = T.retarget(PREFAB, 9876543210)
        self.assertIn("--- !u!1 &9876543210", out); self.assertIn("m_GameObject: {fileID: 9876543210}", out)
        self.assertNotIn("&111\n", out); self.assertIn("&333", out)           # child GameObject untouched
        self.assertEqual(T.retarget(out, 9876543210), out)
        self.assertEqual(T.root_file_id(out), 9876543210)

    def test_retarget_does_not_touch_longer_numbers_containing_the_id(self):
        t = PREFAB.replace("&333", "&1119")
        self.assertIn("&1119", T.retarget(t, 5))

    def test_committed_pins_are_well_formed(self):
        d = T.load(ROOT)
        self.assertEqual(T.validate(d), [])
        self.assertEqual(len(d["prototypes"]), 20)

    def test_present_prefabs_match_pins(self):
        self.assertEqual(T.check(ROOT), [])

    def test_apply_on_synthetic_project(self):
        with tempfile.TemporaryDirectory() as t:
            t = Path(t); (t / "Assets/Settings/Architecture").mkdir(parents=True)
            (t / "Assets/World/Prefabs").mkdir(parents=True)
            pins = {"prototypes": [{"name": n, "guid": "%032x" % (i + 1), "rootFileId": 1000 + i} for i, n in enumerate(T.NAMES)]}
            (t / T.PINS).write_text(json.dumps(pins))
            for n in T.NAMES[:2]:
                (t / f"Assets/World/Prefabs/{n}.prefab").write_text(PREFAB)
            self.assertEqual(T.apply(t), 1)                                    # others missing -> nonzero
            self.assertEqual(T.root_file_id((t / "Assets/World/Prefabs/ef_slot_autumnA.prefab").read_text()), 1000)

if __name__ == "__main__":
    unittest.main()
