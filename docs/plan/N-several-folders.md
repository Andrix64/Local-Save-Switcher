# Phase N — Several save folders per game, and a look after every session

Goal:

1. A game can keep its saves in **several places** (Terraria: `userdata`
   and Documents; a game with saves in AppData and settings-with-progress
   in Documents; loose files such as `<game folder>/*.sav`). LSS switches
   **all of them together**, with the same safety as today.
2. After **every** game session LSS looks at what changed on the disk. If
   something that looks like saves changed **outside** the folders it
   already knows, it proposes it, with the reasons (phase M). Same for a
   game that LSS doesn't manage yet: when it closes, LSS may say "I found
   where it saves".

Requires phases K, L, M. Branch: `several-folders`. Rules in
[README.md](README.md).

## Words

| Word in the code | Meaning |
|---|---|
| spot / `SaveSpot`, on disk `posto` | one place where a game saves: a whole folder, or some files of a folder (pattern) |
| primary spot | spot with id 1: the first one found; its files stay at the top of each playthrough folder, as today |
| extra spot | any other spot; its files are in `<snapshot>/.lss-posizioni/<id>/` |
| snapshot | any folder that holds a copy of the game's saves: a playthrough, a saved point, a backup |

## Why this layout

Today a snapshot **is** a copy of the save folder. Extra spots go in a
hidden top-level folder of the snapshot, `.lss-posizioni/<id>/`, so:

- games with one spot (all games today) don't change at all, no data
  migration;
- copying, renaming, deleting, exporting a snapshot carries every spot
  along without new code;
- an old snapshot simply has no `.lss-posizioni/<id>`: see the rule below.

**Rule for a spot missing in a snapshot**: folder `.lss-posizioni/<id>`
**absent** → that spot is **left as it is** when the snapshot is put in
place. Folder **present** (even empty) → the spot is made identical to it
(empty folder = spot emptied). This makes every operation safe for
snapshots made before the spot existed.

---

## N1 — `SaveSpot` and `GameInfo.Spots`

New file `src/Lss.Core/SaveSpot.cs`:

```csharp
/// <summary>
/// Un posto in cui il gioco salva: una cartella intera (Pattern "") o solo
/// i file della cartella che corrispondono a Pattern ("*.sav;slot?.dat").
/// </summary>
public sealed record SaveSpot(int Id, string Folder, string Pattern, bool Recursive,
    string PortablePath, SaveSource Source)
{
    public bool IsWholeFolder => Pattern == "";
}
```

