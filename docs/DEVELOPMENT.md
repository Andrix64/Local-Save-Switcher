# Development

Technical notes for people working on the code. The user guide is in the
[README](../README.md).

## Projects

| Project | Target | Contents |
|---|---|---|
| `src/Lss.Core` | `net10.0` | The logic: no interface and no operating-system API. |
| `src/Lss.Platform` | `net10.0` | What depends on the system: `WindowsPlatform`, `LinuxPlatform`. |
| `src/Lss.App` | `net10.0`, Avalonia 12.1.4 | The interface. The exe is `LSS.exe` on Windows and `lss` on Linux (`AssemblyName` `LSS`). |
| `tests/Lss.Tests` | `net10.0`, xUnit | Tests. They run on Windows and on Linux. |
| `tools/UiScreenshots` | `net10.0` | Pictures of the windows with fake data (see below). |

`Lss.Core` never calls the system directly: it asks `IPlatform` (in
`src/Lss.Core/IPlatform.cs`). The tests use `FakePlatform`.

## Paths

| What | Path |
|---|---|
| Game saves | `<Steam>\userdata\<AccountID>\<AppID>\` (Linux: `<Steam>/userdata/<AccountID>/<AppID>/`) |
| App data | `%LOCALAPPDATA%\LSS\` (`AppData.Root`); on Linux `~/.local/share/LSS/` |
| Playthroughs | `<data>\<AccountID>\<AppID>\Profili\Profilo_<name>\` |
| Note and play time | `<data>\<AccountID>\<AppID>\Info\Profilo_<name>.ini` |
| Automatic copies | `<data>\<AccountID>\<AppID>\Backup\<reason>_<date>\` plus `<reason>_<date>.ini` next to it |
| Saved points | `<data>\<AccountID>\<AppID>\Savestate\Profilo_<name>\<date>_<point name>\` |
| State | `<data>\<AccountID>\<AppID>\stato.ini` |
| Log | `<data>\log.txt` (above 1 MB it becomes `log.old.txt`) |
| Settings (last account and game) | `<data>\lss.ini`: `UltimoAccount=`, `UltimoGioco=` |
| Steam Cloud setting | `<Steam>\userdata\<AccountID>\7\remote\sharedconfig.vdf` (read only) |

Data lives in `%LOCALAPPDATA%` rather than Documents because Documents is often
synced with OneDrive, which locks files while uploading them, keeps them only
in the cloud ("files on demand") and creates "conflict" copies: all things
that make copies fail or mix up playthroughs in a save folder. `Local` rather
than `Roaming` because backups can be large.

Data of Control Save Manager is moved here at startup: see
[Compatibility with Control Save Manager](#compatibility-with-control-save-manager).

Steam folders:

- Windows: the registry value `HKCU\Software\Valve\Steam\SteamPath`, then the
  standard paths under `Program Files (x86)` and `Program Files`
  (`WindowsPlatform`).
- Linux: `~/.steam/steam` (and `~/.steam/root`), `~/.local/share/Steam`,
  the Flatpak folder `~/.var/app/com.valvesoftware.Steam/...` and the Snap
  folder `~/snap/steam/common/...` (`LinuxPlatform`).

Single instance:

- Windows: `WindowsPlatform.AcquireSingleInstance` takes the named mutex
  `Local\LSS` and, for compatibility, the old one of Control Save Manager. If
  either is taken, the app does not start.
- Linux: a lock file `lss.lock` in `$XDG_RUNTIME_DIR` (or the temporary folder).
  The system frees it when the process ends, also after a crash.

## Compatibility with Control Save Manager

LSS started as Control Save Manager (0.9.0, Control only). Everything that
exists only to take over its data is in **`src/Lss.Core/LegacyMigration.cs`**
and its tests in **`tests/Lss.Tests/LegacyMigrationTests.cs`**, so it can be
removed in one go once nobody needs it.

What it does:

- **Data folder.** Control Save Manager kept its data in
  `%LOCALAPPDATA%\ControlSaveManager`, and earlier development versions used
  `Documents\ControlSaves`. At startup `LegacyMigration.Run` moves them to
  `LSS` in two steps: `Documents\ControlSaves` to `ControlSaveManager`, then
  `ControlSaveManager` to `LSS`. Each step goes through
  `LegacyMigration.Move`: on the same drive a single `Directory.Move`; across
  drives a copy into `<destination>.spostamento` (`FileOps.CopyAllVerified`),
  verifying every file, and only then deleting the original. If the
  destination already has data, nothing is moved (`BothExist`).
- **Layout.** `LegacyMigration.RestructureAccounts` moves each account's
  Control data from `<AccountID>\` into `<AccountID>\870780\`, skipping
  whatever is already there, and turns `account.txt` into `lss.ini`. It runs
  at every start, also on the old folder when a move fails (so the app finds
  the playthroughs there), and can be repeated safely.
- **Message.** `LegacyMigration.Notice` writes the outcome to the log and
  returns the text shown once to the user (`DataMoved`, `DataMovedOldLeft`,
  `DataBothExist`, `DataMoveFailed`).
- **Mutex.** `LegacyMigration.MutexName` (`Local\ControlSaveManager`): LSS
  also holds it, so an old Control Save Manager can't run next to it.
- **Exports.** `LegacyMigration.ExportedGame` reads a zip of format 1
  (Control Save Manager, no `AppId`) as Control.

To remove it:

1. Delete `src/Lss.Core/LegacyMigration.cs` and
   `tests/Lss.Tests/LegacyMigrationTests.cs`.
2. Startup (`Program.cs` of the app): remove the call to
   `LegacyMigration.Run()` and the message from `LegacyMigration.Notice`.
   `AppData.Root` stays `AppData.DefaultRoot`.
3. `WindowsPlatform.AcquireSingleInstance`: take only `Local\LSS`; remove
   `InstanceLock` if it holds a single mutex.
4. `SaveManager.ImportProfileCore`: read `AppId` and `Gioco` from
   `partita.ini` directly and refuse zips without `AppId` with
   `Strings.ImportInvalid`. Delete the test `Import_Format1IsControl` in
   `PlaythroughTests.cs`.
5. Texts: delete `DataMoved`, `DataMovedOldLeft`, `DataBothExist`,
   `DataMoveFailed` from both resx files; in `ImportInvalid` remove "or
   Control Save Manager".
6. `FileOps.CopyAllVerified` and `CopyAll`: delete them (and their test in
   `FileOpsTests.cs`) if nothing else uses them.
7. Docs: this section, the "Coming from Control Save Manager" part of the
   README, and the CHANGELOG line about the migration in the next version.
8. `grep -rn "LegacyMigration\|ControlSaveManager\|account.txt" src tests`
   must print nothing.

## Accounts and games

If more than one Steam account has saves for a game, the app asks which one to
use at startup (name read from `userdata\<AccountID>\config\localconfig.vdf`)
and preselects the last one chosen. Each account has its own playthroughs,
copies and saved points.

In the code and on disk the historical names remain: a *profile* is a
playthrough, a *savestate* is a saved point, a *backup* is an automatic copy.
Folder, file and key names on disk are Italian (`Profili`, `stato.ini`,
`ProfiloAttivo`...) and stay so whatever the app's language.

Steam's `remotecache.vdf` file, at the top level of the game folder, is never
copied or deleted. Everything else is copied and verified at any depth: there
are no other exclusions (a file with that name further down is a save like
any other).

## Steam libraries and games

Steam keeps games in the Steam folder and in the other libraries listed in
`steamapps\libraryfolders.vdf`. `SteamLibrary` reads that file (paths are
written with `\\`, which `Vdf` already handles), keeps only the folders that
exist, and looks for each game's `steamapps\appmanifest_<AppID>.acf` in all
of them to get its name, install folder and library. A game without a
manifest is shown with the name from a short list of known games
(`SteamLibrary.KnownNames`) or as `App <AppID>`.

A game is *supported* (`SupportState.Supported`) when its folder
`userdata\<AccountID>\<AppID>\` has a file other than the top-level
`remotecache.vdf`. If only `remotecache.vdf` is there, the game saves elsewhere
(Steam Auto-Cloud): `SavesElsewhere`. An empty folder is `NoSaves`. Supported
games come first in the list.

Skipped folders: `7`, `760` and `241100` (Steam settings, screenshots,
controller settings) and any name that is not a number. Manifests of Proton
and of the Steam runtimes are not games.

The images are in `appcache\librarycache`: the 32×32 icon is the `.jpg` named
with 40 hexadecimal characters in `<AppID>\` (or `<AppID>_icon.jpg` in older
versions), the cover is `library_600x900.jpg` (or `<AppID>_library_600x900.jpg`).

Linux reads the same files from the Steam folders above. Steam's `registry.vdf`
(in the same folders) tells whether a game is running (see below).

## Copy safety

- Every backup and every copy is compared byte by byte with the original.
- Activation and "Save progress" work in two steps: the source is copied to
  `<folder>.nuovo`, verified, and then the folders are swapped (the old one
  goes through `<folder>.vecchio` and is deleted). If the folder is in use and
  can't be moved, it falls back to copying directly from the already verified
  copy. If anything fails, the backup is restored.
- `stato.ini` is written to `stato.ini.tmp` and then replaced.
- Recovery at startup from an interrupted operation (for example a power
  cut): before looking for accounts, for every game folder, and on opening,
  for `Profili` and `Savestate`. If the folder is missing but
  `<folder>.vecchio` exists, that goes back in place (it's the state before
  the operation); if the folder exists, leftover `.nuovo` and `.vecchio`
  folders are deleted. An interrupted case-only rename (`<profile>.rinomina`)
  is completed. A lone `.nuovo` is left alone. Every fix goes to the log.
- An account is recognised if it has the game's `870780` folder, even without
  `remote` (which is missing after activating a hand-made profile). Any other
  game is recognised by its folder `userdata\<AccountID>\<AppID>`.
- Only one instance of the app at a time (see *Paths*).
- Every operation that touches the game's saves is blocked while the game is
  running, both in the logic (`IsGameRunning` in every operation) and in the
  interface: the main window and History check it every 2 seconds and disable
  the buttons. `IsGameRunning` is true when Steam says so or when a process
  runs from the game's install folder. If that can't be checked (for example
  a process started as administrator), it counts as running, to be safe.
- At startup the main window reports (and logs) the folders in `Profili`
  ignored because their name is invalid.

### How LSS knows the game is running

- Windows (`WindowsPlatform`): the `RunningAppID` value and `Apps\<AppID>\Running`
  under `HKCU\Software\Valve\Steam`, and the processes whose executable is in the
  game's install folder (their start time gives the session's start).
- Linux (`LinuxPlatform`): the same values in the `registry.vdf` files of Steam
  (native, Flatpak and Snap), and the processes read from `/proc`: a process
  counts when its executable or environment belongs to the game.

Both are read again every 2 seconds while the app is open.

## Playthroughs and play sessions

- **Play this** (`MainWindowViewModel`, `GameViewModel`): if the game has
  unsaved progress it saves it to the current playthrough, then activates the
  chosen one and starts the game with `steam://rungameid/<AppID>`
  (`SaveManager.LaunchGame`). With `AvviaGioco=0` or "Load without starting" it
  doesn't start the game and offers **Undo**, which does the same steps back to
  the previous playthrough.
