import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

spec = importlib.util.spec_from_file_location("image_comparison", Path(__file__).with_name("Compare-RenderPipelineImages.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ImageComparisonTests(unittest.TestCase):
    def capture(self, root, phase, name, size=(128, 100), pixel=None):
        path = Path(root) / phase / "suite" / name
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGB", size, (10, 20, 30))
        if pixel:
            image.putpixel(*pixel)
        image.save(path)

    def run_comparison(self, root):
        with contextlib.redirect_stdout(io.StringIO()):
            module.compare(Path(root))
        return json.loads((Path(root) / "comparison/image-matrix.json").read_text(encoding="utf-8"))

    def test_exact_and_known_change_preserve_dimensions_and_color(self):
        with tempfile.TemporaryDirectory() as root:
            for name in ["same.png", "changed.png"]:
                self.capture(root, "before", name)
                self.capture(root, "after", name, pixel=((50, 70), (255, 0, 255)) if name == "changed.png" else None)
            rows = self.run_comparison(root)
            self.assertEqual(rows[1]["max_channel_error"], 0)
            self.assertEqual(rows[0]["new_magenta_pixels"], 1)
            self.assertEqual(rows[0]["changed_bounds"], "50,70,51,71")
            overlay = Image.open(Path(root) / rows[0]["overlay"])
            self.assertEqual(overlay.size, (128, 100))
            self.assertEqual(overlay.getpixel((50, 70)), (132, 10, 142))

    def test_missing_capture_fails(self):
        with tempfile.TemporaryDirectory() as root:
            self.capture(root, "before", "lost.png")
            with self.assertRaises(SystemExit):
                self.run_comparison(root)

    def test_size_mismatch_is_not_resampled(self):
        with tempfile.TemporaryDirectory() as root:
            self.capture(root, "before", "size.png")
            self.capture(root, "after", "size.png", size=(129, 100))
            with self.assertRaises(SystemExit):
                self.run_comparison(root)
            rows = json.loads((Path(root) / "comparison/image-matrix.json").read_text(encoding="utf-8"))
            self.assertEqual(rows[0]["status"], "size mismatch")

    def test_matching_images_do_not_hide_failed_capture_suite(self):
        with tempfile.TemporaryDirectory() as root:
            for phase in ("before", "after"):
                self.capture(root, phase, "same.png")
                (Path(root) / phase / "suite/status.txt").write_text("FAIL" if phase == "after" else "PASS", encoding="utf-8")
            with self.assertRaises(SystemExit):
                self.run_comparison(root)
            summary = json.loads((Path(root) / "comparison/summary.json").read_text(encoding="utf-8"))
            self.assertEqual(summary["exact"], 1)
            self.assertEqual(summary["failed_or_missing_suites"], 1)


if __name__ == "__main__":
    unittest.main()
