# ClubControl Lite — Specification

The small version of ClubControl: computer club PC lock and billing system for Windows 11, inspired by GameClass 3 but much simpler.
One club, one admin laptop (server), up to ~30 gaming PCs (clients) on the same local network.

**What Lite does not have** (the full ClubControl does): the Bar (shop), session items, named "Other" resources (billiards, PS5 rooms), "Update client" / "Update server" from the admin panel, the light theme. Lite is a separate product: its own ports, data folder, installer and firewall rules, so it can sit next to the full version without touching it.

## 1. Architecture

- **ClubControl.Shared** (net8.0): DTOs, enums, SignalR message contracts, constants (ports).
- **ClubControl.Server** (net8.0-windows, WPF): ONE app that is both the server and the admin panel.
  - Hosts ASP.NET Core (Kestrel) in-process with a SignalR hub on `0.0.0.0:5060`.
  - Listens for UDP discovery on port `5061`.
  - SQLite database (EF Core) in `%ProgramData%\ClubControlLite\club.db`.
  - Runs in the system tray like Telegram Desktop: closing the window only hides it; tray menu has "Open panel" and "Exit"; Exit requires the admin password.
  - Starts automatically with Windows (registry Run key). Single instance only.
- **ClubControl.Client** (net8.0-windows, WPF): runs on every gaming PC. Lock screen + session timer + command executor.
  - Started at user logon by a Task Scheduler task with highest privileges (created by an install script).
  - Windows auto-logon is configured manually on each PC (document it in README, e.g. Sysinternals Autologon).
- **ClubControl.Tests** (xUnit): billing and session-rule tests.

## 2. Networking

- Client finds the server by UDP broadcast on port 5061; the server answers with its IP, port and a server ID. The client remembers the server ID and ignores other servers.
- Client keeps a SignalR connection to the server, auto-reconnects forever, sends a heartbeat every 5 s.
- Server marks a client Offline if no heartbeat for 15 s.
- Client identity: Windows MachineGuid (`HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`). The client also reports its IP, MAC address and hostname.
- A new, unknown client appears in the admin panel as "New PC" until the admin gives it a number and tariff.
- Once a PC has a number and tariff, the server mints it a random secret token on its next heartbeat and sends it once; the client stores it and must present it on every later heartbeat, or the server ignores that heartbeat entirely (no state change, no commands). This stops a second machine from taking over a PC's identity merely by knowing or guessing its MachineGuid. Clearing a PC's number (Settings) revokes its token; reassigning a number re-provisions a fresh one. See README.md "Security notes" for the full rationale and the accepted plain-HTTP (no TLS) trade-off.
- First run of the server (elevated) adds Windows Firewall inbound rules for TCP 5060 and UDP 5061, Private profile only. The firewall is never disabled.

## 3. Time

- The server's clock is the only source of truth. The client never uses its own system clock for session time.
- The server sends remaining/elapsed seconds; the client counts locally with a monotonic timer (Stopwatch) and re-syncs on every heartbeat.

## 4. Client behaviour

**States:** Locked, Prepay session, Postpay session, Limit reached (locked), Manually unlocked.

**Lock screen (Locked / Limit reached):**
- Borderless fullscreen topmost window on every monitor; re-asserts topmost every 2 s.
- Shows club name, PC number, tariff, current price per hour, custom text. "Limit reached" shows "Please go to the admin".
- While locked, a low-level keyboard hook (WH_KEYBOARD_LL) blocks: Win keys, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4. Hook is removed when unlocked.
- Do NOT change Windows policies, registry shell, or Task Manager settings. Ctrl+Alt+Del cannot be blocked and that is accepted.

**Session timer (unlocked):**
- Small always-on-top movable window in a corner.
- Prepay: counts down remaining time. Postpay: counts up elapsed time and shows current cost.
- No "Finish" button. Only the admin can end a session.
- Warnings (popup + sound) at configurable minutes left, default 5 and 1 (prepay only).
- Postpay: warning when cost reaches 90% of the tariff maximum.

