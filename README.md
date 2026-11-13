# ClubControl Lite

The small version of ClubControl: a computer club PC lock + billing system for Windows 11.
See `SPEC.md` for the specification and `CLAUDE.md` for the project rules.

One admin laptop runs **ClubControl.Server** (server + admin panel). Every gaming PC runs
**ClubControl.Client**. Both must be on the same local network.

**What Lite has:** PC lock screen and session timer, prepay and postpay sessions, tariffs, working
shifts (with an automatic close hour), Move/swap a session, power control (Turn on by Wake-on-LAN,
Restart, Shut down), offline PCs added by hand, reports (Shift / Day / Week / Month, CSV export),
daily database backup, three languages (Russian, Uzbek, English on the lock screen), dark theme.

**What Lite does not have** (the full ClubControl does): the Bar (shop), items added to a session,
named "Other" resources (billiards, PS5 rooms), "Update client" / "Update server" from the admin
panel, the light theme. To update Lite, run a newer setup file (see below).

Lite is a **separate product** and never touches a full ClubControl on the same PC or network:
its own ports (TCP **5060**, UDP **5061**), its own discovery message, data folder
(`%ProgramData%\ClubControlLite`), installer identity, startup task and firewall rules.
A Lite client cannot find or talk to a full ClubControl server, and the other way round.

## Installing with ClubControlLite-Setup.exe (recommended)

Download `ClubControlLite-Setup.exe` from the "latest" release on GitHub (or build it, see below)
and copy it to each PC. It needs nothing else installed - .NET is bundled - and asks for
Administrator rights once.

1. Run it and choose the setup type:
   - **Server** - on the admin laptop. Installs to `C:\ClubControlLite\Server` (a folder your normal
     Windows user can write to, because the admin password reset file lives there). Adds the Start
     menu entry (and an optional desktop shortcut) "ClubControl Lite Server", opens the two firewall
     ports (TCP 5060, UDP 5061, **Private network profile only**) and offers to start it.
   - **Client** - on each gaming PC. Installs to `C:\Program Files\ClubControlLite\Client` - on
     purpose a folder only an administrator can change, because the client starts with
     administrator rights at logon. It asks which Windows account logs on automatically on that PC
     (see "Configure Windows auto-logon" below), creates the "ClubControl Lite Client" startup task
     for that account and starts it.
2. The per-PC Windows settings below (Wake-on-LAN, Fast Startup, auto-logon) are still done by hand.
3. **To update**, run a newer `ClubControlLite-Setup.exe` over the old install with the same type
   (do the server first, then every client). It stops the running program first and keeps the database.
4. Uninstall from Windows "Apps & features". It removes the program, the startup task and the
   firewall rules and **leaves `%ProgramData%\ClubControlLite` (database, backups, logs) untouched**.

Unattended installs accept the usual Inno Setup switches plus two of its own:
`ClubControlLite-Setup.exe /VERYSILENT /TYPE=client /KioskUser="ClubPC1"` (add `/NoStart=1` to not
start it right away) and `/TYPE=server` (`/DIR="D:\Club"` picks another folder).

