# QS3D-CAD CLI batch automation

The standalone CLI keeps its existing interactive REPL and single-command argv behavior, and also exposes deterministic non-interactive automation modes.

## Modes

```text
QS3D.Cad.Cli --batch <file>
QS3D.Cad.Cli --batch <file> --continue-on-error
QS3D.Cad.Cli --stdin
QS3D.Cad.Cli --stdin --continue-on-error
```

Exactly one batch input source is required. `--batch` and `--stdin` are mutually exclusive. Duplicate batch options and unknown batch tokens fail as usage errors.

Batch input is fully buffered before the first CAD command is executed. This prevents a file/stdin read failure from occurring after a partial command stream has already mutated the standalone document.

Blank lines and lines whose trimmed text begins with `#` are ignored. Other lines are passed to the existing `StandaloneCadApplication.Execute` command engine; the CLI does not implement a second CAD command parser or mutation model.

## Execution policy

Default batch execution is fail-fast. The first failed command ends the batch. `--continue-on-error` keeps executing subsequent commands but the process still returns a failure exit code if any command failed.

Per-command status is deterministic:

```text
OK line=<physical-line>
ERROR line=<physical-line> <single-line-message>
```

The batch always emits a final command summary when command execution begins:

```text
SUMMARY commands=<n> succeeded=<n> failed=<n> stoppedEarly=true|false
```

Editor messages emitted by the existing host are preserved after each command. Message cursors reset when the active in-memory document/editor changes, so switching documents does not suppress the new document's messages.

## Exit codes

- `0`: every executed CAD command succeeded.
- `1`: one or more CAD commands failed.
- `2`: batch invocation/input failure, such as conflicting options, a missing path, or unreadable input.

## Validation boundary

`tests/QS3D.Cad.SmokeTests` references `QS3D.Cad.Cli` directly so authoritative standalone smoke compiles and executes the reusable batch runner. Regression coverage includes stdin/file success, comment/blank-line handling, physical line numbers, default fail-fast behavior, continue-on-error behavior, invalid option combinations, missing files, and preservation of the legacy non-batch argv route.

This feature is a standalone reference-host automation surface. It does not claim native AutoCAD/BricsCAD scripting fidelity, native DWG command-line behavior, process isolation, shell escaping semantics, or proprietary CAD runtime parity.
