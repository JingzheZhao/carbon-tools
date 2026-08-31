# Attribution and third-party licences

## Carbon Conscience dataset

The carbon factors distributed in `data/` are derived from:

> **Carbon Conscience — Landscape Data Set (Version 2)**
> Sasaki Associates, 2023.
> Licensed under [Creative Commons Attribution-NonCommercial 4.0 International
> (CC BY-NC 4.0)](https://creativecommons.org/licenses/by-nc/4.0/).

The dataset comprises 259 site elements, 193 landscape assemblies, and 216
building benchmarks. Files in `data/` are reformatted from the published tables
into JSON for machine reading; the values themselves are unmodified. A
per-cubic-metre material table (`cc_materials.js`) is derived from the published
assemblies by dividing out their stated thicknesses — this is a transformation of
the source data, not new measurement.

### What CC BY-NC 4.0 requires of anyone using this project

- **Attribution** — credit Sasaki and indicate if changes were made.
- **NonCommercial** — you may not use the dataset, or this tool while it carries
  the dataset, for commercial purposes.

Because Carbon Tools bundles this dataset, **the project as distributed is
non-commercial**. This holds regardless of how the project's own source code is
licensed. Anyone wishing to use the software commercially would need to remove
the bundled dataset and supply their own carbon factors.

### Known limitation

The published dataset provides values without per-value citations. Requesting the
underlying bibliography from Sasaki is an open item — traceability to primary
sources is a hard requirement for an open tool, and until it exists the factors
should be treated as authoritative-but-unsourced.

## Runtime dependencies

| Library | Version | Licence |
|---|---|---|
| [three.js](https://threejs.org/) | r128 | MIT |
| Newtonsoft.Json | as shipped with Rhino | MIT |
| RhinoCommon, Grasshopper, GH_IO | referenced, not redistributed | McNeel EULA |

three.js is bundled inside `viewer.html` so the tool works offline. Rhino and
Grasshopper assemblies are referenced at build time and are **not** redistributed
with this project.

## Typefaces

The viewer requests Instrument Sans and IBM Plex Mono (both SIL Open Font
License 1.1) and falls back to system fonts when offline.
