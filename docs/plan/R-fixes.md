# Phase R — Fixes after the first try of 2.0

**Runs after phase P and before phase Q** (it was added later, so its letter
comes after Q). Requires phases K–P. Branch: `v2`. Rules in
[README.md](README.md).

Done by **Claude Sonnet 5.5** sessions, one task at a time, in order.
Commit trailer:

```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

What Andrix found trying phase P (2026-10-25), and the task for each:

| # | Problem | Task |
|---|---|---|
| 1 | A player with a PIN is not asked for it when picked on "Who's playing?"; PINs should be possible for **every** player | R3 |
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
| — | **Everything that exists only for data of LSS 1.0 (or of builds before this phase) goes in one separate file**, so it can be deleted in one go when 1.0 is gone (Andrix is the only user) | R0, and R9–R11 put their migrations there |

The design canvas (https://claude.ai/artifact/YEykCPUor96ApEYdzMnmRS) stays
the reference for every screen. Where this file says something different
(R6, R7, R8), this file wins.

## Rules for this phase (on top of README.md)

- **Read before writing.** Every task names classes and methods found on
  2026-10-25; check them in the code first. If something is not where this
  file says, or works differently, follow README rule 6: stop, write it in
  `PROGRESS.md` "Notes", don't improvise.
- **Old data never breaks.** Andrix's real data (Windows and Steam Deck)
  was written by 1.0 and by 2.0 development builds. After every task, LSS
  must open that data and show the same players, PINs, favourites,
  playthroughs, history, save spots and ignored folders. R10 adds a test
  that builds such a data folder and checks it.
- **Compatibility code only in `V1Compat`** (from R0 on). Code whose only
  job is to read, convert or tolerate data written by LSS 1.0.x or by
  builds before phase R goes in `src/Lss.Core/V1Compat.cs`, tests in
  `tests/Lss.Tests/V1CompatTests.cs`. The rest of the code calls it from as
  few places as possible, each call marked with the comment
  `// V1Compat: <why>` so `grep V1Compat` finds every one. Control Save
  Manager stays in `LegacyMigration.cs` as today (already a separate
  file).
- Existing tests are not deleted. A test whose rule this phase changes
  on purpose (R3: PIN for users) is changed, and the commit message says
  which and why.

---

## R0 — One file for the 1.0 compatibility

**Goal.** When 1.0 is retired, removing its compatibility must be: delete
`V1Compat.cs` and `V1CompatTests.cs`, delete the lines marked
`// V1Compat`, build, done. Today it is spread over many classes.

