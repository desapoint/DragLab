"""Independently cross-check the generated, read-only Unity statistics audit."""
from pathlib import Path
import collections
import csv
import json
import hashlib
import math
import re
import sys

root = Path(__file__).resolve().parents[1]
folder = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "reports/part-statistics-audit-20261021"
fixed = "--expect-fixed" in sys.argv[2:]
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
check(len(mismatches) == counts.get("torque-helper-capacity-disagreement", 0), "reported capacity disagreements reproduce independently from per-model measured torque")
flywheels = [r for r in sensitivity if r["probe"] == "authoring-flywheel-mass-inertia"]
if fixed:
    check(not mismatches and not any(r["severity"] in {"review", "error"} for r in findings), "fixed audit has no capacity disagreement or unresolved software-contract findings")
    check(len(flywheels) == totals["Models"] and all(math.isclose(float(r["first_value"]), 2, abs_tol=1e-5) and math.isclose(float(r["third_value"]), 1.5 * float(r["second_value"]), abs_tol=1e-7) for r in flywheels), "absolute flywheel edits independently change translating mass and rotational inertia")
    blocks = [p for p in catalog if p["category"] == 37]
    check(len(blocks) == totals["Models"] * 2 and all(p["rpmLimit"] > 0 and p["torqueLimitNm"] > 0 for p in blocks), "every authored block carries positive torque and RPM ratings")
    check(all(math.isclose(float(r["safe_rpm"]), float(r["simulation_safe_rpm"]), abs_tol=.01) for r in stock), "stock resolver and global protection thresholds agree")
    baseline = root / "reports/part-statistics-audit-20261021"
    before_contract = json.loads((folder / "baseline-contracts.json").read_text(encoding="utf-8-sig"))
    allowed = {"rpmLimit", "torqueLimitNm", "description"}
    canonical = [{k: v for k, v in p.items() if not (p["category"] == 37 and k in allowed)} for p in sorted(catalog, key=lambda p: p["id"])]
    fingerprint = hashlib.sha256(json.dumps(canonical, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("utf-8")).hexdigest()
    check(len(catalog) == before_contract["definitions"] and fingerprint == before_contract["unchangedCatalogueSha256"], "authored identity/specification baseline is preserved except the explicit block ratings/description")
    with (baseline / "stock-matrix.csv").open(encoding="utf-8-sig", newline="") as stream:
        old_stock = {r["model"]: r for r in csv.DictReader(stream)}
    check(all(math.isclose(float(r["resolved_hp"]), float(old_stock[r["model"]]["resolved_hp"]), abs_tol=1e-6) and math.isclose(float(r["mass_kg"]), float(old_stock[r["model"]]["mass_kg"]), abs_tol=1e-5) for r in stock), "all pre-fix stock HP and translating masses are preserved")
else:
    check(len(flywheels) == totals["Models"] and all(abs(float(r["first_value"])) < 1e-6 and math.isclose(float(r["second_value"]), float(r["third_value"]), abs_tol=1e-10) for r in flywheels), "detached flywheel mass/inertia edits demonstrably leave runtime values unchanged")
check(totals["Flagged invariant failures"] == 0, "baseline/control invariant failures are zero; review findings remain explicit")
limit = "Dataset cross-checks do not establish real-world engineering calibration." if fixed else "Dataset cross-checks do not establish real-world calibration or resolve the reported contracts."
receipt = "\n".join(log) + "\nLIMIT " + limit + "\n"
(folder / "independent-verification.txt").write_text(receipt, encoding="utf-8")
print(receipt)
