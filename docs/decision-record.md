# Sitewise — decision record

> A carbon assessment tool for landscape and urban design, linking Grasshopper to
> a web front end.
> Updated 2026-09-13.

> Companion documents: `system-design.md` (the seven-layer architecture and the
> provenance mechanism) and `ui-design.md` (interface and interaction spec).
>
> **Major update 2026-08-28**: the complete Carbon Conscience dataset has been
> obtained and verified, closing all four open items in §24 (dataset, scenario
> entry points, maintenance carbon, individual trees). The central change is §9 —
> the "20 scenario entry points" plan is cancelled in favour of the **provenance
> mechanism** (measured / inferred / assumed). See the system design document.

---

# Part 1 — Product and methodology

## 1. Product positioning

| Item | Decision |
|---|---|
| Nature | **open source, free, non-commercial** |
| Discipline | landscape and urban design; no detailed construction |
| Design stage | concept and early design |
| Comparable products | Carbon Conscience (Sasaki), Pathfinder (Climate Positive Design) |

**Differentiators:**

1. **Live calculation inside Rhino** — designers are already developing the
   scheme in Rhino and should not have to draw it again. Carbon Conscience makes
   the user draw polygons in a web page, which is both awkward and imprecise.
2. **3D and visual expressiveness** — Carbon Conscience leans on 2D and tables.
3. **AI suggestions and scheme comparison.**
4. **Fewer questions, without sacrificing precision** (revised 2026-08-28; was
   "coarser granularity"). Carbon Conscience's 230+ types are the product of
   material family × wall density × pedestrian/vehicular × climate zone, and the
   last three axes should never be put to the user. Geometry and project location
   resolve 44% of them automatically (85 of 193 landscape assemblies). The
   original "coarser granularity" traded precision for ease of use; that trade is
   neither valid nor necessary — both are available at once. See system design §4
   on provenance.

## 2. Data strategy (settled)

**Use the Carbon Conscience dataset directly as the data layer.**

The white paper's copyright page (p.3) grants this explicitly:

> Creative Commons **Attribution-NonCommercial 4.0 International License**
> "The associated application, graphics, **dataset**, and work are provided for
> free and public use… we ask for attribution and for any derivative works to be
> made **free and open source as well**."

Three conditions, all satisfied by this project:

| Condition | How it is met |
|---|---|
| Attribution | the dataset is cited as "Carbon Conscience Dataset by Sasaki"; individual data points are cited to their original sources; the application layer credits "Carbon Conscience" |
| Non-commercial | no charge, no paid tier, no subscription SaaS. The NC clause restricts distribution, not a designer using it on a commercial project — Sasaki is itself a design practice and intended exactly that |
| Open derivatives | this project is open source |

**Citation**: Hardy, C., & Frechette, M. (2023). *Designing with a Carbon
Conscience V2: A web-based application to inform planning and urban design
projects on potential carbon impacts.* Sasaki Associates.

**Why this decision matters**: it removes the need to rebuild 400+ literature
reviews, 100+ material carbon factors, 230+ landscape land uses, and 200+
building land uses — conservatively 3–5 person-years. All effort goes into the
Rhino link, the visualisation, and the AI instead.

**Status (updated 2026-08-28)**: **the dataset has been obtained and verified
complete**, and is no longer a blocker. Extracted from the application front end
into `data/`:

| File | Rows | Contents |
|---|---:|---|
| `cc_site_elements.json` / `carbon_conscience_data_table.csv` | 259 | element carbon factors (low/mid/high, plus sequestration and storage) |
| `cc_landuse_surface.json` | 193 | landscape assemblies with full subItems percentages |
| `cc_landuse_building.json` | 216 | building benchmarks, above/below grade separated |

Completeness check: all 214 element ids referenced by assemblies resolve, with
**zero dangling references**. This is what Tables 1a–1e, 2, and 3 were needed for.

**Still to pursue: the Zotero bibliography.** The data carries values but no
source for any individual value. For an open-source tool and a written
methodology this is a hard requirement; obtaining the data has not lightened the
obligation of attribution and traceability.

**Upstream sources** (for reference — the Carbon Conscience dataset itself
derives from these): ICE Database v4.1, Ökobaudat (has an API), EC3 (has an API),
i-Tree, IPCC AFOLU, CLF Benchmark, deQo, Athena, Tally.

## 3. Calculation framework

A concept-stage carbon ledger is five accounts, laid onto a timeline by year and
accumulated into a **cumulative net carbon curve**. The year it crosses zero is
the carbon-neutral year.

| # | Account | When | In CC? |
|---|---|---|---|
| ① | material production A1-A3, transport A4, construction A5 | year 0 | ✅ all included |
| ② | loss of existing condition (tree removal, soil disturbance) | year 0 | ✅ included, under demolition and site preparation |
| ③ | maintenance B2-B3 (annual) | recurring | ❌ **not included** |
| ③ | replacement B5 (pulses at service life) | recurring | ✅ included |
| ④ | plant and soil sequestration (non-linear) | annual | ✅ included, under D |
| ⑤ | end-of-life C | end of period | ❌ **not included** |

**Study period: 60 years** (following Carbon Conscience V2; V1 used 80).
Reason: their sequestration figures are projected to year 60, so using their data
requires matching it. Sixty years is also the WBLCA standard timeline in Tally,
Athena, and EPIC.

(Note: the Carbon Conscience appendix cites ILFI's threshold over 50 years —
<500 kgCO₂e/m²/50yr — and Atelier Ten's suggested 185 kgCO₂e/m²/50yr. Any report
quoting these benchmarks must note the difference in period.)

**Our methodological additions relative to Carbon Conscience:**

