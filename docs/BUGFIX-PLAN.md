# Bug fix plan after 2.0.0

A self-contained plan: everything a session needs is in this file (it does
not depend on the old `docs/plan/`, which is being removed). Done by
**Claude Sonnet 5.5** sessions, **one task per session**, in order. The
checklist and the notes are at the end of this file.

## Starting a session

Prompt to paste as the first message of every session:

> Work in the LSS repository, branch `fix-bug-hunt` (the folder `git
> worktree list` shows with `[fix-bug-hunt]`). Read `docs/BUGFIX-PLAN.md`
> completely. Do the first task of its checklist that is not ticked,
> exactly as written, following every rule of the file: first the test that
> fails, then the fix, build and tests green, commit on `fix-bug-hunt`,
> tick the task in the checklist, never push. If something doesn't match
> the plan, stop and write it in the Notes at the end of the file. When the
> task is committed, stop and tell me what you did.

Before changing anything: `git status` clean, `git branch --show-current`
is `fix-bug-hunt`, `git log -1` matches the last commit named in the
checklist (or `ef0dbdb` / this plan's commit for S1), then build and test.
If the tests already fail, stop and write it in the Notes.

What a code review of `main` at `ef0dbdb` found (2026-10-30). Problems 2
and 4 were reproduced with a test; the others come from reading the code.

| # | Problem | Severity | Task |
|---|---|---|---|
| 1 | Simple mode: when the game closes, the play time is counted twice and the progress may be saved twice **at the same time** (the advanced-mode game monitor runs too) | high | S1 |
| 2 | With several save spots: spot 1 empty and only an extra spot changed → `SaveProgress` refuses, so switching playthroughs is blocked, in both modes | high | S2 |
| 3 | Renaming the playthrough in the game while the game is running: play time goes to an orphan info file and the save on exit is skipped | high | S3 |
| 4 | A new playthrough with the name of a deleted one inherits its automatic copies (history) | medium | S4 |
| 5 | The delete confirmation says deleted playthroughs are kept "for a while / only the last N copies", but they are never pruned | medium | S5 |
| 6 | Exporting the playthrough in the game exports the last saved copy, without the unsaved progress, with no warning (also while the game runs) | medium | S6 |
| 7 | A failed import can leave a half-imported playthrough with no owner | medium | S7 |
| 8 | Simple mode: preparing and saving copy files on the UI thread (the window freezes) | medium | S8 |
| 9 | Simple mode: a game that takes more than 2 minutes to start counts as finished | medium | S9 |
| 10 | Backup and point folder names use the current culture's calendar: with a non-Gregorian one they are not found | medium | S10 |
| 11 | Simple mode, "Add a player": an invalid or taken name does nothing, without a message | low | S11 |
| 12 | Simple mode: when nothing was saved, the play time is lost | low | S11 |
| 13 | `DeleteProfileCore` compares with the active playthrough case-sensitively | low | S11 |
| 14 | A missing spot-1 folder makes the backup before a switch fail | to check | S12 |

**Not in this plan** (Andrix decides first): creating a playthrough from
the game's saves (`CreateProfileCore` with `copyCurrentSaves`) makes a
`Prima_creazione` backup although the game is not touched; it takes one of
the kept copies. Removing it changes the history, so it waits.

## Rules (mandatory)

**Working**

- **One task at a time, in order.** Take the first unticked task of the
  checklist and do exactly what it says. Don't start the next one until the
  current one is committed and ticked.
- **Branch `fix-bug-hunt`**, in the folder `git worktree list` shows with
  `[fix-bug-hunt]`. Never commit on `main`. **Never push**, never create
  tags or releases: Andrix does that.
- **Before every commit**, from the repository root:

  ```bash
  dotnet build LSS.slnx -c Release
  dotnet test LSS.slnx -c Release
  ```

  Both must end with `0 Error(s)` and no failed tests. Don't commit with
  warnings you introduced.
- **Commit message**: the one given in the task, in English, then an empty
  line and:

  ```
  Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
  ```

- **Then tick the task** in the checklist at the end of this file
  (`- [ ]` → `- [x]`) with the short commit hash and the test count, in the
  same commit or in a separate `Progress: <task id>` commit; be consistent.
- **If something doesn't match the plan** (a class, method or line is not
  where this file says, or works differently; a test still fails after two
  tries): **stop**, don't improvise a large change. Write what you found in
  the Notes at the end of this file, commit that, and ask Andrix.
