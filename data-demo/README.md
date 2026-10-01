# Demo dataset — invented numbers

**Every figure in this folder is made up.** It is not Carbon Conscience data, it
is not measured, and it is not fit for assessing a real project. It exists so the
interface can be shown with a short, legible list of options instead of 214
referenced elements and 192 assemblies.

The real dataset lives in `../data/` and is unchanged.

## What is here

Two materials priced by volume, for solids:

| | kg CO2e / m³ |
|---|---|
| Wood | 165 |
| Steel | 15,700 |

Five assemblies priced by area, for surfaces:

| | kg CO2e / m² | |
|---|---|---|
| Lawn | 4.8 | Softscape, sequesters 0.9 over the study period |
| Crushed Stone | 15.6 | Hardscape |
| Unit Paver | 36.1 | Hardscape |
| Concrete Paver | 50.1 | Hardscape |
| Steel | 60.1 | Hardscape |

The figures were chosen to be plausible rather than round. A demo whose numbers
are obviously fake teaches the audience to discount whatever the tool says next,
and the spread between the options is the thing the screen is for.

Each assembly is built from three or four elements — wearing course, base,
separation layer, subgrade preparation — so the per-element breakdown has
something real to show.

## Serving it

The plugin reads whichever folder `CARBONTOOLS_DATA` names, so nothing is
overwritten to switch:

```powershell
[Environment]::SetEnvironmentVariable('CARBONTOOLS_DATA', 'E:\Carbon\carbon-tools\data-demo', 'User')
```

Back to the real data:

```powershell
[Environment]::SetEnvironmentVariable('CARBONTOOLS_DATA', 'E:\Carbon\carbon-tools\data', 'User')
```

Rhino reads its environment when it starts, so restart it after either one.