**What counts.** Code that would be dead if every user started 2.0 with an
empty data folder. **Not** compatibility: crash recovery (`SaveSet`
journal `cambio.ini`, `RecoverAllGameFolders` and `.vecchio` folders),
safety rules (`SaveFolderSafety`, "don't save when the main folder is
empty" in `SaveManager`), Control Save Manager (`LegacyMigration`).

**Do.**

1. **Inventory first**, written in `PROGRESS.md` Notes before moving
   anything: search the code (`1.0`, `prima della K5`, `beta`,
   `ProprietariOk`, `senza proprietario`, `Formato 1 e 2`, `cartella.ini`,
   `OldFileName`, `Migrate`, `Legacy` outside `LegacyMigration`...). Known
   on 2026-10-25, check each one:
   - `DataLayoutMigration` (whole class: the beta layout
     `dati\IDAccount\AppID`).
   - `AppSettings.Refers`: stored values without `provider/` (before K5).
   - `AppSettings.FirstStart` comment "chi ha già lss.ini (1.0) resta in
     avanzata": the rule itself stays (no settings file → first start),
     only the 1.0 reasoning moves if there is code for it.
   - Playthroughs without an owner: `AppState` `ProprietariOk` and the
     adoption in `SaveManager.Favourites.cs` (the block "Partite fatte prima
     delle persone").
   - Copies without an owner: `SaveManager.GetBackupsWithoutOwner` and
     `HistoryViewModel` legacy copies (`LegacyCopies`, `ConfirmLegacy*`);
     backup kinds read from old folder names (`"Prima_salvataggio"`,
     `"Prima_creazione"` without owner in `WithBackupInfo`).
   - Export zips of format 1 and 2 (no `Provider`, no `posizioni.ini`) in
     the import (`SaveManager.Playthroughs.cs`, `LegacyMigration.ExportedGame`
     — the Control Save Manager part stays in `LegacyMigration`).
   - `GameSpots` `cartella.ini` → `posti.ini` (`OldFileName`, `Migrate`) and
     the note in `IgnoredFolders`.
   - `SaveLocator`: "Un gioco gestito con la userdata (come nella 1.0)":
     decide with the rule above. If it protects how a game is handled
     today, it stays where it is (it is behaviour, not old data); write the
     decision in the Notes.
2. Move each item into `V1Compat` as `internal static` methods with a
   clear Italian comment ("Dati della 1.0: …"). The original class keeps one
   call, marked `// V1Compat: …`. Move the tests that are about old data
   into `V1CompatTests.cs` (same assertions).
3. App part: the History "copies from before" section stays visible only
   when `V1Compat` says there are such copies; keep the UI, but its data
   comes from one `V1Compat` method.
4. Add to `docs/DEVELOPMENT.md` a section **"Compatibility with LSS 1.0"**
   like the Control Save Manager one: what `V1Compat` does, and the
   removal checklist (delete the two files, grep `V1Compat`, delete the
   `resx` keys used only there — list them —, run the tests).
5. **Boundary test** `V1CompatBoundaryTests`: walks `src/` from the
   repository root (find `LSS.slnx` going up from the test's folder) and
   fails if an old file name (`"lss.ini"`, `"persone.ini"`,
   `"preferite.ini"`, `"stato.ini"`, `"posti.ini"`, `"cartella.ini"`,
   `"proposte.ini"`, `"cartelle-ignorate.ini"`, `"gioco.ini"`,
   `"posizioni.ini"` …: take the list from the inventory, and add the R9–R10
   ones when they move) appears outside `V1Compat.cs` and
   `LegacyMigration.cs`. Until R9–R10 are done, the test lists today's
   allowed exceptions explicitly, each with a comment `// fino a R9`
   / `// fino a R10`; R9 and R10 remove them. This keeps future code from
   spreading old formats again.

**Tests.** All existing tests green, moved ones in `V1CompatTests`, the
boundary test.

Commit: `Move the LSS 1.0 compatibility into V1Compat`

## R1 — Renaming a playthrough keeps its automatic copies

**Problem.** `SaveManager.RenameProfile` (`src/Lss.Core/SaveManager.cs`)
moves the playthrough folder, its savestates (`Savestate/<name>`) and its
info, but **not** its automatic copies. A copy in `Backup/` belongs to a
playthrough by name: in the folder name (`NewBackupPath(SaveBackupPrefix +
State.ActiveProfile)` → `Prima_salvataggio_<Profilo_name>_<date>`, the same
for `DeletedBackupPrefix`) and/or in the copy's info file (`<backup>.ini`,
`WriteBackupInfo` / `WithBackupInfo`). After a rename,
`GetBackupsOf(newName)` finds nothing: the history looks empty.

**Do.**

1. Read `GetBackupsOf`, `WithBackupInfo`, `WriteBackupInfo` and the
   prefixes in `SaveManager.Playthroughs.cs` to see exactly what decides a
   copy's owner. Don't guess.
2. In `RenameProfileCore`, after the savestates are moved: every copy in
   `Backup/` that belongs to the old name (same rule as `GetBackupsOf`)
   now belongs to the new name. Prefer writing the owner in the copy's info
   file over renaming folders; rename a folder only if the owner can come
   from the folder name alone. A copy without an info file gets one
   (written with `WriteBackupInfo`), so it doesn't depend on its folder
   name any more.
3. Copies of **deleted** playthroughs (`BackupKind.Delete`) with the old
   name are **not** touched: they are another playthrough that had that
   name.
4. A failure on one copy doesn't stop the rename: log it (Italian) and add
   it to the result like `SavestatesNotMoved` does (new key
   `BackupsNotMoved`, both `resx`).
5. Case-only renames (`caseOnly`) work too. Renaming the active
   playthrough works too (`State.ActiveProfile`).

**Tests** (`RenameKeepsBackupsTests.cs`): two copies (save, activate),
rename → `GetBackupsOf(new)` has both, `GetBackupsOf(old)` none; a deleted
playthrough with the old name keeps its copy; case-only rename; rename of
the active playthrough; a read-only info file → rename succeeds with the
warning.

Commit: `Renaming a playthrough keeps its automatic copies`

## R2 — "Add a game": cancel means cancel

**Problem.** `MainWindowViewModel.AddGameAsync` asks the program
(`PickFileAsync`) and then **always** asks the name, even when the file
choice was cancelled (`exe is null`).

**Do.** If `PickFileAsync` returns `null`, stop: nothing is added and no
other window opens. A game can no longer be added without a program;
that's what Andrix asked. The simple mode's "Add a game" tile uses the same
method (`addGame`): check it.

**Test.** Move the steps into a small class taking `IDialogService` (for
example `AddGameFlow`, like `SimplePlayFlow`) and test: program choice
cancelled → no prompt (`FakeDialogService.Calls`), no game in
`ManualGames`; name cancelled → no game.

Commit: `Add a game: cancelling the program choice stops`

## R3 — PIN for every player, asked when the player is picked

**Decided with Andrix (2026-10-25):** a PIN is possible for **every**
player, not only admins. It is still optional.

**Do.**

1. **Core (`People`).** `SetPin` accepts any player (remove the admin
   check). `SetAdmin(id, false)` **keeps** the PIN (today it removes it:
   comment "Diventare utente toglie il PIN"). `HasPin` no longer depends on
   `IsAdmin`. Update `PinTests` and any test with the old rule, saying so in
   the commit message.
2. **Wrong tries per player, not per window.** Today `PinPadViewModel`
   keeps `wrongTries` and `waitUntil` per instance: closing and reopening
   the window resets them, so the 30 s wait after 5 wrong PINs can be
   skipped. Move them to one place for the whole app run (for example a
   `PinGuard` class in `Lss.Core`, one instance shared through `People` or
   passed in), keyed by player id, in memory only (reset when LSS
   restarts, as planned in P4a). Test: 5 wrong tries, new pad for the same
   player → still waiting; another player → not waiting.
3. **One PIN dialog for anyone.** Add
   `IDialogService.CheckPinAsync(People people, Person person)`: `true` at
   once if the player has no PIN; otherwise the pad for that player only
   (reuse `AdminPickerWindow` in its `OnlyPin` form, or split the pad into
   its own `PinWindow` — pick one, no duplicate pad XAML). `false` if
   cancelled. `ChooseAdminAsync` uses it for the admin's PIN.
4. **"Who's playing?"** (`SimpleView` `Player_Click` →
   `SimpleViewModel.SelectPlayer`): new `SelectPlayerAsync(Person)` that
   calls `CheckPinAsync` first; wrong PIN or cancel → stay on "Who's
   playing?". Keep `SelectPlayer` for code that has already checked.
5. **Unlocked while selected.** Once a player typed their PIN, the
   actions that need **that** player's PIN (an admin's admin actions) don't
   ask it again while that player stays selected; going back to "Who's
   playing?" locks again. In memory only, in `SimpleViewModel`.
   `AdminPickerViewModel.WithoutWindow` / `KnownAdmin` get this flag (or
   the dialog service gets "already unlocked") so `EnsureAdminAsync`,
   `AddPlayerAsync`, the cloud check and R7/R8 follow it.
6. **Players window.** The PIN column shows **Set PIN… / Change PIN…** for
   every row, and "Remove PIN" in every ⋯ with a PIN. Changing asks the
   current PIN (as today); removing doesn't (an admin is already needed to
   open the window: that is the way out of a forgotten PIN).
