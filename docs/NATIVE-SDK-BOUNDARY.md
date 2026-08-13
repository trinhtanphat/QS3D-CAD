# Native CAD SDK boundary

QS3D CAD requires a production drawing database/geometry/rendering implementation for real DWG work. That implementation must be supplied through a legally licensed SDK and isolated behind `QS3D-Platform`/QS3D adapter contracts.

## Rules

- No ODA, BricsCAD, Autodesk or other proprietary SDK binary is committed to this repository.
- No vendor type may appear in `QS3D.Platform` or the public QS3D application API.
- The repository must still build/test its host-neutral bootstrap without a proprietary SDK.
- A configured SDK path is not runtime qualification; exact-version native build plus drawing corpus tests are required.
- The bootstrap JSON format exists only for deterministic architecture tests and must never be described as DWG support.

## External configuration

The bootstrap probe uses `QS3D_ODA_SDK_DIR` as a future integration root. It currently verifies only that a configured directory exists. Once commercial terms and the exact SDK package are finalized, a dedicated adapter project may add compile-time references from that external path.

## Qualification required before a DWG claim

1. exact SDK version and license/distribution rights recorded;
2. synthetic DWG open/save succeeds;
3. entity identity survives save/reopen;
4. independent CAD round-trip comparison is performed;
5. layers/blocks/dimensions/hatch/layout/xref corpus is exercised;
6. unsupported/proxy data behavior is documented;
7. malformed and large drawings are tested;
8. native resource lifetime and memory pressure are tested.