- **Maintenance carbon (B2-B3) — not in phase one (settled 2026-08-28), but for
  a different reason than originally recorded.**
  The original reason was that Carbon Conscience discusses low-maintenance
  standards in its conclusions without modelling them, and that this is a gap.
  The gap is real, but checking the data revealed something more decisive:
  **searching all 259 elements, there is not one maintenance or operational
  carbon figure.** The 10 hits are all A1-A3 production carbon for irrigation
  **hardware** — pipe, heads, controllers, weather stations, pumps — with not a
  drop of water or a single mowing.
  So doing maintenance carbon means **leaving the Carbon Conscience dataset**:
  building our own factors, finding our own literature, carrying our own
  controversy — which is precisely what dismantles §2's core argument that using
  their data saves 3–5 person-years.
  **Approach**: exclude it from phase one; surface the irrigation hardware that
  is in the data as a separate hook; state plainly in reports that we are
  "consistent with Carbon Conscience, excluding B2–B3 operational carbon"; and
  schedule it as a phase-two module with its own literature basis. That keeps the
  methodological boundary clean and stops one module from undermining the
  legitimacy of the whole data strategy.
- **Stage C** — Carbon Conscience omits it on the grounds that disposal is too
  uncertain at concept stage. That reasoning is sound; low priority.

## 4. Carbon Conscience's sequestration model (adoptable as-is)

**Logistic (S-curve):**

```
Ct = M / (1 + e^(−r × (t − M/2)))

Ct = cumulative sequestration at year t
M  = carbon carrying capacity = total carbon potential of the mature system (TCP)
r  = distributed sequestration rate, fitted to the maturity year:
     woody ecosystems 80 years (USFS 2003)
     grassland 20 years (Deng et al., 2022)
Wetlands are the exception — linear regression, not logistic
```

**True sequestration rate, net of decomposition:**

```
TSR = ASR − ((ASR × LPG) × DF)

ASR = annual sequestration rate from the literature
LPG = leaf proportion of growth (the readily decomposed fraction)
DF  = decomposition factor, by climate zone
```

**Total carbon potential**: `TCP = above-ground biomass + below-ground biomass +
soil organic carbon`
**Conversion**: `GWP = carbon mass × 3.66`

**Key empirical parameters:**

| Parameter | Value |
|---|---|
| Carbon content of dry biomass | 50% |
| Below-ground biomass multiplier — forest | 20% |
| Below-ground biomass multiplier — prairie | 300% |
| Below-ground biomass multiplier — dry meadow | 200% |
| Below-ground biomass multiplier — grassland/wetland | 12.5% |
| Urban soil organic carbon | 2.8 × biomass |
| Remaining after 1 year of decomposition — warm climate | 24% |
| Remaining after 1 year of decomposition — cold climate | 60% |
| Remaining after 1 year of decomposition — wetland | 70% |
| Share of a lawn's annual growth entering SOC | 5% |
| Allochthonous carbon | 5% of the annual rate (humid forest only, after year 80 only, linear thereafter) |
| A5 installation allowance | low 1% / average 3% / high 4% |

**Important conclusion**: soil carbon storage frequently equals or exceeds
biomass storage. Counting living biomass alone misses more than half.

## 5. How plants are measured (settled 2026-08-28: per tree)

**Decision: do it, and it is cheap.** Area plus individual trees.

**The original concern does not hold.** The earlier note said Carbon Conscience
**deliberately** measures by land area rather than stem density (Braakhekke et
al., 2019), and that doing per-tree counts would therefore challenge their
methodology. The data shows otherwise: **their urban tree sequestration is itself
parameterised by spacing**, merely frozen at three points:

| Canopy cover | Spacing | m²/tree | Sequestration kgCO₂e/m² | Back-solved: per tree |
|---|---:|---:|---:|---:|
| 17% | 12 m | 144 | −2.66 | −383 |
| 35% | 6 m | 36 | −5.63 | −203 |
| 50% | 4 m | 16 | −8.51 | −136 |

(Elements `329300.28 / .29 / .30`, whose names literally read "trees 12M o.c." /
"6M o.c." / "4M o.c.")

The element names in the street tree assembly are blunter still:
`5 cm Cal. B&B/Box Tree Planting (1 Tree/10 SM)` — **one tree per 10 m²,
hard-coded**.

So this is not a question of whether to challenge Carbon Conscience. **They
already counted trees and then froze the count into a constant.** Solving back
out is three-point interpolation and takes almost no work. In Rhino a tree is
already a point or a block, so count, position, and spacing are all there.

> ⚠️ **Critical detail**: per-tree sequestration **falls** as density rises
> (competition). It must never be written as `N × constant`; it must be
> interpolated along this curve.
> This is also good material for a client conversation: "plant twice as many
> trees and you sequester less than twice as much."

**Tree grouping** (if finer specification is wanted): three groups by mature
canopy spread (small <6 m / medium 6–12 m / large >12 m), with growth rate as a
secondary parameter defaulting to medium (leaving an interface for the AI),
climate zone coefficients derived automatically, and survival rate built in as a
requirement.

## 6. Existing site conditions (account ②)

**Required, but one click by default.**

This is often the largest account in a project, and it is the part design can
actually change. The core narrative: "keeping this row of trees = 400 m² less
concrete."

**Carbon Conscience's approach** (adoptable as-is):

- assumes organic carbon is **entirely released** during demolition and site
  preparation
- global assumptions: average 0.5 m depth of grading, clearing and grubbing, site
  protection
- waste is assumed to travel to a facility within 16 km (distance and mode are
  user-editable)
- stored carbon is recorded as negative and released as an equal positive on
  demolition (e.g. temperate forest −79.77 kgCO₂e/m² stored → +79.77 on removal)
- on-site salvage and reuse: add the crusher's carbon cost, subtract the
  extraction, processing, and transport of an equal volume of imported
  aggregate → a net saving