7. **Texts.** `PinSetHint` today talks about admin rights: new text for
   everyone, for example (it) "Facoltativo. Con un PIN, solo tu puoi
   giocare con il tuo nome; se sei admin, anche i permessi di admin. Se lo
   dimentichi, un admin può toglierlo." and the English one. The admin
   picker's "with PIN / no PIN" stays.
8. Update `docs/plan/Q-release.md` Q1: forgotten PIN → an admin removes it
   in Players; the only admin forgot theirs → edit the players file (from
   R9 `persone.json`) with LSS closed: say which field.

**Tests.** `SimpleViewModelTests`: player with PIN, pad cancelled →
`Current` null; right → games page; then "Add a game" as that admin
doesn't ask again; back to "Who's playing?" → asked again; player without
PIN → no dialog. `PlayersViewModelTests`: a user gets a PIN; making an
admin a user keeps it. `PinGuardTests` as in point 2.

Commit: `PIN for every player, asked when picked; wrong tries per player`

## R4 — Sidebar: launcher titles apart, no "Games" title, icons in "Other games"

**Problem.** In `MainWindow.axaml` the launcher title (`LauncherHeader`,
"STEAM", "EPIC GAMES") is inside the template of the **first game** of the
group, so the selection highlight and the spacing glue it to that game.
The `GamesCaption` title on top adds nothing. "Other games" show only the
name, no icon.

