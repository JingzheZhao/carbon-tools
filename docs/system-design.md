# Sitewise — system design

> A measurement-first carbon model — Rhino/Grasshopper plus a web front end.
> Built on the complete Carbon Conscience V2 dataset.
> Updated 2026-08-28.

> Companion documents: `decision-record.md` (product and methodology decisions)
> and `ui-design.md` (interface and interaction specification).

---

## 0. The claim, in one sentence

> **Never ask the user for anything the model can measure. Whatever cannot be
> measured, label explicitly as an assumption.**

Every architectural decision below follows from that rule.

---

# Part 1 — Diagnosis

## 1. The problem we are actually solving

It is not "Carbon Conscience has 230 types and that is too many." It is:

> **Carbon Conscience cannot see geometry, so it has to disguise geometric
> questions as multiple-choice questions and hand them to the user.**

Carbon Conscience offers a concrete plaza in three grades and asks you to pick:

- `minimal walls (1% or less)`
- `some walls (5%)`
- `significant walls (10%)`

Translated honestly, the question it is asking is:

> "On this 1000 m² plaza of yours, is there **10 m**, **50 m**, or **100 m** of
> 1-metre-high retaining wall?"

At concept stage no designer has counted the length of the retaining walls, so
the user can only guess. Yet in Rhino those walls are sitting right there,
accurate to the millimetre.

## 2. What a wrong guess costs (measured)

The same 1000 m² plaza, varying only that one dropdown — units tCO₂e:

| CC option | Implied wall | Low | Mid | High |
|---|---:|---:|---:|---:|
| minimal walls (1% or less) | 10 lm | 131.0 | 173.9 | 240.6 |
| some walls (5%) | 50 lm | 215.6 | 286.8 | 394.3 |
| significant walls (10%) | 100 lm | 334.4 | 446.1 | 610.1 |

**One dropdown, a 2.6× swing worth 272 tCO₂e, decided by a guess.**

This is not a one-off. Holding everything else constant and varying only wall
density, the multiplier is remarkably stable:

| Paving family | 1% walls | 5% walls | 10% walls | Ratio |
|---|---:|---:|---:|---:|
| CIP vehicular concrete | 227 | 337 | 491 | 2.2× |
| Clay brick (sand-set) | 199 | 306 | 463 | 2.3× |
| Monolithic stone (sand-set) | 119 | 198 | 315 | 2.6× |
| Precast / CMU concrete | 285 | 374 | 542 | 1.9× |
| Stone veneer (mortar-set) | 236 | 340 | 496 | 2.1× |

## 3. Key finding: that dropdown controls far more than walls

Diffing the three assemblies line by line shows it moves **21 of the 25
sub-elements** at once:

| Sub-element | 1% | 5% | 10% | Why it moves |
|---|---:|---:|---:|---|
| 1M CIP retaining wall `033001.04` | 1% | 5% | 10% | the wall itself |
| 100 mm CIP paving `033001.01` | 95% | 90% | 83% | **the wall takes paving's ground** |
| 150 mm aggregate base `321100.01` | 95% | 90% | 83% | follows the paving |
| Light poles / fittings / bases `265600.*` | 0.5% | 1% | 2% | complex sites get more lighting |
| Stainless drainage structures `334000.01` | 1% | 1% | 3% | more level change means more drainage |
| Railing paint / metal / timber `099001, 323300` | 1% | 2% | 4% | walls bring railings |

**Conclusion**: it is not a "wall density" control. It is a bundled **site
complexity tier**.

And those 21 linked elements have **completely different knowability** — Rhino
can measure walls and paving; it cannot measure light fittings and drainage
channels. Bundling them into one dropdown is Carbon Conscience's compromise. It
should not be ours.

This finding determines the system's core abstraction directly.

---

# Part 2 — The core abstraction

## 4. Every number carries a provenance

No carbon figure in the system is bare. Each one must answer: **were you
measured, inferred, or assumed?**

