# LSS implementation plan

Step-by-step plan to turn Control Save Manager into **LSS — Local Save
Switcher**: several playthroughs for **every Steam game that saves in
`<Steam>/userdata/<AccountID>/<AppID>/`**, on **Windows and Linux**, with an
**Avalonia** interface.

The plan is written to be carried out task by task by a coding agent
(Claude Haiku 5.5) or by a person. Every task says which files to touch,
what to write, how to check it and what to commit.

## Files of the plan

| File | Content |
|---|---|
| `README.md` | This file: goal, decisions, rules, glossary. **Read it first, every session.** |
| `PROGRESS.md` | Checklist of all tasks. Tick a task when its commit is done; write notes there. |
| `C-core.md` | Phase C — split the logic into `Lss.Core`, add `IPlatform`. |
| `D-catalog.md` | Phase D — find Steam libraries and games (`SteamLibrary`). |
| `E-per-game.md` | Phase E — `SaveManager` works for any game. |
| `F-data.md` | Phase F — new data folder and migration. |
| `G-avalonia.md` | Phase G — the Avalonia interface replaces WinForms. |
| `H-linux.md` | Phase H — Linux support. |
| `I-packaging.md` | Phase I — packages and GitHub Actions. |
| `J-docs-release.md` | Phase J — documentation, beta and release. |

Phases A (rename) and B (Avalonia test, `spike/`) are already done.

## How to work (rules for the agent)

1. **One task at a time, in order.** Open `PROGRESS.md`, take the first task
   not ticked, open its phase file and do exactly what it says. Don't start
   the next task until the current one is committed.
2. **One branch per piece of work.** The `lss` branch (phases A–J5) is
   merged into `main` and closed. For new work, create a branch from
   `main` named after it (for example `beta-2` or `fix-history-scroll`)
   unless the session brief names one. Never commit on `main`.
   **Never push**, never create tags, never create releases: Andrix does
   that.
3. **Before every commit** run, from the repository root:

   ```bash
   dotnet build LSS.slnx -c Release
   ```

   ```bash
   dotnet test LSS.slnx -c Release
   ```

   Both must end with `0 Error(s)` and no failed tests. Until task C1 has
   created `LSS.slnx`, use `LSS.csproj` and `tests/Lss.Tests` instead.
   Don't commit with warnings you introduced.
4. **Commit message**: the one given in the task, in English, followed by
   an empty line and:

   ```
   Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
   ```

5. **Then tick the task** in `PROGRESS.md` (`- [ ]` → `- [x]`) and add the
   short commit hash, in a separate commit `Progress: <task id>`, or in the
   same commit if you prefer: be consistent.
6. **If something doesn't match the plan** (a file, a method or a line is not
   where the plan says, a test fails twice after fixing, a package version
   doesn't exist): **stop**, don't improvise a large change. Write what you
   found under "Notes" in `PROGRESS.md` and ask Andrix.
7. **Tasks marked 👤** must be done by Andrix (they touch GitHub,
   the real game or another computer). Skip them, leave them unticked, and
   continue with the next task that doesn't depend on them.
