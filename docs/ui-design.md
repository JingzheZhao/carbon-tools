# Sitewise — UI and interaction design

> Interface skeleton, interaction mechanics, and result presentation specs.
> Main references: the infrared.city Platform (studied hands-on, 2026-08-28), and
> Carbon Conscience (as a counter-example).
> Updated 2026-08-28.

> Companion documents: `system-design.md` (the seven-layer architecture; this
> document expands its L5 presentation layer) and `decision-record.md`.

---

## 0. Conclusion first

The strongest thing about infrared.city is not its 3D view. It is the structure
they use to **explain a single number**. That happens to be exactly the lesson we
most need to learn.

Three things worth porting directly:

1. **The seven-part ladder in the result panel** (the most valuable)
2. **Canvas-as-application** as the layout skeleton
3. **Scenarios as first-class objects, with progressive density**, for comparison

---

# Part 1 — The result panel: a seven-part ladder

Open any analysis result and the order is always these seven parts, never fewer.
This is the structure most worth copying verbatim.

## 1. What the seven parts are

| Part | Content | infrared's example | Our equivalent |
|---|---|---|---|
| ① **Metadata** | extent · area · count · generated at | `Site · 0.343 km² · 5 tiles · Generated 28 Aug, 22:15` | `Plaza · 1000 m² · 27 elements · synced 14:02` |
| ② **One-sentence definition** | what this metric is, and who it is for | "Felt temperature, combining air temperature, wind, humidity and radiation — the standard measure of pedestrian heat stress" | "Embodied carbon: the one-off emissions from making, moving, and installing materials" |
| ③ **FINDINGS narrative** | a plain-language conclusion carrying numbers | "Mean 21.3 °C, peak 24.0 °C. 100% of the area sits in the comfort band (9–26 °C, no heat stress); 0% in strong heat stress (>32 °C, a health risk to vulnerable people under prolonged exposure)" | see §2 for how to write ours |
| ④ **KPI tiles** | 2×2, four numbers | mean / peak / share in comfort / share in heat stress | embodied / sequestered / net / carbon-neutral year |
| ⑤ **Distribution** | with a colour ramp, labelled min/μ/max | histogram | stacked bars by element category |
| ⑥ **Context** | compared against a benchmark | "EPW summer high 33.2 °C… a UTCI above ambient indicates urban heat amplification" | against industry benchmarks and 2030 targets |
| ⑦ **METHODOLOGY + ASSUMPTIONS** | method plus a list of assumptions (collapsed) | see §3 | see §3 |

## 2. How to write ③ FINDINGS

Two moves in infrared's narrative are worth learning:

1. **Name the threshold**, do not just report the number — "0% in strong heat
   stress (**>32 °C**)"
2. **Name who is affected** — "a health risk to **vulnerable people** under
   **prolonged exposure**"

Our carbon reporting follows the same pattern:

> ❌ "Embodied carbon 221 t, sequestration −18 t, net 203 t."
>
> ✅ "This scheme emits 221 tonnes, and the site's planting will only take back 18
> of them over sixty years. **Net 203 tonnes — eleven times what this plaza can
> sequester in thirty years, spent at once.** That is 42% above the ASLA 2030
> target line; turning the 30 m retaining wall into a graded slope brings it back
> under."

The rule: **number + threshold + who is affected + what to do next**.

## 3. ⑦ ASSUMPTIONS — the part most worth copying

infrared writes its assumptions like this:

```
▾ ASSUMPTIONS
  · Weather: EPW from Bishop-Toronto.City.AP, CAN
    (0.0 km from site, fetched 2026-08-29)
  · Ground material emissivity per the layer
    (asphalt, concrete, soil, vegetation, water)
  · Stress thresholds per ISO 7730 UTCI categories
```

Note what it discloses:

- **how far the weather station is from the site** (0.0 km)
- **when the data was fetched** (2026-08-29)
- **which standard the thresholds come from** (ISO 7730)

**This is our provenance mechanism in finished form** — and it proves the
approach holds up in a real product rather than being wishful thinking.

Ours would read:

```
▾ Assumptions (7)
  · Light fitting density 0.5%/m² — CC default; no fittings found in the model
    [draw fittings in Rhino to replace this]
  · Reinforcement ratio — CC assembly 033001.21, 95% coverage
  · Carbon factor source — Carbon Conscience V2 dataset (2023),
    low/mid/high bands; where citations >5 the mid value is a true median
  · Sequestration curve — logistic, maturity at 80 years (woody),
    projected to year 60
```