**Opportunity cost** (those trees would have kept sequestering) → **not counted
in the headline figure**. Instead, draw a "do nothing" baseline curve alongside.

> The two-curve chart is likely the product's most communicable image: the area
> between the lines is the design's real cost or contribution.
> It answers the client's "so why not just do nothing?" naturally.

## 7. Result benchmarks

Three coexist and the user can switch between them — a progression of persuasive
logic:

1. **Target bar** — where you should be. Anchored to existing industry
   commitments (ASLA Climate Action Commitment, Climate Positive Design 2030
   targets, ILFI Zero Carbon <500 kgCO₂e/m²/50yr, Atelier Ten's suggested 185)
2. **Industry benchmark** — where you stand now. Carbon Conscience appendices C
   and D give actual carbon values for 67 landscape land uses and 8 building
   uses, usable directly as an initial benchmark
3. **A/B scheme comparison** — how to get from one to the other

*Do not* build "equivalent to X cars" style conversions.

**Selected reference values from Carbon Conscience appendix D** (kgCO₂e/m², read
approximately off the charts): CIP pedestrian concrete ~190, sand-set brick ~205,
sand-set monolithic stone ~130, stabilised gravel ~68, pedestrian asphalt ~68,
CIP concrete wall ~1205, dry-laid stone wall ~450, timber wall ~5, EPDM safety
surfacing ~322, mulch safety surfacing ~22. Restoration types (negative):
mangrove −265, temperate oceanic rainforest −143, temperate wetland/salt marsh
−92, tropical rainforest −87, boreal forest −85, temperate continental forest
−75, prairie −58.

## 8. Presenting uncertainty

**Lead with the median, show the range as secondary** — one large number with the
range small beneath it, and error bands on charts.

The key definition: **the range represents the space of design choices, not data
error.**

- lower bound = what this scheme reaches if every low-carbon option is taken
- upper bound = every conventional option
- median = typical industry practice

This turns the range from a "we cannot be sure" disclaimer into **an actionable
target** — "you are at 520, but the same design could reach 380." It also
connects naturally to the AI suggestions.

**Carbon Conscience's approach aligns**: material carbon factors are stored as
LCf / MCf / HCf. Where citations exceed 5, MCf is a true median with the bounds
one standard deviation out; where citations are few, it is a simple mean with the
minimum and maximum. Note: **ecological carbon has a single value only, with no
three bands.**

## 9. Taxonomy

**The underlying data structure is a composition of design elements (replaceable
slots); the interface does not wrap them in scenarios but resolves them through
the provenance mechanism.** (Revised 2026-08-28; was "phase one wraps the
interface in functional scenario entry points" — reasoning at the end of this
section.)

Carbon Conscience's three-tier structure is adoptable as-is: **base materials →
site elements → the average composition of each land use**

```
Plaza 500 m²
├─ paving:  cast-in-place concrete   [swappable]
├─ base:    150 mm aggregate         [swappable]
├─ tree pits: 8                      [editable]
└─ seating: 12 m                     [editable]
```

The key reason: **AI suggestions only work if replaceable slots exist.** A pure
scenario black box has no slots to swap, so the AI can only make empty noises.

**Carbon Conscience's four landscape categories**: Existing to Remain /
Demolition & Site Preparation / Hardscape (11 sub-types) / Softscape (4
sub-types)

**Their hardscape naming logic** (well worth learning from): defined first by
paving type, then differentiated by **wall density** — 1% walls = a simple open
plaza, 5% = a complex plaza with level changes and planters, 10% = a highly
complex space. Planning stage defaults to 5%.

### Our approach (redecided 2026-08-28; was "20 scenario entry points")

> **The "20 scenario entry points" plan is cancelled.**

The original plan was a layer of roughly 20 scenario entry points above the 230+
land uses, covering 80% of cases.

**With the data in hand, that turns out to treat the symptom.** The 230 types are
not a classification problem, they are a **"cannot see geometry" problem** —
Carbon Conscience can only disguise geometric questions as multiple choice. Its
list is the product of:

```
material family × wall density × pedestrian/vehicular × climate zone
```

The last three axes need never be put to the user:

| Axis | What CC does | What we do | Share of 193 |
|---|---|---|---|
| climate zone | duplicates every ecosystem N times | fixed by project location, set once | 48 (25%) |
| wall density 1%/5%/10% | **makes the user guess** | vertical area ÷ plan area, measured directly in Rhino | 26 (13%) |
| walls / steps / railings | split into separate land uses | identified automatically from face normals | 11 (6%) |
| **material family** | user picks | **user picks — the only genuine design decision** | 108 (56%) |

The cost of that wall-density dropdown, on the same 1000 m² plaza, measured:

| CC option | Implied wall | Mid tCO₂e |
|---|---:|---:|
| minimal walls (1% or less) | 10 lm | 173.9 |
| some walls (5%) | 50 lm | 286.8 |
| significant walls (10%) | 100 lm | 446.1 |

**One dropdown, a 2.6× swing worth 272 tonnes, decided by a guess** — over a
purely geometric quantity.

**The new approach — provenance**: rather than wrapping Carbon Conscience's list,
take it apart. Every sub-element gets one of three labels:

| Provenance | Where the quantity comes from | What the user can do |
|---|---|---|
| **Measured** | straight off the Rhino geometry | change the model |
| **Inferred** | a measured value plus one written rule | change the rule (rarely needed) |
| **Assumed** | a Carbon Conscience assembly default | change the value, or go and draw it |

**The key property: graceful degradation.** Draw nothing but a plan boundary and
the system falls back to Carbon Conscience's answer exactly. (Verified: measuring
10 m of wall produces 131.0 / 173.9 / 240.6, matching their 1% tier digit for
digit.) Every additional thing drawn replaces one assumption with a measurement.