**Staff unlock (replaces the old automatic offline unlock):**
- A PC never unlocks itself because the server is unreachable. Instead, on a locked PC (Locked / Limit reached) staff press **Ctrl+Shift+F12**; a password window opens and the correct password unlocks the PC. This works whether or not the server is reachable, because the PC may lock itself for no good reason.
- The password is a separate "PC unlock password" in Settings (General). Only its PBKDF2 hash is sent to the clients (with ApplySettings, on connect and on every Settings save) and cached on disk, so the hotkey works after a reboot with the server down. Wrong guesses are throttled like the admin login (5 tries, then 30 s, doubling).
- After unlocking the PC is in free use (no session, no billing) with a small "Unlocked manually" label, until the server sends a command that changes its state (Lock, Unlock, a session). If the PC is connected the server logs a "manual unlock" event; if not, it is reported when the connection returns.
- **Repair (admin panel):** when a PC with no session is unlocked this way, its card changes from Locked to **Repair** (purple, 0 UZS) and a "Repair" counter appears in the top bar. It ends when a session is started on it, or when the PC reconnects and is locked again (reboot, or offline for more than 15 s). A PC that is locked at its cost limit keeps showing its session instead (the unlock is only logged).
- **Sessions while the server is unreachable:** the PC keeps running the session on its own clock. Prepay counts down and, at 00:00, the PC ends the session exactly as the server would (after-session action, then lock). Postpay keeps growing the cost with the same billing math as the server (`BillingCalculator.PostpayCostWithOpenSegment`) and, at the tariff maximum, locks as "Limit reached". While the server is reachable none of this locking happens on the client - the server stays the only one that ends sessions.
- When the server comes back to a PC it considered offline, it sends the PC's current state (so a manually unlocked PC is locked again, or continues its session). No warning message.

**Commands from server:** Lock, Unlock, StartSession, UpdateSession, EndSession, Shutdown, Restart, ApplySettings, ProvisionToken.

**After session ends:** depending on settings, either (a) close processes whose names are in a configurable list (e.g. steam.exe, game exes), or (b) restart the PC. Then lock.

## 5. Sessions and billing (server-side logic, unit-tested)

- Every price is in UZS. Round every charge UP to the nearest 100 UZS.
- **Charge per started minute.** A charge is never below the tariff minimum.
- **Prepay:**
  - Start by money: minutes = floor(money / pricePerHour × 60).
  - Start by time: price = minutes × pricePerHour / 60 (rounded up to 100).
  - Payment must be between tariff min and max; total paid in one session cannot exceed max.
  - Ends automatically when time runs out. Early stop: only the time used counts as income; the unused money (shown in the stop window) is handed back, and the session is recorded at the money used.
- **Postpay:**
  - Open session, counts up. Current cost = started minutes × price/60, never below min.
  - When cost reaches the tariff maximum → client goes to "Limit reached" (locked), session waits for the admin.
  - Admin ends it → payment window shows amount due → admin clicks "Paid" → session closed, PC locked.
- **Note:** the start dialog has an optional note (e.g. "birthday group"); it is saved with the session and shown in the card tooltip and the event log. There is no free-time option.
- **Move:** select two cards on the same tariff and press Move: a running session moves onto a free card, or two running sessions swap. A session is stored as segments (PC, price per hour, start, end).
  - Prepay: the remaining time is unchanged (same tariff, so the price is the same).
  - Postpay: the cost so far is kept; counting continues at the same price.
- **Shifts (smena):** the working shift is opened and closed with the button on the main panel (a chip in the title row shows the state). Every session belongs to the shift that is open when it starts; with none open, starting a session opens one. A shift cannot be closed while any session is running or waiting to be paid. **Auto-close:** a shift nobody closed ends by itself when no session has started or ended for **Settings > General > "Close a forgotten shift automatically after N hours"** (default 8, allowed 1-72; never while a session runs). It is closed at the time of its last activity, not at the moment the server noticed, so the idle hours do not count into the shift. Checked every 30 s, so also right after a server restart. Reports has a Shift view (arrows walk between shifts) that lists exactly the sessions of that shift, also past midnight.
- **Power cuts / restarts:** every session and every PC state is saved to the database immediately. On server start, active sessions are restored. A prepay session whose end time passed while the server was down is closed at its planned end time. Postpay keeps counting (customer was playing).

