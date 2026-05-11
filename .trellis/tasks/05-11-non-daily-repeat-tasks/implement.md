# Implementation Checklist

- [x] Add item repeat kind to the state schema with backward-compatible daily default.
- [x] Extend `RhythmState` add/rename/remove/rollover logic for non-daily items.
- [x] Surface item kind through UI view models.
- [x] Add edit-window controls for choosing the kind of newly added items.
- [x] Update unit tests and state-store round-trip coverage.

## Validation

- `dotnet test`