- **Sessions** (`SessionTracker`, `Services/GameMonitor`): the main window checks
  the game every 2 seconds. When the game starts it records the time (the
  process start time, if readable) and the playthrough; when it closes it adds
  the time with `AddPlayTime` and, with `SalvaAllaChiusura=1`, saves the
  progress (`SaveProgress(onGameExit: true)`). Only works while the app is open.
- **Files kept the same** (`FileCondivisi` in `stato.ini`, `GameDefaults.SharedFiles`):
  when activating a playthrough, each listed file is first copied into the
  playthrough; when going back to a point or a copy it's put back afterwards.
  **Same game options for all playthroughs** (`OpzioniCondivise=1`) does the same
  for the options file of the game.
- **Copy owner**: every copy has a `.ini` next to it with the playthrough its
  content belongs to, the operation (`BackupKind`) and a detail. A
  playthrough's History shows only its own copies; those without an owner
  (earlier versions, or made with no playthrough in the game) are in a
  separate section and ask for confirmation.
- **Deleted playthroughs**: the folder becomes an `Eliminato_<playthrough>`
  copy with note and play time in its `.ini`; "Recover" puts it back among the
  playthroughs (with a free name if the old one has been reused). Saved points
  are deleted.
- **First start**: if there are no playthroughs but the game has saves,
  `AdoptGameSaves` saves them as the first playthrough and marks it as in the
  game, without touching the game folder.

