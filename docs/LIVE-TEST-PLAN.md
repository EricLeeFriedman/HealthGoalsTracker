# Live Product Test Plan

The machine-readable source of truth is
[`scripts/live-tests/features.json`](../scripts/live-tests/features.json). It defines
the product outcomes that every supported live runner must verify. Platform drivers
choose native interaction mechanisms but may not weaken those expected outcomes.

| Feature ID | Required outcome |
|------------|------------------|
| `app.launch` | The application starts with a usable Home page and no fatal runtime errors. |
| `home.initial-state` | A clean profile contains the default goals and starts at 0 / 14. |
| `navigation.flyout` | All implemented flyout items are present, readable, and navigable. |
| `goals.complete-and-reset` | Sleep completion changes the card and score to 3 / 14; Reset Today restores 0 / 14. |
| `measurements.save-and-display` | A valid weight/body-fat entry persists and appears in history. |
| `history.calendar` | Weekday labels, seven-column geometry, legend, dates, and selected-week detail render correctly. |
| `notifications.configuration` | All notification types render; supported platforms have permission and four schedules. |
| `diagnostics.runtime` | Expected events are recorded without health values, crashes, or unhandled exceptions. |

## Runners

- `scripts/verify-windows.ps1` uses Windows UI Automation plus screenshot-content
  checks. It launches with an isolated data directory.
- `scripts/verify-android.ps1` builds a self-contained debug APK, installs it on a
  running emulator/device, clears only this application's data, and uses ADB plus
  UIAutomator selectors. It never depends on fixed tap coordinates.
- Both runners save screenshots, logs, diagnostics, and `live-test-results.json`
  beneath their ignored platform artifact directory.

The Android runner requires a booted emulator or connected device. Its default SDK
location can be overridden with `-AdbPath`. Pass `-NoBuild` only when the current APK
was already built with `EmbedAssembliesIntoApk=true`.