- `PortablePath`: the folder written with the root names of L6 in braces
  (`{WinMyDocuments}/My Games/X`, `{gameinstall}/saves`,
  `{LinuxXdgDataHome}/X`), made by a new
  `SavePathResolver.ToPortable(string folder, SaveContext context)`: the
  longest root folder of the context that contains `folder` is replaced by
  its name; none → the full path. Then the account's 64-bit and 32-bit
  Steam ids inside the rest of the path become `{64BitSteamID}` /
  `{Steam3AccountID}` (so an exported path doesn't carry them). Used by
  export/import (N8) and by the save configurations (N9a–N9c).
- `GameInfo`: the `SaveFolder` parameter is replaced by
  `IReadOnlyList<SaveSpot> Spots`, with a property
  `public string SaveFolder => Spots.Count > 0 ? Spots[0].Folder : "";`
  so most code keeps compiling. Fix the constructor calls (compiler-driven).
  Every game today gets one spot with `Id = 1`, `Pattern = ""`.

Tests: `ToPortable` for Documents, prefix `AppData/Roaming`, game folder,
unknown folder, a path containing the account's 64-bit id;
`GameInfo.SaveFolder` with zero and one spot.

Commit: `Save spots`

## N2 — Pattern spots in `FileOps` and `SaveFolderSafety`

`FileOps`, new functions with the same guarantees as the folder ones
(exception at the first error, verify byte by byte):

```csharp
/// <summary>
/// File di folder che corrispondono al pattern (";" tra i glob): solo il primo
/// livello, o tutti i livelli se recursive (il pattern vale sul nome del file).
/// </summary>
public static IReadOnlyList<string> MatchedFiles(string folder, string pattern, bool recursive);
/// <summary>Copia in destination solo i file corrispondenti e controlla la copia.</summary>
public static void CopyMatchedVerified(string folder, string pattern, bool recursive, string destination);
/// <summary>
/// Rende i file corrispondenti di folder identici a quelli di source: copia
/// ognuno come .nuovo accanto all'originale, verifica, poi sostituisce;
/// elimina i corrispondenti che source non ha. Gli altri file non vengono toccati.
/// </summary>
public static void ReplaceMatched(string source, string folder, string pattern, bool recursive);
public static bool MatchedEqual(string folder, string pattern, bool recursive, string snapshot);
```

The glob matcher of L6 is reused (move it to an `internal static class Glob`
if it is private there). `ReplaceMatched` recovery: `RecoverInterruptedReplace`
gets a twin `RecoverInterruptedMatched(folder)` that finishes or removes
`.nuovo` / `.vecchio` files left next to matched files.

`SaveFolderSafety.CheckPattern(string folder, string pattern, ...)`:

- refused patterns: `*`, `*.*`, empty parts, parts without any literal
  character after removing `*` and `?` (`??`), parts matching extensions
  `.exe`, `.dll`, `.so`, `.pak`, `.pck`, `.assets`, `.bundle`, `.ucas`,
  `.utoc`, `.sig`, `.lnk`, `.desktop`;
- the folder may be `InstallPath` or a protected folder (only the matched
  files are touched) but not a drive root, `/`, a launcher folder or the LSS
  data;
- at most 2,000 matched files and `MaxBytes`.

Tests: matching, copy and replace leave the other files untouched (compare
before/after), deleting a matched file absent in source, interrupted
replace recovered, every refused pattern.

Commit: `Pattern spots`

## N3 — Spots of a game: `posti.ini`

New file `src/Lss.Core/GameSpots.cs`, file `posti.ini` in the game's data
folder (it replaces `cartella.ini` of L9: L9's file is read once and turned
into spot 1 with its origin, then deleted — test it):

```
Prossimo=5
Launcher.{WinMyDocuments}/My Games/Terraria=2
Posto.3=C:\Users\x\AppData\Roaming\Foo|{WinAppDataRoaming}/Foo||1|imparata
Posto.4=D:\Giochi\Bar|{gameinstall}|*.sav;slot?.dat|0|scelta
Ignorate=C:\Users\x\AppData\Local\Foo;C:\Users\x\Documents\Bar
CartelleExtra=D:\Giochi
```

Format of `Posto.<id>`: `folder|portable path|pattern|recursive|origin`
(`recursive` = `1` or `0`), origin
`scelta` or `imparata`. `|` cannot be in a path on Windows;
on Linux a path with `|` is refused when adding (message `SpotBadName`).
Ids are never reused (`Prossimo`). `Ignorate` = the folders ignored **for
this game** (M3b), and `CartelleExtra`: both move here from `cartella.ini`;
`IgnoredFolders.ForGame` / `AddForGame` / `RemoveForGame` (M3b) now read and
write `posti.ini` (they keep the same behaviour and tests). The folders
ignored for every game stay in `cartelle-ignorate.ini`.

```csharp
internal sealed class GameSpots
{
    public GameSpots(string gameDataFolder);
    public IReadOnlyList<SaveSpot> Spots { get; }
    public IReadOnlyList<string> ExtraRoots { get; }
    public void Load();
    public SaveSpot Add(string folder, string pattern, string portable, SaveSource source);
    public void Remove(int id);
    public void Save();
}
```

The Steam `userdata` folder and the folders from the launcher rules are **not**
written here: they are found again at every start (they can move, for
example to the SD card). Only chosen and learned spots, ignored folders and
extra roots are stored. Exception: the **ids** of launcher spots must stay the
same across starts, so `posti.ini` keeps `Launcher.<portable path>=<id>`
lines for them.

Tests: round trip, ids not reused, migration from `cartella.ini`, stable
ids for launcher spots after the folder moved (same portable path, other full
path).

Commit: `Spots of a game`

## N4 — Snapshots with extra spots

`FileOps`: a top-level name `.lss-posizioni` (constant
`FileOps.ExtraSpotsFolder`) is treated like `remotecache.vdf` is today:
never copied from a snapshot to the game, **kept** in the destination when
a snapshot is replaced from the game (`ReplaceFolder` keeps it like it keeps
the Steam Cloud file), ignored by `FoldersAreEqual` and `HasNoSaveFiles`.
Read `FileOps.cs` first and list in the notes every place that handles
`SteamCacheFile`: each must handle the new name too.

Tests: `ReplaceFolder(snapshot → game)` doesn't create `.lss-posizioni` in
the game folder; `ReplaceFolder(game → snapshot)` keeps the snapshot's
`.lss-posizioni`; equality and emptiness ignore it.

Commit: `Snapshots keep extra spots`

## N5 — `SaveSet`: every operation on all spots

The core of the phase, split in four commits. After each one **all
existing tests pass unchanged**; if a step can't be done that way, stop and
write why.

```csharp
/// <summary>Le operazioni sui salvataggi del gioco, per tutti i posti insieme.</summary>
internal sealed class SaveSet
{
    public SaveSet(IReadOnlyList<SaveSpot> spots);
    public void CopyGameTo(string snapshot);              // tutti i posti, verificato
    public void ReplaceGameFrom(string snapshot, string backup, string journal);
    public void ReplaceSnapshotFromGame(string snapshot); // come SaveProgress oggi
    public bool GameEquals(string snapshot);
    public bool GameIsEmpty();
    public void RecoverInterrupted(string journal, string? backup);
}
```

Spot 1 is the top of the snapshot; spot `n` is `.lss-posizioni/<n>`.
Whole-folder spots use `ReplaceFolder` / `CopyFolderVerified`, pattern
spots the N2 functions. The "absent = left as it is" rule applies to
`ReplaceGameFrom` and `GameEquals` (an absent extra spot counts as equal).

### N5a — `SaveSet` without the journal, unused

New file `src/Lss.Core/SaveSet.cs` with every method above except the
journal: `ReplaceGameFrom` ignores its `journal` parameter for now, and
`RecoverInterrupted` is empty. Nothing calls `SaveSet` yet.

Tests `SaveSetTests.cs`: two folder spots + one pattern spot (one of them
recursive): copy to a snapshot and back, equality, emptiness; a snapshot
without spot 3 leaves spot 3 untouched; an empty `.lss-posizioni/3`
empties it; with one whole-folder spot `SaveSet` does exactly what
`FileOps` does today (compare the folders).

Commit: `SaveSet for several save spots`

### N5b — Journal and recovery

- `ReplaceGameFrom`: before touching anything, write `cambio.ini`
  (journal: snapshot, backup, list of spot ids, with `IniFile`); after each
  spot, add its id to `Fatti=`; at the end delete the journal. When a spot
  fails: put back the spots already done **from the backup**, delete the
  journal, rethrow.
- `RecoverInterrupted`: first each spot's own recovery
  (`RecoverInterruptedReplace` / `RecoverInterruptedMatched`), then every
  spot listed in `Fatti=` is put back from the backup, journal deleted, log.
  Returns whether it restored something.

Tests: a failure on spot 2 (read-only file) puts spot 1 back and the game
folders equal the backup; a journal left by a simulated crash (written by
the test, with some spots in `Fatti=` and a `.vecchio` folder) →
`RecoverInterrupted` gives back the state before the switch; no journal →
nothing happens.

Commit: `Journal for switches of several spots`

### N5c — `SaveManager` switches through `SaveSet`

Read `SaveManager.cs` (`ReplaceGameSaves`, `ActivateProfileCore`,
`RestoreSavestate`, `RestoreBackup`) first. Every place that puts a
snapshot **into the game** uses `SaveSet.ReplaceGameFrom` (journal file
`cambio.ini` in the game's data folder); every backup taken before it uses
`CopyGameTo`. `SaveManager.Open` calls `RecoverInterrupted` when
`cambio.ini` exists and reports `SwitchInterruptedRestored` to the
interface (a flag on the manager, shown once by N9).

List in the notes every `SavePath` use you changed in this commit.

Tests (`SaveManager` tests with two spots): activate, restore saved
point, restore backup move every spot; a crash during activate → the next
`Open` restores and sets the flag.

Commit: `Switching and restoring use every spot`

### N5d — `SaveManager` saves through `SaveSet`, and the check

Every place that copies **from the game** into a snapshot (`SaveProgress`,
`CreateProfile` with current saves, `CreateSavestate`, `AdoptGameSaves`,
export) uses `CopyGameTo` / `ReplaceSnapshotFromGame`;
`HasUnsavedProgress` and `GameSavesAreEmpty` use `GameEquals` /
`GameIsEmpty`. **Shared files** (E4) stay relative to spot 1 only: write it
in a comment and in `DEVELOPMENT.md` (N13).

Then `grep -n "SavePath" src/Lss.Core/SaveManager*.cs`: the only uses left
are building the `SaveSet` and log texts. Write the result in the notes.

Tests: with two folder spots + one pattern spot, a full round (create,
switch, save, saved point, restore, delete) moves every spot; unsaved
progress in spot 2 only is detected.

Commit: `Saving uses every spot`

## N6 — The locator finds several spots

`SaveLocator` (L8) returns `IReadOnlyList<SaveSpot>` instead of one folder:

- several folders (not nested) → one spot each, all checked with
  `SaveFolderSafety`; any unsafe → the game is `Unsafe` (never a partial
  set: missing a spot would mix saves of two playthroughs);
- matched files → grouped by parent folder, one pattern spot per folder,
  pattern = the rule's `Pattern` (`*.sav`, or an exact file name), checked
  with `CheckPattern`, keeping `Recursive` (Clair Obscur: `*.sav` in
  every subfolder of its save folder); `*.*` or `*` with `Recursive` is a
  whole-folder spot;
- a Steam game with `userdata` files **and** other folders from the launcher rules
  (Terraria) → `userdata` is spot 1, the others are extra spots, all
  `Supported`. `HasOtherSaves` and `SomeSavesElsewhere` (L) are no longer
  used: remove them;
- more than 8 spots → `Unsure` (`SupportUnsure` new text: "This game saves
  in too many places for LSS. You can choose its folders yourself.").
- chosen/learned spots from `posti.ini` are added after those found; a
  chosen folder equal to or inside a found one is dropped (log).

Update the L8 tests: the `Unsure` cases for two folders and for files become
supported with two spots / one pattern spot.

Commit: `Locator returns every save spot`

## N7 — Adding and removing a spot of a game that has playthroughs

`SaveManager`:

```csharp
public enum NewSpotFill { CopyCurrent, Empty }

/// <summary>
/// Aggiunge un posto. I file che ci sono ora appartengono alla partita
/// attiva; per le altre partite: copia dei file attuali o posto vuoto.
/// </summary>
public Result AddSpot(SaveSpot spot, NewSpotFill others);
public Result RemoveSpot(int id);
```

`AddSpot`: game running → `CloseGameFirst`; safety check; backup of the
new spot (a backup of type `Prima_nuovo_posto`, text in `BackupReasonText`);
copy the spot into the active playthrough's `.lss-posizioni/<id>`; for the
other playthroughs `CopyCurrent` → the same copy, `Empty` → an empty
`.lss-posizioni/<id>`; saved points and older backups are left without it
(rule "absent = left as it is"); then `GameSpots.Add` + `Save`. Any failure
→ remove the `.lss-posizioni/<id>` folders made so far.

`RemoveSpot`: only `GameSpots.Remove`; the copies in the snapshots stay
(ignored), so adding the same folder back later (new id) is a new spot;
log it.

Tests: add with both fills, add while the game runs refused, failure in
the middle cleans up, remove keeps the snapshot data.

Commit: `Add and remove save spots`

## N8 — Export and import with spots

Export format **4**: the zip gets `posizioni.ini` with `<id>=<portable path>|<pattern>|<recursive>`
for every spot of the game. Import:

- format 2/3 → as today (spot 1 only);
- format 4 → each spot of the zip is matched to a spot of this game by
  **portable path, pattern and recursive flag** (ignoring case on Windows); matched → its
  data goes under this game's id; unmatched → not imported, and the result
  says `ImportSpotsSkipped` with the list of portable paths.

Tests: same PC round trip; other PC (different user name, same portable
paths) → all matched; game without that spot → skipped and listed.

Commit: `Export and import every save spot`

## N9 — Interface: the save folders of a game

Read `GameView.axaml`, `GameViewModel.cs` and phase L's `SaveFolderChoice`
first.

1. The `SaveFolderFrom…` line of L12 becomes a small list **Save folders**:
   one line per spot (folder, pattern if any, origin as in L12), click
   opens the folder; ⋯ on a chosen/learned spot → **Remove from the save
   folders** (confirmation `RemoveSpotText`).
2. **Add another folder…** under the list: picker → safety → confirmation
   `AddSpotText` with the two choices for the other playthroughs as
   buttons (`AddSpotCopy` default, `AddSpotEmpty`), shown only when the game
   has more than one playthrough → `AddSpot`.
3. Learn wizard (M4): candidates become **checkboxes** (first checked);
   **Use these folders**. For a game without spots the first checked is
   spot 1, the others extra spots; for a game with playthroughs each new
   folder goes through `AddSpot` with the choice of point 2.
4. `SwitchInterruptedRestored` message at the first open after a crash.

Texts (both `resx`; Italian with the same meaning):

| Key | English |
|---|---|
| `SaveFoldersCaption` | Save folders |
| `SpotFiles` | {0} (only {1}) |
| `AddSpot` | Add another folder… |
| `AddSpotText` | LSS will also switch this folder:\n{0}\nIts files now belong to the playthrough "{1}". What about the other playthroughs? |
| `AddSpotCopy` | Give them a copy of these files |
| `AddSpotEmpty` | Start it empty for them |
| `RemoveSpot` | Remove from the save folders |
| `RemoveSpotText` | LSS will stop switching this folder. Its files stay where they are, and the copies in the playthroughs are kept. |
| `LearnUseFolders` | Use these folders |
| `SpotBadName` | LSS can't use a folder whose path contains "\|". |
| `SwitchInterruptedRestored` | The last switch of {0} was interrupted (maybe the computer turned off). LSS put back the saves as they were before it. |
| `ImportSpotsSkipped` | Some saves were not imported, because this game doesn't use these folders here: {0} |

Tests on the view models with fakes; `LocalizationTests`, `XamlKeysTests`.

Commit: `Interface for several save folders`

## N9a — Save configuration: the file and the export

A **save configuration** says where a game keeps its saves, written so it
works on another PC: an admin who found the folders of a game (by the
search, by hand, or from the launcher) exports it and shares the file with
anyone who plays that game **on the same kind of system**; they import it
and the game is ready, without the search. The file is shared by hand
(chat, forum, e-mail): LSS never sends or downloads anything.

File: `<Game name> - <platform>.lss-config.json` (UTF-8 JSON):

```json
{
  "format": "lss-save-config",
  "version": 1,
  "createdWith": "LSS 2.0.0",
  "created": "2026-10-24",
  "platform": { "os": "linux", "distro": "Arch Linux", "steamDeck": false },
  "games": [
    {
      "provider": "steam",
      "id": "632470",
      "name": "Disco Elysium",
      "installDir": "Disco Elysium",
      "proton": true,
      "spots": [
        { "root": "WinAppDataLocalLow", "path": "ZAUM Studio/Disco Elysium/SaveGames", "pattern": "", "recursive": true },
        { "root": "WinAppDataLocalLow", "path": "ZAUM Studio/Disco Elysium/Settings", "pattern": "", "recursive": true }
      ],
      "notes": ""
    }
  ]
}
```

- `platform.os`: `windows` or `linux`; `distro`: Windows → the product
  name (`RuntimeInformation.OSDescription`), Linux → `PRETTY_NAME` of
  `/etc/os-release`; `steamDeck`: `/etc/os-release` has `ID=steamos`.
  Informative except `os` (see N9b).
- `proton`: the game ran in a prefix when exported (`PrefixPath != ""`).
- `spots`: from `SaveSpot.PortablePath` (N1), split into `root` (one of the
  L6 roots, `gameinstall` included) and `path` (relative, `/` separators,
  tokens `{64BitSteamID}` / `{Steam3AccountID}` allowed). A spot that is
  not under any root (an absolute path such as `D:\Giochi\Saves`) **is
  not exported**: the export result lists it (`ConfigSpotSkipped`).
- `id`: the provider's game id; for `manual` games it is "" and `name` plus
  `installDir` (the exe's folder name) identify the game.
- Several games in one file are allowed (export "all my configured games").

New file `src/Lss.Core/SaveConfig.cs`:

```csharp
public sealed record SaveConfigSpot(string Root, string Path, string Pattern, bool Recursive);
public sealed record SaveConfigGame(string Provider, string Id, string Name, string InstallDir,
    bool Proton, IReadOnlyList<SaveConfigSpot> Spots, string Notes);
public sealed record SaveConfigFile(string Os, string Distro, bool SteamDeck, string CreatedWith,
    string Created, IReadOnlyList<SaveConfigGame> Games);

internal static class SaveConfig
{
    /// <summary>Configurazione di un gioco dai suoi posti, con l'elenco di quelli non esportabili.</summary>
    public static (SaveConfigGame Game, IReadOnlyList<string> Skipped) FromGame(GameInfo game, SaveContext context);
    public static string Write(SaveConfigFile file);               // JSON indentato
    /// <summary>Legge e controlla un file; mai eccezioni, gli errori nel Result.</summary>
    public static Result<SaveConfigFile> Read(string json);
}
```

`System.Text.Json`, property names exactly as above. Exporting a game
whose spots all come from Steam's own rules is allowed (useful on a PC
where the launcher data is missing), but the preview says so
(`ConfigFromLauncher`).

Tests `SaveConfigTests.cs`: round trip; a spot outside every root skipped
and listed; the account's Steam ids never appear in the JSON (search the
text for both); manual game; several games.

Commit: `Save configurations: file and export`

## N9b — Save configuration: import

An imported file comes from someone else: **treat it as untrusted**.

`SaveConfig.Read` refuses the whole file (`ConfigInvalid` with the reason)
when: larger than 1 MB; `format` is not `lss-save-config`; `version` is
greater than 1 (`ConfigTooNew`); more than 200 games or 8 spots per game;
any spot with a root not in the L6 table, a `path` that is absolute,
contains `..`, `\`, `*` or `?`, or a token other than the two allowed; a
`pattern` refused by `CheckPattern` (N2) or containing `/`. Unknown JSON
properties are ignored.

Applying (`SaveConfig.Plan` + `SaveConfig.Apply`):

1. **Match** each game of the file to a game of this PC: same `provider`
   and `id`; for `manual` (or no match) by `Normalize`d name or
   `installDir` (M3). No match → listed as `ConfigNoGame` (not applied).
2. **Platform**: same `os` → fine. File from Windows, game here in a
   prefix (Proton/Wine) → fine, with the note `ConfigWindowsInPrefix` (the
   Windows roots go into the prefix). Spots whose roots have no value here
   (a Linux file on Windows, Windows roots for a native Linux game) are
   listed as "not for this system"; a game with none left is not applied.
3. **Resolve** each spot with L6 into a real folder, then
   `SaveFolderSafety.Check` / `CheckPattern` **as for any other folder**.
   For each spot the plan says: folder found, folder missing (never played
   here: `ConfigNotYet`, the spot is kept and is used once the folder
   exists), or refused (the safety reason).
4. **Apply** only what the user confirms (N9c): spots go to `posti.ini`
   with the new origin `importata` (`SaveSource.Imported`: add it to L2's
   enum, and the text `SaveFolderImported`). A game that already has
   playthroughs adds each new spot through `AddSpot` (N7) with the
   copy/empty choice. Spots equal to one the game already has are skipped.

Tests: every refusal rule (one test each, file text built in the test);
match by id and by name; a Windows file on a Proton game resolves into the
prefix; a Linux file on Windows → "not for this system"; a spot resolving
to Documents itself → refused by safety; missing folder kept as
`ConfigNotYet`; apply writes `posti.ini` with `importata`; nothing is
written when the user cancels.

Commit: `Save configurations: import`

## N9c — Save configuration: interface

Admins only (advanced mode).

- Game view, ⋯ menu of the game: **Export save configuration…** → a
  preview window with the spots as they will be written (portable form),
  the skipped ones, and a **Notes** box (optional) → **Save…** →
  save-file picker (filter `*.lss-config.json`, name from the file rule).
- Games list footer: **Import save configuration…** → open-file picker →
  `ImportConfigWindow`: one block per game of the file — game name and
  launcher, `ConfigMadeOn`, notes, then one row per spot with the real
  folder and its state (✓ found, ⏳ not yet, ✗ refused with the reason,
  — not for this system), a checkbox per game (on when at least one spot
  can be applied). **Import** applies the checked games; the result says
  what was done.
- Settings: **Export all save configurations…** (every game with at least
  one chosen, learned or imported spot).

Texts (both `resx`; Italian with the same meaning):

| Key | English |
|---|---|
| `ExportConfig` | Export save configuration… |
| `ExportAllConfigs` | Export all save configurations… |
| `ImportConfig` | Import save configuration… |
| `ConfigPreviewTitle` | Save configuration of {0} |
| `ConfigPreviewText` | These folders will be in the file, written so they work on another computer. No user name and no saves are in it. |
| `ConfigSpotSkipped` | Not exported (not in a standard folder): {0} |
| `ConfigFromLauncher` | {0} already knows these folders: this file is useful only where it doesn't. |
| `ConfigNotes` | Notes (optional) |
| `ImportConfigTitle` | Import save configuration |
| `ConfigMadeOn` | Made on {0} |
| `ConfigWindowsInPrefix` | Made on Windows: here the folders are inside the game's Proton/Wine folder. |
| `ConfigFound` | found |
| `ConfigNotYet` | not here yet: it will be used once the game creates it |
| `ConfigRefused` | refused: {0} |
| `ConfigNotForThisSystem` | not for this system |
| `ConfigNoGame` | {0} is not on this computer. |
| `ConfigInvalid` | This file is not a valid save configuration: {0} |
| `ConfigTooNew` | This file was made by a newer LSS. Update LSS to import it. |
| `ConfigImported` | Save folders imported for {0} game(s). |
| `SaveFolderImported` | Saves: {0} (imported) |

Tests on the view models with fakes: export preview and save; import of a
mixed file (one game applied, one missing, one spot refused); cancel
writes nothing.

Commit: `Save configurations: interface`

## N10 — Look after every session (core)

New file `src/Lss.Core/SessionScan.cs`:

```csharp
/// <summary>Cartella che sembra di salvataggi, trovata dopo una partita.</summary>
public sealed record SpotSuggestion(string Folder, string Pattern, int Score,
    IReadOnlyList<ScoreReason> Reasons, DateTime FoundUtc);

internal static class SessionScan
{
    public const int MinScore = 9;   // più severo del wizard: nessuno l'ha chiesto

    public static IReadOnlyList<SpotSuggestion> Run(GameInfo game, SaveContext context,
        DateTime sessionStartUtc, GameSpots spots, IReadOnlyList<string> launcherFolders,
        string lssDataRoot, CancellationToken cancel);
}
```

- Roots: `SaveFinder.Roots` with the game's extra roots; `FindChanged`
  since `sessionStartUtc - 5 s`; `Rank` with `knownFolders` = every spot
  folder; drop candidates that `IgnoredFolders.IsIgnored` (M3b: for this
  game or for every game), those with score below `MinScore`,
  and those already suggested.
- Loose save-like files in a protected folder (the install folder) become
  a pattern suggestion: exact names of the changed save-like files, or
  `*.<ext>` when at least 3 share an extension; must pass `CheckPattern`.
- Suggestions are stored in `proposte.ini` in the game's data folder
  (`Proposta.<n>=folder|pattern|score|found`; reasons are computed again
  when shown) and kept until accepted or ignored.

Tests: a session that changed only the known spot → nothing; a new folder
with the game's name in Documents → suggested with reasons; an ignored
folder → not suggested; a browser cache → not suggested; loose `.sav` in
the install folder → pattern suggestion.

Commit: `Look for other save folders after a session`

## N11 — Look after every session (interface)

- **When:** the session of a managed game ends (`GameMonitor` session end
  in advanced mode, end of play in simple mode, phase P7), **after** the
  save on exit. Also when a game of *Other games* that LSS can see running
  (`CanDetectRunning`) closes: then the scan is the M2/M3 one and a good
  candidate becomes "LSS found where {0} saves" on that game.
- One scan at a time, on a background thread with
  `ThreadPriority.BelowNormal`, 90 s limit, cancelled if the same game
  starts again.
- **Setting** in Settings (advanced): **Look for other save folders after
  playing**, on by default (`lss.ini` `CercaDopoPartita`).
- **Advanced mode:** toast `SuggestionsFound` with **Look**; the game view
  shows `SuggestionsPending` until handled. **Look** opens
  `SpotSuggestionsWindow`: each suggestion with its reasons (as in M4) and
  three buttons: **Add to the save folders** (→ N9 flow), **Not saves…**
  (→ the same choice as in the wizard, M3b/M4: **Only for {game}** or
  **For every game**; the suggestion is removed and never comes back),
  **Later**.
- **Simple mode:** never asks anything; suggestions wait for the advanced
  mode (the ⚙ icon gets a small dot).

| Key | English |
|---|---|
| `LookAfterPlaying` | Look for other save folders after playing |
| `SuggestionsFound` | LSS found files of {0} that may be saves, in a folder it doesn't switch yet. |
| `SuggestionsLook` | Look |
| `SuggestionsPending` | {0} possible save folders to check |
| `SuggestionsTitle` | Possible save folders of {0} |
| `SuggestionAdd` | Add to the save folders |
| `SuggestionNotSaves` | Not saves… |
| `SuggestionLater` | Later |
| `SaveFolderFoundForOther` | LSS found where {0} saves. |

Tests: setting off → no scan; session end → scan → suggestion stored →
toast; "Not saves" for this game → not suggested again for it but still
for another game; "Not saves" for every game → not suggested for any game;
simple mode → no dialog.

Commit: `Suggest save folders after playing`

## N12 — Screenshots

`tools/UiScreenshots`: game view with three spots (one pattern), the add
confirmation, the wizard with checkboxes, the suggestions window, the
toast. Look at the PNGs; notes.

Commit: `Screenshots of several save folders`

## N13 — Docs for developers

`DEVELOPMENT.md`: spots, the snapshot layout (`.lss-posizioni`), the rule
"absent = left as it is", the journal `cambio.ini`, shared files only in
spot 1, `posti.ini` / `proposte.ini` formats.

Commit: `Docs: save spots`

## N14 — 👤 Checks

Andrix:

1. **Terraria** (userdata + Documents): two playthroughs, switch, both
   folders follow.
2. A game with loose save files in its folder, if one is found (else skip).
3. Remove one spot of a game with two, play and save: the scan after the
   session proposes it again.
4. Turn the PC off (or kill LSS) during a switch of a game with two
   folders, on a test game: at the next start the message and the saves as
   before.
5. Same as 1 and 3 on the Steam Deck.
