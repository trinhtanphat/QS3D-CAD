# Command journal alias reservation

## Problem

The standalone application owns `UNDO` and `REDO` because those operations coordinate both drawing database history and semantic workspace history. Their command-line aliases are `U` and `RE`.

`StandaloneCadApplication` intentionally checks the registered command catalog before applying built-in aliases. Without reserving the journal aliases, an extension could register a command named `U` or `RE` and shadow the application-owned history path.

## Contract

Extension registration through `StandaloneCommandCatalog.Register` rejects these names case-insensitively:

- `UNDO`
- `REDO`
- `U`
- `RE`

Surrounding whitespace is normalized for the reservation check.

Other extension command names remain registerable and executable through the existing registry.

## State boundary

This change does not modify alias resolution or undo/redo implementation. It only prevents extension registration from taking ownership of names that already belong to the standalone application journal.

The application journal remains responsible for coordinated drawing and semantic undo/redo. Native AutoCAD/BricsCAD command-stack behavior is outside this standalone/reference-host contract.

## Regression coverage

`CommandJournalAliasReservationModuleSmoke` verifies that:

- canonical and alias journal names cannot be registered by extensions;
- an ordinary custom extension command still registers and executes;
- `U` undoes a drawing command through the application journal;
- `RE` redoes it through the same journal path.
