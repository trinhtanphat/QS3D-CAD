# QS3D CAD

Standalone Windows CAD/BIM/QS product. This repository is intentionally **not a BricsCAD plugin** and does not require `NETLOAD`, `BrxMgd.dll` or `TD_Mgd.dll`.

The current source implements a real standalone application/command/document boundary against the vendor-neutral `QS3D-Platform` contracts. It uses the Platform in-memory/reference adapter for deterministic development until a legally licensed production DWG/rendering SDK adapter is connected. Reference/in-memory success is **not DWG, renderer or native CAD qualification evidence**.

Read [`PLANNING.md`](PLANNING.md) first.

## Clone

The Platform dependency is pinned as an exact Git submodule commit:

```bash
git clone --recurse-submodules https://github.com/trinhtanphat/QS3D-CAD.git
cd QS3D-CAD
```

## Validate source + reference baseline

On Windows with the .NET 8 SDK:

```powershell
./scripts/validate.ps1
```

The validation script checks the exact Platform gitlink/checkout, standalone source boundaries, all pinned Platform source gates, then builds and runs deterministic Platform/CAD smoke suites when a usable SDK is available.

## Desktop shell

```powershell
dotnet run --project src/QS3D.Cad.Desktop/QS3D.Cad.Desktop.csproj -c Release
```

The desktop File menu uses `*.qs3d` project packages. Save publishes through the validated previous-generation backup path; Open uses the recovery reader and reports when a validated `.qs3d.bak` was used. Raw `*.qs3d-bootstrap.json` remains an internal deterministic fixture format and is not the primary desktop project format.

The desktop shell currently visualizes the reference database as an entity list until a production native viewport adapter is connected.

## Command surfaces

Drawing and document-journal commands include:

- `LINE x1 y1 x2 y2`
- `CIRCLE cx cy radius`
- `RECTANG x1 y1 x2 y2`
- `MOVE handle dx dy`
- `SELECT handle...`
- `ERASE handle...`
- `LIST`
- `UNDO`, `REDO` — owned by the coordinated application journal, not the public command registry.

Layer/block surfaces include `LAYERS`, `LAYER ...`, `BLOCK`, `INSERT`, `BLOCKS`, `BLOCKDELETE`.

Semantic/QS surfaces include:

- `QSTAG handle kind [name]`
- `QSLIST`
- `QSHEALTH`
- `QSCOUNT [kind]`
- `QSFLOOR`, `QSZONE`, `QSPROP`, `QSLOC`
- `QSQTY`, `QSSCHEDULE`

`QSHEALTH` combines shared semantic/readiness diagnostics with standalone live-handle validation. A semantic source/generated reference whose handle disappeared from the active drawing reports `ORPHAN_HANDLE` and blocks readiness.

Reference-only navigation/document services include `VIEW`, `ZOOMEXTENTS`, `ZOOMWINDOW`, `HITTEST`, `SNAP`, `SELPOLY`, `XREFREF`, `LAYOUTREF`, `PLOTREF`. These are deterministic adapter behavior; `PLOTREF` records a plot request and deliberately does not claim to create a native PDF.

## Persistence

### Internal bootstrap drawing fixture

Current schema-v4 `*.qs3d-bootstrap.json` can persist reference CAD entities, semantic project state, stable IDs/CAD references, layers/current layer and block definitions. The loader remains backward-readable for earlier supported bootstrap schemas and normalizes corrupt/invalid content to `InvalidDataException` at the storage boundary.

This raw JSON format is an internal architecture/test fixture, not a DWG interoperability claim.

### `.qs3d` project package

The current `.qs3d` bootstrap/reference container is real ZIP file I/O with:

- exact entry and manifest declaration sets;
- bounded package/manifest/payload reads;
- SHA-256 and declared-length validation;
- exact semantic/drawing media types;
- project identity and embedded semantic consistency checks;
- same-directory temporary publication;
- validated previous-generation `.qs3d.bak` publication;
- fail-closed recovery from corrupt/missing primary packages.

The drawing payload is still the QS3D reference/bootstrap representation. The `.qs3d` container is therefore a real project-container foundation, but it is **not native DWG payload/fidelity evidence** and is not a substitute for the native SDK qualification lane.

## Native SDK boundary

`QS3D.Cad.Native.Oda.Bootstrap` only discovers external SDK configuration. It deliberately does not ship or impersonate ODA binaries. See [`docs/NATIVE-SDK-BOUNDARY.md`](docs/NATIVE-SDK-BOUNDARY.md) and [`docs/LOCAL-NATIVE-QUALIFICATION.md`](docs/LOCAL-NATIVE-QUALIFICATION.md).

No `BUILD_PASS`, `DWG_PASS`, `LOCAL_NATIVE_PASS` or `PRODUCTION_QUALIFIED` claim should be inferred from this README. Those require the exact source SHA to pass the corresponding build/native evidence gates.