To build the setup yourself you need [Inno Setup 6](https://jrsoftware.org/isdl.php), then:

```
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

It publishes both programs self-contained and writes `dist\ClubControlLite-Setup.exe`.

## Building without the setup

```
dotnet publish src/ClubControl.Server/ClubControl.Server.csproj -c Release -r win-x64 --self-contained true -o publish/server
dotnet publish src/ClubControl.Client/ClubControl.Client.csproj -c Release -r win-x64 --self-contained true -o publish/client
```

Copy `publish/server` to the admin laptop and `publish/client` to every gaming PC.
`scripts/install-server.ps1` / `scripts/install-client.ps1` (and the double-click launchers
`install-server.bat` / `install-client.bat`) are not part of a local publish output - copy them next
to the matching `.exe` before running them. They do what the setup does (firewall rules, startup entry /
startup task).

## One-time setup per PC

> Installed with the setup? Then the firewall rules and startup entry/task are already done - do only
> the Windows settings below (Wake-on-LAN, Fast Startup, auto-logon) and the first-run steps.

### Admin laptop (server)

1. Start `ClubControl.Server.exe`. The first run asks you to create the admin password.
2. ClubControl Lite runs in the system tray - closing the window only hides it. Use the tray icon's
   "Exit" (password-protected) to actually quit.
3. Forgot the admin password? Create an empty file named `reset-password` next to
   `ClubControl.Server.exe` and start the server: it lets you set a new password, then deletes the file.

### Every gaming PC (client)

1. **Enable Wake-on-LAN in the BIOS/UEFI** ("Wake on LAN", "Power On by PCIE/PCI", "Resume by LAN").
2. **Enable "Wake on Magic Packet" on the network adapter** (Device Manager > adapter properties:
   Power Management tab "Allow this device to wake the computer"; Advanced tab "Wake on Magic Packet" =
   Enabled).
3. **Turn off Fast Startup** (Control Panel > Power Options > "Choose what the power buttons do" >
   "Change settings that are currently unavailable" > uncheck "Turn on fast startup").
4. **Configure Windows auto-logon** with Sysinternals Autologon
   (`https://learn.microsoft.com/sysinternals/downloads/autologon`). Use a dedicated low-privilege
   Windows account per PC, not an admin account. The client must run in a real desktop session to show the lock screen.
5. Install the client (setup, or `install-client.bat -UserId <that account>`). **The account matters**:
   the startup task has to target the kiosk account from step 4.
6. Reboot to confirm the client starts by itself and finds the server. It appears as "New PC" in the
   admin panel - assign it a number and a tariff under Settings > Computers.

### PCs with no LAN connection

A PC with no network can't run the client. Add it with **Settings > Computers > "Add offline PC"**:
pick a number and a tariff. It is billed from the panel (Start / Stop / Move) like any other PC but never
shows online or offline, and the Turn on / Restart / Shut down buttons do nothing for it.

## Working shifts

The **Open shift / Close shift** button on the main panel starts and ends the working shift (smena); a chip
in the title row shows the state. A session started with no shift open opens one. A shift can't be closed
while a session is running. If nobody closes it, it closes by itself: **Settings > General > "Close a
forgotten shift automatically after N hours"** (default 8, 1-72) - when no session started or ended for that
long (and none is running) the shift is closed at the time of its last activity. Reports > **Shift** shows
one shift; the arrows walk to older ones.

## Manual test checklist

- **Client stays running**: start the client, confirm no window appears in the taskbar and nothing you do
  on that PC closes it. End `ClubControl.Client.exe` in Task Manager - the startup task relaunches it within
  about a minute, back in the right lock/session state.
- **Wake-on-LAN**: shut a configured client PC down fully, select it, click "Turn on". Starting a session on
  a switched-off PC also sends it. If nothing happens re-check BIOS / network adapter / Fast Startup, and that
  both are on the same subnet.
- **Shutdown / Restart**: select a client (or none for "all") and click it. With an active session it asks first.
- **Sessions**: Start prepay (by money) and postpay on two PCs. Watch the card: prepay shows time left and
  "spent / paid", postpay the running cost. Stop each; the payment/stop window shows start time and duration.
- **Bulk start**: select several PCs on one tariff and Start - one dialog starts all. Mixed tariffs are refused.
- **Move**: select two PCs on the same tariff and click Move (a session moves to the free one, two sessions swap).
- **Shifts**: open a shift, start/stop a session, close the shift. Set the auto-close hours to 1 in
  Settings and leave the shift idle - it should close by itself and say so in the event log.
- **Reports**: Shift / Day / Week / Month, the Sessions tab, the breakdowns and the CSV export.
- **Languages**: Settings > General - admin panel language (Russian / Uzbek, restart to fully switch) and
  lock-screen language (Uzbek / Russian / English, applies at once).
- **Auto-shutdown**: Settings > General > "Shut down idle locked PCs after (minutes, 0 = disabled)". A locked PC
  shuts down once the time passes - never while "Limit reached" or mid-session.
- **Staff unlock**: press Ctrl+Shift+F12 on a locked PC and enter the "PC unlock password" (Settings > General);
  the card shows "Repair" until a session starts or the PC reconnects.
- **Daily backup**: `%ProgramData%\ClubControlLite\Backups` gets a `club_YYYY-MM-DD.db`; the newest 30 are kept.
- **Damaged database**: on start the server checks `club.db`; if it is damaged it offers to restore the newest
  good backup and keeps the damaged file as `club_damaged_<date>_<time>.db`.
- **Unexpected errors**: an error in the admin panel doesn't close the server; it goes to
  `%ProgramData%\ClubControlLite\server-log.txt` with a tray notice. The client logs to
  `%ProgramData%\ClubControlLite\client-log.txt` (check it first on a "stuck" PC).

## Database

The server uses EF Core migrations (`src/ClubControl.Server/Data/Migrations`) and applies any it hasn't
applied yet on every start. Before it upgrades a database that already holds data it saves a full copy as
`Backups\club_before-upgrade_<date>_<time>.db`. Lite starts with a single `InitialCreate` migration: it
creates a fresh database and **does not read a full ClubControl `club.db`** (different folder on purpose).

## Security notes

- **Plain HTTP, no TLS.** Kestrel listens on `http://0.0.0.0:5060`; SignalR traffic (including the one-time
  delivery of a PC's auth token) is unencrypted on the LAN. This is an accepted trade-off for a single club on a
  physically controlled network. If the network is ever not fully trusted, put the server behind a LAN-only
  reverse proxy/VPN first.
- **Per-PC identity.** A PC's `MachineGuid` alone can't speak for it: once a PC has a number and tariff the server
  mints a secret token (stored in `%ProgramData%\ClubControlLite\client-token.dat`) and every later heartbeat must
  carry it, or the server ignores it (a rejected heartbeat is logged once per 10 minutes and the card says
  "Auth failed"). To revoke or rotate a PC's token, clear its **Number** in Settings and Save, then give it a number again.
- **The hub exposes only `Heartbeat`** (and the staff-unlock report) to connected PCs - no session, billing or
  command RPCs. The admin panel calls the services in-process.
- **Detailed SignalR errors** are only on when `ASPNETCORE_ENVIRONMENT=Development`.
- The firewall is never disabled; the rules are inbound-allow, Private profile only.

## Development

- `ClubControl.Shared` and `ClubControl.Tests` are plain .NET and build/test anywhere:
  `dotnet test src/ClubControl.Tests`.
- `ClubControl.Server` and `ClubControl.Client` are WPF (`net8.0-windows`) and only build on Windows. The GitHub
  Actions workflow (`.github/workflows/build.yml`) builds both, runs `ClubControl.Server.Tests` (Windows only) and
  publishes `ClubControlLite-Setup.exe` as the "latest" release on every push to `main`.
- A schema change needs its EF migration in the same commit. Generate it with the "Add EF Migration" workflow
  (Actions tab > run it with a migration name; it commits the migration to `main`), or on a Windows machine with
  `dotnet ef migrations add <Name> --project src/ClubControl.Server --output-dir Data/Migrations`.