**Do.**

1. Remove the `GamesCaption` `TextBlock`, and the `resx` key if nothing else
   uses it (`XamlKeysTests` / `LocalizationTests` tell).
2. Launcher titles become rows of their own: the `ListBox` binds to a
   list of rows (`SidebarHeader` for a launcher, `GameItemViewModel` for a
   game), one `DataTemplate` each. The `ListBox` `SelectedItem` binds to a
   new `SelectedRow` (`object?`) that sets `SelectedGame` when it is a game
   and **refuses** a header (puts the previous game back). Header
   `ListBoxItem`s are not focusable and not hit-testable for selection
   (style on the header type), so arrow keys and clicks skip them.
   Everything else keeps binding to `SelectedGame` as today.
3. Look of the canvas board **Advanced**: header 11 px bold muted,
   letter spacing 0.6, padding `12 6 6`; game rows 40 px, 26 px icon (or
   the initial in a 26 px rounded square), 3 px red bar on the left of the
   selected one, selected background `#3E1816`.
4. "Other games" (`OtherGames` expander): each entry shows the icon (or the
   initial) next to the name, 26 px, like the main list; launcher titles
   there as in point 2 (not selectable, they are not a `ListBox`: just
   separate rows).
5. `ShowSidebar` (hidden with one game) keeps working.

**Tests.** The rows are built in the view model: headers once per
launcher, before their games, in the same order as today; `SelectedRow` =
header → `SelectedGame` unchanged. Screenshot `01-main`.

Commit: `Sidebar: launcher titles on their own row, icons in Other games`

## R5 — Long game names: "…" and a slow scroll

**Problem.** Some names are too long. Andrix wants them cut with **…**;
when the mouse is on them **or the game is selected**, the whole name
**scrolls slowly, forever** (marquee).

**Do.**

1. New control `src/Lss.App/Controls/MarqueeText.cs`: one line, `Text`,
   `FontSize`, `FontWeight`, `Foreground`, and `IsActive`. Not active, or
   active but the text fits → normal `TextBlock` with
   `TextTrimming="CharacterEllipsis"`. Active and too long → the text moves
   left at about **30 px/s** with a `TranslateTransform`, followed by a
   ~40 px gap and the text again, so the loop has no jump. Clip to the
   control's bounds. Back to "…" at once when not active.
2. `IsActive` = pointer over the control **or** a bound selection (the
   template binds it to `ListBoxItem.IsSelected`).
3. Animation runs only while active and visible (no timer or animation
   per item otherwise: a Steam Deck can list hundreds of games). Stop it on
   detach from the visual tree.
4. Use it for game names in: the sidebar (R4), "Other games", the simple
   mode tile name under the cover (hover only), the advanced game title
   if it can overflow. Big titles that wrap (playing card, dialog titles)
   stay wrapped.
5. Pure logic (needs scrolling? loop length and offset at time *t*) in a
   small helper with unit tests; the rendering is checked by eye.

Commit: `Long game names: ellipsis and slow scroll when hovered or selected`

## R6 — Simple mode tiles: badge under the chip, icon when there's no cover

