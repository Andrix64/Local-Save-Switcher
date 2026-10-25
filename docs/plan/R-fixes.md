# Phase R — Fixes after the first try of 2.0

**Runs after phase P and before phase Q** (it was added later, so its letter
comes after Q). Requires phases K–P. Branch: `v2`. Rules in
[README.md](README.md).

Done by **Claude Sonnet 5.5** sessions, one task at a time. Commit trailer:

```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

What Andrix found trying phase P (2026-10-25), and the task for each:

| # | Problem | Task |
|---|---|---|
| 1 | A player with a PIN is not asked for it when picked on "Who's playing?" | R3 |
| 2 | The ⚙ icon of the simple mode opens the advanced mode | R7 |
| 3 | Game settings and LSS settings are in one window | R8 |
| 4 | Renaming a playthrough loses its automatic copies (history) | R1 |
| 5 | Sidebar: the launcher titles stick to the first game; the "Games" title is useless | R4 |
| 6 | Long game names | R5 |
| 7 | "Other games" have no icon | R4 |
| 8 | Simple mode: "Ask an admin" sits on top of the launcher chip | R6 |
| 9 | Simple mode: games without a cover are empty tiles | R6 |
| 10 | "Add a game": cancelling the file choice still asks the name | R2 |
| 11 | Settings files are `.ini`: they should be JSON | R9, R10 |
| 12 | Old saved paths have `\` instead of `/` | R11 |

The design canvas (https://claude.ai/artifact/YEykCPUor96ApEYdzMnmRS) stays
the reference for every screen. Where this file says something different
(R6, R7, R8), this file wins.

---

## R1 — Renaming a playthrough keeps its automatic copies

**Problem.** `SaveManager.RenameProfile` (`src/Lss.Core/SaveManager.cs`)
moves the playthrough folder, its savestates (`Savestate/<name>`) and its
info, but **not** its automatic copies. A copy in `Backup/` belongs to a
playthrough by name: in the folder name (`NewBackupPath(SaveBackupPrefix +
State.ActiveProfile)` → `Prima_salvataggio_<Profilo_name>_<date>`, the same
for `DeletedBackupPrefix`) and in the copy's info file (`<backup>.ini`,
written by `WriteBackupInfo`, read by `WithBackupInfo`). After a rename,
`GetBackupsOf(newName)` finds nothing: the history looks empty.

**Do.**

1. Read `GetBackupsOf`, `WithBackupInfo`, `WriteBackupInfo` and the
   prefixes in `SaveManager.Playthroughs.cs` to see exactly which of the
   two (folder name, info file) decides the owner. Don't guess.
2. In `RenameProfileCore`, after the savestates are moved: for every copy
   in `Backup/` that belongs to the old name (same rule as
   `GetBackupsOf`), point it to the new name. Prefer changing the info
   file (`<backup>.ini`, from R10 on the JSON file) over renaming folders;
   rename a folder only if the owner comes from the folder name alone.
   Deleted playthroughs (`BackupKind.Delete`) of the old name are **not**
   touched: they are a different playthrough that had that name.
3. A failure on one copy doesn't stop the rename: log it (`Log.Write`, in
   Italian) and add it to the result message like `SavestatesNotMoved`
   does (new key `BackupsNotMoved` in both `resx`).
4. Case-only renames (`caseOnly`) work too.

**Tests** (`SaveManagerTests` or a new `RenameKeepsBackupsTests.cs`):
create a playthrough, make two copies (save, activate), rename it →
`GetBackupsOf(new)` has both, `GetBackupsOf(old)` none; a deleted
playthrough with the old name keeps its own copy; case-only rename.

Commit: `Renaming a playthrough keeps its automatic copies`

## R2 — "Add a game": cancel means cancel

**Problem.** `MainWindowViewModel.AddGameAsync` asks the program
(`PickFileAsync`) and then **always** asks the name, even when the file
choice was cancelled (`exe is null`).

**Do.** If `PickFileAsync` returns `null`, return: nothing is added and
no other window opens. Same for the name window (already done). Check the
simple mode's "Add a game" tile goes through the same method (it calls
`addGame`).

**Test.** The method opens real windows, so move the decision into a small
testable piece if needed (for example a `AddGameFlow` class taking
`IDialogService`, like `SimplePlayFlow`), with a test: file choice
cancelled → no prompt, no game in `ManualGames`.

Commit: `Add a game: cancelling the program choice stops`

## R3 — PIN when a player with a PIN is picked

**Problem.** On "Who's playing?" (`SimpleView`, `Player_Click` →
`SimpleViewModel.SelectPlayer`) anyone can tap an admin who has a PIN and
play as them; the PIN is asked only for admin actions.

**Do.**

1. Tapping a player who **has a PIN** (`Person.HasPin`) opens the PIN pad
   for that player only, before the games page: use
   `IDialogService.ChooseAdminAsync(people, person)` — with a known admin
   it already shows only the pad (`AdminPickerViewModel.OnlyPin`).
   Wrong PIN or cancel → stay on "Who's playing?".
2. Make it a method `SimpleViewModel.SelectPlayerAsync(Person)` used by
   the view; keep `SelectPlayer` for code that has already checked (tests,
   after "Before LSS" adoption...).
3. Once the PIN is right, the actions that need an admin don't ask it
   again **while that player stays selected** (go back to "Who's playing?"
   → asked again). Store this in `SimpleViewModel` (in memory only), and
   let `EnsureAdminAsync` / `AddPlayerAsync` / the cloud check skip the pad
   when the current player was unlocked. The ⇄ switch of R7 follows the
   same rule.
4. PINs stay for admins only (P4a). If Andrix wants a PIN for users too,
   that is a separate decision: don't do it here.

**Tests** (`SimpleViewModelTests`, with `FakeDialogService.ChosenAdmin`):
admin with PIN, pad cancelled → `Current` stays null; pad right →
games page; then "Add a game" doesn't ask again; back and forth → asked
again; player without PIN → no dialog.

Commit: `Simple mode: picking a player with a PIN asks it`

## R4 — Sidebar: launcher titles apart, no "Games" title, icons in "Other games"

**Problem.** In `MainWindow.axaml` the launcher title (`LauncherHeader`,
"STEAM", "EPIC GAMES") is inside the template of the **first game** of the
group, so the selection highlight and the spacing glue it to that game.
The `GamesCaption` title on top adds nothing. "Other games" show only the
name, no icon.

**Do.**

1. Remove the `GamesCaption` `TextBlock` (and the `resx` key if nothing
   else uses it; `XamlKeysTests` will tell).
2. Launcher titles become their own rows, not part of a game: for example
   the `ListBox` binds to a list of rows (`SidebarHeader` for a launcher,
   `GameItemViewModel` for a game) with one `DataTemplate` each, and a
   header row is not selectable (`ListBoxItem` with
   `IsEnabled="False"` or `Focusable="False"` via a style on the header
   type). Keyboard up/down skip headers. Look at the canvas board
   **Advanced**: header 11 px bold muted, letter spacing, padding
   `12 6 6`; game rows 40 px high, 26 px icon, red 3 px bar on the left of
   the selected one, background `#3E1816` when selected.
