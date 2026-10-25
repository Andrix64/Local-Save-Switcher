# Phase K — Several launchers in the core

Goal: the code no longer assumes Steam outside the Steam parts, so the next
phases can add save detection (L, M), several save folders per game (N),
Epic / GOG / Heroic / any game (O) and the simple mode (P). **No visible change** for users except the games
list grouped by launcher (K7).

Where this phase fits: see "The 2.0 plan" in [README.md](README.md).

Branch: `launchers-core` (from `main`). Rules in [README.md](README.md).

Every task of this phase is a refactoring: **all existing tests must pass
unchanged**, except for renamed types and members. If a test needs a change
in what it checks, stop and write it in the notes.

---

## K1 — `GameAccount` instead of `SteamAccount`

`SteamAccount(Id, Name, Folder, LastSaved)` (in `SaveManager.cs`) becomes
`GameAccount(string Provider, string Id, string Name, string Folder, DateTime? LastSaved)`
in its own file `src/Lss.Core/GameAccount.cs`. `Provider` = `"steam"` for
every account created today. Keep `DisplayName` and any other member as is.
Rename every use (compiler-driven: build, fix, build).

Commit: `GameAccount replaces SteamAccount`

## K2 — `GameInfo` with a provider

- New first parameter `string Provider`; `AppId` renamed `Id`
  (`ControlAppId` stays, it is a Steam constant).
- `InstallPath` becomes a stored parameter instead of being computed from
  `LibraryPath` + `steamapps/common` + `InstallDir`; `SteamLibrary`
  computes it. `LibraryPath` and `InstallDir` stay (Steam uses them; other
  providers fill `InstallDir` with the last folder name of `InstallPath`
  and leave `LibraryPath` "").
- New optional parameters at the end:
  `string LaunchTarget = ""` (what the provider needs to start the game: an
  AppID, an URI, an exe path) and `string PrefixPath = ""` (Wine / Proton
  prefix of the game, filled in phase L).
- `GameInfo.Control(...)` sets `Provider = "steam"`.

Commit: `GameInfo has a provider and a generic id`

## K3 — Generic platform functions

Read `IPlatform.cs`, `WindowsPlatform.cs`, `LinuxPlatform.cs` first.
Add to `IPlatform` (Italian summaries), implement on both systems and in
`FakePlatform`:

```csharp
/// <summary>Processi il cui eseguibile è dentro la cartella, con l'ora di avvio se nota.</summary>
IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder);
/// <summary>Apre un indirizzo con il programma registrato (steam://, com.epicgames.launcher://, heroic://...).</summary>
void OpenUri(string uri);
/// <summary>Avvia un programma. false se non parte.</summary>
bool StartProgram(string exe, string arguments, string workingDir);
/// <summary>true se c'è un processo con quel nome di file (senza .exe), dell'utente.</summary>
bool IsProcessRunning(string name);
/// <summary>Valore stringa del registro di Windows, null altrove o se manca.</summary>
string? ReadRegistryString(string key, string name);
/// <summary>Sottochiavi di una chiave del registro di Windows, vuoto altrove.</summary>
IReadOnlyList<string> RegistrySubKeys(string key);
```

- `key` is a full path like `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games`
  (`Microsoft.Win32.Registry`, as `WindowsPlatform` already does, **never**
  `DllImport`).
- Move the existing "process under the install folder" code of both
  platforms into `FindProcessesUnder` and call it from the Steam methods,
  so nothing is written twice.