**The AI's slots need no separate design** — the provenance label is the slot, and
the AI acts only on "assumed" ones.

The user **never picks a scenario**, only a material family (= colour, see §13).
Full argument in `system-design.md`.

> **Note**: a catalogue entry point is not wrong in itself; it is wrong on the
> wrong axis. **Do not do it on the material axis; do do it on the question
> axis** — workflows like "does this scheme meet the 2030 target?" and "is keeping
> this row of trees worth it?" are correct. See `ui-design.md` §13.

## 10. Rating systems

**Deferred to phase two.** Very few LEED or SITES credits can be derived
automatically from a carbon model; most concern documentation, process, or site
conditions. If built, it should be positioned as a pre-assessment or self-check
list, not an official calculator (trademark compliance).

(Note: Carbon Conscience's references include USGBC 2021 *SITES Pilot Credit 3:
Assess and Improve Site Carbon Performance* — the direct entry point when rating
work begins.)

## 11. Research gaps Carbon Conscience admits to (our opportunities)

- **A5 construction carbon for landscape projects** — they state plainly that they
  could find no relevant research
- **Nursery embodied carbon** — "the single largest gap" in the literature, with
  no EPD standard
- **Urban landscape and garden sequestration** — few researchers, and concentrated
  in the US and Europe
- **Maintenance and operational carbon** — explicitly excluded, though their own
  conclusions acknowledge this as an institutional obstacle
- Lighting, electrical, irrigation, water features, and telecoms use **Product
  Placeholders**, some without a primary citation

---

# Part 2 — Technical architecture

## 12. The core division of labour

> **Grasshopper handles geometry and quantities only. The web handles all
> semantics.**

Almost every technical decision below follows from this.

## 13. How things are tagged in Rhino (settled: colour grouping)

**Decision: the user picks a colour on a Grasshopper component and feeds geometry
into it. Colour is the grouping key.**

Rejected alternatives and why:

| Alternative | Why rejected |
|---|---|
| Automatic detection by layer (requiring fixed naming) | forces users to change their modelling habits — the most common way plugins die |
| Read existing layers, map them in the web app | every practice has different layer standards, and at concept stage layers are often chaotic or absent |
| A material dropdown on the Grasshopper component | Carbon Conscience has 230+ types, and a Grasshopper value list becomes unusable past twenty or thirty items; also, updating the material library would require re-releasing the plugin |

**Three benefits of the colour approach:**

1. **The plugin is decoupled from the database** — the material library lives on
   the web, can be updated online, and the Grasshopper plugin never needs
   re-releasing. For an open-source project this is an enormous difference in
   maintenance cost.
2. **What you see is what you get** — the colour is simultaneously the Rhino
   viewport display colour, so the act of grouping produces a coloured zoning
   diagram, which is already the common language of landscape and urban design.
3. **Colour carries through the whole product** — Rhino viewport → web 3D →
   legend → pie and bar charts → report, one palette throughout. The visual
   language is defined by the user and needs no separate design.

**Known risk**: if the user adjusts a colour in Grasshopper the mapping breaks,
since the key is RGB. The fix lives on the web side — remember that "red = cast
in place concrete", and when red disappears and a new dark red arrives, ask "is
this the same one?" with one-click inheritance. No extra Grasshopper input.

## 14. Component design

```
┌─────────────────────┐        ┌─────────────────────┐
│ CC Group            │        │ CC Sync             │
│  ▸ Geometry         │───┐    │  ▸ Objects  (+ port)│
│  ▸ Color ■          │   ├───▸│  ▸ Run      (toggle)│
│  ◂ Tagged Objects   │   │    │  ▸ Open     (button)│
└─────────────────────┘   │    │  ◂ Status / Port    │
      (copy as needed) ───┘    └─────────────────────┘
```

**Sync's switch must be split in two.** Once the toggle is true it runs on every
recompute, which is how continuous sync works; but opening the browser must not
happen on every recompute, or dragging one point spawns ten tabs. Hence `Run` (a
toggle, the sync switch) and `Open` (a button, manual browser launch) separately.

**Project-level parameters do not go into Grasshopper** (project name, location,
climate zone, transport distance assumptions). They are entered on the web. The
Grasshopper side stays clean.

## 15. Protocol conventions

```json
{ "meta": { "units", "origin_offset", "timestamp", "mesh_tolerance" },
  "groups": [
    { "color": "#RRGGBB",
      "objects": [
        { "id": "geometry hash",
          "quantity": {
            "area": 0,            // plan projection area
            "vertical_area": 0,   // added 2026-08-28
            "mean_height": 0,     // added 2026-08-28
            "count": 0, "length": 0, "volume": 0
          },
          "mesh": { "vertices": [...], "faces": [...] } }
      ] } ] }
```

**Five key conventions:**

1. **Compute and send every dimension together** — Grasshopper does not know the
   material type, so it computes area, count, length, and volume alike, and the
   web takes whichever the type needs. A few extra numbers on the wire cost
   nothing and buy a Grasshopper side that needs no material knowledge at all.
   > The dataset confirms this: the 259 elements come in exactly **four units** —
   > area 219, count EA 15, length LM 13, and wall footprint 12 (height built
   > into the element, in 1M and 2M tiers).

   1b. **Vertical area and mean height must be sent separately** (added
   2026-08-28) — this is the key to resolving the wall density axis, see §9.
   Split by the Z component of the face normal, threshold 60°:
   `|normal.Z| > cos(60°)` means horizontal, otherwise vertical.

2. **The id is a geometry hash** — unchanged geometry keeps its id, which is the
   basis for incremental updates that push only what changed. A sequence number
   will not do: delete one object and everything after it shifts.
3. **Units are converted to metres on the Grasshopper side** — Rhino may be in
   feet, millimetres, or inches.