- **Tasks marked 👤** are Andrix's. Skip them and leave them unticked.
- **Never touch the real Steam folder or the real app data.** Tests work
  only in temporary folders (`TestFolder` in `tests/Lss.Tests`). Don't run
  the app against real saves.
- **Linux check** only where a task says so: `bash scripts/linux-check.sh
  lss-ci@192.168.1.3 --tests-only`. That host is a machine on Andrix's
  network (SSH key already set up, Docker only, no root): use it only
  through that script. If it can't be reached, write it in the task's line
  of the checklist and go on.
- **Read before writing.** Every task names classes, methods and line
  numbers found on 2026-10-30 at `ef0dbdb`. Lines move: find the method by
  name. Read the file first and write code like the code around it.

**Code conventions**

- Code comments, XML doc comments (`/// <summary>`) and log messages
  (`Log.Write`) **in Italian**, short, like the existing ones.
- User-visible texts **only** in `src/Lss.Core/Strings.resx` (English) and
  `Strings.it.resx` (Italian), same key and same `{0}`, `{1}`… in both.
  Never hard-code a user text in C# or XAML. `LocalizationTests` checks it.
- Names on disk stay as they are (`Profili`, `Info`, `Backup`, `Savestate`,
  `Profilo_<name>`, `Prima_di_…`, `.nuovo`, `.vecchio`, `.rinomina`,
  `salvataggi`, `punti`, `stato.json`, `cambio.ini`…).
- Code that exists only to read data of LSS 1.0 goes in
  `src/Lss.Core/V1Compat.cs`, called from lines marked `// V1Compat: <why>`.
  None of these tasks should need it.
- GitHub documentation (`CHANGELOG.md`, `docs/`) in English.

**For these fixes**

- **Test first.** Every task starts by writing the test that shows the
  problem and checking that it **fails** (or, for S12, that shows what
  happens). Then fix, then all tests green. Say in the commit message which
  new tests there are.
- Existing tests are not deleted. A test whose rule a task changes on
  purpose is changed, and the commit message says which and why.
- No new dependencies, no data format changes, no renamed files on disk.
  Old data must still open (`V1UpgradeTests` must stay green).
- User texts only in both `resx` files (English and Italian), same key;
  `LocalizationTests` checks them.

---

## S1 — Simple mode: one session, one save

**Problem.** `MainWindowViewModel.ManagerOrNull` calls `GameFor(item)`,
which creates a `GameViewModel`; `GameMonitor.CheckAsync`
(`src/Lss.App/Services/GameMonitor.cs`) watches every `GameViewModel`, in
both modes. When a game started from the simple mode closes:

- the monitor calls `GameViewModel.OnGameState` → `OnGameClosedAsync`,
  which calls `Manager.AddPlayTime` and, with `State.SaveOnGameExit`
  (default `true`), `Manager.SaveProgress(onGameExit: true)` inside
  `RunBusyAsync` (on a worker thread);
- `SimplePlayFlow.FinishAsync` calls `SaveProgress(onGameExit: true)` and
  `AddPlayTime` again;
- `OnGameState` also calls `StartLookAfterSession`, and
  `RunSimpleAsync` calls `ScanAfterSession`: two scans.

Result: play time always doubled; often two `SaveProgress` on the same
folders at once (same `.nuovo` / `.vecchio` names in `FileOps.ReplaceFolder`).

**Do.**

1. `GameViewModel`: new property `internal bool SimpleSessionActive { get; set; }`
   (Italian doc comment: the session belongs to the simple mode, which
   saves and counts the time itself).
2. `GameViewModel.OnGameState`, case `SessionChange.Ended`: when
   `SimpleSessionActive` is true, only `Refresh()` (no `OnGameClosedAsync`,
   no `StartLookAfterSession`). `Started` and the default case stay as they
   are.
3. `MainWindowViewModel.RunSimpleAsync`: set
   `GameFor(item).SimpleSessionActive = true` **before**
   `simpleFlow.StartAsync` and back to `false` in a `finally` that covers
   everything up to after `simple.EndPlaying()` (also when `StartAsync`
   returns null or throws).
