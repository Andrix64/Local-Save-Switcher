# Phase M — Find the save folder by watching the disk

Goal: when LSS doesn't know where a game saves (no `userdata`, not in the
list, or any game added by hand), it **learns it**: the user starts the
game, saves, quits; LSS looks at which files changed on the disk in the
meantime and proposes the save folder. The user confirms; from then on the
game works like any other.

Requires phase L (`SaveContext`, `SaveFolderSafety`, `GameSaveSettings`,
the `SaveFolderChoice` helper). Branch: `learn-saves`. Rules in
[README.md](README.md).

## How it works

```
 [Start the game] ──► user plays, saves, quits ──► LSS scans the folders
        │ T0 = now                                  for files changed after T0
        ▼                                                     │
 game closed (detected,                                       ▼
 or "I've quit the game")               candidates scored ──► proposal
                                                              │
                         "Use this folder" ─► same confirmation as L12
```

- No watcher runs while the game is open: LSS remembers the time `T0`
  just before the start and, after the game closed, walks the search
  folders looking for files written or created after `T0 - 5 s`. Simpler
  than `FileSystemWatcher`, works the same on Windows, Linux and Steam Deck
  (no inotify limits), and nothing runs during the game.
- The walk has a time limit (90 s) and can be cancelled; results found
  until then are used, marked partial.
- **Nothing is written** during learning: it only reads timestamps.

---

## M1 — Search folders

New file `src/Lss.Core/SaveFinder.cs`, first part:

```csharp
/// <summary>Cartella in cui cercare i file cambiati, con la profondità massima.</summary>
public sealed record SearchRoot(string Folder, int MaxDepth, bool GameOwned);

internal static partial class SaveFinder
{
    /// <summary>Cartelle in cui un gioco può salvare, senza doppioni né cartelle una dentro l'altra.</summary>
    public static IReadOnlyList<SearchRoot> Roots(SaveContext context, IReadOnlyList<string> extraFolders);
}
```

Roots (only those that exist; `GameOwned` = everything inside belongs to
this game):

| System | Folder | Depth | GameOwned |
|---|---|---|---|
| all | `InstallPath` | 8 | true |
| all | Steam `userdata/<account>/<AppID>` (Steam games) | 8 | true |
| all | `extraFolders` (added by the user, M4) | 8 | false |
| Windows | Documents, `<home>/Saved Games`, AppData, LocalAppData, LocalAppDataLow | 6 | false |
| Windows | Public Documents (`<Public>/Documents`), ProgramData | 4 | false |
| prefix | `U` (whole user folder) | 7 | `PrefixIsPerGame` |
| prefix | `C/ProgramData`, `C/users/Public` | 5 | `PrefixIsPerGame` |
| Linux | XdgData, XdgConfig | 5 | false |
| Linux | Home, only its direct dot-folders (`~/.mygame`) | 4 | false |
| Linux | Documents | 6 | false |
| Linux, no prefix known | `~/.wine/drive_c/users`, `~/Games/*/drive_c/users`, `~/.local/share/bottles/bottles/*/drive_c/users`, `~/.var/app/com.usebottles.bottles/data/bottles/bottles/*/drive_c/users` | 6 | false |

When a root is inside another, keep the outer one (with the larger depth
of the two).

Tests: Windows set (fake `KnownFolders`), prefix set, Linux set, nested
roots merged, missing folders skipped.

Commit: `Learn saves: search folders`

## M2 — Changed files

```csharp
/// <summary>File cambiato dopo l'avvio del gioco.</summary>
public sealed record ChangedFile(string Path, long Size, DateTime WrittenUtc, SearchRoot Root);

public static IReadOnlyList<ChangedFile> FindChanged(IReadOnlyList<SearchRoot> roots, DateTime sinceUtc,
    IReadOnlyList<string> skipFolders, TimeSpan timeLimit, CancellationToken cancel, out bool partial);
```

- Changed = `max(LastWriteTimeUtc, CreationTimeUtc) >= sinceUtc`.
- Walk with `Directory.EnumerateFileSystemEntries` per folder in
  try/catch (no access → skip), never follow symbolic links
  (`FileAttributes.ReparsePoint`), respect `MaxDepth`.
- **Skip** folders whose name is in `SkipNames` (ignoring case):
  `Temp`, `tmp`, `Cache`, `Caches`, `.cache`, `cache2`, `GPUCache`,
  `Code Cache`, `ShaderCache`, `shadercache`, `D3DSCache`, `DXCache`,
  `GLCache`, `NVIDIA`, `NVIDIA Corporation`, `AMD`, `Intel`, `Microsoft`,
  `Packages`, `Google`, `Mozilla`, `BraveSoftware`, `discord`, `Spotify`,
  `CrashDumps`, `Crashes`, `CrashReports`, `crashpad`, `logs`, `Logs`,
  `htmlcache`, `webcache`, `mesa_shader_cache`, `fontconfig`, `pulse`,
  `Trash`, `.Trash-1000`, `node_modules`, `.git`, `windows` (only
  directly under `C`), `Program Files`, `Program Files (x86)` (under `C`,
  except when `InstallPath` is inside them);
  plus `skipFolders` = LSS data root, every `launcherFolders` entry except
  the game's own `userdata/<account>/<AppID>` and `InstallPath`.
