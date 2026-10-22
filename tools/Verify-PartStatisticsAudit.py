"""Independently cross-check the generated, read-only Unity statistics audit."""
from pathlib import Path
import collections
import csv
import json
import math
import re
import sys

root = Path(__file__).resolve().parents[1]
folder = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "reports/part-statistics-audit-20261021"
def rows(name):
    with (folder / name).open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))
def check(ok, message):
    assert ok, message
    log.append("PASS " + message)
log = []
catalog = json.loads((folder / "catalog-snapshot.json").read_text(encoding="utf-8-sig"))["parts"]
stock, effects, categories = rows("stock-matrix.csv"), rows("part-effects.csv"), rows("category-matrix.csv")
findings, sensitivity = rows("finding-matrix.csv"), rows("sensitivity-matrix.csv")
summary = (folder / "verification.txt").read_text(encoding="utf-8-sig")
totals = {key: int(value) for key, value in re.findall(r"^([^:\n]+): (\d+)$", summary, re.MULTILINE)}
check(len(catalog) == totals["Catalogue definitions"] == len({p["id"] for p in catalog}), "catalogue count and distinct IDs agree with the Unity receipt")
check(len(stock) == totals["Models"] == len({r["model"] for r in stock}), "stock matrix covers every model exactly once")
check(len(effects) == totals["Fitting combinations"] == len({(r["model"], r["part"]) for r in effects}), "replacement matrix contains every reported fitting pair exactly once")
check(sum(int(r["count"]) for r in categories) == len(catalog) and len(categories) == totals["Categories"], "category ranges account for the complete catalogue")
stock_by_model = {r["model"]: r for r in stock}
for r in stock:
    assert math.isclose(float(r["authored_hp"]), float(r["resolved_hp"]), rel_tol=1e-8, abs_tol=1e-6)
    assert not r["stock_warnings"]
    assert float(r["mass_kg"]) > 0 and float(r["safe_rpm"]) > 0
check(True, "stock output is preserved with positive mass/limits and no stock warnings")
changes_output = {"HeadIntake", "Induction", "Exhaust", "Pistons", "Camshaft", "FuelSystem", "Ignition", "Controls"}
rejected = []
for r in effects:
    if r["drivable"] == "False":
        rejected.append(r)
        assert "EFI" in r["error"] or "followers" in r["error"], r
        continue
    for key in ["hp", "torque", "mass_kg", "safe_rpm", "compression"]:
        assert math.isfinite(float(r[key])) and float(r[key]) > 0, r
    if r["category"] not in changes_output:
        assert math.isclose(float(r["hp"]), float(stock_by_model[r["model"]]["resolved_hp"]), rel_tol=1e-6, abs_tol=.02), r
check(len(rejected) == totals["Expected combination rejections"], "accepted replacement data is finite; rejected pairs require EFI controls or matching followers")
counts = {r["code"]: int(r["count"]) for r in findings}
mismatches = [r for r in sensitivity if r["probe"] == "authored-head-fuel-combination" and abs(float(r["first_value"]) - float(r["second_value"])) > .02]
check(len(mismatches) == counts["torque-helper-capacity-disagreement"], "reported capacity disagreements reproduce independently from per-model measured torque")
flywheels = [r for r in sensitivity if r["probe"] == "authoring-flywheel-mass-inertia"]
check(len(flywheels) == totals["Models"] and all(abs(float(r["first_value"])) < 1e-6 and math.isclose(float(r["second_value"]), float(r["third_value"]), abs_tol=1e-10) for r in flywheels), "detached flywheel mass/inertia edits demonstrably leave runtime values unchanged")
check(totals["Flagged invariant failures"] == 0, "baseline/control invariant failures are zero; review findings remain explicit")
receipt = "\n".join(log) + "\nLIMIT Dataset cross-checks do not establish real-world calibration or resolve the reported contracts.\n"
(folder / "independent-verification.txt").write_text(receipt, encoding="utf-8")
print(receipt)