**Do** (`SimpleView.axaml`, `SimpleGameTile` in `SimpleViewModel.cs`):

1. The status badge ("ASK AN ADMIN", "TO SET UP", "PLAYING") goes **under
   the launcher chip, on the left**: a vertical `StackPanel` at the top
   left with the chip, then the badge, 6 px apart. Nothing top right any
   more. (Replaces the canvas, where the badge is top right.)
2. A tile without a cover (`!HasCover`) shows the **game's icon** in the
   middle (64 px), with the name at the bottom as today. No icon → the
   initial in a 64 px rounded square. No tile is empty.
3. **Icons arrive later**: `GameItemViewModel.Icon` is loaded in the
   background (`LoadIconAsync`). `SimpleGameTile` is a record built once,
   so it must not copy the icon: expose the item's icon through a binding
   to `Item.Icon` / `Item.HasIcon` (or make the tile observe
   `PropertyChanged`), so the tile updates when the icon arrives. Test it.
4. The playing card (`PlayingTile`) and "My games" (48×72 cover) use the
   same fallback.

**Tests.** A game without cover shows its item's icon, also when the icon
arrives after the tile was built.

Commit: `Simple mode: badge under the launcher, icon on tiles without a cover`

## R7 — A Simple / Advanced switch instead of ⚙

**Do.**

1. Remove the ⚙ button from `SimpleView` (and `ToAdvancedCommand` if
   nothing else uses it).
2. Add a two-option switch **Simple | Advanced** (two buttons in a pill,
   the current one filled with the accent) at the right of the header in
   **both** modes: in `SimpleView`'s header and in the advanced header of
   `MainWindow.axaml`. Remove every other way to change mode that exists
   today (the "Simple mode" button: find `SwitchToSimple` /
   `SimpleModeText` in `SettingsViewModel` and the advanced header), so
   there is only the switch. The first-run choice (`ModeChoiceWindow`)
   stays.
3. Simple → Advanced needs an admin as today
   (`MainWindowViewModel.SwitchToAdvancedAsync`, with R3's rules).
   Advanced → Simple needs nothing. A refused or cancelled switch leaves
   the switch on the current mode.
4. Disabled while a game is being played from the simple mode
   (`IsPlaying`) and while an operation runs.
5. Texts: `ModeSimpleTitle` / `ModeAdvancedTitle`; tooltips
   `SwitchToAdvanced` / `SwitchToSimple`.

**Tests.** The switch command both ways (fake admin: allowed, refused);
disabled while playing.

Commit: `Simple/Advanced switch in the header instead of the gear`

## R8 — Two settings windows: the game's and LSS's

**Decided with Andrix:** LSS settings are reachable in the simple mode by
**admins only**; game settings too, from the game's ⋯, admins only.

**Problem.** `SettingsWindow` / `SettingsViewModel` mix the selected
game's settings (`manager.State`, per game: launch the game, save on exit,
shared preference files, copies to keep) with LSS's own (`AppSettings`:
look after playing; Players; export all; ignored folders; data folder;
about).

**Do.**

1. Split into:
   - **`GameSettingsWindow`** + `GameSettingsViewModel`: what reads or
     writes `manager.State`, the game's ignored folders (the per-game part
     of `IgnoredFolders`), and the game's export link if it's there. Title
     "Settings of {0}" (new key).
   - **`LssSettingsWindow`** + `LssSettingsViewModel`: `AppSettings` (look
     after playing), Players…, Export all configurations…, the ignored
     folders for every game, data folder, about. No mode button (R7).
   Keep existing `resx` keys where they fit; new ones in both files.
2. **Advanced mode:** the header "Settings" button opens LSS settings; the
   game page gets "Game settings" (a small button in the game's title row,
   as on the canvas board Advanced).
3. **Simple mode:**
   - The ⋯ on a game cover opens a small menu: **My games** (everyone) and
     **Game settings…** (shown to everyone, asks an admin with R3's rules,
     then opens `GameSettingsWindow` for that game). If the game isn't
     ready (no saves found), only "Game settings…" makes sense: show the ⋯
     for those too.
   - A **"Settings"** text button in the header next to the R7 switch:
     asks an admin, then opens `LssSettingsWindow`.