**Every assumption carries a sentence saying how to turn it into a measurement.**
That is where we go one step further than infrared.

## 4. The visual language of provenance

Follow drafting convention — solid means measured, dashed means assumed:

| Provenance | Mark | Colour |
|---|---|---|
| MEASURED | solid underline / filled chip | primary (blue) |
| INFERRED | dotted / outlined chip | primary (blue) |
| ASSUMED | dashed / tinted chip | secondary (ochre) |

**Every number carries its mark**, and opening it shows "these 12 tonnes come
from the 30 m retaining wall you drew."

---

# Part 2 — Layout skeleton

## 5. Canvas as application

```
┌─ Top bar: logo · mode switch · project status · avatar ──────┐
├─┬────────────┬──────────────────────────────┬─────────────┬─┤
│ │ left panel │                              │ right card  │ │
│l│ ┌──────────┐│                              │ project name│r│
│e│ │Materials/││        canvas fills          │ 1000 m²     │i│
│f│ │Results ③ ││        the whole screen      │ scenario    │g│
│t│ └──────────┘│        (three.js)            │ chips + add │h│
│ │ search      │                              │ Layers |    │t│
│ │ categories  │   panels float over canvas   │ Compare     │ │
└─┴────────────┴──────────────────────────────┴─────────────┴─┘
```

Four rules:

1. **The canvas fills the screen; panels float over it.**
   Carbon Conscience splits the window rigidly down the middle, so you can only
   ever see half the drawing. Since we are doing 3D, we should follow infrared's
   floating approach even more than they do.

2. **The left panel stacks "input" and "output" as tabs, not as two pages.**
   `Materials` / `Results ③` (with a count badge). The user never navigates away.

3. **The two icon rails have distinct jobs.**
   - far left = application areas (project / materials / history / settings)
   - far right = viewport tools (camera, north arrow, measure, text, sun angle,
     pin, section)
   - "what am I doing" and "how am I looking" stay separate.

4. **The right card is status, not content.**
   Project name + area + scenario chips + three actions. Tiny, but always there.

## 6. Colour carries through

Decision record §13 already settled this: colour = material family. That palette
must run the whole way through:

```
Rhino viewport → web 3D → legend → pie/bar charts → report
```

The visual language is defined by the user, so we do not need to design a palette
at all. This is an advantage over infrared, whose fixed heat-map ramp gives the
user no say.

---

# Part 3 — Interaction mechanics

## 7. Scenarios are first-class

infrared's approach: a `Baseline •` chip, `+ Add`, and `Compare`.

In the comparison view, **columns are scenarios and rows are metrics**. Adding an
option adds a column.

```
                    Baseline        Option A        Option B
                    OSM baseline    permeable       keep trees
                                    paving
  ─────────────────────────────────────────────────────────────
  Embodied           221 t          198 t           176 t
  Sequestered        −18 t          −18 t           −64 t
  Net                203 t          180 t           112 t
  Carbon-neutral yr  —              —               year 47
```

This is exactly the A/B comparison decision record §7 asks for.

## 8. Total / Δ / % as a three-way toggle

One set of data, three ways of saying it, one button:

| Mode | Shows | Audience |
|---|---|---|
| **Total** | 203 t | absolute quantity, for engineers |
| **Δ** | −23 t | difference from baseline, for design decisions |
| **%** | −11% | percentage, for client presentations |

Very cheap, very useful. **Build it.**

## 9. Progressive density

infrared uses one toolbar to render the same content at five densities:

```
large cards → small cards → rows → hidden → plain table
```

**The table setting is the form that goes straight into a report.** That resolves
the tension between "presenting from the screen" and "delivering a document"
without building two interfaces.

## 10. Empty states teach the next step

infrared:

> "1 scenario · 2 workflows — you're looking at your baseline; **add a scenario in
> Setup to compare**"

Ours:

> "Only one scheme — you are looking at the baseline. Change a material in Rhino,
> sync again, and you can compare."

**An empty state is not "no data". It is instructions for the next action.**

## 11. Cart before checkout (as a principle only)

