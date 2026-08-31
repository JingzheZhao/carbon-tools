"""
Prototype of the core idea: rebuild a CC landuse from MEASURED geometry
instead of from CC's frozen 1% / 5% / 10% wall-density guess.

Run:  python resolver_prototype.py
"""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
E = {e["id"]: e for e in json.load(open(os.path.join(HERE, "cc_site_elements.json"), encoding="utf-8"))}
S = json.load(open(os.path.join(HERE, "cc_landuse_surface.json"), encoding="utf-8"))
BY_REF = {s["refNum"]: s for s in S}


def cc_way(ref, plan_area):
    """How Carbon Conscience does it: user picks a landuse, everything is
    a fixed percentage of plan area. The wall density is baked in."""
    lu = BY_REF[ref]
    lo = med = hi = 0.0
    for si in lu["subItems"]:
        e = E[si["id"]]
        lo += si["pct"] * e["costLow"] * plan_area
        med += si["pct"] * e["costMedian"] * plan_area
        hi += si["pct"] * e["costHigh"] * plan_area
    return lo, med, hi


def our_way(plan_area, wall_footprint_area, base_ref="49.00"):
    """How we do it: take the same recipe, but replace the *assumed* wall
    quantity with the one measured off the Rhino model.

    CC expresses walls as a fraction of plan area, where 1 m2 of footprint
    ~= 1 linear metre of 1m-high wall (1m-deep spread footer).
    """
    lu = BY_REF[base_ref]
    WALL_IDS = {"033001.04", "033001.22"}  # 1M CIP retaining wall + its rebar

    lo = med = hi = 0.0
    for si in lu["subItems"]:
        e = E[si["id"]]
        if si["id"] in WALL_IDS:
            qty = wall_footprint_area          # MEASURED
        else:
            qty = si["pct"] * plan_area        # recipe default
        lo += e["costLow"] * qty
        med += e["costMedian"] * qty
        hi += e["costHigh"] * qty
    return lo, med, hi


def t(x):
    return f"{x/1000:8.1f}"


PLAN = 1000.0
print("=" * 74)
print(f"  A {PLAN:.0f} m2 pedestrian concrete plaza.  Values in tCO2e.")
print("=" * 74)
print()
print("CC makes the user answer: 'minimal / some / significant walls?'")
print("Translated honestly, that question is: 'how many linear metres of")
print("1m-high retaining wall are on your site: 10, 50, or 100?'")
print()
print(f"  {'CC option':<34}{'implied wall':>14}{'low':>9}{'median':>9}{'high':>9}")
for ref, label, pct in [("49.00", "minimal walls (1% or less)", 0.01),
                        ("50.00", "some walls (5%)", 0.05),
                        ("51.00", "significant walls (10%)", 0.10)]:
    lo, med, hi = cc_way(ref, PLAN)
    print(f"  {label:<34}{pct*PLAN:>11.0f} lm{t(lo)}{t(med)}{t(hi)}")

lo1, m1, h1 = cc_way("49.00", PLAN)
lo3, m3, h3 = cc_way("51.00", PLAN)
print()
print(f"  --> Guessing wrong across this one dropdown swings the answer by")
print(f"      {(m3-m1)/1000:.0f} tCO2e ({m3/m1:.1f}x) on a single plaza.")
print()
print("-" * 74)
print("  Our way: don't ask. Measure the wall off the model.")
print("-" * 74)
print()
print(f"  {'measured wall on the model':<34}{'footprint':>14}{'low':>9}{'median':>9}{'high':>9}")
for lm in [0, 10, 30, 50, 100]:
    lo, med, hi = our_way(PLAN, lm)
    print(f"  {lm:>3} linear metres of 1m wall{'':<7}{lm:>11.0f} m2{t(lo)}{t(med)}{t(hi)}")

print()
lo, med, hi = our_way(PLAN, 30)
print(f"  A real site with 30 lm of wall = {med/1000:.0f} tCO2e.")
print(f"  CC would have forced it to {m1/1000:.0f} (if user said 1%) or {cc_way('50.00', PLAN)[1]/1000:.0f} (if 5%).")
print(f"  Both are wrong, and the user had no way to know which.")
print()
print("  Note the low-high band is preserved throughout: it is the")
print("  DESIGN CHOICE range (all-low-carbon vs all-conventional),")
print("  not an error bar. Geometry does not narrow it - material does.")
print()