3. `SelectedGame` keeps working as today (the rest of the app binds to
   it).
4. "Other games" (`OtherGames` expander): each entry shows the game's
   icon (`Icon` / `HasIcon`, or the initial in a square like the main
   list) next to the name, 26 px, same row layout as the main list; the
   launcher titles there follow point 2 too.

**Tests.** The list of rows is built in the view model: test that headers
come once per launcher, before their games, and are never `SelectedGame`.
Screenshot `01-main` in `tools/UiScreenshots` shows it.

Commit: `Sidebar: launcher titles on their own row, icons in Other games`

## R5 — Long game names: "…" and a slow scroll

**Problem.** Some names are too long (sidebar, simple mode tiles, "My
games" title...). Andrix wants them cut with **…**, and when the mouse is
on them **or the game is selected**, the full name **scrolls slowly,
forever** (marquee).

**Do.**

1. New control `src/Lss.App/Controls/MarqueeText.cs` (+ style in
   `Styles/Theme.axaml` if needed): shows `Text` on one line with
   `TextTrimming="CharacterEllipsis"`; when `IsActive` is true **and** the
   text doesn't fit, it shows the whole text and moves it left with a
   `TranslateTransform` at about **30 px/s**, then starts again (a gap of
   ~40 px, or the text repeated after a gap so it loops without a jump).
   When `IsActive` turns false it goes back to the "…" version at once.
   Respect the system's "reduce motion" if Avalonia exposes it; otherwise
   keep it slow.
2. `IsActive` = pointer over the control **or** a bound `IsSelected`.
3. Use it for game names in: the sidebar (active when hovered or
   selected), "Other games", the simple mode tile name under the cover
   (hover), the playing card title stays wrapped (it's big: no marquee).
4. No timers per item when not active (many games on a Steam Deck).

**Tests.** Logic that can be tested without rendering (does it need to
scroll for a given text width and box width, the loop offset) goes in a
small helper with a unit test. Check by eye with `tools/UiScreenshots`.

Commit: `Long game names: ellipsis and slow scroll when hovered or selected`

## R6 — Simple mode tiles: badge under the chip, icon when there's no cover

**Do** (`SimpleView.axaml`, `SimpleGameTile` in `SimpleViewModel.cs`):

1. The status badge ("ASK AN ADMIN", "TO SET UP", "PLAYING") goes **under
   the launcher chip, on the left**: a vertical `StackPanel` at the top
   left with the chip, then the badge, 6 px apart. Nothing at the top
   right any more. (This replaces the canvas, where the badge is top
   right.)
2. A tile without a cover (`!HasCover`) shows the **game's icon** in the
   middle (64 px, from the same source as the sidebar icon:
   `GameItemViewModel.Icon`), with the name at the bottom as today. No
   icon either → the initial in a 64 px rounded square, so no tile is
   empty. Add `Icon` / `HasIcon` to `SimpleGameTile` (fill it in
   `BuildGames`).
3. The playing card (`PlayingTile`) and "My games" (48×72 cover) use the
   same fallback.

**Tests.** `SimpleViewModelTests`: a game without cover has the icon of
its item. Screenshot 24 shows both cases.

Commit: `Simple mode: badge under the launcher, icon on tiles without a cover`

## R7 — A Simple / Advanced switch instead of ⚙

**Problem.** The ⚙ of the simple mode opens the advanced mode: a gear
means settings to everyone.

**Do.**

1. Remove the ⚙ button from `SimpleView`.
2. Add a two-option switch **Simple | Advanced** (segmented control: two
   buttons in a pill, the current one filled) in the header of **both**
   modes, at the right: in `SimpleView`'s header and in the advanced
   header of `MainWindow.axaml` (where the button to go to the simple mode
   is today — find it: `SwitchToSimple`, `SimpleModeText` in
   `SettingsViewModel`; remove that one so there's a single way).
3. Simple → Advanced needs an admin as today
   (`MainWindowViewModel.SwitchToAdvancedAsync`, with R3's rule).
   Advanced → Simple needs nothing.
4. Texts: reuse `ModeSimpleTitle` / `ModeAdvancedTitle` if they fit;
   tooltips with the existing `SwitchToAdvanced` / `SwitchToSimple`.
5. Update the canvas boards Main, Games, Advanced only if Andrix asks:
   this file is the reference here.

**Tests.** `MainWindowViewModel` part is already covered by mode tests;
add one for the switch command in each direction (fake admin).

Commit: `Simple/Advanced switch in the header instead of the gear`

## R8 — Two settings windows: the game's and LSS's

**Problem.** `SettingsWindow` / `SettingsViewModel` mix the settings of
the selected game (`AppState`, per game: launch the game, save on exit,
shared preference files, copies to keep) with LSS's own (`AppSettings` in
`lss.ini`: look after playing; players; export all; ignored folders; data
folder; about; simple mode).

**Do.**

1. Split into:
   - **`GameSettingsWindow`** + `GameSettingsViewModel`: everything that
     reads or writes `manager.State` (the game's `stato.ini`), plus the
     game's own links (export this game's configuration if it's there).
     Title "Settings of {0}".
   - **`LssSettingsWindow`** + `LssSettingsViewModel`: `AppSettings`
     (look after playing, mode if it is shown), Players…, Export all
     configurations…, Ignored folders…, data folder, about.
   Keep the existing `resx` keys where they fit; new ones in both files.
2. **Advanced mode:** the header button "Settings" opens LSS settings;
   the game page gets a "Game settings" entry (a ⚙ small button next to
   the game title, or in a ⋯ menu of the game header — pick the one that
   matches the canvas Advanced board's header row).
3. **Simple mode, admins only:**
   - The ⋯ on a game cover opens a small menu: **My games** (everyone),
     **Game settings…** (admin: asks the admin with R3's rule, then opens
     `GameSettingsWindow` for that game). For a user the ⋯ opens "My
     games" directly, as today.
   - **LSS settings** in the simple mode: a "Settings" text button in the
     header next to the R7 switch, admin only (asks the admin first).
4. `SettingsViewModelTests` split the same way; every existing test keeps
   its meaning.

Commit: `Settings split: game settings and LSS settings`

## R9 — JSON settings files: the format and the app-wide files

**Why.** Andrix prefers JSON to `.ini` for LSS's own files.

**Do.**

1. New `src/Lss.Core/JsonFile.cs`: read/write a typed object with
   `System.Text.Json` (in .NET: **no NuGet package**), indented, UTF-8
   without BOM, written to `<file>.tmp` then moved over (same safety as
   `IniFile.Write` and `AppState.Save`). Unknown properties are ignored on
   read; a broken file is logged (Italian) and treated like a missing
   one, as today.
2. Move to JSON, **same folder, same Italian base name**:
   `lss.ini` → `lss.json` (`AppSettings`), `persone.ini` → `persone.json`
   (`People`, PIN hash and salt included), `cartelle-ignorate.ini` →
   `cartelle-ignorate.json` (`IgnoredFolders`), manual games
   `gioco.ini` → `gioco.json` (`ManualGames`).
   Property names: short Italian words like the ini keys today
   (`"Modalita"`, `"Persone"`: an array of objects, not `<id>.Nome` keys).
3. **Migration** in `DataLayoutMigration` (not `LegacyMigration`: this is
   LSS's own data): if the `.json` doesn't exist and the `.ini` does, read
   the `.ini` with `IniFile`, write the `.json`, then rename the `.ini` to
   `.ini.vecchio` (keep it, don't delete: if something goes wrong Andrix
   can recover). Run it at start-up before anything reads these files
   (`App.StartAsync`, next to `DataLayoutMigration.Run`). `LegacyMigration`
   keeps writing what it writes today; the JSON step runs after it.
4. `IniFile` stays: the migration and `LegacyMigration` still read `.ini`.
5. Update `docs/plan/Q-release.md` (Q1: the forgotten-PIN help now talks
   about `persone.json` and its PIN fields).

**Tests.** One per class: round trip in JSON; migration from a real
`.ini` sample (copy one from the existing tests) gives the same values;
`.ini.vecchio` left; second run does nothing; broken JSON → defaults.

Commit: `Settings in JSON: lss, persone, cartelle-ignorate, gioco`

## R10 — JSON for the per-game files

Same rules as R9, for the files in each game's data folder:

| Today | After | Class |
|---|---|---|
| `stato.ini` | `stato.json` | `AppState` |
| `preferite.ini` | `preferite.json` | `SaveManager.Favourites.cs` |
| `posti.ini` (and old `cartella.ini`) | `posti.json` | `GameSpots`, `IgnoredFolders` (per game part) |
| `proposte.ini` | `proposte.json` | `SessionScan` |
| `Info/<Profilo>.ini` (`partita.ini` naming: check `InfoFile`) | `.json` | `SaveManager.Playthroughs.cs` |
| `Backup/<copy>.ini` | `Backup/<copy>.json` | `WriteBackupInfo` / `WithBackupInfo` |

**Not** changed:

- `cambio.ini`, the switch journal of `SaveSet`: it is written in the
  middle of a switch and read after a crash; leave it as it is (its format
  is not a setting).
- The files **inside exported `.zip`** (`posizioni.ini`, the info file):
  zips made by 1.0 and by today's 2.0 must import forever. Write JSON in
  new zips **and** keep reading the `.ini` ones; test both.
- `.lss-config.json` (already JSON).

Migration: per game folder, when a `SaveManager` is created for it
(`RecoverAllGameFolders` time or the constructor), same `.ini.vecchio`
rule. Backup info files: migrate lazily when read, or all at once per
game; pick one and write why in a comment.

**Tests** like R9, plus: an export zip with `.ini` inside still imports;
a new zip has JSON.

Commit: `Settings in JSON: per-game files`

## R11 — Old paths with `\` on Linux

**Problem.** On Linux (Steam Deck) some saved paths show `\` where `/`
belongs: paths saved by older builds (before the fixes in
`SavePathResolver`) or written from rules / configurations made on
Windows. New paths are right; the old ones stay wrong.

**Do.**

1. First **find where** they are: write a test that builds the data of an
   older build (a `posti.ini` / spot with `Saves\Slot1` in a path, an
   ignored folder, a pending suggestion) and shows the wrong value. Look
   at every stored path: game spots (`GameSpots`), ignored folders,
   suggestions (`SessionScan`), the active spot data in `stato`, manual
   games' exe and save folder.
2. One helper `PathText.Normalize(string path)` in `Lss.Core`: on Linux
   turn every `\` into `/` (a real Linux file name can contain `\`, but
   LSS never creates one, and Steam/Proton paths never have it); on
   Windows turn `/` into `\`. Don't touch `*` patterns' meaning.
3. Apply it **once**, in the R9/R10 migration (so the JSON files are
   clean), and also when reading, for files written by hand or imported
   (`.lss-config.json` from another system, R9 JSON written by older 2.0
   betas).
4. Write the change to the log (Italian): which file, how many paths.

**Tests.** `PathTextTests` (both systems: use the separator of the system
the test runs on, like the existing path tests), and a migration test with
backslashes that checks the Linux result only when
`OperatingSystem.IsLinux()`, the way the existing Linux-only tests do.

Commit: `Fix old saved paths with backslashes on Linux`

## R12 — Screenshots, Linux check

1. Update `tools/UiScreenshots`: sidebar (01), simple tiles with icon and
   badge under the chip (24), switch in both headers, the two settings
   windows, the ⋯ menu for admins; regenerate, look at each one next to
   the canvas.
2. `bash scripts/linux-check.sh lss-ci@192.168.1.3` (full, packages
   included).
3. Note in `PROGRESS.md`: what differs from the canvas on purpose (R6
   badge position, R7 switch, R8 windows).

Commit: `Screenshots for phase R`

## R13 — 👤 Check on the Steam Deck and Windows

Andrix: points 1–12 of the table above, one by one, on Windows and on the
Steam Deck (AppImage). Data of 2.0 betas migrated to JSON without losing
anything (players, PINs, favourites, history, spots, ignored folders).