## Formats

### `stato.ini`

```ini
ProfiloAttivo=Profilo_Mario
AttivatoIl=22/10/2026 18:30:00
SalvatoIl=-
BackupDaConservare=20
AvviaGioco=1
SalvaAllaChiusura=1
OpzioniCondivise=0
FileCondivisi=remote/preferences_data
```

`-` means an empty value. `BackupDaConservare=0` keeps all copies. Dates are
always written as `dd/MM/yyyy HH:mm:ss` (invariant culture) and shown in the
system language's format.

| Key | Meaning |
|---|---|
| `ProfiloAttivo` | Playthrough in the game |
| `AttivatoIl` / `SalvatoIl` | When it was loaded / last saved |
| `BackupDaConservare` | Automatic copies to keep |
| `AvviaGioco` | "Play this" also starts the game |
| `SalvaAllaChiusura` | Save progress when the game closes |
| `OpzioniCondivise` | Same game options for all playthroughs |
| `FileCondivisi` | Files kept the same, separated by `\|`, relative to the save folder, with `/` |

### Folder names

- Playthroughs: `Profilo_<name>`, with letters (accented too), digits, spaces,
  `_` and `-`, no leading or trailing spaces, at most 50 characters. Folders in
  `Profili` that don't follow the rule are ignored and reported.
