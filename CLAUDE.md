# ClubControl Lite

The small version of ClubControl: computer club PC lock + billing system for Windows 11. Spec: SPEC.md (read only the sections a task needs).
A separate product from the full ClubControl (own ports 5060/5061, data folder `%ProgramData%\ClubControlLite`, installer id,
startup task and firewall rules) - never share any of those with the full version.

## What Lite is not
No Bar (shop), no session items, no named "Other" resources, no "Update client" / "Update server" in the panel, no light theme,
no free sessions. Do not add them back here; they live in the full ClubControl.

## Stack
C# .NET 8. WPF for Server (admin panel + hosted ASP.NET Core/SignalR) and Client. SQLite + EF Core. xUnit tests.

## Projects
- ClubControl.Shared: DTOs, enums, contracts, constants
- ClubControl.Server: server + admin panel (tray app)
- ClubControl.Client: lock screen + timer on gaming PCs
- ClubControl.Tests: billing maths and other cross-platform tests
- ClubControl.Server.Tests: SessionService/shift/report tests on a real in-memory SQLite database (Windows only, references Server)
- ClubControl.TestClient: console stand-in for a client, for checking the server

## Rules
- Keep code simple and readable; the owner is learning.
- Billing and session logic lives on the server and must have tests.
- Never disable the firewall or change Windows policies/registry shell.
- If the same error happens twice, stop and explain instead of retrying.
- EF model changes (new/changed columns, relationships) must ship with their migration in the same
  change - never push a schema-affecting edit without one (a missing migration caused a crash loop once in the full version).
  Generate it with the "Add EF Migration" workflow or `dotnet ef migrations add`.
- Identity must never be IP-based (use the MachineGuid + per-PC token); never expose session/billing RPCs or a command-push
  on ClubHub (it exposes only Heartbeat and the staff-unlock report).

## Dev environment
- ClubControl.Shared and ClubControl.Tests are plain .NET - they build and run anywhere.
- ClubControl.Server and ClubControl.Client target net8.0-windows (WPF/WinForms) and can only be built, run, or
  click-tested **on Windows** (or this repo's GitHub Actions). On Linux they can be compile-checked with generated XAML stubs and
  the Server tests run in a self-contained plain-net8.0 harness; XAML itself is never compiled there.

## Progress
- [x] Forked from ClubControl 1.1.5 on 2026-11-13. Removed: Bar, session items (`SessionExtra`), named "Other" resources
      (`ResourceKind`, `Pc.Name`, `Tariff.Kind`, `AddResourceWindow`), "Update client" / "Update server" (`CommandType.UpdateClient`,
      the updater classes, the update script/signature code in Shared, `StartupTaskRepair`), the light theme (`ThemeManager`, `UiTheme`;
      `Themes/Theme.xaml` is the dark palette, merged in `App.xaml`), free sessions (`SessionType.Free`, `Session.IsFree`).
- [x] Kept: shifts, postpay + prepay, Move/swap, Wake-on-LAN (Turn on and automatic wake on session start), offline PCs
      (`Pc.IsManual`, added with `AddOfflinePcWindow`), all three languages, staff unlock (Ctrl+Shift+F12), client-missing / auth-failed
      detection, daily backup + damaged-database check, reports (Shift / Day / Week / Month, CSV).
- [x] New: `Setting.ShiftAutoCloseHours` (default 8, Settings > General, 1-72): the idle auto-close of a forgotten shift
      (`ShiftHelper.CloseIfIdleAsync`) reads it instead of a fixed 8 hours. Covered by `ShiftTests`.
- [x] Fresh database history: one `InitialCreate` migration (no upgrade path from a full ClubControl `club.db`; different data folder on purpose).
- [x] Own identity: ports 5060/5061, discovery text `CLUBCONTROLLITE_*`, `%ProgramData%\ClubControlLite`, mutexes, Run key,
      startup task "ClubControl Lite Client", firewall rules "ClubControl Lite ...", installer `ClubControlLite-Setup.exe` with its own AppId.
- [ ] Not yet run on real hardware. Verified only by: compile-check of Server + Client with XAML stubs, Server.Tests (113) and Tests (180)
      on Linux, XAML well-formedness / handler / resource-key checks. Click through the main panel, Settings (incl. the new shift
      field), Reports, Add offline PC, Start/Stop/Move on Windows before relying on it; then the first real-club test.