8. **Never touch the real Steam folder or the real app data.** Tests work
   only in temporary folders (see `TestFolder`). Don't run the app against
   real saves unless a 👤 task says so.
   **Linux checks:** if the brief of your session gives you an SSH host
   for Linux checks, run `bash scripts/linux-check.sh <host>` after every
   task that touches Linux code, packaging or tests (`--tests-only` when
   packaging didn't change). It only uses temporary containers on that
   machine; never run anything else there.
9. **Keep the existing style.** Read the file before editing it and write
   code like the code around it.

## Code conventions (mandatory)

- **Code comments in Italian**, short, like the existing ones. XML doc
  comments (`/// <summary>`) in Italian too.
- **Log messages (`Log.Write`) in Italian.**
- **User-visible texts only in `Strings.resx` (English) and
  `Strings.it.resx` (Italian)**, with the same key in both and the same
  `{0}`, `{1}`... placeholders. Never hard-code a user text in C# or XAML.
  `LocalizationTests` checks both files.
- **Names on disk stay Italian**: `Profili`, `Info`, `Backup`, `Savestate`,
  `stato.ini`, `Profilo_<name>`, `Prima_di_...`, `.nuovo`, `.vecchio`,
  `.rinomina`, `partita.ini`, `salvataggi`, `punti`. Don't rename them.
- **Documentation for GitHub (README, docs/, CHANGELOG, workflow comments)
  in English.**
- `Nullable` enabled, file-scoped namespaces (`namespace Lss;`), one public
  type per file unless the file already groups small records.
- Every new behaviour gets an xUnit test in `tests/Lss.Tests` (folder per
  phase is not needed: one test file per class, named `<Class>Tests.cs`).
- Tests use `TestFolder` for files and **`FakePlatform`** (created in task
  C3) for anything that would touch the registry, processes or Steam.
- Never `DllImport` / `LibraryImport` (Smart App Control, see
  `docs/DEVELOPMENT.md`).
- **Compatibility with Control Save Manager only in `LegacyMigration`.**
  Anything that exists only to take over Control Save Manager's data,
  files or behaviour goes in `src/Lss.Core/LegacyMigration.cs` (tests in
  `LegacyMigrationTests.cs`) and is called from one place, so it can be
  deleted later with the checklist in `docs/DEVELOPMENT.md`
  ("Compatibility with Control Save Manager"). If a task needs more of it,
  add it there and add a line to that checklist.
- No new NuGet packages except those named in a task, with the exact
  version given.

## Decisions already taken

| Topic | Decision |
|---|---|
| Name | **LSS — Local Save Switcher**. Subtitle "Multiple playthroughs for your PC games" (generic: other launchers come after 1.0, see J9). "Steam" never in the product name (Valve trademark). Disclaimer: *"Steam is a trademark of Valve Corporation. LSS is not affiliated with or endorsed by Valve."* |
| Repository | `Andrix64/Local-Save-Switcher` (rename done by Andrix, task J1). |
| Exe | `LSS.exe` on Windows, `lss` on Linux. Namespace `Lss`. |
| Versions | `1.0.0-beta.N` for tests, then `1.0.0`. |
| Supported games | Games with files in `userdata/<AccountID>/<AppID>/` other than `remotecache.vdf` at the top level. Others are shown as not supported. |
| Systems | Windows 10/11 x64; Linux x64 (Ubuntu 22.04+, Debian 12+, Fedora 40+, openSUSE, Arch and derivatives, SteamOS desktop mode). |
| UI | Avalonia **12.1.4**, CommunityToolkit.Mvvm **8.4.2**, MVVM, compiled bindings. |
| Data folder | `Environment.SpecialFolder.LocalApplicationData` + `LSS` (Windows `%LOCALAPPDATA%\LSS`, Linux `~/.local/share/LSS`). Until phase F it stays `ControlSaveManager`. |
| Signing | **None.** SignPath refused the free signing (2026-10-25); 1.0 ships with the Windows exe unsigned, explained in the README. The native Avalonia dlls are already signed by Microsoft and AvaloniaUI. |
| Linux packages | `.tar.gz`, `.deb`, `.rpm`, AppImage for 1.0; Flatpak and AUR later. |

## Target layout (after phase G)

```
LSS.slnx
src/
  Lss.Core/          net10.0   logic, no UI, no OS-specific API
  Lss.Platform/      net10.0   WindowsPlatform, LinuxPlatform
  Lss.App/           net10.0   Avalonia app (exe LSS / lss)
tests/
  Lss.Tests/         net10.0   xUnit, runs on Windows and Linux
packaging/linux/     desktop file, icons, metainfo, nfpm.yaml, AppImage
scripts/             release.ps1 (Windows), release.sh (Linux)
docs/
```

Until phase G ends, the WinForms app stays at the root (`LSS.csproj` and
the `*.cs` files next to it) and keeps working.

## Glossary

| Word in the code | Meaning |
|---|---|
| profile / `Profilo_<name>` | a playthrough |
| savestate | a saved point |
| backup | an automatic copy |
| `SavePath` | the game's save folder in Steam: `userdata/<AccountID>/<AppID>` |
| `basePath` | the app's data for one account (later: one account and one game) |
| AppID | Steam's number for a game (Control = `870780`) |
| `remotecache.vdf` | Steam's file at the top of the game folder: never copied or deleted |