- Copies: `Prima_salvataggio_<playthrough>` holds the playthrough before
  saving, `Eliminato_<playthrough>` a deleted playthrough; the others
  (`Prima_di_<playthrough>`, `Prima_creazione`, `Prima_ripristino`) the game's
  saves. The text shown comes from `SaveManager.BackupDescription`, which uses
  the `.ini` next to the copy or, for copies from earlier versions, the name.

### `.ini` files of a playthrough and of a copy

```ini
; Info\Profilo_Mario.ini
Nota=Polaris mission, before Ahti
TempoDiGioco=45000
UltimaSessione=2026-10-22 22:11:00

; Backup\Prima_di_Profilo_Luigi_2026-10-22_22-11-03-120.ini
Partita=Profilo_Mario
Motivo=attivazione
Dettaglio=Profilo_Luigi
```

`TempoDiGioco` (play time) is in seconds. `Motivo` (reason) is one of
`attivazione` (activation), `salvataggio` (save), `chiusura` (save on game
exit), `ripristino` (restore), `creazione` (creation), `eliminazione`
(deletion).

### Exported playthrough

A zip with `partita.ini` (`Formato=2`, `AppId`, `Gioco`, `Nome`, `Nota`,
`TempoDiGioco`, `UltimaSessione`), `salvataggi\` (the playthrough folder) and,
if any, `punti\` (the saved points). `AppId` is the Steam number of the game
and `Gioco` its name. Import rejects zips without `salvataggi\`, paths that
leave the folder, and playthroughs of another game (`ImportOtherGame`).
Zip entries always use `/`, so the same zip imports on Windows and Linux.

Format 1 (written by Control Save Manager) has no `AppId`: it is read as
Control (`870780`) by `LegacyMigration.ExportedGame`. Format 2 is the only
one written now.

### Steam Cloud

Steam stores each game's "Keep games saves in the Steam Cloud" setting in
`sharedconfig.vdf`, in the block `apps → <AppID> → "cloudenabled"`. The app
reads only that value (`Vdf.cs` is a minimal VDF reader): `"0"` means off; a
missing key or file means on (Steam's default). Steam's global "Enable Steam
Cloud" setting is not read.

With the cloud off, the warning bar isn't shown. Steam regenerates
`remotecache.vdf` even with the cloud off: that's normal.

## Languages

Interface texts are in `src/Lss.Core/Strings.resx` (English, the fallback
language) and `Strings.it.resx` (Italian). The language follows the system
(`CultureInfo.CurrentUICulture`). The `Strings` class is generated by the build
(`StronglyTypedLanguage` in the `.csproj`), no Visual Studio needed.

- Texts with values: `Loc.F(Strings.Key, value1, value2)`.
- To add a text: same key in both files. The `LocalizationTests` test checks
  that every key exists in both languages with the same `{0}`, `{1}`...
  placeholders. `XamlKeysTests` checks the keys used in the XAML.
- To add a language: `Strings.<code>.resx` with the same keys.
- The log (`log.txt`), code comments and on-disk names stay in Italian.

## Code structure

### `src/Lss.Core`

| File | Contents |
|---|---|
| `SaveManager.cs` | Core logic: Steam accounts, playthroughs, activation, saving, saved points, automatic copies, Steam Cloud. |
| `SaveManager.Playthroughs.cs` | Note and play time, copy owner, deleted playthroughs, first start, settings, shared files, export, import, starting the game. |
| `SessionTracker.cs` | Game sessions: when the game starts and stops, and the play time. |
| `IPlatform.cs` | What depends on the system: Steam folders, running games, starting the game, single instance. |
| `Platform.cs` | The platform in use (`Platform.Current`). |
| `IniFile.cs` | Safe reading and writing of `.ini` files. |
| `FileOps.cs` | Copying, verifying and replacing folders. |
| `AppState.cs` | Reading and writing `stato.ini`. |
| `AppData.cs`, `AppSettings.cs` | App data folder and `lss.ini`. |
| `GameDefaults.cs` | Files kept the same for known games. |
| `CardText.cs` | Lines of text of a playthrough card. |
| `LegacyMigration.cs` | Everything for Control Save Manager: data migration, its mutex, format 1 exports (removable, see above). |
| `Vdf.cs` | Reader for Steam configuration files. |
| `SteamLibrary.cs` | Steam libraries, manifests, support state, images and the list of an account's games. |
| `GameInfo.cs` | `GameInfo` record and `SupportState`. |
| `Log.cs` | Log of operations and unexpected errors. |
| `Result.cs` | Outcome of operations (message, backup created, serious error). |
| `Loc.cs`, `Strings.resx`, `Strings.it.resx` | Texts in English and Italian. |

### `src/Lss.Platform`

| File | Contents |
|---|---|
| `WindowsPlatform.cs` | Windows: Steam path from the registry, running games (registry and processes), the single-instance mutex. |
| `LinuxPlatform.cs` | Linux: Steam folders, `registry.vdf`, processes through `/proc`, the single-instance lock file, starting the game. |

### `src/Lss.App`

| File or folder | Contents |
|---|---|
| `Program.cs`, `App.axaml(.cs)` | Startup, version (`Program.Version`), the application. |
| `Views/` | Windows and the playthrough card (`MainWindow`, `HistoryWindow`, `MessageWindow`, `SettingsWindow`...). |
| `ViewModels/` | The logic of each window, without controls. |
| `Services/` | Dialogs (`DialogService`), the game monitor (`GameMonitor`), `InfoKind`. |
| `Localization/Str.cs` | Markup extension for texts in XAML: `Text="{loc:Str Undo}"`. |
| `Styles/Theme.axaml` | Colours and styles of the dark theme. |
| `Assets/app.svg` | Source of the icon (`scripts/make-icons.sh` makes the rest from it). |
| `Assets/app.ico` | Icon of the exe and the windows. |
| `Properties/PublishProfiles/` | Publish profiles `win-x64.pubxml` and `linux-x64.pubxml`. |
| `Lss.App.csproj` | Version (`<Version>`), packages and publish settings. |

### Other files

| File | Contents |
|---|---|
| `scripts/make-icons.sh` | Builds `app.ico` and the Linux icons from `app.svg`. |
| `scripts/release.ps1` | Builds the Windows zip (locally and on GitHub Actions). |
| `scripts/release.sh` | Builds the Linux packages: tar.gz, deb, rpm, AppImage. |
| `scripts/linux-check.sh` | Tests and packages on a Linux machine with Docker (see below). |
| `packaging/linux/` | Desktop file, metainfo, icons, `nfpm.yaml` (deb and rpm), `lss.sh`. |
| `.github/workflows/build.yml` | Tests, Windows and Linux builds and draft release on GitHub. |
| `PRIVACY.md` | What LSS does and doesn't do with data. |
| `tests/Lss.Tests/` | xUnit tests (run on Windows and Linux). |
| `tools/UiScreenshots/` | Pictures of every window with fake data (see below). |

## Building

Requires the .NET 10 SDK. The solution has all the projects (app, core,
platform, tests and tools):

```bash
dotnet build LSS.slnx -c Release
```

Single exe for 64-bit Windows, with the .NET runtime included (users don't
have to install anything, about 50 MB), in `bin\publish\win-x64\`:

```bash
dotnet publish src/Lss.App/Lss.App.csproj -p:PublishProfile=win-x64
```

The Linux build, in `bin/publish/linux-x64/`, uses the `linux-x64` profile the
same way. `scripts/release.sh` (see *Packages*) runs it for you.

The icon comes from `src/Lss.App/Assets/app.svg`, the only source. After
changing it, `bash scripts/make-icons.sh` rebuilds `app.ico` and the Linux
icons (needs `rsvg-convert` and `icotool`: on Debian/Ubuntu `librsvg2-bin`
and `icoutils`).

The version is `<Version>` in `src/Lss.App/Lss.App.csproj`. The window header
and `LSS --version` show it without the commit suffix the SDK adds
(`Program.Version`, from the informational version).

For debugging in VS Code there are `.vscode/launch.json` and
`.vscode/tasks.json`.

## Tests

xUnit tests in `tests/Lss.Tests`: they work on fake folders in the temporary
folder (`TestFolder`) and use `FakePlatform` for everything that would touch
the registry, processes or Steam. They never touch the app's real data. They
cover copying and swapping folders, playthroughs, saved points, automatic
copies and their owner, note and play time, deleted playthroughs, export and
import, shared files, first start, `stato.ini`, Steam Cloud, accounts,
recovery from interrupted operations, game detection, Steam libraries and
manifests, Linux paths and permissions, moving data from Documents, translated
texts, card lines and the view models. Tests run in Italian whatever the system
language. The tests' log goes to `csm_test_log.txt` in the temporary folder.

```bash
dotnet test LSS.slnx -c Release
```

The windows are not checked by the tests (only the view models are). The
tests run on every push and pull request on GitHub (Windows and Ubuntu), see
*Build on GitHub*.

### Checking on Linux without GitHub

`scripts/linux-check.sh` runs on a Linux machine with Docker, reached over
SSH with a key (no password; the user must be able to run `docker`, root is
not needed). It copies the current commit (`HEAD` only, not uncommitted
changes) to `~/lss-ci` and, in temporary containers only:

1. runs the tests (`mcr.microsoft.com/dotnet/sdk:10.0`);
2. builds the packages with `scripts/release.sh`, as the `linux` job of the
   workflow does, and leaves them in `~/lss-out`;
3. installs each package in a clean distribution and runs `lss --version`:
   deb on Ubuntu 24.04, Ubuntu 22.04 and Debian 13, rpm on Fedora 42, the
   AppImage on Ubuntu 24.04, the tar.gz with `install.sh` on Arch Linux.

```bash
bash scripts/linux-check.sh user@host
```

With `--tests-only` it stops after the tests. It can't replace a check with
the real Steam and a desktop.

### Pictures of the windows

`tools/UiScreenshots` draws the windows of the Avalonia app (main window,
confirmations, new playthrough, note, History, Settings, first start,
accounts, deleted playthroughs) with fake data and saves them as PNG, to
check the look without opening the app on real saves. It works in a
temporary folder (data and log included) and never touches Steam or the
app's data folder. Second argument: `it` (default) or `en`. The README pictures
are made with `en`.

```bash
dotnet run --project tools/UiScreenshots -c Release -- <folder outside the repository> en
```

Menus opened from a button (the card's ⋯) are separate popups and are not
in the pictures. If Smart App Control blocks the tool's dll, rebuild it
with `--no-incremental` (see [Smart App Control](#smart-app-control)).

## Packages (Linux)

`scripts/release.sh` (on Linux, or in the Linux Docker check) builds, in
`dist/`:

- `LSS-<version>-linux-x64.tar.gz`: the exe, `install.sh`, the desktop file, the
  metainfo and the icons. `./install.sh` installs into `~/.local`,
  `./install.sh --uninstall` removes the program (not the data);
- `lss_<version>_amd64.deb` and `lss-<version>-1.x86_64.rpm`, made with
  [nfpm](https://nfpm.goreleaser.com) (version `2.47.0`, the script checks that
  it's installed). The metadata is in `packaging/linux/nfpm.yaml`;
- `LSS-<version>-x86_64.AppImage`, made with `appimagetool` `1.9.1`, downloaded
  by the script and checked with its SHA-256 hash.

Every file has a `.sha256` next to it. The packages install `lss` in
`/usr/bin`, the program in `/usr/lib/lss/`.

## Build on GitHub

The repository is on [GitHub](https://github.com/Andrix64/Local-Save-Switcher).
The `.github/workflows/build.yml` workflow runs on every push to `main` and
`v*` tags, on every pull request and on demand (*Run workflow*):

1. `test`: the tests on Windows and Ubuntu;
2. `windows`: on tags it checks that the tag matches `<Version>`; it publishes
   the single exe, builds the zip with `scripts/release.ps1 -Exe <exe>` and
   uploads it as the `lss-windows` artifact, with the test results;
3. `linux`: on Ubuntu 22.04 it runs `scripts/release.sh` and checks the packages
   by installing them: deb on Ubuntu 24.04, rpm on Fedora 42, AppImage on
   Ubuntu 24.04. The packages are uploaded as `lss-linux`;
4. `release`: on tags only, it prepares a **draft** release with the zips,
   the packages and the `.sha256` files. A tag with `-` (for example
   `v1.0.0-beta.1`) is a pre-release.

## Code signing

The Windows exe is **not** code-signed. Smart App Control and SmartScreen
(see below) want a signature from an authority trusted by Microsoft; a
self-signed certificate isn't enough. Free signing through SignPath
Foundation was tried and the application was refused (2026-10-25). The
remaining options cost money every year (for example Azure Trusted Signing or
a code signing certificate from a certificate authority) and are not planned.
The README tells users what to do with SmartScreen and Smart App Control.

The native Avalonia dlls inside the exe (SkiaSharp, HarfBuzz, ANGLE) are
already signed by Microsoft and AvaloniaUI. If signing is added one day, only
`LSS.exe` needs it: add a signing step to the `windows` job between
"Single exe" and "Zip", and pass the signed exe to `release.ps1 -Exe`.

## Release

1. Update `<Version>` in `src/Lss.App/Lss.App.csproj` (for example `1.0.0-beta.1`)
   and the [CHANGELOG](../CHANGELOG.md), and the release line in
   `packaging/linux/io.github.andrix64.LocalSaveSwitcher.metainfo.xml`, then
   commit. The version shows in the window header, in the exe properties and
   in the log. The tag must be `v` followed by the same version.
2. Create the version tag and push it to GitHub:

   ```bash
   git tag v1.0.0-beta.1
   ```

   ```bash
   git push origin main v1.0.0-beta.1
   ```

3. On GitHub, in **Releases**, check the draft (every file and its `.sha256`)
   and publish it. Pre-releases are published as such.

Without GitHub, the Windows zip can also be built locally:

```bash
powershell -ExecutionPolicy Bypass -File scripts/release.ps1
```

The script refuses to run with uncommitted changes, runs the tests, publishes
the exe and creates in `dist\` the zip `LSS-<version>-win-x64.zip`
(exe, README, CHANGELOG and LICENSE) with its `.sha256` file. With
`-Exe <path>` it uses a ready exe instead of publishing one.

## Smart App Control

Smart App Control (Windows 11) may block a self-built exe, which isn't
signed: the app closes right after starting and the event log shows "An
Application Control policy has blocked this file". It happened with a version
that called `dwmapi.dll` and `uxtheme.dll` directly (dark title bar and
scrollbars); without those calls it started. Better to avoid `DllImport` and
`LibraryImport` in the project.

The verdict is about the single file (its hash), not just the code: a Debug
build was blocked (`FileLoadException ... 0x800711C7`) while the Release build
of the same sources started. A deterministic build always produces the same
file, so rebuilding didn't help. That's why Debug builds are not
deterministic: if one is blocked, rebuild everything (VS Code task `rebuild`,
or `dotnet build --no-incremental`) and the new file usually passes. On
2026-10-23 every new build of Control Save Manager was blocked; on 2026-10-24
the unsigned Avalonia `LSS.exe` started with Smart App Control on. Since the
exe is not signed, users with Smart App Control on may still be blocked: see
the README. The event log is `Microsoft-Windows-CodeIntegrity/Operational`
(events 3033 and 3077).

## Other documents

- [Game compatibility](COMPATIBILITY.md)
- [Known bugs](KNOWN_BUGS.md)
- [To do](TODO.md)
- [What's new in each version](../CHANGELOG.md)
