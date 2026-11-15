#!/usr/bin/env python3
"""Clean-clone audit + pins: behaviour on synthetic trees (anti-tautology) and invariants of the real repo config."""
from __future__ import annotations
import json, re, subprocess, sys, tempfile, unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "Tools/architecture"))
import clean_clone_audit as A  # noqa
import guid_pins as P  # noqa

def g(n): return "ab" * 8 + f"{n:016x}"
def meta(root: Path, rel: str, guid: str, create=True):
    p = root / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    Path(str(p) + ".meta").write_text(f"fileFormatVersion: 2\nguid: {guid}\n")
    if create: p.write_bytes(b"x")

class Synthetic(unittest.TestCase):
    def build(self, td: Path):
        scene = td / "Assets/Scenes/ValVert.unity"
        scene.parent.mkdir(parents=True)
        meta(td, "Assets/Scenes/ValVert.unity", g(1))
        # mat present, tex present, mesh missing entirely, orphan-meta asset (meta but no file), binary terrain ref to g(7)
        meta(td, "Assets/m.mat", g(2)); meta(td, "Assets/t.png", g(3))
        meta(td, "Assets/orphan.png", g(5), create=False)
        meta(td, "Assets/bin.asset", g(6)); meta(td, "Assets/bound.prefab", g(7))
        scene.write_text("%YAML 1.1\n" + "".join(f"  - {{fileID: 1, guid: {x}, type: 2}}\n" for x in (g(2), g(4), g(5), g(6), "0" * 16 + "e000000000000000")))
        (td / "Assets/m.mat").write_text("%YAML 1.1\n  m_Tex: {fileID: 2, guid: " + g(3) + ", type: 3}\n")
        (td / "Assets/bin.asset").write_bytes(b"\x00" * 50 + A.swapped(g(7)) + b"\x01" * 30)
        return td

    def test_closure_yaml_binary_orphan_and_builtin(self):
        with tempfile.TemporaryDirectory() as t:
            td = self.build(Path(t))
            g2p, present = A.index(td, packages=False)
            seen, missing = A.closure(td, g2p, present, set(g2p) | {g(4)})
            self.assertEqual(seen, {g(2), g(3), g(6), g(7)}, "transitive: scene->mat->tex and scene->binary->prefab")
            self.assertEqual(set(missing), {g(4), g(5)}, "never-existed guid + tracked meta whose asset file is absent")
            self.assertNotIn("0" * 16 + "e000000000000000", missing, "Unity built-in resources are not missing")

    def test_swapped_roundtrip(self):
        self.assertEqual(A.swapped("0123456789abcdef0123456789abcdef"), bytes.fromhex("1032547698badcfe1032547698badcfe"))

    def test_classify(self):
        ctx = dict(expected_dangling={g(9)}, vendor_globs=["Assets/Realistic Tree/*"], rebuild_python={"Assets/a.png"}, pins={"Assets/b.asset"},
                   lfs_globs=["Assets/c.asset"], tracked_paths=set())
        c = A.classify
        self.assertEqual(c("Assets/a.png", g(1), ctx), "rebuild-python"); self.assertEqual(c("Assets/b.asset", g(1), ctx), "rebuild-unity")
        self.assertEqual(c("Assets/Realistic Tree/x.prefab", g(1), ctx), "vendor"); self.assertEqual(c("Packages/x/y.cs", g(1), ctx), "package")
        self.assertEqual(c(None, g(9), ctx), "known-dangling"); self.assertEqual(c(None, g(1), ctx), "unresolved")
        self.assertEqual(c("Assets/c.asset", g(1), ctx), "tracked-lfs"); self.assertEqual(c("Assets/zzz", g(1), ctx), "unclassified")