4. **`origin_offset` is mandatory** — landscape models routinely carry real
   survey coordinates in the hundreds of thousands. three.js uses float32, which
   jitters and even tears at that magnitude. Grasshopper translates everything to
   the origin and stores the offset separately in `meta`.
5. **Coordinates stay Z-up as Rhino has them**; the web side converts to
   three.js's Y-up.

## 16. Geometry sync (settled: build our own)

**A local server embedded in our own Grasshopper component, not Speckle.**

Why Speckle was rejected: users would have to register an account, install a
connector, and understand streams and commits — three hurdles, each shedding
users. Its viewer's styling and interaction are also outside our control, and
handing our central differentiator (compelling visualisation) to a third party is
a poor trade.

**Order of work** (important — do not tackle WebSocket first):

1. Fix the protocol
2. Write the Grasshopper components, emitting JSON
3. Web skeleton: three.js reads the JSON, displays in 3D, colours by group, lists
   groups in a sidebar
4. Swap the transport from writing a file to WebSocket (the protocol does not
   change; the web side edits one function)

## 17. Back-end architecture

### Redecided 2026-08-30: no back end in phase one

The original plan (retained below) was geometry local, scheme snapshots in the
cloud, plus accounts. **This is now changed to no back end at all in phase one.**

Re-examining which features genuinely need a server, only three do, and none of
them is load-bearing:

| Feature | Needs a back end? |
|---|---|
| Portal page, plugin download, documentation | static hosting suffices (GitHub Pages) |
| Rhino → web sync | no; localhost is faster and works offline |
| Carbon and cost calculation | no; a 200 KB dataset computes comfortably in the browser |
| User edits to default data | not for local overrides; only for community sharing |
| Saving schemes | not for one machine; only across devices |
| Accounts | yes |

**Three reasons for the reversal:**

1. **Client confidentiality** — the original plan already kept geometry local, but
   scheme snapshots would still be uploaded. For a free project maintained by one
   person with no security budget, holding any practice's project data is a
   liability. "Nothing leaves your machine" passes a practice's IT review more
   easily than "only the geometry doesn't", and is easier to explain.
2. **Maintenance cost** — server renewals, backups, authentication, secrets, and
   GDPR never stop. This is the most common way a one-person free project dies. A
   local version costs nothing and keeps working after the author stops
   maintaining it.
3. **The NC licence** — CC BY-NC 4.0 already forbids commercialisation, so there
   is no "build the back end now for future revenue" argument. Charging would
   first require replacing the data layer, not adding a paywall.

**The shared material library is implemented without a back end**: the dataset is
a versioned JSON file in the repository, the plugin reads it, and others
contribute by pull request. The review step is a feature here — a wrong carbon
factor silently pollutes someone else's report, and nobody should be able to
write to it directly.

**Saving schemes**: phase one uses browser local storage (implemented; see
`SCHEMES` in `viewer.html`).
**Fixed 2026-08-30**: `SyncServer` now claims fixed port **51275**, falling
back to a random port only if it is taken. Local storage is scoped to origin
including port, so only a stable port lets schemes survive between Rhino
sessions.

**When to reconsider**: when team collaboration is needed, or when
commercialisation is wanted (the latter requires replacing the data layer first).

---

### The original plan (2026-08-28, superseded above, retained for reference)

The user wants accounts, saved scheme history, and scheme comparison. But **not a
heavy back end**.

```
geometry (large, confidential, not needed for comparison)   →  local only, never uploaded
scheme snapshot (mapping + quantities + results + thumbnail) →  cloud, tens of KB
material library (read-only public data)                     →  static JSON, not a database
```

Three benefits:

- **Privacy** — many practices forbid uploading models to third-party servers.
  "We never upload your model" is a powerful sentence.
- **Cost** — each scheme snapshot is tens of KB, so a free-tier database holds
  thousands of users. The server bill stays near zero rather than scaling with
  users.
- **Offline use** — fully usable without signing in (modelling, carbon
  calculation, charts, report export). Signing in only adds "save it and compare
  later." No registration on first use, which matters for an open-source tool's
  reach.

**The cost**: reopening a six-month-old scheme shows no 3D, only data, charts,
and a thumbnail. Acceptable — scheme comparison is about the numbers anyway, and
if 3D is genuinely needed, sync again; the model is on the user's own machine.

**The colour → material mapping lives in the scheme snapshot**, travelling with
the project across machines. No write-back to Rhino and no sidecar JSON needed.

**The back end must be self-hostable and replaceable** — practices can deploy it
internally, and if the public instance goes down or the author stops maintaining
it, others can carry on.

## 18. Development environment

**The `.gha` must be compiled locally.** Neither the Cowork cloud container nor
the on-device Linux sandbox can reach NuGet to obtain the Grasshopper SDK
packages. Running Claude Code locally uses this machine's own dotnet and network,
which can compile and drop the result straight into the Grasshopper Libraries
folder for testing — the shortest possible loop.

**Division of work**: anything requiring compilation (the `.gha`) happens in local
Claude Code; methodology, data analysis, and white paper work happen in Cowork.

**Python first, or straight to `.gha`**: settled on writing the `.gha` directly.
The colour picker needs custom attributes, which a Script component cannot
provide; and C# is an order of magnitude faster than the Python interpreter, which
shows clearly when meshing thousands of objects.

## 18.5. Code collaboration model (settled)

**Claude writes the code, but must then walk the user through two steps. Writing
it is not the end of the job.**

Background: Claude writes and runs all the code on this project, and the user
barely touches an IDE. Two worries follow — that the user will not understand what
was built and will not be able to debug it, and that this will show in an
interview. The conclusion after discussion: **the user does not need to hand-write
every line**, but must reach the level of reading and judging — able to look at
code and tell whether it is right, and see where it will break at the edges,
rather than using it as a black box.