4. `GameViewModel.CloseSession` (app closed while playing) stays as it is:
   in that case `FinishAsync` never runs, so the monitor's time is the only
   one.

**Tests** (`GameViewModelTests.cs`, which already builds a `GameViewModel`
with `FakeDialogService`): with `SimpleSessionActive = true`,
`OnGameState(new SessionEvent(SessionChange.Ended, "Profilo_X", start, end))`
→ `GetInfo("Profilo_X").PlayTime` unchanged and no new backup in
`GetBackups()`; with `false` (and `SaveOnGameExit = true`, unsaved changes)
→ time added and one `BackupKind.SaveOnExit` copy, as today.

Commit: `Simple mode: the session end is handled once`

## S2 — Several spots: an empty spot 1 must not block switching

**Problem.** `SaveManager.SaveProgressCore` (`src/Lss.Core/SaveManager.cs`,
around line 980) refuses with `GameSavesEmptyWouldClear` when
`GameSavesAreEmpty() || saves.PrimaryIsEmpty()`. `HasUnsavedProgress`
compares every spot. With spot 1 empty and an extra spot changed (a new,
empty playthrough where the game wrote only its settings),
`HasUnsavedProgress()` is true but `SaveProgress()` fails, so
`GameViewModel.PlayAsync` (around line 513) and `SimplePlay.PrepareStart`
stop with "Nothing changed…". In the simple mode nobody can play that game
any more. Reproduced:

```csharp
// two whole-folder spots; AdoptGameSaves("Anna"); CreateProfile("Bea", empty); ActivateProfile("Profilo_Bea");
// write only second/settings.ini
Assert.True(manager.HasUnsavedProgress());
Assert.Equal(Strings.GameSavesEmptyWouldClear, manager.SaveProgress().Message); // blocked
```

**Rule.** The refusal exists so that saving never empties a playthrough's
saves. So: refuse only when the save **would remove save files from the
playthrough's spot 1**, i.e. the game's spot 1 has none **and** the
playthrough's spot 1 has some. When the playthrough's spot 1 is empty too,
saving loses nothing: allow it.

**Do.**

1. `SaveSet`: new `public bool SnapshotPrimaryIsEmpty(string snapshot)`:
   like `PrimaryIsEmpty`, but on the snapshot (whole folder:
   `FileOps.HasNoSaveFiles(snapshot)`, which already skips
   `.lss-posizioni`; pattern: `FileOps.MatchedFiles(snapshot, pattern,
   recursive).Count == 0`).
2. `SaveProgressCore`: the check becomes
   `!allowEmptySaves && (GameSavesAreEmpty() || saves.PrimaryIsEmpty()) && !saves.SnapshotPrimaryIsEmpty(profile)`
   (compute `profile` before it). Update the Italian comment above it.