| Provenance | Where the quantity comes from | Examples | What the user can do |
|---|---|---|---|
| **MEASURED** | straight off the Rhino geometry | paving area, wall area and height, deck area, tree count and position | change the model |
| **INFERRED** | a measured value plus one written rule | paving area = plan area − wall footprint; climate zone = project location; base course area = paving area | change the rule (rarely needed) |
| **ASSUMED** | a Carbon Conscience assembly default | reinforcement ratio, light fitting density, drainage structure percentage, paint coats | change the value, or go and draw it in the model |

## 5. Four properties this abstraction buys

### ① Graceful degradation — verified

Draw nothing but a plan boundary and the system falls back to exactly Carbon
Conscience's answer, to the digit. Every additional thing you draw replaces one
assumption with a measurement.

Prototype results (1000 m² plaza, tCO₂e):

| Wall measured in the model | Low | Mid | High | Comparison |
|---|---:|---:|---:|---|
| 0 m (flat paving only) | 113.0 | 150.1 | 207.9 | CC has no such tier |
| **10 m** | **131.0** | **173.9** | **240.6** | **matches CC 1% exactly** |
| 30 m ← the real site | 167.0 | 221.5 | 306.2 | CC can only offer 174 or 287 |
| 50 m | 203.1 | 269.1 | 371.7 | CC 5% = 286.8 |
| 100 m | 293.1 | 388.0 | 535.5 | CC 10% = 446.1 |

**The 10 m row reproduces Carbon Conscience digit for digit.** That proves we are
not starting a rival model but thawing theirs — it was pinned to three points, and
it is now continuous.

Prototype code: `tools/resolver_prototype.py`, runnable as-is.

> **An honest boundary**: the 50 m and 100 m rows come in below Carbon
> Conscience because its higher tiers also raise lighting and drainage, while we
> only replaced the wall and paving we could measure. **That is the correct
> behaviour** — light fittings and drainage channels are assumptions and should
> not be dragged along by wall length. But the interface must say so: those two
> remain assumed values.

### ② The AI's foothold is defined automatically

The AI **only touches "assumed" slots**. Measured values are facts and should not
be proposed away; assumed values are where "you could do this differently" lives.

This incidentally settles the point in decision record §9 that AI suggestions
require replaceable slots — **the slots do not need designing, the provenance
label is the slot.**

### ③ Precision is visible and cumulative

The user can see how much precision one more drawn object bought, instead of
facing a black box.

### ④ The report can prove itself

Every number traces back either to "the 30 m wall you drew" or to "Carbon
Conscience's default light fitting density." No competitor offers that.

---

# Part 3 — Seven layers

Data flows top to bottom. Each layer does one thing and talks only to its
neighbours, so the Grasshopper plugin never has to be re-released because the
material library changed.

## L0 — Data (static read-only JSON, never in a database)

**Contents**

| File | Rows | Contents |
|---|---:|---|
| `cc_site_elements.json` | 259 | element carbon factors (low/mid/high, plus sequestration and storage) |
| `cc_landuse_surface.json` | 193 | landscape assemblies, with full subItems percentages |
| `cc_landuse_building.json` | 216 | building benchmarks, above and below grade separated |

**Completeness verified**: all 214 element ids referenced by assemblies resolve.
Zero dangling references.

**Elements come in four units** — which is why the L2 protocol sends every
dimension at once:

| Unit | Rows |
|---|---:|
| area m² | 219 |
| count EA | 15 |
| length LM | 13 |
| wall footprint m² (height built into the element, 1M and 2M tiers) | 12 |

**The only new work**: tagging every sub-element in every assembly as measured,
inferred, or assumed. About 25 categories, done once by hand.

**Out →** an element table carrying provenance labels

## L1 — Rhino / Grasshopper (measure only, never judge)

**Components**