**Agreed process:**

1. Claude writes the code (as usual; the user does not need to open an IDE)
2. After delivery, Claude walks the user through the key **"what happens if this
   is changed or removed"** boundary questions (for example: the id is a geometry
   hash — what happens if two objects collide? What happens without
   `origin_offset`?)
3. The user then **explains the code's logic back to Claude**, who corrects errors
   and checks that the user has genuinely understood rather than memorised the
   conclusion

**Acceptance criterion**: for every technical reason written into this decision
record, the user should be able to say concretely what would break without it,
not merely remember that it should be done.

**Scope**: all code collaboration on this project, especially the decision points
where the document states a "why" (Grasshopper grouping logic, protocol JSON
structure, geometry hash generation, and so on). Routine boilerplate can be
treated more lightly.

**Timing revision (2026-08-27)**: doing the full boundary Q&A and explain-back
after each component broke the rhythm of finishing the project. Changed to:
**finish the whole project first, then do one thorough overall review**, at which
point every key decision point goes through the process above. Explanations during
development stay brief — enough to say what a piece of code does — without stopping
for the full Q&A and explain-back after every block.

**Language of explanation**: the user will be interviewing in English, so code
explanations are given in English (other day-to-day project discussion is not
restricted). Explain directly and completely rather than posing open questions for
the user to guess at.

## 19. Current output

| File | Location | Status |
|---|---|---|
| Decision record | `docs/decision-record.md` | this file — product and methodology decisions |
| **System design** | `docs/system-design.md` | seven layers, provenance, order of work |
| **UI and interaction design** | `docs/ui-design.md` | seven-part result panel, layout skeleton, interaction mechanics |
| CC_Group.py | `CC_Group.py` | Python prototype of the grouping component; runs, kept as logic reference for the C# version |
| **Resolver prototype** | `tools/resolver_prototype.py` | runnable; verifies graceful degradation and digit-for-digit agreement with CC |
| **CC site elements** | `data/cc_site_elements.json` + `.csv` | 259 rows, three carbon factor bands |
| **CC landscape assemblies** | `data/cc_landuse_surface.json` | 193 rows, with subItems percentages |
| **CC building benchmarks** | `data/cc_landuse_building.json` | 216 rows, above/below grade separated |
| infrared.city screenshots | `_research/ui-references/01-…05-*.png` | interface reference (outside the repository) |
| White paper page images | `_research/_pages/` | 92 JPGs for reference (outside the repository) |
| White paper source | `_research/ISSUU PDF Downloader.pdf` | 92 pages, images only, no text layer (outside the repository) |

## 20. Project storage (settled 2026-09-13)

### The problem this solves

Design work is not continuous. A designer creates a project, saves one scheme,
and returns a week later to try a second. If the first scheme has to be set up
again before the two can be compared, the comparison feature is worthless in
the only situation it was built for.

Schemes already survive a restart — they live in browser local storage, and §17
pinned the server port so that storage is no longer orphaned on every launch.
What does not exist is a **project**. `PROJECT` is a text label, not a
container, so:

- every project's schemes pile into one flat list, and opening a second project
  shows the first one's schemes
- local storage is not a file: clearing browser data erases it, a second machine
  has none of it, and it can neither be backed up nor sent to a colleague
- there is nowhere to put the screenshots that cross-model comparison needs
  (§21); base64 in local storage would hit its few-megabyte ceiling quickly

This is a data-model gap, not a hosting gap. It does not need a back end.

### Decision: a project folder chosen by the user

The user picks a directory. Everything about the project lives in it:

```
Park1/
├─ project.json      name, climate zone, display units, geometry fingerprint
├─ schemes/
│  ├─ 001.json       material assignment, rates, quantities as measured
│  └─ 002.json
├─ thumbs/
│  └─ 001.jpg        the viewport as it looked when the scheme was saved
└─ reports/
```

One file per scheme rather than one large document: a save rewrites one small
file instead of the whole project, and the folder stays readable in a diff if
anyone version-controls it.

**Why a folder and not a single file**: thumbnails. Cross-model comparison (§21)
needs an image per scheme, and a folder lets an image be an image.

### How a browser writes to disk: it does not

A page in a browser cannot freely read or write a directory. The alternatives
open to an ordinary web application are all poor — the File System Access API is
Chrome and Edge only, `<input webkitdirectory>` can read but never write, and
download prompts on every save are unusable.

**We are not an ordinary web application.** The plugin already runs a local HTTP
server in C#, with full file system access. The browser therefore needs no file
permission at all:

```
page  ──POST /project/new──▶  server
                              ├─ native folder dialog
                              ├─ create the directory, write project.json
                              └─ return the project
```

`Sitewise.csproj` already sets `UseWindowsForms`, so `FolderBrowserDialog`
needs no new dependency. This beats the File System Access API on every count:
it works in any browser, uses the operating system's own dialog, and raises no
permission prompt.

### The welcome screen

Opening the viewer always lands on a welcome screen offering exactly two
choices: **Open Project** and **Create New Project**. There is no third
"look around without a project" mode — it would create a state in which saving
silently does nothing, which needs explaining every time it is entered.

**The project is never chosen automatically.** Remembering the last project and
going straight in was considered and rejected: open `Riverside.gh` while the
viewer silently restores `Park1`, and you are reading one project's schemes
against another's geometry, with nothing on screen saying so. Guessing the
context wrongly and in silence is worse than one deliberate click — the same
principle that governs §21's refusal to load a scheme across models.

To keep that deliberate click cheap, the welcome screen shows:

- **what Rhino has sent** — document name, object count, colour groups — so the
  geometry can be checked before a project is picked. When nothing has synced
  yet it says so and the project choice still works; the two are independent.