3. Advanced mode only, the case that is still refused (the playthrough
   had saves, the game's spot 1 is now empty): in `GameViewModel.PlayAsync`,
   when the save fails with `GameSavesEmptyWouldClear`, don't stop at the
   error. Ask with `dialogs.ConfirmAsync` (new keys `SwitchEmptyHeading`,
   `SwitchEmptyText`: the game's main save folder is empty; switching
   keeps the playthrough as it was saved last, and the game's current files
   go to an automatic copy; verb `SwitchEmptyVerb`). Yes → go on with
   `ActivateProfile` (it makes the backup); No → stop. The save runs inside
   `RunBusyAsync`: split it so the question is asked on the UI thread,
   between the save and the activation. The simple mode keeps the error
   (a child must not decide this).

**Tests** (`SaveManagerSpotsTests.cs` or a new `EmptyPrimaryTests.cs`):
the reproduction above → `SaveProgress()` succeeds and the playthrough has
the new `settings.ini`; a playthrough **with** a file in spot 1 and the
game's spot 1 emptied → still `GameSavesEmptyWouldClear`; one spot only,
game empty, playthrough with saves → still refused (old behaviour).
`GameViewModelTests`: the refused case with `FakeDialogService` answering
yes → the other playthrough is active; answering no → nothing changed.

Commit: `An empty main save folder no longer blocks switching when nothing would be lost`

## S3 — No rename of the playthrough in the game while the game runs

**Problem.** `PlaythroughCardView.axaml` line 136: the "Rename" menu item
has no `IsEnabled`. `SessionTracker` keeps the playthrough name taken when
the game started (`SessionTracker.Update`). After a rename during the
session, `GameViewModel.OnGameClosedAsync` calls `AddPlayTime(e.Profile)`
with the old name (an orphan `Info/Profilo_<old>.json` is written) and
skips the save on exit because `Manager.State.ActiveProfile != profile`.

**Do.**

1. `PlaythroughViewModel`: `public bool CanRename => !game.IsBusy && !(IsActive && game.GameRunning);`
   notified in `NotifyStateChanged`. Bind the "Rename" item's `IsEnabled`
   to it.
2. `GameViewModel.RenameAsync`: return at once when `IsBusy`, or when
   `GameRunning` and the profile is `Manager.State.ActiveProfile`.
3. `SaveManager.RenameProfileCore` (defence for every caller): when the
   profile is the active one (compare with
   `StringComparison.OrdinalIgnoreCase`) and `IsGameRunning()`, fail with
   `Loc.F(Strings.CloseGameFirst, Game.Name)`.

**Tests.** `SaveManager`: can't be faked running easily; if there is no
way to fake `IsGameRunning` (look at how existing tests do it, e.g. a
provider stub), test only the view model: `CanRename` false for the active
card while `GameRunning`, true for the others. Write in the commit if the
core check has no test.

Commit: `The playthrough in the game can't be renamed while the game runs`

## S4 — Deleted playthroughs take their automatic copies with them

**Problem.** A copy belongs to a playthrough by name only
(`SaveManager.Playthroughs.cs`, `GetBackupsOf`: `b.Owner == profileName`).
`DeleteProfileCore` (`SaveManager.cs`) moves the playthrough to
`Backup/Eliminato_<name>_<date>` but leaves its copies owned by
`Profilo_<name>`. A new playthrough with the same name shows them in its
history and can go back to them. Reproduced:

```csharp
// AdoptGameSaves("Mario"); change; SaveProgress(); CreateProfile("Altro", empty); ActivateProfile("Profilo_Altro");
// DeleteProfile("Profilo_Mario"); CreateProfile("Mario", empty)
Assert.NotEmpty(manager.GetBackupsOf("Profilo_Mario")); // the old Mario's copies
```

**Do.**

1. On delete, after the move succeeded: give every copy of the playthrough
   (same rule as `GetBackupsOf`, so not the deleted-playthrough backups)
   an owner no playthrough can have: the **folder name of the deleted
   backup** (`Path.GetFileName(backup)`, e.g.
   `Eliminato_Profilo_Mario_2026-10-30_…`). Playthrough folders always
   start with `Profilo_`, so there is no clash. Reuse
   `MoveBackupOwner(oldName, newName)` (it writes with `WriteBackupInfo`
   and returns the warning text). **Do not use `""`**: an empty owner means
   "copy of LSS 1.0" (`V1Compat.BackupsWithoutOwner`) and would show the
   copies in every history.
2. A failure there doesn't undo the delete: add the text to the result
   like `ProfileDeletedSavestatesNot` does, and log it in Italian.
3. `RecoverDeletedProfileCore`: after the move, give the copies owned by
   `Path.GetFileName(deleted.Path)` to the recovered `profileName` (which
   can be "Mario 2", from `UniqueName`).
4. `PruneBackups` doesn't change: those copies are still counted and
   pruned like any other.

**Tests** (`DeletedCopiesTests.cs`): the reproduction above →
`GetBackupsOf("Profilo_Mario")` empty; `RecoverDeletedProfile` of the old
Mario while the new one exists → `GetBackupsOf("Profilo_Mario 2")` has the
copies; `V1Compat.BackupsWithoutOwner` stays empty; a read-only copy info
file → the delete succeeds with the warning.

Commit: `A deleted playthrough takes its automatic copies with it`

## S5 — The delete confirmation tells the truth

**Problem.** `GameViewModel.DeleteAsync` builds `ConfirmDeleteText` with
`ConfirmDeleteKeepAll` / `ConfirmDeleteKeepN` ("you can still recover it
for a while: only the last N automatic copies are kept"). But
`SaveManager.PruneBackups` skips `BackupKind.Delete` on purpose (P5a):
deleted playthroughs stay until deleted for good.

**Do.**

1. `ConfirmDeleteText` (both `resx`) without `{0}`: the playthrough
   disappears from the list and stays in "Deleted playthroughs" until it is
   deleted for good there. Italian in the style of the file.
2. `DeleteAsync`: use the text as it is. Remove `ConfirmDeleteKeepAll`
   and `ConfirmDeleteKeepN` from both `resx` if nothing else uses them
   (`grep` in `src` and `tools`, `*.cs` and `*.axaml`).

**Tests.** `LocalizationTests` green; if a test checks the old text,
change it.

Commit: `Delete confirmation: deleted playthroughs are kept until deleted for good`

## S6 — Exporting the playthrough in the game

**Problem.** `SaveManager.ExportProfileCore` zips `Profili/<name>`. For the
playthrough in the game with unsaved progress, the zip is older than what
the user sees in the game. The "Export" menu item is enabled while the game
runs.

**Do.**

1. `PlaythroughCardView.axaml`: "Export" `IsEnabled` bound to a new
   `CanExport => !game.IsBusy && !(IsActive && game.GameRunning)` (notify
   it in `NotifyStateChanged`).
2. `GameViewModel.ExportAsync`: return when `IsBusy`. When the profile is
   the active one and `Manager.HasUnsavedProgress()`: ask with
   `dialogs.ChooseAsync` (new keys `ExportUnsavedHeading`,
   `ExportUnsavedText`, options `ExportSaveFirst` "Save the progress and
   export" and `ExportLastSaved` "Export the last saved state"; -1 or
   cancel → stop). Before the file picker. "Save first" → run
   `Manager.SaveProgress()` inside the same `RunBusyAsync` as the export,
   before it; if it fails, show the error and don't export.

**Tests** (`GameViewModelTests`): active playthrough with a changed game
file, `FakeDialogService` choosing 0 and giving a zip path → the zip's
`salvataggi` has the new content; choosing 1 → the old one. Check how
`FakeDialogService` answers `ChooseAsync` and `PickZipToSaveAsync`; extend
it if needed.

Commit: `Exporting the playthrough in the game offers to save its progress first`

## S7 — A failed import leaves nothing behind

**Problem.** `SaveManager.ImportProfileCore`
(`SaveManager.Playthroughs.cs`): the folder is removed on failure only in
the `ImportSpots` branch. With an old zip (no `posizioni.json`) a failed
`CopyFolderVerified(saves, profile)` leaves it; a failed copy of the points
(`punti`) leaves the playthrough without an info file (no owner) while the
user reads "import failed".

**Do.** After `profileName` is chosen (it is new: `UniqueName`), wrap the
rest in a `try` whose `catch` removes what this import created —
`Profili/<profileName>`, `Savestate/<profileName>`, `Info/<profileName>.json`
— with the existing `TryDeleteFolder` / `TryDeleteFile`, and rethrows (the
outer `catch` blocks keep making the message). Remove the inner
`try/catch` around `ImportSpots`, now redundant.

**Tests** (`ImportTests` or wherever `ImportProfile` is tested): before
importing, create a **file** named `Savestate/Profilo_<zip name>` so the
points copy fails → result fails, `GetProfiles()` doesn't contain the
playthrough, no `Profili/` or `Info/` leftover. Same with an old-format zip
whose save copy fails (if there is a simple way; otherwise only the first).

Commit: `A failed import removes the half-imported playthrough`

## S8 — Simple mode: file work off the UI thread

**Problem.** `SimplePlay.PrepareAsync` / `PrepareWithAsync` /
`PrepareNewAsync` run `SaveProgress`, `ActivateProfile`, `AdoptGameSaves`
and `CreateProfile` synchronously, and `SimplePlayFlow.FinishAsync` runs
`SaveProgress` synchronously: they are called from the UI thread, so the
window freezes ("not responding") with big saves. Nothing stops a second
click on a tile meanwhile.

**Do.**

1. `SimplePlay`: every `SaveManager` call that copies files runs in
   `await Task.Run(...)`. The question `ask.WhoseSavesAsync` stays on the
   caller's thread (don't wrap the whole method).
2. `SimplePlayFlow.FinishAsync`: `await Task.Run(() => session.Manager.SaveProgress(onGameExit: true))`;
   the `AddPlayTime` call too.
3. `SimpleViewModel.ActivateAsync` and `OpenMyGamesAsync` path: a
   `starting` flag so a second click while a game is being prepared does
   nothing; reset it in a `finally`. Show `Strings.BusyLoading` (or a new
   key if it doesn't fit) in `Message` while preparing.
4. Simple mode tests (`SimplePlayTests` or similar) must stay green; they
   may need `await` where they called synchronously.

**Tests.** A test that `ActivateAsync` called twice quickly calls `play`
once (use a `play` delegate that waits on a `TaskCompletionSource`).

Commit: `Simple mode: preparing and saving no longer freeze the window`

## S9 — Simple mode: a slow game start is not the end of the session

**Problem.** `SimplePlayFlow.WaitWhileRunningAsync` waits at most 60 × 2 s
for the game to appear, then for it to close. A game that takes longer
(update, shader cache, another launcher in between) counts as finished:
LSS saves at once and doesn't follow the real session. The "The game didn't
start" button (`GameNotStartedCommand`) is always visible in the playing
panel (`SimpleView.axaml` around line 313).

**Do.**

1. `WaitWhileRunningAsync`: no limit on the wait for the start. Once the
   game has been seen running, wait until it closes, as today.
2. After 2 minutes without seeing the game, show the "I've finished
   playing" panel too (`ShowFinishButton`), so the user can end the session
   if LSS can't see the game: `PlaySession` or `SimpleViewModel` gets a
   `StartOverdue` flag, `ShowFinishButton => Playing is { CanDetect: false } || StartOverdue`,
   raised with `OnPropertyChanged`. Wording: reuse `SimpleCantDetect` if it
   fits, otherwise a new key.
3. `WaitForEndAsync` keeps ending on whichever comes first: game closed or
   button.

**Tests.** If `WaitWhileRunningAsync` can't be tested without a real
process, extract the timing into a method that takes a `Func<bool>
isRunning` and a delay, and test that: never running → doesn't end by
itself; running after N checks then stopped → ends.

Commit: `Simple mode: a game that starts slowly is still followed`

## S10 — Folder names independent of the culture

**Problem.** `SaveManager.CreateSavestateCore`
(`DateTime.Now.ToString(SavestateDateFormat)`) and `NewBackupPath`
(`DateTime.Now.ToString(BackupDateFormat)`) use the current culture, but
`GetSavestates` / `GetBackups` parse with `InvariantCulture`. With a
culture whose calendar is not Gregorian (e.g. `th-TH`, year 2569), names
are wrong or not parsed: copies disappear from the history and are never
pruned.

**Do.** Add `CultureInfo.InvariantCulture` to those two calls, and to
`ManualGames.cs` (`DateTime.Now.ToString("yyyyMMdd-HHmmss")`) and `Log.cs`
(log line date). `grep -rn "DateTime.Now.ToString(\|\.ToString(\"yyyy" src`
to find any other date written to disk; leave the ones shown to the user
(`"d"`, `"g"`, `"G"`) as they are.

**Tests.** Set `CultureInfo.CurrentCulture = new CultureInfo("th-TH")` in a
`try/finally` that restores it: create a point and a backup →
`GetSavestates()` / `GetBackups()` find them, with today's Gregorian year.

Commit: `Backup and point names no longer depend on the culture's calendar`

## S11 — Small fixes

Three small fixes, one commit each (or one commit if very small, say it).

1. **"Add a player" in the simple mode** (`SimpleViewModel.AddPlayerAsync`):
   when `people.Add(name)` fails, show `result.Message` with
   `dialogs.InformAsync(Strings.AddPlayer, …, InfoKind.Warning)`.
   Test with `FakeDialogService`: a taken name → one message shown.
   Commit: `Simple mode: adding a player with a taken name says why`
2. **Play time when nothing was saved** (`SimplePlayFlow.FinishAsync`):
   count the time (`AddPlayTime`) also when the save fails with
   `GameSavesEmptyWouldClear`: the person did play. Not when the user
   pressed "The game didn't start" (`RunSimpleAsync` already skips
   `FinishAsync` then). Commit: `Simple mode: play time counts even when nothing was saved`
3. **Case-insensitive active check** (`SaveManager.DeleteProfileCore`):
   `string.Equals(profileName, State.ActiveProfile, StringComparison.OrdinalIgnoreCase)`,
   like `ProfileExists`. Test: deleting `"profilo_anna"` while
   `Profilo_Anna` is active fails with `CannotDeleteActive`.
   Commit: `The active playthrough can't be deleted with a different case`

## S12 — Missing main save folder: check, then decide

**Problem (to check).** `SaveSet.CopyGameTo` → `FileOps.CopyFolderVerified`
throws `DirectoryNotFoundException` when a whole-folder spot 1 doesn't
exist. So the backup before a switch fails ("Backup failed. Operation
cancelled.") if the user deleted the game's save folder to start over, or
the game never ran. `SaveManager.Open` checks the spots with
`SaveLocator.CheckSpot` for non-`userdata` games; it may already refuse a
missing folder.

**Do.**

1. Write a test: a game with one whole-folder spot, a playthrough
   `Anna`, then delete the game folder, then `ActivateProfile("Profilo_Anna")`.
   Write the result in the Notes of this file. Read `SaveLocator.CheckSpot`
   and write what it does with a missing folder.
2. If activation fails only because of the backup: in `SaveSet.CopyGameTo`
   a missing **spot 1** folder gives an empty snapshot folder
   (`Directory.CreateDirectory(target)`) instead of throwing, like a game
   with no saves. The activation then creates the game folder from the
   playthrough. Keep the test as a regression test.
3. If `Open` / `CheckSpot` already refuse it on purpose, change nothing:
   write it in the Notes and tick the task.

Commit: `A missing main save folder no longer blocks switching` (only if
step 2 was done; otherwise the Notes commit `Progress: S12`).

## S13 — Linux check, changelog

1. `bash scripts/linux-check.sh lss-ci@192.168.1.3 --tests-only` (see
   the rules). Write the result in the S13 line of the checklist.
2. `CHANGELOG.md` (English): a new section `Unreleased` at the top with one
   line per user-visible fix of S1–S12, in the style of the file. No
   version number: Andrix decides it.

Commit: `Changelog for the fixes after 2.0.0`

## S14 — 👤 Check (Andrix)

On Windows and the Steam Deck, with a copy of the data folder:

- simple mode: play a game, close it → play time grows by the real time
  only, one "save on exit" copy in the history;
- a game with two save folders: new empty playthrough, start the game,
  change only the settings, quit → switching to another playthrough works;
- rename and export are greyed out on the playthrough in the game while
  the game runs;
- delete a playthrough, create one with the same name → its history is
  empty; recover the deleted one → its copies are back.

Then Andrix merges `fix-bug-hunt` into `main` and decides the version.
This file can be deleted once everything is merged.

---

## Checklist

Tick with the short commit hash and the test count, e.g.
`- [x] S1 — … (abc1234; 858 tests)`. Before S1: 854 tests on `ef0dbdb`.

- [x] S1 — Simple mode: one session, one save (91f4dad; 856 tests)
- [x] S2 — Several spots: an empty spot 1 must not block switching (80ae70f; 860 tests)
- [x] S3 — No rename of the playthrough in the game while the game runs (7240060; 862 tests)
- [ ] S4 — Deleted playthroughs take their automatic copies with them
- [ ] S5 — The delete confirmation tells the truth
- [ ] S6 — Exporting the playthrough in the game
- [ ] S7 — A failed import leaves nothing behind
- [ ] S8 — Simple mode: file work off the UI thread
- [ ] S9 — Simple mode: a slow game start is not the end of the session
- [ ] S10 — Folder names independent of the culture
- [ ] S11 — Small fixes (add player message, play time when nothing saved, case-insensitive active check)
- [ ] S12 — Missing main save folder: check, then decide
- [ ] S13 — Linux check, changelog
- [ ] S14 — 👤 Check on Windows and the Steam Deck (copy the data folder first)

## Notes

Anything that didn't match the plan, decisions taken with Andrix, results
of checks. Newest first, with the date.

- 2026-10-30 — Plan written from a review of `main` at `ef0dbdb`
  (854 tests green). Problems 2 and 4 reproduced with tests, not committed.