4. Split `SettingsViewModelTests` the same way; every existing assertion
   keeps its meaning.

Commit: `Settings split: game settings and LSS settings`

## R9 — JSON settings: the format and the app-wide files

**Do.**

1. New `src/Lss.Core/JsonFile.cs`: read/write a typed object with
   `System.Text.Json` (part of .NET: **no NuGet package**), indented,
   UTF-8 without BOM, written to `<file>.tmp` then moved over the file
   (same safety as `IniFile.Write`). Unknown properties ignored on read; a
   missing file → `null`; a broken file → logged (Italian), the broken file
   kept as `<file>.rotto`, `null` returned (callers use defaults as today
   with a broken `.ini`).
2. These files become JSON, **same folder, same Italian base name**:
   `lss.ini` → `lss.json` (`AppSettings`), `persone.ini` →
   `persone.json` (`People`, PIN salt and hash included),
   `cartelle-ignorate.ini` → `cartelle-ignorate.json` (`IgnoredFolders`),
   manual games `gioco.ini` → `gioco.json` (`ManualGames`).
   Property names short Italian words, like the ini keys today; lists as
   JSON arrays (players: an array of objects in order, not `<id>.Nome`
   keys).
3. The normal classes read and write **only JSON**. Reading the `.ini`
   and converting is in **`V1Compat`**: `V1Compat.ConvertAppFiles(root)`.
4. **Conversion rules** (also used by R10):
   - `.json` exists → the `.ini` is ignored (if it's still there, it is
     renamed as below).
   - Only `.ini` → read it (`IniFile` / the class's old parser, now in
     `V1Compat`), write the `.json` (tmp + move), then rename the `.ini` to
     **`<name>.ini.precedente`**. Never delete it. Never use `.vecchio`
     (that suffix means crash recovery for `SaveSet` folders).
   - Converting twice changes nothing. A crash at any point leaves either
     the `.ini` or the `.json` usable, and the next start finishes.
   - Each converted file is logged (Italian).