- **recent projects**, each with its scheme count and last-opened date, so
  reopening is one click rather than a walk through a folder dialog. A project
  whose name matches the Rhino document sorts first, as a suggestion only.

### Known limits

- A folder the user moves, renames or deletes leaves a dangling recent entry.
  The screen must offer to locate it again rather than fail silently.
- Two people opening the same folder on a network share will overwrite each
  other, last write winning. Acceptable for phase one, but real.

### Relationship to the back end

§17 stands: no back end in phase one. This decision does not reopen it, because
the folder's contents are precisely what a sync layer would upload. Building it
is the shared prerequisite of both paths, not an alternative to one of them. If
remote access is wanted later, the question narrows to "also sync this folder",
which is a much smaller decision than designing storage from nothing.

## 21. Comparing schemes (settled 2026-09-13)

### Two comparisons, not one

They answer different questions, run on different data, and should not share an
interface.

| | Live switching | Comparison tab |
|---|---|---|
| Question | what changes if this material changes? | how do these design options differ? |
| Geometry | the model loaded now | each scheme's own, as a screenshot |
| Data | recomputed live | frozen at save time |
| Where | the toolbar's scheme list | a tab between Result and Report |

The Comparison tab lays schemes out as columns: name, screenshot, charts, then
aligned rows of detail, so the eye reads across. Report export selects which
schemes to include — one, several, or all.

### A scheme records the specification, not the design

A scheme stores material assignment, cost rates, climate zone, and the
quantities measured at the moment it was saved. It does not store geometry.

This is what makes "same model, different materials" work: change the model and
a scheme's materials reapply to the new geometry by colour, and the totals
recompute. It is also why the second comparison exists at all — geometry
variants are invisible to a specification.

### A scheme from a different model cannot be loaded

**Settled: refused, not recomputed.** Where the geometry has moved on, the
scheme is archive-only — visible in the Comparison tab with its screenshot and
its frozen figures, but not loadable onto the current model.

This was chosen over the alternative of silently reapplying it, and it removes
a whole class of fault rather than patching one. Two schemes saved against
different models hold identical specifications and differ only in their stored
numbers; loading either produces the same live result, while the list shows two
different figures side by side as though they were a comparison. Under this
rule that situation cannot arise: a scheme that matches loads, and its stored
figures are still correct because the model and specification are unchanged;
one that does not match never claims to be current.

**The live assignment is not cleared when the model changes.** Only saved
schemes lock. Clearing `ASSIGN` on every geometry edit would mean reassigning
every colour after nudging a bench, and the colour key exists precisely so that
assignment survives geometry edits (§13).

### What counts as the same model

Per-colour-group quantities — area, volume, count — not vertex positions. Carbon
depends on those quantities and nothing else, so moving a bench 100 mm changes
no number and must not lock anything.

- any change to the **set of colours** — one added or removed — locks the scheme;
  that is structural, not a nudge
- a group's quantity changing by **more than 1%** locks it
- **1% or less** counts as the same model

The 1% threshold is a judgement, not a derivation: far below any deliberate
design change, far above modelling jitter.

### The screenshot's camera is permanent

A scheme's thumbnail is taken at save time and cannot be retaken — the geometry
it showed is gone. A row of thumbnails shot from different angles is actively
misleading, since volumes appear to differ when only the camera moved. The save
flow therefore records the camera alongside the image, and the fixed Top and
Axonometric view buttons exist partly to make a consistent angle easy to choose.

## 22. What a colour group is, and when it cannot be priced (settled 2026-09-14)

### The test is closure, not the kind of object

A piece of geometry is priced by volume if it has one, and by area otherwise.
The plugin decides with `Brep.IsSolid`, which asks whether the brep is closed —
not whether it is a polysurface, and not how many surfaces were joined to make
it.

- Several open surfaces joined into an open polysurface → area
- Surfaces joined into something closed → volume

Joining changes nothing on its own; closing does. A designer who joins six
surfaces into a box has, as far as this tool is concerned, modelled a solid.

Once an object reports a volume its area is discarded. The area of a closed
solid is its whole skin — six faces of a box, not its footprint — and
multiplying that by a per-square-metre paving assembly would be meaningless.

This is why the landuse assemblies (defined per m² of ground, bundling paving,
base, drainage and lighting) are offered only to surface groups, and the
per-m³ materials only to solid groups.

### A mixed group is refused, not guessed

A colour holding both closed solids and open surfaces has no single basis. It
is not a question the tool can answer by picking one: the two are priced in
different units.

Until 2026-09-14 the code pretended otherwise. `groupKind` returned `'mixed'`
and **nothing ever read that value** — every consumer asked `=== 'solid'` and
otherwise used surface area, so the solids' volume left the totals without a
word, while the card went on displaying both an area and a volume as though
both were counted.

A mixed group is now reported as unassigned everywhere, through a single gate
(`priced()`), and the card says why. The stored material choice is kept rather
than deleted: split the geometry in Grasshopper and the material comes back.

### An object may only wear one colour

Wiring the same geometry into two CC Group components sends it twice. If both
groups get a material, its area or volume is added to the total twice, and
nothing about either card looks wrong.

Every object carries a content hash, so the payload is scanned for ids
appearing under more than one colour and the Materials tab reports it above
the cards — the fault belongs to a pair of groups, not to either one.

The hash had to be strengthened to carry this. Type plus bounding box let a
cylinder and a box collide; it now also carries the area, volume and length
already computed, plus whatever topology counts are free to read. Two objects
must agree on all of it to collide, which means they are the same object or an
exact duplicate in the same place — and counting *that* twice is also wrong.

## 23. Sync performance (settled 2026-09-14)

A 1,230-object model took 11.2 seconds to reach the viewer. It now takes about
0.7, and the payload fell from 12.8 MB to 6.9 MB.