## 6. Power control

- **Turn on:** Wake-on-LAN magic packet (UDP broadcast) to the MAC reported by the client. Starting a session on a switched-off PC sends it by itself.
- **Shut down / Restart:** command to the client.
- Each works on one PC, selected PCs, or all PCs. Ask for confirmation if any target PC has an active session.
- README must explain the one-time PC setup: WoL in BIOS, "Wake on Magic Packet" on the network adapter, Fast Startup OFF.

## 7. Admin panel (Server app UI)

- On open: admin password prompt (single admin account). First run asks to create the password (stored hashed, PBKDF2).
- Password reset: if a file named `reset-password` exists next to the exe at startup, allow setting a new password, then delete the file.
- **Main window = one screen, GameClass style:**
  - Toolbar: Start, Stop, Move | Turn on, Restart, Shut down (each with "selected / all") | Shift, Settings, Reports.
  - One card per PC, multi-select (click, drag, Ctrl/Shift, arrow keys); double-click or Enter starts a session. Card colours: offline = red, connected and free = blue, session running = green, limit reached = orange, no client = pink, repair = purple. Offline overrides the others.
  - Event log panel at the bottom; drag the bar above it to resize it, down to the edge to hide it, double-click the bar to hide / show (the height is remembered in `%ProgramData%\ClubControlLite\log-panel-height.txt`). Events are also saved in the database; the list starts empty at every admin log-on.
- Separate windows: Start session, Move, Postpay payment, Stop (prepay), Settings, Reports.
- **Offline PCs** (no LAN connection, no client): added by hand in Settings > Computers ("Add offline PC"). They are billed from the panel like any PC but never show online/offline and have no lock screen or power control.

## 8. Settings

- **Computers:** number, tariff, enabled switch (disabled PCs stay locked and are hidden from "all"), IP/MAC/hostname shown read-only, "Add offline PC".
- **Tariffs:** freely named; price per hour, minimum payment, maximum payment (a tariff with a zero price or limit is refused).
- **General:** club name, lock-screen text, lock-screen language (Uzbek / Russian / English), admin panel language (Russian / Uzbek), warning minutes, after-session action (close process list / restart / shut down), process list, PC unlock password, auto-shutdown of idle locked PCs, **shift auto-close hours**.

## 9. Reports

- A row of buttons `←  Shift | Day | Week | Month  →`: Shift is the newest shift (the arrows walk to older ones), Day is today, Week is the last 7 days, Month is the 1st of this month up to today. The arrows step one day back / forward and only work in Day mode (greyed out otherwise). No date pickers.
- Total income, number of sessions, total hours.
- Sessions list (first tab, newest first): one row per session with the PC, start, end ("running" if not ended), duration, type, tariff, amount and note. Also in the CSV export.
- Breakdown per PC, per tariff, per session type (prepay / postpay).
- The Reports window opens from the toolbar button (top right, next to Settings) or the right-click menu on the main panel.
- The stop window (prepay) and the payment window (postpay) show, for each PC, when the session started and how long it has run.

## 10. Reliability

- Automatic daily backup of the database to `%ProgramData%\ClubControlLite\Backups`, keep last 30.
- At startup `club.db` is checked (`PRAGMA integrity_check`) before it is used; if it is damaged the admin may restore the newest good backup, and the damaged file is kept (`club_damaged_*.db`), never deleted.
- An unexpected error in the admin panel after startup is logged to `server-log.txt` and shown as a tray notice; it does not close the server.
- All UI text in .resx resource files (ru, uz, en).

## 11. Out of scope

Packages and night tariffs, messages to PCs, call-admin button, customer accounts, bookings, snacks/bar, Telegram reports, game launcher, auto-update.