class RealRepo(unittest.TestCase):
    def test_pins_wellformed_and_unique(self):
        d = P.load(); self.assertEqual(P.validate(d), [])
        kinds = {k.rsplit("/", 1)[0] for k in d["assets"]}
        self.assertTrue({"Assets/ValVert/Gen", "Assets/ValVert/Prefabs", "Assets/ThirdParty/ValVertGrass"} <= kinds)
        self.assertGreater(len(d["assets"]), 700)

    def test_pins_never_collide_with_tracked_guids(self):
        tracked = {}
        for m in subprocess.run(["git", "ls-files", "*.meta"], cwd=ROOT, capture_output=True, text=True).stdout.split():
            mg = P.meta_guid(ROOT / m)
            if mg: tracked[mg] = m
        for path, guid in P.load()["assets"].items():
            self.assertNotIn(guid, tracked, f"{path}: pinned guid also owned by tracked {tracked.get(guid)}")

    def test_tracked_city_meshes_are_not_ignored_and_are_lfs(self):
        attrs = (ROOT / ".gitattributes").read_text()
        for p in ["Assets/ValVert/Gen/m_Bld_facade.asset", "Assets/ValVert/Gen/m_Bld_kerb.asset", "Assets/ValVert/Gen/m_Bld_metal.asset",
                  "Assets/ValVert/Gen/m_Bld_white_trim.asset", "Assets/ValVert/Gen/m_OuterRing.asset", "Assets/ValVert/demfix_Urban_-2392.asset"]:
            self.assertEqual(subprocess.run(["git", "check-ignore", "-q", p], cwd=ROOT).returncode, 1, p + " must not be gitignored")
            self.assertIn(p + " filter=lfs", attrs)

    def test_vendor_manifest_and_expected_files(self):
        vm = json.loads((ROOT / "Tools/rebuild/vendor_manifest.json").read_text())
        self.assertGreaterEqual(len(vm["packages"]), 4)
        for pk in vm["packages"]:
            self.assertRegex(pk["sha256"], r"^[0-9a-f]{64}$"); self.assertEqual(pk["method"], "extract-remap")
            self.assertTrue(pk["marker"].startswith("Assets/"))
        ex = json.loads((ROOT / "Tools/architecture/clean_clone_expected.json").read_text())["knownDangling"]
        self.assertTrue(ex and all(re.fullmatch(r"[0-9a-f]{32}", k) for k in ex))

    def test_pin_patterns_cover_only_ignored_paths(self):
        for p in ["Assets/ValVert/Gen/grass_0.asset", "Assets/ValVert/Prefabs/ef_ash_1.prefab", "Assets/ValVert/Prefabs/uns_bush_a.prefab", "Assets/ThirdParty/ValVertGrass/grass_pack_0.mat"]:
            self.assertEqual(subprocess.run(["git", "check-ignore", "-q", p], cwd=ROOT).returncode, 0, p + " is generated: must be ignored (else pin it differently)")

if __name__ == "__main__":
    unittest.main()


class VendorExtract(unittest.TestCase):
    def test_extract_keeps_guids_synthesizes_missing_meta_excludes_hdr(self):
        import tarfile, io, importlib.util
        spec = importlib.util.spec_from_file_location("rw", ROOT / "Tools/rebuild/restore_world.py"); rw = importlib.util.module_from_spec(spec); spec.loader.exec_module(rw)
        with tempfile.TemporaryDirectory() as t:
            t = Path(t); pkg = t / "p.unitypackage"
            def add(tf, name, data):
                ti = tarfile.TarInfo(name); ti.size = len(data); tf.addfile(ti, io.BytesIO(data))
            with tarfile.open(pkg, "w:gz") as tf:
                add(tf, "aaa/pathname", b"Assets/V/tex.png\n00"); add(tf, "aaa/asset", b"PNG"); add(tf, "aaa/asset.meta", b"fileFormatVersion: 2\nguid: aaa\nTextureImporter: {}\n")
                add(tf, "bbb/pathname", b"Assets/V/nometa.fbx\n00"); add(tf, "bbb/asset", b"FBX")
                add(tf, "ccc/pathname", b"Assets/V\n00")                                  # folder entry, no meta
                add(tf, "ddd/pathname", b"Assets/V/big.hdr\n00"); add(tf, "ddd/asset", b"HDR")
            dest = t / "proj"; dest.mkdir()
            rw.extract_remap(pkg, ["Assets/V", "Assets/ThirdParty/V"], dest, exclude=["*.hdr"])
            self.assertEqual((dest / "Assets/ThirdParty/V/tex.png").read_bytes(), b"PNG")
            self.assertIn("guid: aaa", (dest / "Assets/ThirdParty/V/tex.png.meta").read_text())
            self.assertIn("guid: bbb", (dest / "Assets/ThirdParty/V/nometa.fbx.meta").read_text(), "package GUID (dir name) preserved when package has no .meta")
            self.assertFalse((dest / "Assets/ThirdParty/V/big.hdr").exists(), "excluded")
            self.assertIn("folderAsset: yes", (dest / "Assets/ThirdParty/V.meta").read_text())
            self.assertIn("guid: ccc", (dest / "Assets/ThirdParty/V.meta").read_text())