- Linux: a Windows game run by Wine has `/proc/<pid>/exe` = the Wine
  loader. So `FindProcessesUnder` also matches when an argument of
  `/proc/<pid>/cmdline` (NUL-separated), after turning `\` into `/` and
  removing a leading `Z:`, starts with the folder (ignoring case). Pure
  helper `internal static bool CmdlineMatches(ReadOnlySpan<byte> cmdline, string folder)`.
- `OpenSteamUri` becomes a call to `OpenUri` (keep it until K4 removes its
  callers).

Tests: `CmdlineMatches` with a Linux path, a `Z:\home\...` path, another
folder; `FindProcessesUnder` of an empty temporary folder → empty;
`ReadRegistryString` on Linux → null (Linux-only test).

Commit: `Generic platform functions for launchers`

## K4 — `IGameProvider` and `SteamProvider`

This is the riskiest refactoring of the phase, so it is split in four
commits. After each one **all existing tests pass unchanged** and the app
works as before; if a step can't be done that way, stop and write why.

### K4a — The interface, unused

New file `src/Lss.Core/IGameProvider.cs`:

```csharp
/// <summary>
/// Un launcher (Steam, Epic, GOG, Heroic...) o i giochi aggiunti a mano:
/// trova account e giochi, dice se un gioco è aperto e lo avvia.
/// </summary>
public interface IGameProvider
{
    string Id { get; }            // "steam", "epic", "gog", "heroic", "manual"
    string DisplayName { get; }   // nome del launcher (marchio: non tradotto)
    IReadOnlyList<GameAccount> FindAccounts();
    IReadOnlyList<GameInfo> FindGames(GameAccount account);
    /// <summary>false se il provider non sa dire se il gioco è aperto.</summary>
    bool CanDetectRunning(GameInfo game);
    bool IsGameRunning(GameInfo game);
    DateTime? GameStartTime(GameInfo game);
    bool CanLaunch(GameInfo game);
    Result Launch(GameInfo game);
    bool IsLauncherRunning();
    CloudState GetCloudState(GameAccount account, GameInfo game);
}
```

`SteamProvider` (new file) implements it **by calling the code that exists
today** (`SaveManager.FindAccountsWithGames` / `FindAccountsWithAnyGame`,
`SteamLibrary.FindGames`, `Platform.Current.IsGameRunning`, ...), nothing
moved yet. New static `Providers.All(IPlatform platform)` returns
`[new SteamProvider(platform)]`. `FakeProvider` in `tests/Lss.Tests`.
Nothing calls them yet.

Tests `SteamProviderTests.cs`: with a fake Steam folder (as in
`SteamLibraryTests`) the provider returns the same accounts and games as the
static functions.

Commit: `IGameProvider and SteamProvider, not used yet`

### K4b — Accounts and games through the provider

`App.axaml.cs` and every other caller of `FindAccountsWithGames`,
`FindAccountsWithAnyGame` and `SteamLibrary.FindGames` outside `Lss.Core`
use `SteamProvider` (from `Providers.All`). Then **move** the bodies of
those account functions from `SaveManager` into `SteamProvider` (private
or `internal static` helpers); `SaveManager` keeps no account search.
`RecoverGameFolders` stays where it is.

Tests: the existing account tests now call the provider (only the call
changes, not what is checked).

Commit: `Accounts and games come from the provider`

### K4c — Running, launch and cloud through the provider

`SaveManager` gets an `IGameProvider` in its constructor and in `Open`
(`Initialize`, the old Control-only entry point, passes a `SteamProvider`).
Move into `SteamProvider`: `IsGameRunning`, `GameStartTime`,
`IsSteamRunning` (→ `IsLauncherRunning`), `LaunchGame` (→ `Launch`),
`GetCloudState` (it uses `GameAccount.Folder` instead of the parent of
`SavePath`). `SaveManager` keeps thin methods with the same names that call
the provider, so the ViewModels don't change in this commit.

Tests: `FakeProvider` replaces the Steam parts of `FakePlatform` in the
`SaveManager` tests that check running / launch / cloud; same assertions.

Commit: `Running, launch and cloud come from the provider`

### K4d — Clean-up and check

- `grep -n "steam://\|userdata\|sharedconfig" src/Lss.Core/SaveManager*.cs`
  prints nothing (comments excluded); fix what it finds.
- The Steam-specific methods of `IPlatform` (`FindSteamRoots`, registry
  `Running`) stay in `IPlatform`; only `SteamProvider` calls them (check
  with grep and write the result in the notes).
- Remove what became unused; build with 0 warnings.

Commit: `Steam code only in SteamProvider`

## K5 — Data folder per provider

Layout: `<data>/<provider>/<account>/<game>/` (today
`<data>/<AccountID>/<AppID>/`).

New file `src/Lss.Core/DataLayoutMigration.cs` (not `LegacyMigration`:
that file is only for Control Save Manager, and these are LSS beta users):
at start-up, before anything reads the data, every folder of `<data>` whose
name is all digits moves to `<data>/steam/<name>`. Same safety as the
`AppData` moves: rename when possible, otherwise copy + verify + delete the
source only after the verify; an interrupted move is resumed at the next
start; a folder already present in `steam/` is never overwritten (log and
leave both). Called from `App.axaml.cs` right after `LegacyMigration.Run()`.

`lss.ini` `UltimoAccount` / `UltimoGioco` become `provider/id` (old values
without `/` are read as `steam/<value>`).

Tests `DataLayoutMigrationTests.cs`: moves, idempotent second run, existing
target left untouched, interrupted move resumed, files and non-numeric
folders of `<data>` (`log.txt`, `lss.ini`, `steam` itself) left where they
are.

Commit: `Data folder per launcher`

## K6 — Export format 3

The export zip records the provider. Import accepts format 2 as `steam`,
and refuses a zip of another provider with the existing `ImportOtherGame`
text. Read the export code first (`SaveManager.Playthroughs.cs`, F5 in
`F-data.md`).

Commit: `Export format 3 with the launcher`

## K7 — Start-up and games list for several providers

- Start-up asks every provider for accounts. With one account per provider
  no question is asked: the user sees all providers' games together. With
  more accounts in one provider the account window asks as today, per
  provider, and shows the launcher name. Read `App.axaml.cs`,
  `AccountWindow` and `MainWindowViewModel` first and write the behaviour
  you chose in the notes **before** coding it.
- The games list is grouped by launcher (header with `DisplayName`), with
  *Other games* inside each group. With only Steam the list looks as today.
- Texts that name Steam in a generic place become generic (search
  `Strings.resx` for `Steam` and list each key in the notes with "kept" or
  "changed").

Commit: `Games of several launchers in one list`

## K8 — 👤 Check

Andrix: Windows, the data moved to `steam/`, everything works as before.