- Ignore files with extensions `.log`, `.tmp`, `.dmp`, `.etl`, `.lock`,
  `.pid`, `.pf` and names ending in `~`.

Tests with `TestFolder`: a file written after `since` is found, an older
one isn't (set times with `File.SetLastWriteTimeUtc`/`SetCreationTimeUtc`);
skipped names; depth limit; ignored extensions; a cancelled token returns
what was found with `partial = true`.

Commit: `Learn saves: changed files`

## M3 — Candidates and their score

```csharp
/// <summary>Perché una cartella ha preso (o perso) punti: mostrato all'utente.</summary>
public enum ScoreReasonKind { GameName, GameOwned, SaveLikeFile, ChangedFiles, SaveLikePath, OnlySettings, OtherProgram }

/// <summary>Una voce del punteggio, con il dettaglio da mostrare (nome trovato, file, numero).</summary>
public sealed record ScoreReason(ScoreReasonKind Kind, int Points, string Detail);

/// <summary>Cartella proposta come cartella dei salvataggi.</summary>
public sealed record SaveCandidate(string Folder, int Score, IReadOnlyList<ChangedFile> Files,
    IReadOnlyList<ScoreReason> Reasons);

public static IReadOnlyList<SaveCandidate> Rank(IReadOnlyList<ChangedFile> files, SaveContext context,
    string gameName, IReadOnlyList<string> launcherFolders, string lssDataRoot,
    IReadOnlyList<string> knownFolders);
```

`knownFolders` = save folders the game already has (phase N): files inside
them are not proposed again. Empty in the wizard of a new game.

1. **Group** files by their parent folder.
2. **Raise** each group to the folder to propose: walking up from the
   parent folder towards the root, the highest folder whose name contains
   a **game token** (below), if it is still below the `SearchRoot`;
   otherwise the parent folder itself. Example:
   `Documents/My Games/Hollow Knight/Saves/user1.dat` → `Documents/My Games/Hollow Knight`.
   `SaveFinder.Normalize(string)`: lower case, letters and digits only
   (`"Hollow Knight™"` → `"hollowknight"`); words = split on anything that
   is not a letter or digit first. Game tokens = words of `Normalize`d `gameName` and `InstallDir` with at
   least 3 characters, plus the whole normalised name (`hollowknight`);
   a folder name matches if its normalised form contains a token.
3. **Merge** proposals that are the same folder or one inside another
   (keep the outer, join the files).
4. **Score** each proposal:
   - +6 the folder name or a parent below the root matches a game token;
   - +5 the root is `GameOwned` (game folder, its prefix, its `userdata`);
   - +3 a file has a save-like name: extension `.sav`, `.save`, `.sl2`,
     `.ess`, `.dat`, `.bin`, `.slot`, or the name contains `save`, `slot`,
     `profile`, `progress`, `player`, `world`;
   - +1 per changed file, up to +3;
   - −4 every file is a settings-like file (`.ini`, `.cfg`, `.xml`, `.json`
     with names containing `config`, `setting`, `option`, `prefs`);
   - −6 the folder is under a root that is not `GameOwned` and nothing
     matches a game token (probably another program);
   - +2 a folder between the `SearchRoot` and the files has a usual save
     folder name (`SaveFolderWords`, compared after `Normalize`: `saves`,
     `save`, `savegames`, `savedgames`, `saved`, `savedata`, `savefiles`,
     `profiles`, `mygames`); +3 if two of them are found.
   Every item that gives or takes points adds one `ScoreReason` (Detail =
   the token found, the first save-like file name, the number of files...).
5. Drop proposals that fail `SaveFolderSafety.Check`, and those with a
   score below 3. Sort by score, then by most recent file; keep 5.

Write the weights as named constants at the top of the file, so they can be
tuned after the real checks (M7). Tests also check the reasons of each
candidate (kind and points).