```
┌─────────────────────┐        ┌─────────────────────┐
│ CC Group            │        │ CC Sync             │
│  ▸ Geometry         │───┐    │  ▸ Objects  (+ port)│
│  ▸ Color ■          │   ├───▸│  ▸ Run      (toggle)│
│  ◂ Tagged Objects   │   │    │  ▸ Open     (button)│
└─────────────────────┘   │    │  ◂ Status / Port    │
      (copy as needed) ───┘    └─────────────────────┘
```

**Every object must yield:**

- plan area (horizontal projection)
- **vertical face area and mean height** ← new, and the key to the whole approach
- length / count / volume
- bounding box and mesh

**Splitting vertical from horizontal**: by the Z component of the face normal,
threshold 60°. `|normal.Z| > cos(60°)` means horizontal, otherwise vertical.

The Grasshopper side needs no material knowledge whatsoever.

**Out →** the full set of dimensions for each colour group

## L2 — Protocol

Extending decision record §15:

```json
{ "meta": { "units", "origin_offset", "timestamp", "mesh_tolerance" },
  "groups": [
    { "color": "#RRGGBB",
      "objects": [
        { "id": "geometry hash",
          "quantity": {
            "area": 0,            // plan projection area
            "vertical_area": 0,   // new
            "mean_height": 0,     // new
            "count": 0, "length": 0, "volume": 0
          },
          "mesh": { "vertices": [...], "faces": [...] } }
      ] } ] }
```

**Five conventions** (carried over from decision record §15):

1. **Compute and send every dimension together** — elements come in four units
   and Grasshopper cannot know which will be used, so send them all
2. **The id is a geometry hash** — unchanged geometry keeps its id, which is the
   basis for incremental updates. A sequence number will not do.
3. **Units are converted to metres on the Grasshopper side**
4. **`origin_offset` is mandatory** — landscape models routinely carry real
   survey coordinates, and three.js float32 jitters and tears at that magnitude
5. **Coordinates stay Z-up as Rhino has them**; the web side converts to
   three.js's Y-up

**Out →** JSON, over a local file or a WebSocket

## L3 — Resolver (the heart of the system)

**In**: geometric quantities, colour, project location
**Out**: an element schedule carrying provenance labels

**Three resolution rules:**