Every step came from measuring, not guessing. The first guess — that
tessellation dominated — was wrong, and the CC Sync component grew a **Timing**
output precisely so the next question could be answered the same way. Keep it.

| | Waited |
|---|---|
| as found | 11.24 s |
| asking for area without the moments | 3.40 s |
| running the per-object work in parallel | 1.02 s |
| writing meshes as text instead of JSON nodes | ~0.7 s |

- **`AreaMassProperties.Compute(brep)` also integrates the centroid and the
  second and product moments.** None of them reach the payload. That unused
  integration was 78% of the sync. The four-flag overload asks for area alone,
  with no loss of precision — it is still the exact NURBS integral.

- **Objects are independent**, so unwrapping, tessellating and measuring run
  across all cores. Writing the coordinates has to wait: the origin offset
  comes from the bounding box of the whole model, so it is not known until
  every object has been unwrapped. Hence two passes.

- **A JObject per vertex and per face** meant roughly 1.7 million short-lived
  objects per build. Once the rest was cheap, that garbage was 96% of what
  remained — every thread doing nothing but allocating. Meshes are now written
  as text and handed to Newtonsoft as one `JRaw`.

### Mesh format 2

Both arrays are flat: vertices as `x,y,z` triples, faces as groups of **four**
indices. A triangle repeats the third index, which is what Rhino's own
`MeshFace` does (`IsQuad` is `C != D`), so no per-face bracket or marker is
needed. Coordinates are fixed-point decimals to 0.1 mm — finer than a concept
model means, and far shorter than the seventeen significant digits
`double.ToString` emits by default.

`meta.mesh_format` records the version. A page left open across a plugin
upgrade says so rather than drawing an empty scene.

### Why there is no geometry cache

One was built and removed the same day. It kept each object's tessellation and
mass properties, keyed on the Grasshopper goo instance, on the reasoning that
reference identity can only ever miss — never be wrong.

**That reasoning does not hold for Grasshopper goo.** `IGH_GeometricGoo`
reloads referenced geometry *into the existing instance*. Edit the model in
Rhino and the key is unchanged, so the cache returns the reading from before
the edit. The viewer then shows a stale model with no error, no warning and no
slowdown — it simply lies.

It bought roughly 400 ms. Do not rebuild it on the same key. If it is ever
worth revisiting, the invalidation has to come from the Rhino document itself
(`ReplaceRhinoObject`, `AddRhinoObject`, `DeleteRhinoObject`,
`AfterTransformObjects`), not from the identity of what Grasshopper hands over.

### Still costing something

The viewer re-fetches the whole payload every 1.5 s and compares the entire
string. While nothing changes in Rhino, the server keeps serialising and
sending several megabytes a second for no reason. A `/version` endpoint
returning a counter would end that and allow faster polling.

The build also runs synchronously inside `SolveInstance`, so Rhino is frozen
for its duration.

## 24. Open items

**Three closed on 2026-08-28:**

- [x] ~~obtain the Carbon Conscience Landscape Data Set~~ → **obtained and verified
      complete** (§2)
- [x] ~~the specific list of 20 scenario entry points~~ → **plan cancelled**,
      replaced by the provenance mechanism (§9)
- [x] ~~whether to do maintenance carbon~~ → **not in phase one**, with a different
      reason (§3)
- [x] ~~whether individual trees are worth doing~~ → **yes**; CC's own data is
      parameterised by spacing (§5)

**Still open:**

- [ ] keep pursuing the **Zotero bibliography** from Sasaki (the data has values
      but no sources — a hard requirement for open traceability)
- [ ] **L0 provenance tagging** — label assembly sub-elements as measured,
      inferred, or assumed; about 25 categories. Pure data work, **recommended
      first**, as it defines the shape of every interface that follows
- [ ] design and implement the CC Sync component
- [ ] add the vertical/horizontal area split to CC_Group (§15, 1b)
- [ ] the web 3D skeleton (three.js)
- [ ] AI agent tool calling design and the what-if mechanism — the slots are
      already defined by the provenance labels
- [ ] report generation and export, with per-scheme selection (§21)
- [ ] **project folder and welcome screen** (§20) — server-side folder dialog,
      `project.json`, per-scheme files, recent list
- [ ] **Comparison tab** (§21) — columns of schemes with thumbnails, feeding the
      report
- [ ] geometry fingerprint and the 1% rule (§21); until it exists, a scheme
      saved against a different model still loads and misreports
- [x] ~~back-end selection (accounts + scheme snapshots)~~ → **no back end in phase
      one**, fully local (§17, redecided 2026-08-30)
- [x] ~~change `FindFreePort()` to prefer a fixed port~~ → **done**; fixed 51275,
      falling back to random if taken (2026-08-30)
- [x] ~~initialise the git repository, normalise directories, move research assets
      out~~ → **done** 2026-08-30; the 212 MB PDF and 92 JPGs are kept outside the
      repository
- [x] ~~lift the inlined Carbon Conscience dataset out of viewer.html~~ →
      **done**; `data/cc_dataset.json` and `data/materials.json`, served by the
      plugin at `/data/cc.js` and `/data/materials.js`; viewer.html dropped from
      200 KB to 77 KB (2026-08-30)
- [ ] **`/version` endpoint** (§23) — the viewer re-fetches the whole payload every
      1.5 s and compares the whole string, so an idle model still costs several
      megabytes a second of serialising and sending
- [ ] **build off the Grasshopper UI thread** (§23) — it runs inside
      `SolveInstance`, so Rhino is frozen for its duration
- [ ] three.js is loaded from a CDN, so the first open needs network access.
      Genuine offline use requires inlining roughly 600 KB
- [ ] split viewer.html into modules once the UI has settled

> Order of work: see part 6 of `system-design.md`.
