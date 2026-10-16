import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import shutil
import sys
import tempfile
import unittest

from PIL import Image, ImageDraw

spec = importlib.util.spec_from_file_location("visual_gate", Path(__file__).with_name("Validate-RenderPipelineImages.py"))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)

LIMITS = dict(channel_tolerance=8, tile_size=32, max_changed_percent=.5,
              max_mean_channel_error=.25, max_worst_tile_changed_percent=8,
              max_worst_tile_mean_error=12, max_new_magenta_pixels=0)


class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for phase in ("before", "after"):
            folder = self.root / phase / "suite"
            folder.mkdir(parents=True)
            Image.new("RGB", (512, 256), (30, 40, 50)).save(folder / "frame.png")
            (folder / "status.txt").write_text("PASS", encoding="utf-8")
            (folder / "verification.txt").write_text("Checks: 1 / runtime errors: 0", encoding="utf-8")
        self.contract = dict(version=1, suites=["suite"], limits=copy.copy(LIMITS), captures=[
            dict(image="suite/frame.png", width=512, height=256,
                 before_sha256=hashlib.sha256((self.root / "before/suite/frame.png").read_bytes()).hexdigest())])

    def change(self, edit):
        path = self.root / "after/suite/frame.png"
        with Image.open(path) as source:
            image = source.convert("RGB")
        edit(image)
        image.save(path)

    def run_gate(self):
        return gate.validate(self.root, self.contract)

    def test_identical_passes(self):
        self.assertEqual(self.run_gate()["verdict"], "PASS")

    def test_single_pixel_noise_within_budget(self):
        self.change(lambda image: image.putpixel((40, 40), (100, 100, 100)))
        self.assertEqual(self.run_gate()["verdict"], "PASS")

    def test_small_missing_control_fails_local_budget(self):
        self.change(lambda image: ImageDraw.Draw(image).rectangle((29, 29, 40, 40), fill="white"))
        result = self.run_gate()
        self.assertEqual(result["verdict"], "FAIL")
        self.assertLess(result["captures"][0]["changed_percent"], LIMITS["max_changed_percent"])

    def test_low_amplitude_global_tint_fails(self):
        self.change(lambda image: image.paste((31, 41, 51), (0, 0, 512, 256)))
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_new_magenta_never_excused_by_dynamic_fixture(self):
        self.contract["nonrepeatable"] = [dict(pattern="suite/*", reason="live simulation")]
        self.change(lambda image: image.putpixel((30, 30), (255, 0, 255)))
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_dynamic_change_is_inconclusive_and_blocks(self):
        self.contract["nonrepeatable"] = [dict(pattern="suite/*", reason="live simulation")]
        self.change(lambda image: image.paste("white", (0, 0, 512, 256)))
        self.assertEqual(self.run_gate()["verdict"], "REVIEW")

    def test_missing_pair_fails(self):
        (self.root / "after/suite/frame.png").unlink()
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_losing_both_sides_cannot_reduce_coverage(self):
        for phase in ("before", "after"):
            (self.root / phase / "suite/frame.png").unlink()
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_missing_suite_evidence_fails_identical_images(self):
        (self.root / "after/suite/status.txt").unlink()
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_failed_report_overrides_pass_status(self):
        (self.root / "after/suite/verification.txt").write_text("Checks: 3 / runtime errors: 1", encoding="utf-8")
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_resize_fails(self):
        Image.new("RGB", (513, 256)).save(self.root / "after/suite/frame.png")
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_changed_baseline_fails_even_when_pair_matches(self):
        self.change(lambda image: image.putpixel((1, 1), (40, 40, 40)))
        (self.root / "before/suite/frame.png").write_bytes((self.root / "after/suite/frame.png").read_bytes())
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_unexpected_capture_fails(self):
        (self.root / "after/suite/extra.png").write_bytes((self.root / "after/suite/frame.png").read_bytes())
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_empty_contract_cannot_pass(self):
        with self.assertRaises(ValueError):
            gate.validate(self.root, dict(version=1, suites=[], captures=[]))

    def test_path_escape_rejected(self):
        self.contract["captures"][0]["image"] = "suite/../../escape.png"
        with self.assertRaises(ValueError):
            self.run_gate()

    def test_blank_frame_fails(self):
        self.change(lambda image: image.paste("black", (0, 0, 512, 256)))
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_dynamic_resize_is_still_a_failure(self):
        self.contract["nonrepeatable"] = [dict(pattern="suite/*", reason="live simulation")]
        Image.new("RGB", (513, 256)).save(self.root / "after/suite/frame.png")
        self.assertEqual(self.run_gate()["verdict"], "FAIL")

    def test_cli_exit_codes_and_reports(self):
        policy = self.root / "contract.json"
        def invoke():
            policy.write_text(json.dumps(self.contract), encoding="utf-8")
            return subprocess.run([sys.executable, spec.origin, str(self.root), "--contract", str(policy)],
                                  capture_output=True, text=True)
        self.assertEqual(invoke().returncode, 0)
        self.change(lambda image: image.paste("white", (0, 0, 512, 256)))
        self.assertEqual(invoke().returncode, 1)
        self.contract["nonrepeatable"] = [dict(pattern="suite/*", reason="live simulation")]
        self.assertEqual(invoke().returncode, 2)
        self.assertIn("REVIEW", (self.root / "validation/index.html").read_text(encoding="utf-8"))
        self.assertTrue((self.root / "validation-matrix.csv").is_file())

    def test_powershell_wrapper_preserves_exit_and_refreshes_gallery(self):
        shell = shutil.which("pwsh")
        if not shell:
            self.skipTest("PowerShell 7 is not installed")
        policy = self.root / "contract.json"
        wrapper = Path(__file__).with_name("Test-RenderPipelineImages.ps1")
        for expected in (0, 1, 2):
            if expected:
                self.change(lambda image: image.paste("white", (0, 0, 512, 256)))
            if expected == 2:
                self.contract["nonrepeatable"] = [dict(pattern="suite/*", reason="live simulation")]
            policy.write_text(json.dumps(self.contract), encoding="utf-8")
            run = subprocess.run([shell, "-NoProfile", "-File", str(wrapper), "-CaptureRoot", str(self.root),
                                  "-Contract", str(policy), "-Python", sys.executable], capture_output=True, text=True)
            self.assertEqual(run.returncode, expected, run.stdout + run.stderr)
            gallery = (self.root / "index.html").read_text(encoding="utf-8")
            self.assertIn("Automatic validation:", gallery)
            self.assertNotIn("STALE —", gallery)


if __name__ == "__main__":
    unittest.main()