| Input | → | Output | Choices removed |
|---|---|---|---|
| colour | → | material family (the user's only genuine design decision) | — |
| vertical area ÷ plan area | → | wall quantity, and by inference paving = plan − wall footprint | 26 |
| project location | → | climate zone | 48 |
| face normal direction | → | walls, steps, railings identified automatically | 11 |

Any sub-element not covered above **takes the Carbon Conscience default and is
labelled assumed**.

> Of 193 landscape assemblies, 85 (44%) are resolved automatically by geometry
> and location. The remaining 108 are genuine material choices.

## L4 — Carbon engine

**One-off carbon**

```
carbon = Σ(quantity × carbon factor)
low / mid / high computed simultaneously
```

**Sequestration curve** (following Carbon Conscience, a logistic S-curve):

```
Ct = M / (1 + e^(−r × (t − M/2)))

M = carbon carrying capacity = total carbon potential of the mature system (TCP)
r = distributed sequestration rate, fitted to the maturity year
    woody ecosystems 80 years / grassland 20 years
Wetlands are the exception — linear regression
```

**Five accounts, laid onto a timeline by year and accumulated:**

| # | Account | When it occurs | In CC? |
|---|---|---|---|
| ① | material production A1-A3, transport A4, construction A5 | year 0 | ✅ |
| ② | loss of existing condition (tree removal, soil disturbance) | year 0 | ✅ |
| ③ | maintenance B2-B3 | recurring | ❌ **not included** (see §7) |
| ③ | replacement B5 | pulses at end of service life | ✅ |
| ④ | plant and soil sequestration (non-linear) | annual | ✅ |
| ⑤ | end-of-life C | end of period | ❌ not included |

**Study period 60 years**, following Carbon Conscience V2 — their sequestration
figures are projected to year 60, so using their data requires matching it.

**Two curves are always computed**: the design, and a do-nothing baseline. The
area between them is the design's real cost or contribution.

**Out →** an annual time series, a cumulative net carbon curve, and the
carbon-neutral year

## L5 — Presentation

Full specification in `ui-design.md`. The essentials:

- the three.js viewport is coloured by group, using the same palette as the Rhino
  viewport
- the primary chart is the **two-curve comparison**
- every number displays its **provenance**, and opening it shows "these 12 tonnes
  come from the 30 m wall you drew"
- **the low and high bounds are not error bars, they are the space of design
  choices** — the lower bound is what you would reach by choosing the low-carbon
  option everywhere. That makes it an actionable target, not a disclaimer.

## L6 — Back end

```
geometry (large, confidential, not needed for comparison)  →  local only, never uploaded
scheme snapshot (mapping + quantities + results + thumbnail) →  cloud, tens of KB
material library (read-only public data)                     →  static JSON, not a database
```

Three benefits: **privacy** (many practices forbid uploading models), **cost** (a
free-tier database holds thousands of users at tens of KB each), and **offline
use** (the tool is fully usable without signing in).

The cost: reopening a six-month-old scheme shows data, charts, and a thumbnail
but no 3D. Acceptable.

The back end must be **self-hostable and replaceable**.

> **Superseded 2026-08-30.** Decision record §17 replaces this layer: phase one
> ships with no back end at all. Schemes are kept in browser local storage and
> the material library is a versioned file in the repository.

## The AI layer (cross-cutting, not a layer of its own)

Tool calling operates only on "assumed" slots. Typical scenarios:

- "What if this plaza's paving became permeable pavers?" → swap the material
  family, recompute, report the difference
- "How do I get this scheme carbon-neutral within 30 years?" → search across the
  assumed slots
- "Why is this number so high?" → walk back up the provenance chain

---

# Part 4 — User flow

For contrast: Carbon Conscience makes the user redraw the polygon in a web page
and then pick one of 193 composite scenarios.

1. **Pick a colour in Rhino and feed the geometry into a CC Group component.**
   Colour is the material family. The same palette runs through the Rhino
   viewport, the web 3D view, the legend, the charts, and the report — the visual
   language is defined by the user.

2. **Turn on Sync; the model and a first carbon result appear in the browser.**
   At this moment everything geometric is already measured and the rest are
   Carbon Conscience defaults. The result is usable immediately, with no forms to
   fill in first.

3. **Enter the project location once, in the web page.**
   That fixes the climate zone, resolves 48 ecosystem assemblies, and is never
   asked again.

4. **Follow the "assumed" labels downward.**
   For more precision, go and draw the lighting, the drainage, the trees — or
   simply edit that assumed value in the web page.

**At no point does the user pick a "scenario."**

The original plan to build 20 scenario entry points covering 80% of cases is
therefore cancelled. That plan was compressing a list the user should never have
had to work through item by item, and it traded precision for ease of use. This
approach gets both.

---

# Part 5 — Conclusions on two open items

## 6. Individual trees — do it, and it is cheap

The decision record originally said Carbon Conscience **deliberately** measures by
area rather than stem density. **The data says otherwise.**

Its urban tree sequestration is already parameterised by **spacing**; it has
simply been frozen at three points:

| Canopy cover | Spacing | m²/tree | Sequestration kgCO₂e/m² | Back-solved: per tree |
|---|---:|---:|---:|---:|
| 17% | 12 m | 144 | −2.66 | −383 |
| 35% | 6 m | 36 | −5.63 | −203 |
| 50% | 4 m | 16 | −8.51 | −136 |

The element names in the street tree assembly are blunter still:

```
5 cm Cal. B&B/Box Tree Planting (1 Tree/10 SM)
```

**One tree per 10 m², hard-coded.**

In Rhino a tree is already a point or a block, so count, position, and spacing
are all there. Three-point interpolation is all that is needed — almost no work.

> ⚠️ **Per-tree sequestration falls as density rises** (competition). It must
> never be written as `N × constant`; it must be interpolated along this curve.
> This is also good material for the argument: "plant twice as many trees and you
> sequester less than twice as much."

## 7. Maintenance carbon — not in phase one, but for a different reason

Searching all 259 elements: there is **not one** maintenance or operational
carbon figure. The 10 hits are all A1-A3 production carbon for irrigation
**hardware** — pipe, heads, controllers, weather stations, pumps. Not a drop of
water, not a single mowing.

**The key implication**: doing maintenance carbon means **leaving the Carbon
Conscience dataset**. We would build our own factors, find our own literature, and
carry our own controversy — which is precisely what dismantles the core argument
that using CC's data saves 3–5 person-years.

**Recommendation:**

- do not compute it in phase one
- surface the irrigation hardware, which is in the data, separately as a hook
- state plainly in the report: "consistent with Carbon Conscience, excludes B2–B3
  operational carbon"
- schedule it as a phase-two module with its own literature basis

That keeps the methodological boundary clean and stops one module from
undermining the legitimacy of the whole data strategy.

---

# Part 6 — Sequence of work

Every step must run and show a result on its own. Do not start with the hardest
part.

1. **L0 provenance tagging first.**
   Pure data work, can start without writing code, and it defines the shape of
   every interface that follows. About 25 categories.

2. **L3 resolver and L4 carbon engine, plain Python/JS, fed with fake data.**
   The prototype already runs and reconciles digit for digit with Carbon
   Conscience. Nail the algorithm before touching interfaces or plugins.

3. **L1 Grasshopper components, writing JSON to a file.**
   Leave WebSocket alone for now. `CC_Group` colour grouping plus the
   vertical/horizontal area split. The `.gha` must be compiled locally.

4. **L5 web skeleton: three.js reads the JSON, colours it, lists groups.**
   At this point it is a complete usable loop, just with manual file transfer.

5. **Swap the transport for WebSocket.**
   The protocol does not change; the web side edits one function. The risk is
   isolated to the last step.

6. **L6 back end, AI layer, report export.**
   All additions to a loop that already works, and can proceed in parallel.

---

# Part 7 — Decision record entries needing revision

| Section | Was | Becomes |
|---|---|---|
| §1 differentiator #4 | "coarser granularity" | **"fewer questions, without sacrificing precision"** — geometry and location resolve 44% of the choices |
| §5 plant measurement | "CC deliberately avoids per-tree counts; reasoning needs scrutiny" | CC's own data is parameterised by spacing; do individual trees, interpolating along the density decay curve |
| §9 taxonomy | "build 20 scenario entry points" | **Cancelled.** Replaced by the provenance mechanism and a single-axis material picker |
| §2 data strategy | "email sent, awaiting reply" | data obtained and verified; still need to pursue the **Zotero bibliography** |
| §15 protocol | area / count / length / volume | add **vertical face area** and **mean height** |
| §24 open items | three open | three resolved |

---

## Compliance note

The white paper's CC BY-NC 4.0 licence covers use of the dataset, and this
project's use — open source, free, attributed — satisfies all three conditions.

But the data was extracted from the application front end, which makes
**attribution and source traceability no lighter an obligation**. Continuing to
pursue the Zotero bibliography is a hard requirement for an open project's
credibility: the data carries values, but no source for any individual value.

**Citation:**

> Hardy, C., & Frechette, M. (2023). *Designing with a Carbon Conscience V2: A
> web-based application to inform planning and urban design projects on potential
> carbon impacts.* Sasaki Associates. CC BY-NC 4.0.

---

## Appendix: data files

| File | Location | Contents |
|---|---|---|
| Site elements | `data/cc_site_elements.json` | 259 rows, three carbon factor bands |
| Site elements (tabular) | `data/carbon_conscience_data_table.csv` | as above, CSV |
| Landscape assemblies | `data/cc_landuse_surface.json` | 193 rows, with subItems |
| Building benchmarks | `data/cc_landuse_building.json` | 216 rows, above/below grade separated |
| Resolver prototype | `tools/resolver_prototype.py` | runnable, demonstrates graceful degradation |
