# QS3D CAD

Standalone Windows CAD/BIM/QS product. This repository is intentionally **not a BricsCAD plugin** and does not require `NETLOAD`, `BrxMgd.dll` or `TD_Mgd.dll`.

The current bootstrap implements a real standalone application/command/document boundary against the vendor-neutral `QS3D-Platform` contracts. It uses the platform in-memory adapter for deterministic development until a legally licensed production DWG/rendering SDK adapter is connected. The in-memory/bootstrap JSON path is **not DWG compatibility evidence**.

Read [`PLANNING.md`](PLANNING.md) first.

## Clone

The platform is pinned as an exact submodule commit:

```bash
git clone --recurse-submodules https://github.com/trinhtanphat/QS3D-CAD.git
cd QS3D-CAD
```

## Validate headless bootstrap

```bash
dotnet build src/QS3D.Cad.Host/QS3D.Cad.Host.csproj -c Release
dotnet run --project tests/QS3D.Cad.SmokeTests/QS3D.Cad.SmokeTests.csproj -c Release
```

## Desktop shell

On Windows with the .NET 8 SDK:

```powershell
dotnet run --project src/QS3D.Cad.Desktop/QS3D.Cad.Desktop.csproj -c Release
```

Implemented bootstrap commands:

- `LINE x1 y1 x2 y2`
- `CIRCLE cx cy radius`
- `RECTANG x1 y1 x2 y2`
- `MOVE handle dx dy`
- `SELECT handle...`
- `ERASE handle...`
- `LIST`
- `UNDO`
- `REDO`
- `QSTAG handle kind [name]` — bind one CAD source entity to a semantic Wall/Beam/Slab/etc. element.
- `QSLIST` — list semantic elements for the active drawing.
- `QSHEALTH` — run shared Platform semantic-health diagnostics.
- `QSCOUNT [kind]` — deterministic semantic element count through the shared Quantity engine.

The standalone application journal orders CAD and semantic changes together. `UNDO`/`REDO` fail closed when a changed domain was mutated outside the application journal rather than risking reversal of the wrong operation.

The desktop shell currently visualizes the database as an entity list until the production viewport adapter is connected.

### Bootstrap persistence

Schema-v2 `*.qs3d-bootstrap.json` persists both the in-memory CAD entity snapshot and the semantic project, including stable project/element IDs and source/generated CAD references. The loader remains backward compatible with schema 1, which contained CAD entities only.

This format remains a deterministic architecture fixture, **not** the final `.qs3d` project container and not a DWG interoperability claim.

## Native SDK boundary

`QS3D.Cad.Native.Oda.Bootstrap` only discovers external SDK configuration. It deliberately does not ship or impersonate ODA binaries. See [`docs/NATIVE-SDK-BOUNDARY.md`](docs/NATIVE-SDK-BOUNDARY.md).
