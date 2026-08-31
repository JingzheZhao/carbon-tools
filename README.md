# Carbon Tools

A free, open-source carbon and cost calculator for landscape and urban design at
concept stage. It is a Grasshopper plugin that streams Rhino geometry to a local
web viewer, where each colour group in the model is assigned a material or a
published assembly and the site's embodied carbon and construction cost are
computed and mapped.

**Your model never leaves your machine.** The plugin runs a small HTTP server on
`localhost` and the viewer is a single page served from it. There is no account,
no upload, and no telemetry. Everything below works offline.

---

## Why this exists

At concept stage a landscape designer has geometry and intent, but no
specification — and that is exactly when the decisions that dominate a project's
carbon footprint get made. Tools that demand a full bill of quantities arrive too
late to change anything.

Carbon Tools asks for the one thing a concept model already has: **colour**. You
group surfaces and solids by colour in Rhino, tell the viewer what each colour is
made of, and get a number you can argue with.

## How it decides what to measure

The rule is deliberately simple, because a concept model cannot support anything
more:

| In Rhino | Measured as | Priced by |
|---|---|---|
| A solid (volume > 0) | its volume | material density × carbon factor per m³ |
| A surface | its area | a published assembly recipe per m² |

A wall is a solid, so it is measured by how much material is actually in it. A
paved terrace is a surface, so it is measured by area and read against an
assembly that already accounts for its build-up. Mixing the two would double
count.

## Provenance

Every number the tool reports carries a label saying where it came from:

| Label | Meaning |
|---|---|
| **Measured** | taken directly from the Rhino geometry |
| **Inferred** | derived from measured quantities |
| **Reference** | a published assembly's own percentage split, not your model |
| **Overridden** | you replaced the default |

This matters more than the totals. A concept-stage carbon figure that does not
say which parts are real and which are assumed is not evidence, it is decoration.

---

## Installing

1. Download the latest `CarbonTools.gha` from Releases.
2. Drop it into `%APPDATA%\Grasshopper\Libraries`.
3. Right-click the file → Properties → **Unblock**, then restart Rhino.

## Using it

1. Colour your geometry in Rhino by material intent.
2. Drop the **CC Group** component on the canvas and feed it your geometry.
3. Drop **CC Sync** downstream and set it to `true`. A browser tab opens.
4. Assign a material or assembly to each colour group in the left panel.
5. Switch between **Colour**, **Carbon**, and **Cost** to see where the weight is.

Units in Rhino can be anything — millimetres, feet, whatever the document is set
to. The plugin converts to metres before sending, because the dataset is metric.
The viewer's display units are a presentation choice and change nothing in the
arithmetic.

### Comparing options

Save the current set of assignments as a **scheme**, change one material, and
save it again. The scheme list shows each option's carbon and cost side by side
with the difference between them. Switching schemes restores the materials,
rates, colour mapping, and panel figures exactly.

> Schemes are stored in browser local storage, which is scoped to the server's
> port. That port is currently chosen at random on each start, so schemes do not
> yet survive a Rhino restart. See `docs/decision-record.zh.md` §17.

---

## Repository layout

```
src/CarbonTools/     the Grasshopper plugin (C#, net48)
  Components/        the Grasshopper components
  Sync/              geometry export, quantities, and the localhost server
  WebAssets/         viewer.html — the single-page viewer, embedded in the .gha
data/                the Carbon Conscience dataset, versioned
docs/                design documents and the decision record (in Chinese)
tools/               development scripts and test fixtures
```

Research inputs — the source white paper, page scans, and UI references — are
deliberately kept **outside** this repository so it stays small enough to clone.

## Building

Requires .NET SDK and a local Rhino 7+ install. The `.csproj` references Rhino
assemblies by absolute path; adjust the `HintPath` entries if your Rhino is not
at `D:\Softwares\Rhino`.

```bash
dotnet build src/CarbonTools/CarbonTools.csproj
```

The build copies the resulting `.gha` into `%APPDATA%\Grasshopper\Libraries`.

### Working on the viewer

`viewer.html` is embedded in the `.gha` as a resource, so a CSS change would
normally mean a rebuild and a Rhino restart. To avoid that, set an environment
variable pointing at the file on disk and the server will serve it live:

```bash
setx CARBONTOOLS_VIEWER "E:\Carbon\carbon-tools\src\CarbonTools\WebAssets\viewer.html"
```

Restart Rhino once, and from then on viewer edits appear on browser refresh.

Run the structural checks after editing the viewer — it is a single document
with one global CSS namespace, so the script catches class collisions, unbalanced
markup, hidden elements forced visible by a `display` rule, and syntax errors:

```bash
python tools/check_viewer.py
```

---

## Data and attribution

The carbon factors come from the **Carbon Conscience** landscape dataset by
Sasaki (2023), licensed CC BY-NC 4.0. See [NOTICE.md](NOTICE.md) for the full
attribution and what the licence permits.

This is a non-commercial project and will stay one for as long as it carries that
dataset.

## Status

Under active development. The viewer, the carbon and cost engines, the scheme
comparison, and the Rhino sync all work. Accounts, a hosted portal, and report
export are not built.