Tests `SaveFinderTests.cs` with realistic fake trees: Documents game
(proposal raised to the game folder); Proton game (whole prefix is the
game's: the game folder in `AppData/Local` wins); a browser profile changed
at the same time in AppData (scores below the game); only an `.ini`
changed (low score); saves inside the install folder (`<game folder>/saves`);
proposal equal to a protected folder dropped; nothing changed → empty.

Commit: `Learn saves: candidates`

## M3b — Folders that are never saves

The user can mark a folder found by LSS as **not a save folder**, so it is
never proposed again: either **for this game only**, or **for every game**
(a browser or Discord cache, a launcher's logs...). Marking a folder also
covers everything inside it.

New file `src/Lss.Core/IgnoredFolders.cs`:

```csharp
/// <summary>Cartella segnata come "non di salvataggi", con l'ambito.</summary>
public sealed record IgnoredFolder(string Folder, string PortablePath, string GameKey, DateTime Added);

/// <summary>
/// Cartelle che la ricerca non propone mai più: per tutti i giochi
/// (cartelle-ignorate.ini nella cartella dei dati) o per un gioco solo
/// (cartella.ini del gioco, chiave Ignorate; da N3 posti.ini).
/// </summary>
internal sealed class IgnoredFolders
{
    public IgnoredFolders(string dataRoot);
    public IReadOnlyList<IgnoredFolder> Global { get; }
    public IReadOnlyList<IgnoredFolder> ForGame(string gameDataFolder);
    public void Load();
    public void AddGlobal(string folder, string portablePath);
    public void AddForGame(string gameDataFolder, string folder);
    public void RemoveGlobal(string folder);
    public void RemoveForGame(string gameDataFolder, string folder);
    /// <summary>true se la cartella, o una che la contiene, è ignorata per questo gioco o per tutti.</summary>
    public bool IsIgnored(string folder, string gameDataFolder, SaveContext context);
}
```

- `<data>/cartelle-ignorate.ini` (via `IniFile`, never throws):
  `Cartella.<n>=<path>` and `Aggiunta.<n>=<date>`. `<path>` is the
  **portable** form (N1's root names in braces, written by a small
  `SavePathResolver.ToPortable` already here if N1 is not done yet: L6's
  roots, longest match) when the folder is under a root, else the full
  path. So `{WinAppDataLocal}/Discord` ignored once is ignored for every
  game, also inside every Proton prefix.
- `IsIgnored`: resolve each global portable entry with the game's context
  (L6 `RootFolder`), then compare: equal to, or a parent of, `folder`
  (ignoring case on Windows and in prefixes on Linux).
- A folder can't be ignored if it is one of the game's **current** save
  folders (`IgnoreCurrentFolder`): remove it from the save folders first.
- `FindChanged` (M2) gets the ignored folders as extra `skipFolders` (the
  walk doesn't even enter them: faster); `Rank` (M3) also drops any
  candidate that `IsIgnored` (a candidate raised to a parent folder can
  contain an ignored one: keep it, but leave the ignored files out).

Tests `IgnoredFoldersTests.cs`: global entry stored in portable form and
matching inside a prefix of another game; game-only entry doesn't affect
another game; subfolder of an ignored folder ignored; remove; current save
folder refused; `FindChanged` doesn't enter an ignored folder (count the
folders visited through an internal counter); `Rank` without the ignored
candidate.

Commit: `Folders that are never saves`

## M4 — The wizard (interface)

New `LearnSavesWindow` + `LearnSavesViewModel` (look at
`NewPlaythroughWindow` for the style). Opened by **Find the save folder…**
(L12), by the simple mode (phase P) and after **Add a game** (phase O).

Steps:

1. **Intro.** `LearnIntroText`: three numbered lines. Buttons:
   **Start the game** (`provider.Launch`, only if `CanLaunch`; otherwise
   the text says to start it by hand and the button is **I've started the
   game**), **Cancel**. Before: if `IsGameRunning` → `LearnCloseFirst`.
   `T0 = DateTime.UtcNow` when the button is pressed.
2. **Playing.** `LearnPlayingText`. Every 2 s
   `provider.IsGameRunning(game)`: when it was seen running and then is no
   longer running for 4 s → step 3. If `CanDetectRunning` is false or the
   game was never seen running after 60 s, the text adds
   `LearnPressWhenDone`. Button **I've quit the game** always there.
3. **Looking.** Progress text (`LearnLooking`, folder being read) and
   **Cancel**; `FindChanged` + `Rank` on a background task.
4. **Result.**
   - Candidates: a list with radio buttons, the first selected and marked
     `LearnRecommended`; each shows the folder, **why** (one line per
     `ScoreReason`: ✓ for points gained, ✗ for points lost, texts below),
     `LearnFilesChanged` (count) and up to 5 file names relative to the
     folder. Candidates with a score below 6 are marked `LearnUnlikely`.
     Each candidate has a small link **Not a save folder…** → a choice
     with two buttons, **Only for {game}** and **For every game**
     (`IgnoredFolders.AddForGame` / `AddGlobal`); the candidate disappears
     from the list at once, with the toast `IgnoredToast` and **Undo**.
     Buttons: **Use this folder** (→
     `SaveFolderChoice` with `Origin = Learned`), **Try again** (back to
     step 1), **Choose the folder myself…**, **Look in another folder…**
     (adds a root for this game, kept in `cartella.ini` as
     `CartelleExtra`, `;`-separated, then back to step 1).
   - No candidate: `LearnNothingFound` and the same last three buttons.
   - Partial: line `LearnPartial` above the list.

Texts (both `resx`; Italian with the same meaning, short sentences — this
window is also used in simple mode by people who are not technical):

| Key | English |
|---|---|
| `LearnTitle` | Find the saves of {0} |
| `LearnIntroText` | LSS doesn't know yet where {0} keeps its saves. Let's find out together:\n1. Start the game.\n2. Save in the game (or play until it saves by itself).\n3. Quit the game.\nLSS will look at what changed on the disk. |
| `LearnStart` | Start the game |
| `LearnStartedByHand` | I've started the game |
| `LearnStartByHandText` | Start {0} yourself now, then press the button. |
| `LearnCloseFirst` | {0} is open. Quit the game first, then start again from here. |
| `LearnPlayingText` | Play, save, then quit {0}. |
| `LearnPressWhenDone` | When you have quit the game, press the button. |
| `LearnQuitDone` | I've quit the game |
| `LearnLooking` | Looking for changed files… {0} |
| `LearnFilesChanged` | {0} files changed |
| `LearnUseFolder` | Use this folder |
| `LearnTryAgain` | Try again |
| `LearnLookElsewhere` | Look in another folder… |
| `LearnNothingFound` | LSS didn't see any file that looks like a save. Did you save before quitting? You can try again, or choose the folder yourself. |
| `LearnPartial` | The search took too long and stopped early: the list may be incomplete. |
| `LearnRecommended` | Recommended |
| `LearnUnlikely` | Unlikely |
| `ReasonGameName` | The name contains "{0}" |
| `ReasonGameOwned` | It belongs to the game (its folder or its Proton/Wine folder) |
| `ReasonSaveLikeFile` | Files that look like saves ({0}) |
| `ReasonChangedFiles` | {0} files changed while you were playing |
| `ReasonSaveLikePath` | It has a usual save folder name ("{0}") |
| `ReasonOnlySettings` | Only settings files changed |
| `ReasonOtherProgram` | It seems to belong to another program |
| `NotSaveFolder` | Not a save folder… |
| `NotSaveFolderText` | LSS won't propose this folder again:\n{0} |
| `IgnoreForGame` | Only for {0} |
| `IgnoreForAll` | For every game |
| `IgnoredToast` | LSS won't propose this folder again. |
| `IgnoreUndo` | Undo |
| `IgnoreCurrentFolder` | This is a save folder of {0}: remove it from its save folders first. |
| `IgnoredFoldersTitle` | Folders that are not saves |
| `IgnoredFoldersEmpty` | No folders: everything LSS finds can be proposed. |
| `IgnoredScopeAll` | every game |
| `IgnoredRemove` | Propose it again |

In **Settings** (advanced mode, admins): **Folders that are not saves…**
→ `IgnoredFoldersWindow`: every ignored folder (global and of each game),
with its scope (`IgnoredScopeAll` or the game's name), the date, and
**Propose it again** (removes it). Empty → `IgnoredFoldersEmpty`. In simple
mode this link appears only in the wizard opened by an admin.

Tests (view model, `FakeProvider`, `FakeDialogService`, fake clock and
finder injected): running → stopped moves to step 3; manual button; launch
not possible → "started by hand" path; result with candidates → choice
saved as `Learned`; nothing found → text; cancel leaves `cartella.ini`
untouched; "Not a save folder" for this game and for every game → the
candidate leaves the list and the right file is written, Undo removes it;
the ignored folders window lists both kinds and "Propose it again" removes
the entry.

Commit: `Learn saves: wizard`

## M5 — Learning a game that already has playthroughs

If the folder of a game with playthroughs is learned or chosen again and
differs from the current one, the existing playthroughs were made from the
old folder: before saving, `ConfirmAsync` with `LearnFolderChanged`
("The playthroughs of {0} were made with another folder. Use the new
folder anyway?"). Tests.

Commit: `Learn saves: warn when the folder changes`

## M6 — Screenshots

`tools/UiScreenshots`: the four steps, a result with three candidates, the
nothing-found case. Look at the PNGs; notes.

Commit: `Screenshots of the learn wizard`

## M7 — 👤 Real check

Andrix, on Windows and on the Steam Deck, with at least three games that
have no launcher rules or are `Unsure` (and one game with Steam rules,
to compare): does the first proposal match the real save folder? Write each
game, the proposal and its score in the notes; the weights of M3 are tuned
from these results (new task if needed).
