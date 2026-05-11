# Non-Daily Task Items

## Goal

Add support for non-daily task items in Rhythm so users can add one-time items alongside the existing daily repeating checklist. Daily items keep the current behavior: completion resets at the next local day. Non-daily items disappear after they have been completed and the app rolls over to the next day.

## Background / Known Context

* The current state model stores `Items` and `CompletedToday`.
* `RhythmState.RolloverIfNeeded` currently clears `CompletedToday` when the local date advances.
* The edit window is the only current UI for adding, renaming, removing, and reordering items.
* Existing saved `state.json` files have no item type field and must remain readable.

## Requirements

* Items must have a repeat type with at least:
  * daily repeating item
  * non-daily item
* Existing items without a repeat type must behave as daily repeating items.
* The add-item flow must let the user choose whether the new item is daily or non-daily.
* Daily items must remain visible after rollover and become incomplete for the new day.
* Non-daily items that are completed must be removed during the next-day rollover and must not be shown afterwards.
* Non-daily items that are not completed must remain visible after rollover.
* Removing a completed non-daily item during rollover must also remove its completion entry.
* Main-window progress should count only currently visible items.

## Acceptance Criteria

* [x] Unit tests cover daily rollover preservation.
* [x] Unit tests cover completed non-daily item removal on next-day rollover.
* [x] Unit tests cover incomplete non-daily item preservation on next-day rollover.
* [x] Unit tests cover old item documents defaulting to daily behavior.
* [x] State persistence round-trips the item repeat type.
* [x] `dotnet test` passes.

## Out of Scope

* Scheduling arbitrary repeat intervals such as weekly or monthly.
* Due dates, reminders, notifications, or calendar integration.
* Immediate auto-removal when a non-daily item is checked during the same day.

## Research References

* No external research needed. Implementation follows existing state-machine and WPF edit-window patterns.