5. **Order at start-up** (`App.StartAsync`): `LegacyMigration.Run` (it may
   still write `lss.ini` from Control Save Manager's `account.txt`) →
   `V1Compat` layout (the old `DataLayoutMigration`, moved in R0) →
   `V1Compat.ConvertAppFiles` → then `AppSettings.Load` and the rest.
   `AppSettings.FirstStart` = no `lss.json` **after** the conversion, so a
   1.0 user is not asked the mode again.
6. `IniFile` stays (`cambio.ini` journal, `V1Compat`, zips of R10).
7. Boundary test (R0): remove the `// fino a R9` exceptions.

**Tests** (`JsonFileTests`, `V1CompatTests`, the classes' own tests):
round trip; conversion of a real `.ini` sample of each file gives the same
values (players with PIN and order, last account, mode, look after playing,
ignored folders, manual game with exe); `.ini.precedente` left; second run
does nothing; `.json` and `.ini` both there → JSON wins; broken JSON →
defaults and `.rotto`.

Commit: `Settings in JSON: lss, persone, cartelle-ignorate, gioco`

## R10 — JSON for the per-game files

Same rules as R9, for the files of each game's data folder:

| Today | After | Class |
|---|---|---|
| `stato.ini` (own parser in `AppState`, not `IniFile`) | `stato.json` | `AppState` |
| `preferite.ini` | `preferite.json` | `SaveManager.Favourites.cs` |
| `posti.ini` (and older `cartella.ini`) | `posti.json` | `GameSpots`, per-game part of `IgnoredFolders` |
| `proposte.ini` | `proposte.json` | `SessionScan` |
| the playthrough info files (`InfoFile(...)`: check name and folder) | `.json` | `SaveManager.Playthroughs.cs` |
| `Backup/<copy>.ini` | `Backup/<copy>.json` | `WriteBackupInfo` / `WithBackupInfo` |

**Not changed:**

- `cambio.ini`, the `SaveSet` switch journal: written in the middle of a
  switch and read after a crash. Not a setting, not touched.
- **Files inside export `.zip`s** (`posizioni.ini`, the zip's info file):
  new zips are written with JSON; zips made by 1.0 or by builds before R10
  must still import, and their reading is in `V1Compat`. Test both.
- `.lss-config.json` (already JSON).

**When.** One pass at start-up, right after `ConvertAppFiles`:
`V1Compat.ConvertGameFiles(root)` walks every game data folder of the
known layout (`<root>/<provider>/<account>/<game>/`, check the real
layout in `AppData` / `SaveManager` before writing it) and converts all of
the above, playthrough info and backup info files included. One place,
same rules, nothing converted lazily.

**Upgrade test** (`V1UpgradeTests`): build in a `TestFolder` a complete
data folder **as 1.0 and the current 2.0 builds write it** (use the
existing helpers and real `.ini` contents from today's tests): two
players with a PIN, favourites, two games with playthroughs, savestates,
automatic copies with and without owner, a deleted playthrough, extra
spots, ignored folders, a pending suggestion, a manual game. Run the whole
start-up sequence (R9 point 5), then open each game with `SaveManager` and
check that everything is there and equal. Then run it again: nothing
changes. This test is the safety net of R9–R11: it stays until 1.0
compatibility is removed (it is in `V1Compat` scope).

Boundary test: remove the `// fino a R10` exceptions.

Commit: `Settings in JSON: per-game files`

## R11 — Old paths with `\` on Linux

**Problem.** On Linux (Steam Deck) some saved paths show `\` where `/`
belongs: written by older builds or coming from rules or configurations
made on Windows. New paths are right; old ones stay wrong.

**Do.**

1. **Find where first**: with a test, build data of an older build with
   `\` in stored paths and see which of these keep it: game spots
   (`GameSpots`), ignored folders, suggestions (`SessionScan`), what
   `stato` stores about spots, manual games' exe and save folder,
   imported `.lss-config.json`. Write the list in the Notes.
2. Helper `PathText.Normalize(string path)` in `Lss.Core`: on Linux every
   `\` becomes `/`; on Windows every `/` becomes `\`. Only for **folder and
   file paths**. Spot patterns (`*.sav`, sub-paths) — read how
   `Glob` / `SaveRule` / `ToLocal` use separators first, and normalise them
   only if those places compare them with the system separator.
3. Old data: in the R9/R10 conversion (`V1Compat`), so the JSON files are
   clean; logged with the file and the number of paths fixed.
4. New data from elsewhere (an imported `.lss-config.json` made on
   Windows): `Normalize` on import, in the normal code (that is not 1.0
   compatibility).

**Tests.** `PathTextTests` (expectations by `OperatingSystem.IsLinux()`,
like the existing Linux-only tests); a conversion test with `\` in every
place of point 1; an import test of a config with `\` paths on Linux.

Commit: `Fix old saved paths with backslashes on Linux`

## R12 — Screenshots, docs, Linux check

1. `tools/UiScreenshots`: sidebar (01), simple tiles with icon and badge
   under the chip (24), switch in both headers, both settings windows, the
   ⋯ menu, the PIN pad on "Who's playing?". Regenerate and look at each
   one next to the canvas.
2. `docs/DEVELOPMENT.md`: the JSON files (names, where), `V1Compat`
   section (R0) up to date.
3. `bash scripts/linux-check.sh lss-ci@192.168.1.3` (full, packages
   included).
4. Notes in `PROGRESS.md`: what differs from the canvas on purpose (R6
   badge, R7 switch, R8 windows).

Commit: `Screenshots and docs for phase R`

## R13 — 👤 Check on Windows and the Steam Deck

Andrix, **first copying the whole LSS data folder somewhere safe** on each
machine (Windows `%LOCALAPPDATA%\LSS`, Linux `~/.local/share/LSS`):

- Points 1–12 of the table above, one by one.
- Everything still there after the JSON conversion: players and PINs,
  favourites, playthroughs and history, save spots, ignored folders,
  manual games; `.ini.precedente` files next to the new `.json`.
- **After R9/R10, don't open LSS 1.0 on the same data**: it no longer finds
  its `.ini` files and would start as on a new computer.