infrared's analyses cost tokens. The price sits on the card (`200 tokens`) with
an explanation ("Pricing scales with your project area"), and you open Details
before you `Add to cart`. **Deciding and spending are separated.**

Our tool is free and open source, so **the entire metering UI is deleted**. But
the principle — see the cost before you commit — survives: give an estimate
before a long computation, such as a year-by-year sixty-year sequestration
projection or a large model sync.

---

# Part 4 — Catalogue cards, and one important distinction

## 12. infrared's workflow card

```
┌────────────────────────────────────────┐
│ 🔥 Heat Island — Extreme Conditions    │  ← 5 ANALYSES
│    Stress-test how your site performs  │
│    in extreme heat and find where risk │
│    concentrates                         │
│    [Engineering] [Stakeholder]          │  ← audience tags
│    200 tokens  ✓ Validated   Details → │  ← price + credibility badge
└────────────────────────────────────────┘

Expanded:
  ABOUT       two paragraphs — what it does, when to use it
  Methodology blog ↗
  ✓ Validated against heatwave-period climate records
  USE CASES   four, written in the voice of tasks to be done:
              · District worst-case resilience screening
              · Climate adaptation extreme-scenario planning
              · Making the case for emergency cooling investment
              · Mapping heat risk for vulnerable populations
```

## 13. The distinction that matters: which axis the catalogue sits on

**A catalogue entry point is not wrong in itself. It is wrong on the wrong axis.**

| Applied to | Result |
|---|---|
| ❌ **materials** | you get Carbon Conscience's 193 composite scenarios and the user cannot tell which to pick |
| ✅ **questions** | "Does this scheme meet the 2030 target?" "Is keeping this row of trees worth it?" One workflow bundles several analyses |

What the system design cancelled was **the 20 scenario entry points on the
material axis**. **A workflow catalogue on the question axis should be built.**

Candidate workflows:

- **Target screening** — against ASLA 2030, ILFI Zero Carbon, Atelier Ten 185
- **Retain vs. clear** — the two-curve comparison for existing vegetation
- **Material substitution sweep** — search for low-carbon alternatives across
  every "assumed" slot
- **Carbon-neutral year** — under what conditions does the curve cross zero

## 14. Two details worth borrowing

1. **Audience tags** (Engineering / Stakeholder) — they tell the user which
   register the output is written in, and they help us decide report templates.
2. **The Validated badge and its evidence** — for an open-source tool, "built on
   the Carbon Conscience V2 dataset, reconciled digit-for-digit against it" is
   our badge. The verification table in system design §5, where 10 m = 173.9
   matches exactly, is the evidence behind it.

---

# Part 5 — Three things not to copy

## 15. Token metering

Our tool is free and open source; the whole metering UI is removed. See §11.

## 16. Geometry editing limited to pushing blocks around

infrared's edit mode offers only **move / rotate / delete** on building masses:

```
Edit Buildings
  Move    ↑↓
  Rotate  Alt + ↑↓
  Delete  ⌫
```

(see `_research/ui-references/04-edit-buildings-shortcuts.png`)

**That is their ceiling, and it is our starting point.** Our geometry comes from
Rhino, at a completely different order of precision and freedom. This difference
should be held onto in how the product is described.

## 17. Black-box composition

infrared's "5 analyses" is a black box — the user cannot see how the five are
combined or what each contributes.

**Our provenance mechanism is the opposite: transparent.** That difference is our
core asset and should not be traded away for a cleaner interface. Collapsing it
is fine; hiding it is not.

---

# Appendix: reference screenshots

| File | Content |
|---|---|
| `_research/ui-references/01-thermal-comfort-july.png` | thermal comfort result panel |
| `_research/ui-references/02-thermal-comfort-august-toolbar.png` | toolbar |
| `_research/ui-references/03-wind-speed.png` | wind speed analysis |
| `_research/ui-references/04-edit-buildings-shortcuts.png` | mass editing and shortcut panel |
| `_research/ui-references/05-thermal-comfort-duplicate.png` | scenario duplication |

**Additional observations from the hands-on session** (2026-08-28,
`platform.infrared.city/workflows`): the seven-part result panel, the Comparison
study modal (Runs / Across scenarios / Within scenario, plus Total/Δ/% and the
five density settings), the workflow catalogue cards and detail panel, and the
Add to cart flow.
